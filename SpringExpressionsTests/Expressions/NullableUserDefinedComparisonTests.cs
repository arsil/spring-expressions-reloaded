using System;
using NUnit.Framework;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A nullable operand reaches its own type's relational operator, and an empty one still sorts
    /// first.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The operator lookup demands exact operand types, so <c>Ordered? &lt; Ordered</c> found nothing:
    /// <c>Nullable&lt;Ordered&gt;</c> declares no operators of its own. The pair then fell through to
    /// <c>ComparisonHelper</c>'s <c>Comparer&lt;T&gt;.Default</c> branch, which <b>compiled and threw at
    /// evaluation</b> for a type with no <see cref="IComparable"/> - so it was a hard failure in every
    /// mode, the weakly typed route included, because compilation succeeded and the fallback was long
    /// finished. The interpreter answered correctly all along, since a boxed <c>Ordered?</c> holding a
    /// value <i>is</i> a boxed <c>Ordered</c> and it never sees a wrapper.
    /// </p>
    /// <p>
    /// The fix is that <c>ComparisonHelper</c> asks the same lookup the four comparison nodes ask, on
    /// the operands <c>NullableValueTypesHelper</c> has already unwrapped. One implementation serves
    /// both call sites (<c>UserDefinedOperatorUtils.TryCreateComparison</c>), so the rule cannot drift.
    /// Open-issues item 18.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class NullableUserDefinedComparisonTests : BaseCompiledTests
    {
        /// <summary>Declares the four relational operators and equality. No <see cref="IComparable"/>.</summary>
        public struct Ordered
        {
            public Ordered(int v) { V = v; }
            public int V { get; }

            public static bool operator <(Ordered a, Ordered b) { return a.V < b.V; }
            public static bool operator >(Ordered a, Ordered b) { return a.V > b.V; }
            public static bool operator <=(Ordered a, Ordered b) { return a.V <= b.V; }
            public static bool operator >=(Ordered a, Ordered b) { return a.V >= b.V; }
            public static bool operator ==(Ordered a, Ordered b) { return a.V == b.V; }
            public static bool operator !=(Ordered a, Ordered b) { return a.V != b.V; }
            public override bool Equals(object o) { return o is Ordered x && x.V == V; }
            public override int GetHashCode() { return V; }
            public override string ToString() { return "O" + V; }
        }

        /// <summary>No operators and no <see cref="IComparable"/> - both backends must still throw.</summary>
        public struct Opaque
        {
            public Opaque(int v) { V = v; }
            public int V { get; }
        }

        public class Root
        {
            public Ordered One { get; set; } = new Ordered(1);
            public Ordered Two { get; set; } = new Ordered(2);
            public Ordered? NullableOne { get; set; } = new Ordered(1);
            public Ordered? NullableTwo { get; set; } = new Ordered(2);
            public Ordered? Empty { get; set; }
            public Opaque Plain { get; set; } = new Opaque(1);
            public Opaque? NullablePlain { get; set; } = new Opaque(2);
        }

        static void Both(string expression, bool expected)
        {
            var root = new Root();

            Assert.AreEqual(expected,
                CompileGetter<Root, object>(expression).GetValue(root), "compiled: " + expression);
            Assert.AreEqual(expected,
                InterpretGetter<Root, object>(expression).GetValue(root), "interpreted: " + expression);
        }

        /// <summary>
        /// The six shapes that used to throw on every path.
        /// </summary>
        [Test]
        public void ANullableHoldingAValueReachesItsOwnOperator()
        {
            Both("NullableTwo < One", false);
            Both("One < NullableTwo", true);
            Both("NullableTwo > One", true);
            Both("NullableTwo <= One", false);
            Both("NullableTwo >= One", true);
            Both("NullableTwo < NullableOne", false);
        }

        [Test]
        public void TwoNullablesHoldingEqualValuesCompareAsThatValueDoes()
        {
            Both("NullableOne <= NullableOne", true);
            Both("NullableOne >= NullableOne", true);
            Both("NullableOne < NullableOne", false);
            Both("NullableOne > NullableOne", false);
        }

        /// <summary>
        /// Nothing sorts before everything, unchanged - and this is where the fix could most easily
        /// have gone wrong.
        /// </summary>
        /// <remarks>
        /// The obvious way to lift a custom operator is
        /// <c>LExpression.LessThan(l, r, liftToNull: false, m)</c>, which is C#'s rule: any comparison
        /// against a null is false. That would have overwritten item 17 for custom types only. These
        /// rows are the guard, and they were already passing before the fix, because the null behaviour
        /// comes from the three sort-order outcomes the caller supplies and never from the operator.
        /// <b>Do not reconcile these with C#</b> - it answers false for every row here.
        /// </remarks>
        [Test]
        public void AnEmptyNullableStillSortsFirst()
        {
            Both("Empty < One", true);
            Both("Empty <= One", true);
            Both("One < Empty", false);
            Both("One > Empty", true);
            Both("Empty > One", false);
            Both("Empty >= One", false);

            Both("Empty < Empty", false);
            Both("Empty > Empty", false);
            Both("Empty <= Empty", true);
            Both("Empty >= Empty", true);
        }

        /// <summary>
        /// A type declaring no operators and no <see cref="IComparable"/> still throws the inherited
        /// error on both backends, nullable or not - the fix admits an operator where one exists and
        /// changes nothing where none does. Two failures are agreement, which is what the evaluation
        /// sweep asks for.
        /// </summary>
        [Test]
        public void ATypeWithNeitherOperatorsNorIComparableStillThrowsOnBoth()
        {
            var root = new Root();

            Assert.Catch<Exception>(
                () => CompileGetter<Root, object>("Plain < Plain").GetValue(root));
            Assert.Catch<Exception>(
                () => InterpretGetter<Root, object>("Plain < Plain").GetValue(root));

            Assert.Catch<Exception>(
                () => CompileGetter<Root, object>("NullablePlain < Plain").GetValue(root));
            Assert.Catch<Exception>(
                () => InterpretGetter<Root, object>("NullablePlain < Plain").GetValue(root));
        }

        /// <summary>
        /// <c>between</c> deliberately does not consult a type's own operators, so it still throws on
        /// both backends for a type with no <see cref="IComparable"/>.
        /// </summary>
        /// <remarks>
        /// <b>Do not "fix" this by passing the operator name at <c>OpBetween</c>'s call into
        /// <c>ComparisonHelper.CreateCompare</c>.</b> The interpreter's <c>between</c> goes through
        /// <c>CompareUtils.Compare</c>, which needs an int ordering and refuses a type with no
        /// <see cref="IComparable"/>; letting the compiled half honour the operator would make it answer
        /// where the interpreter throws. That is open-issues item 12's remaining question - deriving an
        /// order from <c>op_LessThan</c> plus <c>op_GreaterThan</c> invokes an operator the expression
        /// never wrote - and it has to be settled for both backends at once or not at all.
        /// </remarks>
        [Test]
        public void BetweenStillDoesNotConsultATypesOwnOperators()
        {
            var root = new Root();

            Assert.Catch<Exception>(
                () => CompileGetter<Root, object>("One between {One, Two}").GetValue(root));
            Assert.Catch<Exception>(
                () => InterpretGetter<Root, object>("One between {One, Two}").GetValue(root));
        }

        /// <summary>
        /// The weakly typed route, which is where the defect actually bit: it binds at
        /// <c>TContext = object</c> for <c>ExpressionEvaluator</c> but compiles against a typed root
        /// here, and compilation succeeded, so the exception reached the caller.
        /// </summary>
        [Test]
        public void TheWeaklyTypedRouteAnswersToo()
        {
            var root = new Root();

            Assert.AreEqual(false, Expression.Parse("NullableTwo < One").GetValue(root));
            Assert.AreEqual(true, Expression.Parse("One < NullableTwo").GetValue(root));
            Assert.AreEqual(true, Expression.Parse("Empty < One").GetValue(root));
        }

        /// <summary>
        /// Equality was already lifting and is asserted here so the fix is shown not to have moved it -
        /// <c>op_Equality</c> is found through a different path (<c>EqualityHelper</c>'s same-type
        /// branch, which unwraps nullables before comparing types).
        /// </summary>
        [Test]
        public void EqualityWasAlreadyLiftedAndStillIs()
        {
            Both("NullableTwo == Two", true);
            Both("Two == NullableTwo", true);
            Both("NullableTwo != Two", false);
            Both("Empty == Two", false);
        }
    }
}

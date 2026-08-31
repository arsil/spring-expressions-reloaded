using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class TernaryBranchCases
    {
        public int Number { get; set; }

        public string Name { get { return "abc"; } }
        public string NoName { get { return null; } }

        public int? NullInt { get { return null; } }
        public int? SomeInt { get { return 9; } }

        public decimal Price { get { return 2.5m; } }
    }

    /// <summary>
    /// A conditional whose two branches emit different types. <c>LExpression.Condition</c> demands one
    /// type and raised ArgumentException("Argument types do not match") for anything else, which the
    /// absorber then reported as an internal compiler error - a defect of ours for a shape that is
    /// merely uncompiled. Two disagreements have an answer and compile strongly typed; every other
    /// one is refused, and the interpreter serves it.
    /// </summary>
    /// <remarks>
    /// The refusal is not taste. The interpreter has no common-type rule - it returns whichever branch
    /// ran, untouched - so <c>x ? 1 : 2.5</c> is an Int32 when the test holds and a Double when it does
    /// not: the result *type* follows the branch taken. Converting both branches to a common type
    /// therefore diverges from the interpreter whatever type is chosen, C#'s own numeric widening
    /// included. Boxing both to object was considered and rejected: it preserves every value but hands
    /// back a type nothing downstream can use, so the conditional becomes the thing needing a cast,
    /// and it turns disagreeing branches - usually a mistake - into something plausible.
    /// </remarks>
    [TestFixture]
    public class TernaryBranchTypeTests : BaseCompiledTests
    {
        // ----- carve-out 1: a null literal takes the other branch's type

        /// <summary>
        /// The shape that mattered: ordinary, trivial in C#, and it compiles *as a string* rather than
        /// as an object. Retyping a null literal is the rule ConvertParameters already applies to a
        /// null argument and ArrayElementConversions to a null array item.
        /// </summary>
        [Test]
        public void ANullLiteralBranchTakesTheOtherBranchesType()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? Name : null)", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo("abc");

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? Name : null)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(null);
        }

        [Test]
        public void ANullLiteralBranchWorksInEitherPosition()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? null : Name)", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo(null);

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? null : Name)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo("abc");
        }

        [Test]
        public void ANullLiteralBranchAgainstANullableAlsoRetypes()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? SomeInt : null)", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo(9);

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? SomeInt : null)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(null);
        }

        /// <summary>
        /// A null cannot be retyped into a non-nullable value type, so this refuses rather than
        /// inventing a zero. The interpreter answers the branch that ran, null included.
        /// </summary>
        [Test]
        public void ANullLiteralAgainstANonNullableValueTypeIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<TernaryBranchCases, object>(
                    "(Number > 1 ? 1 : null)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<TernaryBranchCases, object>(
                "(Number > 1 ? 1 : null)", EvaluationMode.MustInterpret);

            Assert.AreEqual(1, interpreted.GetValue(new TernaryBranchCases { Number = 4 }));
            Assert.IsNull(interpreted.GetValue(new TernaryBranchCases { Number = 0 }));
        }

        // ----- carve-out 2: a value type meeting its own nullable form lifts

        /// <summary>
        /// Standing engine policy for nullable operands, applied in its mildest form: nothing
        /// propagates here - the branch value is returned untouched - and lifting only widens the
        /// static type enough to hold both possibilities. Boxing a nullable yields the plain boxed T
        /// or the null reference, so no value on the heap can tell the backends apart.
        /// </summary>
        [Test]
        public void ANullableBranchLiftsAgainstItsUnderlyingType()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? NullInt : 5)", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo(null);

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? NullInt : 5)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(5);
        }

        [Test]
        public void ANullableBranchHoldingAValueLiftsToo()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? SomeInt : 5)", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo(9);

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? SomeInt : 5)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(5);
        }

        [Test]
        public void TheNullableMayBeEitherBranch()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? 5 : NullInt)", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo(5);

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? 5 : NullInt)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(null);
        }

        // ----- everything else refuses

        /// <summary>
        /// C# widens these to a common type - <c>x ? 1 : 2.5</c> is a double there. This engine will
        /// not, and the measurement in the remarks above is why: the interpreter's result type follows
        /// the branch taken, so any common type disagrees with it on one path or the other. Refused
        /// compiled, served interpreted, and deliberately stricter than C#. Do not "fix" this by
        /// widening without ruling on the divergence it creates.
        /// </summary>
        [Test]
        public void NumericallyConvertibleBranchesAreRefusedButStillEvaluate()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<TernaryBranchCases, object>(
                    "(Number > 1 ? 1 : 2.5)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<TernaryBranchCases, object>(
                "(Number > 1 ? 1 : 2.5)", EvaluationMode.MustInterpret);

            var whenTrue = interpreted.GetValue(new TernaryBranchCases { Number = 4 });
            var whenFalse = interpreted.GetValue(new TernaryBranchCases { Number = 0 });

            // The very asymmetry that forbids a common type: same expression, two result types.
            Assert.AreEqual(typeof(int), whenTrue.GetType());
            Assert.AreEqual(typeof(double), whenFalse.GetType());
            Assert.AreEqual(1, whenTrue);
            Assert.AreEqual(2.5, whenFalse);
        }

        /// <summary>
        /// Genuinely incompatible branches, which C# refuses too (CS0173).
        /// </summary>
        [Test]
        public void IncompatibleBranchesAreRefusedButStillEvaluate()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<TernaryBranchCases, object>(
                    "(Number > 1 ? Name : 0)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<TernaryBranchCases, object>(
                "(Number > 1 ? Name : 0)", EvaluationMode.MustInterpret);

            Assert.AreEqual("abc", interpreted.GetValue(new TernaryBranchCases { Number = 4 }));
            Assert.AreEqual(0, interpreted.GetValue(new TernaryBranchCases { Number = 0 }));
        }

        [Test]
        public void TwoUnrelatedMemberBranchesAreRefusedButStillEvaluate()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<TernaryBranchCases, object>(
                    "(Number > 1 ? Number : Name)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<TernaryBranchCases, object>(
                "(Number > 1 ? Number : Name)", EvaluationMode.MustInterpret);

            Assert.AreEqual(4, interpreted.GetValue(new TernaryBranchCases { Number = 4 }));
            Assert.AreEqual("abc", interpreted.GetValue(new TernaryBranchCases { Number = 0 }));
        }

        /// <summary>
        /// The refusal is a <see cref="CompileErrorException"/> and specifically not the absorber's
        /// internal-error wrapper, which is the whole point: the shape is uncompiled, not broken.
        /// NUnit's Assert.Throws demands the exact type, so an absorbed defect fails this.
        /// </summary>
        [Test]
        public void TheRefusalIsNotReportedAsAnInternalDefect()
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<TernaryBranchCases, object>(
                    "(Number > 1 ? Name : 0)", EvaluationMode.MustCompile));

            Assert.IsFalse(
                refusal.Message.Contains("internal compiler error"),
                "a shape with no compiled form must not be reported as a defect of the engine");

            StringAssert.Contains("System.String", refusal.Message);
            StringAssert.Contains("System.Int32", refusal.Message);
        }

        // ----- the escape, and the shapes that never broke

        /// <summary>
        /// Casting a branch is the way out, and it yields a real type rather than an object: both
        /// backends answer a double here.
        /// </summary>
        [Test]
        public void CastingABranchGivesBothBranchesOneType()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? 1 as double : 2.5)", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo(1.0);

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? 1 as double : 2.5)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(2.5);
        }

        [Test]
        public void MatchedBranchesAreUnaffected()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? 'a' : 'b')", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo("a");

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? 1 : Number)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(0);

            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? Name : NoName)", new TernaryBranchCases { Number = 0 })
                .ResultEqualsTo(null);
        }

        /// <summary>
        /// The test operand keeps its own rules, which this change did not touch: a bool compiles, a
        /// bool? lifts through GetValueOrDefault, anything else is refused because only the
        /// interpreter reads other types as true or false.
        /// </summary>
        [Test]
        public void TheTestOperandIsUnaffected()
        {
            TestCompiledVsInterpreted<TernaryBranchCases, object>(
                "(Number > 1 ? 'a' : 'b')", new TernaryBranchCases { Number = 4 })
                .ResultEqualsTo("a");

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<TernaryBranchCases, object>(
                    "(Number ? 'a' : 'b')", EvaluationMode.MustCompile));
        }
    }
}

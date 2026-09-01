using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class EmptyAndFullCollections
    {
        public List<int> NoInts { get { return new List<int>(); } }
        public List<int> Ints { get { return new List<int> { 3, 7 }; } }

        public List<double> NoDoubles { get { return new List<double>(); } }
        public List<double> WithNan { get { return new List<double> { 3.0, double.NaN, 7.0 }; } }

        public List<decimal> NoDecimals { get { return new List<decimal>(); } }
        public List<decimal> Decimals { get { return new List<decimal> { 1.5m, 2.5m }; } }

        public List<DateTime> NoDates { get { return new List<DateTime>(); } }
        public List<DateTime> Dates { get { return new List<DateTime> { new DateTime(2020, 1, 1) }; } }

        public List<uint> NoUints { get { return new List<uint>(); } }
        public List<uint> Uints { get { return new List<uint> { 2u, 4u }; } }
        public List<short> Shorts { get { return new List<short> { 2, 4 }; } }

        public List<int?> NoNullableInts { get { return new List<int?>(); } }
        public List<string> NoStrings { get { return new List<string>(); } }
        public List<object> NoObjs { get { return new List<object>(); } }

        public int? NoInt { get { return null; } }
    }

    /// <summary>
    /// <c>min()</c>, <c>max()</c> and <c>average()</c> over an empty collection answer null on both
    /// backends. The compiled path used to throw <c>InvalidOperationException: Sequence contains no
    /// elements</c> whenever the item type was a non-nullable value type, because that is what
    /// <c>Enumerable.Min(IEnumerable&lt;int&gt;)</c> does for an empty sequence, while the interpreter
    /// answered null.
    /// </summary>
    /// <remarks>
    /// <p>
    /// That divergence was worse than a disagreement, and it is worth knowing why this is not a
    /// compile-refusal fix. <b>Compilation succeeded</b> - emitting the call is perfectly valid, and
    /// emptiness is not knowable then - so the exception arrived at <i>evaluation</i>, where the weakly
    /// typed path's fallback cannot help it: that fallback catches
    /// <see cref="CompileErrorException"/> while building the delegate and is long finished. The
    /// exception went straight out to the caller, and only when the data happened to be empty.
    /// </p>
    /// <p>
    /// Which backend a caller got decided the answer, and it followed from their declared context type
    /// rather than from anything they wrote: <c>GetValue&lt;Root&gt;</c> compiled and threw,
    /// <c>GetValue&lt;object&gt;</c> interpreted and returned null. No sweep could have found it either -
    /// <c>CompilationNeverLeaksTests</c> checks what escapes <i>compilation</i>, and this escaped
    /// evaluation.
    /// </p>
    /// <p>
    /// The fix is one edit on the compiled side: the three processors ask for the <b>nullable</b>
    /// overload of a non-nullable value item type, and <c>MethodNode</c> lifts the source to match. Null
    /// is how this engine says "there is no answer" - the ruling first- and last-match selection already
    /// runs on, and those are asserted beside these rows so the two stay one rule.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class EmptyCollectionAggregatorTests : BaseCompiledTests
    {
        // ----- the rows that used to throw

        [Test]
        public void AnEmptyCollectionOfANonNullableValueTypeAnswersNull()
        {
            var root = new EmptyAndFullCollections();

            foreach (var expr in new[]
                {
                    "NoInts.min()", "NoInts.max()", "NoInts.average()",
                    "NoDoubles.min()", "NoDoubles.max()", "NoDoubles.average()",
                    "NoDecimals.min()", "NoDecimals.max()", "NoDecimals.average()",
                    "NoDates.min()", "NoDates.max()",
                    "NoUints.min()", "NoUints.max()", "NoUints.average()"
                })
            {
                Assert.IsNull(
                    CompileGetter<EmptyAndFullCollections, object>(expr).GetValue(root),
                    "compiled: " + expr);

                Assert.IsNull(
                    InterpretGetter<EmptyAndFullCollections, object>(expr).GetValue(root),
                    "interpreted: " + expr);
            }
        }

        /// <summary>
        /// The item types that already agreed still do. A reference item type needs no lift at all -
        /// <c>Min&lt;T&gt;</c> returns null for an empty sequence when <c>T</c> is one - and an
        /// already-nullable type was asking for the nullable overload all along.
        /// </summary>
        [Test]
        public void TheItemTypesThatAlreadyAgreedStillDo()
        {
            var root = new EmptyAndFullCollections();

            foreach (var expr in new[]
                {
                    "NoNullableInts.min()", "NoNullableInts.max()", "NoNullableInts.average()",
                    "NoStrings.min()", "NoStrings.max()",
                    "NoObjs.min()", "NoObjs.max()", "NoObjs.average()"
                })
            {
                Assert.IsNull(
                    CompileGetter<EmptyAndFullCollections, object>(expr).GetValue(root),
                    "compiled: " + expr);

                Assert.IsNull(
                    InterpretGetter<EmptyAndFullCollections, object>(expr).GetValue(root),
                    "interpreted: " + expr);
            }
        }

        // ----- what must not have moved

        /// <summary>
        /// A non-empty collection answers exactly what it did. The runtime type matters as much as the
        /// value here: the compiled body is a <c>Nullable&lt;T&gt;</c> now, and boxing one that holds a
        /// value yields the plain boxed <c>T</c>, so nothing downstream can tell.
        /// </summary>
        [Test]
        public void NonEmptyAnswersAndTheirRuntimeTypesAreUnchanged()
        {
            var root = new EmptyAndFullCollections();

            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Ints.min()", root).ResultEqualsTo(3);
            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Ints.max()", root).ResultEqualsTo(7);
            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Ints.average()", root)
                .ResultEqualsTo(5.0);

            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Decimals.min()", root)
                .ResultEqualsTo(1.5m);
            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Decimals.average()", root)
                .ResultEqualsTo(2.0m);

            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Dates.min()", root)
                .ResultEqualsTo(new DateTime(2020, 1, 1));
            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Uints.min()", root)
                .ResultEqualsTo(2u);
        }

        /// <summary>
        /// The NaN answers are untouched, which is the guard that the nullable overload was not a
        /// behaviour change dressed as a type change: <c>Enumerable.Min</c> answers NaN if any item is
        /// one, and <c>Max</c> walks past it, on the nullable overload exactly as on the other.
        /// </summary>
        [Test]
        public void TheNaNAnswersAreUnchanged()
        {
            var root = new EmptyAndFullCollections();

            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("WithNan.min()", root)
                .ResultEqualsTo(double.NaN);
            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("WithNan.max()", root)
                .ResultEqualsTo(7.0);
        }

        /// <summary>
        /// A defect uncovered on the way and fixed with this, because the same helper serves more types
        /// now. The small integers have no <c>Enumerable.Average</c> overload of their own and were
        /// averaged through <c>Cast&lt;long&gt;</c> - but <c>Cast</c> unboxes, unboxing demands an exact
        /// type, and a boxed <c>uint</c> is not a <c>long</c>. So every <c>uint</c>, <c>short</c>,
        /// <c>ushort</c>, <c>byte</c> and <c>sbyte</c> collection threw <c>InvalidCastException</c>
        /// compiled while the interpreter answered - the same escapes-at-evaluation shape as the bug
        /// above, and just as invisible to a compile-time sweep.
        /// </summary>
        [Test]
        public void TheSmallIntegerTypesAverageInsteadOfThrowing()
        {
            var root = new EmptyAndFullCollections();

            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Uints.average()", root)
                .ResultEqualsTo(3.0);
            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Shorts.average()", root)
                .ResultEqualsTo(3.0);
        }

        // ----- the request shapes

        /// <summary>
        /// A request that can hold "no answer" gets null. A non-nullable one cannot hold it, so it is
        /// <b>refused at compile</b> - and refused whatever the collection contains, because
        /// <c>min()</c> answers a <c>Nullable&lt;T&gt;</c> either way and the request is unsound in
        /// itself, not merely unlucky with this data.
        /// </summary>
        /// <remarks>
        /// It used to compile. <c>Compiler</c> emitted the nullable-to-int conversion - C#'s explicit
        /// <c>(int)n</c> - and the shape threw only when the collection happened to be empty. C# refuses
        /// the same thing outright (<c>int x = someNullable;</c> is CS0266) and makes the programmer
        /// write the cast; inserting it for them meant the strongly typed path, which is exactly where a
        /// caller goes to learn whether their expression is sound, said nothing. The same rule now
        /// covers every nullable body: an <c>int?</c> property, lifted arithmetic, a selection, an
        /// aggregate.
        /// </remarks>
        [Test]
        public void ANonNullableRequestIsRefusedWhateverTheCollectionHolds()
        {
            var root = new EmptyAndFullCollections();

            Assert.IsNull(Expression.ParseGetter<EmptyAndFullCollections, int?>(
                "NoInts.min()", EvaluationMode.MustCompile).GetValue(root));
            Assert.IsNull(Expression.ParseGetter<EmptyAndFullCollections, object>(
                "NoInts.min()", EvaluationMode.MustCompile).GetValue(root));
            Assert.IsNull(Expression.ParseGetter<EmptyAndFullCollections, double?>(
                "NoInts.average()", EvaluationMode.MustCompile).GetValue(root));

            // refused for the empty collection and for the full one alike - the data is not the point
            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "NoInts.min()", EvaluationMode.MustCompile));
            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "Ints.min()", EvaluationMode.MustCompile));

            // and an int? property read into an int request is refused by the same rule
            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "NoInt", EvaluationMode.MustCompile));

            // the message names both fixes
            var thrown = Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "Ints.min()", EvaluationMode.MustCompile));

            Assert.That(thrown.Message, Does.Contain("absent value"));
        }

        /// <summary>
        /// Both escapes work and are the two C# offers: ask for the type the expression produces, or
        /// write the cast. The cast then behaves as a cast does - it fails on an absent value, on both
        /// backends.
        /// </summary>
        [Test]
        public void TheTwoEscapesFromTheRefusalBothCompile()
        {
            var root = new EmptyAndFullCollections();

            Assert.AreEqual(
                3,
                Expression.ParseGetter<EmptyAndFullCollections, int?>(
                    "Ints.min()", EvaluationMode.MustCompile).GetValue(root));

            Assert.AreEqual(
                3,
                Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "Ints.min() as int", EvaluationMode.MustCompile).GetValue(root));

            Assert.Throws<InvalidOperationException>(
                () => Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "NoInts.min() as int", EvaluationMode.MustCompile).GetValue(root));
        }

        /// <summary>
        /// The refusal is about a request that cannot hold an absent value, not about narrowing in
        /// general: a non-nullable body still converts to whatever non-nullable type was asked for.
        /// </summary>
        [Test]
        public void ANonNullableBodyStillConvertsToTheRequestedType()
        {
            var root = new EmptyAndFullCollections();

            Assert.AreEqual(
                2L,
                Expression.ParseGetter<EmptyAndFullCollections, long>(
                    "Ints.count()", EvaluationMode.MustCompile).GetValue(root));

            Assert.AreEqual(
                2,
                Expression.ParseGetter<EmptyAndFullCollections, short>(
                    "Ints.count()", EvaluationMode.MustCompile).GetValue(root));
        }

        /// <summary>
        /// First- and last-match selection answers the same question the same way, which is the point of
        /// asserting it here: "there is no answer" is null carried by a <c>Nullable&lt;T&gt;</c>, and a
        /// non-nullable request cannot hold it, so it is refused. One ruling, two features.
        /// </summary>
        [Test]
        public void SelectionAnswersTheSameQuestionTheSameWay()
        {
            var root = new EmptyAndFullCollections();

            Assert.IsNull(Expression.ParseGetter<EmptyAndFullCollections, int?>(
                "Ints.^{#this > 100}", EvaluationMode.MustCompile).GetValue(root));
            Assert.IsNull(Expression.ParseGetter<EmptyAndFullCollections, object>(
                "Ints.^{#this > 100}", EvaluationMode.MustCompile).GetValue(root));
            Assert.IsNull(Expression.ParseGetter<EmptyAndFullCollections, object>(
                "Ints.${#this > 100}", EvaluationMode.MustCompile).GetValue(root));
            Assert.IsNull(Expression.ParseGetter<EmptyAndFullCollections, object>(
                "NoInts.^{#this > 0}", EvaluationMode.MustCompile).GetValue(root));

            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "Ints.^{#this > 100}", EvaluationMode.MustCompile));

            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "Ints.^{#this > 5}", EvaluationMode.MustCompile));

            Assert.AreEqual(
                7,
                Expression.ParseGetter<EmptyAndFullCollections, int>(
                    "Ints.^{#this > 5} as int", EvaluationMode.MustCompile).GetValue(root));
        }

        // ----- downstream of a null answer

        /// <summary>
        /// The null flows on as a null does anywhere in this engine: lifted arithmetic propagates it, on
        /// both backends.
        /// </summary>
        [Test]
        public void ArithmeticOnAnEmptyAggregateLiftsToNull()
        {
            var root = new EmptyAndFullCollections();

            Assert.IsNull(CompileGetter<EmptyAndFullCollections, object>("NoInts.min() + 1").GetValue(root));
            Assert.IsNull(InterpretGetter<EmptyAndFullCollections, object>("NoInts.min() + 1").GetValue(root));

            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Ints.min() + 1", root)
                .ResultEqualsTo(4);
        }

        /// <summary>
        /// A member access chained onto the null answer diverges, and it is the engine's standing
        /// asymmetry rather than this change's doing: compiled code holds a <c>Nullable&lt;T&gt;</c>
        /// static type where the interpreter holds a type-less null. The identical pair on a plain
        /// <c>int?</c> property is asserted below as the proof.
        /// Do not fix one side: closing it needs a ruling about what a member call on a nullable holding
        /// nothing means.
        /// </summary>
        [Test]
        public void AMemberAccessChainedOntoTheNullAnswerFailsOnBothBackends()
        {
            var root = new EmptyAndFullCollections();

            // Both throw now. This used to be the recorded asymmetry - "" compiled against an exception
            // interpreted - and it was fixed by the ruling that a member written after a nullable is
            // read from the value inside it. The compiled path used to resolve ToString against the
            // Nullable<int> wrapper, which has one and answers "" when empty; it resolves against the
            // int now, and there is no int, so it fails as the interpreter always did.
            Assert.Catch<Exception>(
                () => CompileGetter<EmptyAndFullCollections, object>(
                    "NoInts.min().ToString()").GetValue(root));

            Assert.Catch<Exception>(
                () => InterpretGetter<EmptyAndFullCollections, object>(
                    "NoInts.min().ToString()").GetValue(root));

            // the same pair, on a plain int? property - nothing to do with the aggregators
            Assert.Catch<Exception>(
                () => CompileGetter<EmptyAndFullCollections, object>("NoInt.ToString()").GetValue(root));

            Assert.Catch<Exception>(
                () => InterpretGetter<EmptyAndFullCollections, object>("NoInt.ToString()").GetValue(root));

            // and with items there is no question to answer
            TestCompiledVsInterpreted<EmptyAndFullCollections, object>("Ints.min().ToString()", root)
                .ResultEqualsTo("3");
        }
    }
}

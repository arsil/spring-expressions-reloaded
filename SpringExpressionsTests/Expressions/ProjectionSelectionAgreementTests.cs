using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringCore;
using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Holds stable instances: the two backends are compared by value, and a property handing back a
    /// fresh collection each call would fail on reference identity rather than on anything being wrong.
    /// </summary>
    public class ProjectionSourceHolder
    {
        public List<int> Ints { get; } = new List<int> { 1, 2, 3 };
        public int[] IntArray { get; } = { 4, 5, 6 };
        public List<string> Names { get; } = new List<string> { "Ala", "Ola", "Basia" };
        public int? NullInt { get; } = null;
        public Dictionary<string, int> Dict { get; } = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        public int Age { get; } = 45;
    }

    /// <summary>
    /// Whether the two backends agree on the runtime type of a projection or selection result.
    /// </summary>
    /// <remarks>
    /// The same question as ListLiteralAgreementTests, one node later. The compiled path builds a
    /// List&lt;T&gt; of the item type while the interpreter sees boxed values and can only build a list
    /// of object; the result used to come back as a List&lt;T&gt; from one backend and an ArrayList from
    /// the other - two different classes, not merely two item types. Now both are lists, and the
    /// compiled root is reprojected to List&lt;object&gt; where the caller asked for nothing narrower.
    /// </remarks>
    [TestFixture]
    public class ProjectionSelectionAgreementTests : BaseCompiledTests
    {
        [Test]
        public void ProjectionOverATypedList()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.!{#this + 1}", holder)
                .ResultEqualsTo(new List<object> { 2, 3, 4 });
        }

        [Test]
        public void ProjectionOverAnArray()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("IntArray.!{#this + 1}", holder)
                .ResultEqualsTo(new List<object> { 5, 6, 7 });
        }

        /// <summary>
        /// A projection whose body reads a member of the item, the shape the upstream tests use.
        /// </summary>
        [Test]
        public void ProjectionOfAnItemMember()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Names.!{Length}", holder)
                .ResultEqualsTo(new List<object> { 3, 3, 5 });
        }

        [Test]
        public void SelectionOverATypedList()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.?{#this > 1}", holder)
                .ResultEqualsTo(new List<object> { 2, 3 });
        }

        [Test]
        public void SelectionOverAnArray()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("IntArray.?{#this > 4}", holder)
                .ResultEqualsTo(new List<object> { 5, 6 });
        }

        /// <summary>
        /// An empty result is still a freshly built list of object from both backends - never null and
        /// never the source instance.
        /// </summary>
        [Test]
        public void SelectionThatMatchesNothing()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.?{#this > 99}", holder)
                .ResultEqualsTo(new List<object>());
        }

        /// <summary>
        /// ^{} and ${} had no compiled form at all - both nodes' emitters were an unconditional refusal -
        /// so every use fell back to the interpreter and a strongly typed request for one was a hard
        /// failure. They compile now, mirroring SelectionNode: the predicate is compiled to a delegate
        /// over the item type and handed to a static helper as a constant.
        /// </summary>
        [Test]
        public void ANonEnumerableSourceIsRefusedRatherThanThrownAt()
        {
            var holder = new ProjectionSourceHolder();

            // SelectionNode and ProjectionNode used to throw ArgumentException here themselves, which the
            // weakly typed path's catch (CompileErrorException) cannot see. Note the typed weak root
            // below: ExpressionEvaluator.GetValue binds at object, where the property does not resolve at
            // all and compilation fails earlier for an unrelated reason - which is what hid this.
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ProjectionSourceHolder, object>("Age.?{#this > 1}"));
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ProjectionSourceHolder, object>("Age.!{#this}"));
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ProjectionSourceHolder, object>("Age.^{#this > 1}"));
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ProjectionSourceHolder, object>("Age.${#this > 1}"));

            // the interpreter reports the bad source at evaluation, as it always did
            Assert.Throws<ArgumentException>(
                () => Expression.Parse("Age.?{#this > 1}").GetValue<ProjectionSourceHolder>(holder));
            Assert.Throws<ArgumentException>(
                () => Expression.Parse("Age.!{#this}").GetValue<ProjectionSourceHolder>(holder));
        }

        /// <summary>
        /// A dictionary source compiles, and its item type is KeyValuePair&lt;K, V&gt; on both backends.
        /// <p>
        /// The emitters used to take the first generic argument as the item type - a dictionary's key
        /// type - so a predicate valid for the key got as far as the emitted call and threw
        /// ArgumentException out of LINQ, which the weak path's fallback cannot catch, while everything
        /// else refused with a message blaming the wrong thing. The item type is read from the
        /// IEnumerable&lt;T&gt; the source actually implements now, which both fixes the leak and lets these
        /// shapes emit.
        /// </p>
        /// </summary>
        [Test]
        public void ADictionarySourceCompilesWithKeyValuePairAsItsItemType()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Dict.?{Value > 1}", holder)
                .ResultEqualsTo(new List<object> { new KeyValuePair<string, int>("b", 2) });
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Dict.!{Key}", holder)
                .ResultEqualsTo(new List<object> { "a", "b" });
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Dict.!{Value}", holder)
                .ResultEqualsTo(new List<object> { 1, 2 });
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Dict.^{Value > 1}", holder)
                .ResultEqualsTo(new KeyValuePair<string, int>("b", 2));
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Dict.^{Value > 99}", holder)
                .ResultEqualsTo(null);

            // ${} still refuses one: it walks the source backwards through an indexer, and a dictionary
            // is not an IList - which is exactly what the interpreter demands too.
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ProjectionSourceHolder, object>("Dict.${Value > 1}"));
            Assert.Throws<ArgumentException>(
                () => Expression.Parse("Dict.${Value > 1}").GetValue<ProjectionSourceHolder>(holder));

            // The shape that used to leak: 'Length' is valid for the key type but not for the real item.
            // It must fail as a refusal compiled, and as the interpreter's own error weakly.
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ProjectionSourceHolder, object>("Dict.?{Length > 0}"));
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ProjectionSourceHolder, object>("Dict.!{Length}"));
            Assert.Throws<InvalidPropertyException>(
                () => Expression.Parse("Dict.?{Length > 0}").GetValue<ProjectionSourceHolder>(holder));
            Assert.Throws<InvalidPropertyException>(
                () => Expression.Parse("Dict.!{Length}").GetValue<ProjectionSourceHolder>(holder));
        }

        [Test]
        public void SelectionOfTheFirstAndLastMatch()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.^{#this > 1}", holder)
                .ResultEqualsTo(2);
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.${#this > 1}", holder)
                .ResultEqualsTo(3);

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("IntArray.^{#this > 4}", holder)
                .ResultEqualsTo(5);
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("IntArray.${#this > 4}", holder)
                .ResultEqualsTo(6);

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Names.^{#this == 'Ola'}", holder)
                .ResultEqualsTo("Ola");
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Names.${Length == 3}", holder)
                .ResultEqualsTo("Ola");
        }

        /// <summary>
        /// The nullable-result ruling, pinned. "Nothing matched" is null from both backends for a value
        /// item type as much as for a reference one: the compiled path returns Nullable&lt;T&gt; when the
        /// item type is a non-nullable value type, and boxing a nullable that holds no value produces the
        /// null reference itself - so the weakly typed path, which always asks for object, cannot tell the
        /// backends apart by construction. A helper returning plain T would have answered default(T)
        /// here, which is 0 for an int source and a silent disagreement.
        /// </summary>
        [Test]
        public void SelectionOfTheFirstAndLastMatchThatMatchesNothingIsNull()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.^{#this > 99}", holder)
                .ResultEqualsTo(null);
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.${#this > 99}", holder)
                .ResultEqualsTo(null);

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("IntArray.^{#this > 99}", holder)
                .ResultEqualsTo(null);
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("IntArray.${#this > 99}", holder)
                .ResultEqualsTo(null);

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Names.^{#this == 'zzz'}", holder)
                .ResultEqualsTo(null);
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Names.${#this == 'zzz'}", holder)
                .ResultEqualsTo(null);
        }

        /// <summary>
        /// No value on the heap ever reports Nullable&lt;int&gt; as its type - boxing collapses a nullable
        /// to either null or a plain boxed T - which is the reason the nullable result is invisible to a
        /// caller and the two backends stay indistinguishable.
        /// </summary>
        [Test]
        public void AMatchedValueItemComesBackAsThePlainItemType()
        {
            var holder = new ProjectionSourceHolder();

            var compiled = CompileGetter<ProjectionSourceHolder, object>("Ints.^{#this > 1}").GetValue(holder);
            var interpreted = InterpretGetter<ProjectionSourceHolder, object>("Ints.^{#this > 1}").GetValue(holder);

            Assert.AreEqual(typeof(int), compiled.GetType());
            Assert.AreEqual(typeof(int), interpreted.GetType());
        }

        /// <summary>
        /// A nullable request is satisfied by both backends: the compiled path returns the nullable it
        /// already holds, and the interpreted getter's "value is TResult" test passes for a boxed int.
        /// </summary>
        [Test]
        public void ANullableRequestIsSatisfiedByBothBackends()
        {
            var holder = new ProjectionSourceHolder();

            Assert.AreEqual(2, CompileGetter<ProjectionSourceHolder, int?>("Ints.^{#this > 1}").GetValue(holder));
            Assert.AreEqual(2, InterpretGetter<ProjectionSourceHolder, int?>("Ints.^{#this > 1}").GetValue(holder));

            Assert.IsNull(CompileGetter<ProjectionSourceHolder, int?>("Ints.^{#this > 99}").GetValue(holder));
            Assert.IsNull(InterpretGetter<ProjectionSourceHolder, int?>("Ints.^{#this > 99}").GetValue(holder));
        }

        /// <summary>
        /// A non-nullable request over a body that can be absent is refused, and the refusal does not
        /// depend on the data: <c>^{}</c> answers a <c>Nullable&lt;int&gt;</c> whatever the collection
        /// holds, so a request for a plain <c>int</c> is unsound whether or not anything matches. The
        /// compile phase is where a caller finds that out, so it says so there.
        /// </summary>
        /// <remarks>
        /// This used to compile: <c>Compiler</c> emitted the nullable-to-int conversion - C#'s explicit
        /// <c>(int)n</c> - and the shape then threw <c>InvalidOperationException</c> compiled against
        /// <c>NullReferenceException</c> interpreted, only when nothing matched. C# refuses the same
        /// thing outright (<c>int x = someNullable;</c> is CS0266), and inserting the cast on the caller's
        /// behalf hid an unsound request until the one evaluation that had no answer.
        /// The escapes are both spellings a caller could have written, asserted below.
        /// </remarks>
        [Test]
        public void ANonNullableRequestIsRefusedWhateverTheDataHolds()
        {
            var holder = new ProjectionSourceHolder();

            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<ProjectionSourceHolder, int>(
                    "Ints.^{#this > 1}", EvaluationMode.MustCompile));

            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<ProjectionSourceHolder, int>(
                    "Ints.^{#this > 99}", EvaluationMode.MustCompile));

            // the interpreter serves it, and fails only when nothing matched
            Assert.AreEqual(2, InterpretGetter<ProjectionSourceHolder, int>("Ints.^{#this > 1}").GetValue(holder));
            Assert.Throws<NullReferenceException>(
                () => InterpretGetter<ProjectionSourceHolder, int>("Ints.^{#this > 99}").GetValue(holder));

            // escape one: ask for the type the expression actually produces
            Assert.AreEqual(
                2,
                Expression.ParseGetter<ProjectionSourceHolder, int?>(
                    "Ints.^{#this > 1}", EvaluationMode.MustCompile).GetValue(holder));

            // escape two: write the cast C# would have made you write
            Assert.AreEqual(
                2,
                Expression.ParseGetter<ProjectionSourceHolder, int>(
                    "Ints.^{#this > 1} as int", EvaluationMode.MustCompile).GetValue(holder));
        }

        /// <summary>
        /// ${} needs an IList: the interpreter walks the source backwards through its indexer, and the
        /// compiled helper does the same so that the predicate runs over the same items, in the same
        /// order, the same number of times (LINQ's LastOrDefault would give the same answer while
        /// evaluating the predicate for every item, which a side-effecting predicate can tell apart). A
        /// source that is only enumerable is therefore refused rather than compiled, and the weak path's
        /// fallback hands it to the interpreter, which reports the ArgumentException it always did. ^{}
        /// asks only for IEnumerable and compiles the same source.
        /// </summary>
        [Test]
        public void SelectionOfTheLastMatchNeedsAListWhileTheFirstDoesNot()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<object>("({1,2} + {3}).${#this == 3}"));

            var holder = new ProjectionSourceHolder();
            Assert.Throws<ArgumentException>(
                () => ExpressionEvaluator.GetValue(holder, "({1,2} + {3}).${#this == 3}"));

            TestCompiledVsInterpreted<object>("({1,2} + {3}).^{#this == 3}").ResultEqualsTo(3);
        }

        /// <summary>
        /// A member access chained onto a no-match result diverges - and that is pre-existing, not these
        /// nodes' doing: a plain int? property behaves identically, because the compiled path holds a
        /// nullable static type where the interpreter holds a type-less null. Recorded with the NullInt
        /// twin as the proof that ^{} merely reaches the same edge. Do not "fix" one side without ruling
        /// on nullable member access in general. Everything that is not an error path agrees, including
        /// lifted arithmetic in both directions.
        /// </summary>
        [Test]
        public void AChainOntoAFirstMatchAgreesExceptOnTheNoMatchErrorPath()
        {
            var holder = new ProjectionSourceHolder();

            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.^{#this > 1}.ToString()", holder)
                .ResultEqualsTo("2");
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.^{#this > 1} + 1", holder)
                .ResultEqualsTo(3);
            TestCompiledVsInterpreted<ProjectionSourceHolder, object>("Ints.^{#this > 99} + 1", holder)
                .ResultEqualsTo(null);

            // No match means no value, so a member written after it fails - on both backends. This was
            // the recorded divergence: Nullable<int>.ToString() answered an empty string where the
            // interpreter could not resolve ToString against a null at all. The compiled path resolves
            // members against the value inside the nullable now rather than against the wrapper, so
            // there is no ToString to find either.
            Assert.Catch<Exception>(
                () => CompileGetter<ProjectionSourceHolder, object>("Ints.^{#this > 99}.ToString()").GetValue(holder));
            Assert.Catch<Exception>(
                () => InterpretGetter<ProjectionSourceHolder, object>("Ints.^{#this > 99}.ToString()").GetValue(holder));

            // and the same for a nullable property, which no selection node touches
            Assert.Catch<Exception>(
                () => CompileGetter<ProjectionSourceHolder, object>("NullInt.ToString()").GetValue(holder));
            Assert.Catch<Exception>(
                () => InterpretGetter<ProjectionSourceHolder, object>("NullInt.ToString()").GetValue(holder));
        }

        /// <summary>
        /// A null predicate result counts as no match, not as an error: a nullable operand inside the
        /// predicate makes the whole predicate "unknown", which a filter reads as false. This used to
        /// throw NullReferenceException from the interpreter's (bool) cast. The compiled path refuses a
        /// non-boolean predicate, so the interpreter serves the shape either way.
        /// </summary>
        [Test]
        public void SelectionPredicateTreatsNullAsNoMatch()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ProjectionSourceHolder, object>(
                    "Ints.?{#root.NullInt and 3}", EvaluationMode.MustCompile));

            var holder = new ProjectionSourceHolder();
            var selected = InterpretGetter<ProjectionSourceHolder, object>("Ints.?{#root.NullInt and 3}")
                .GetValue(holder);

            Assert.AreEqual(typeof(List<object>), selected.GetType());
            Assert.AreEqual(new List<object>(), selected);

            Assert.IsNull(ExpressionEvaluator.GetValue(holder, "Ints.^{#root.NullInt and 3}"));
            Assert.IsNull(ExpressionEvaluator.GetValue(holder, "Ints.${#root.NullInt and 3}"));
        }

        /// <summary>
        /// A selection constructs its result, so the caller never receives the source collection itself,
        /// from either backend - even when every item matched.
        /// </summary>
        [Test]
        public void SelectionNeverReturnsTheSourceInstance()
        {
            var holder = new ProjectionSourceHolder();

            var compiled = CompileGetter<ProjectionSourceHolder, object>("Ints.?{#this > 0}").GetValue(holder);
            var interpreted = InterpretGetter<ProjectionSourceHolder, object>("Ints.?{#this > 0}").GetValue(holder);

            Assert.AreNotSame(holder.Ints, compiled, "compiled path handed back the source collection");
            Assert.AreNotSame(holder.Ints, interpreted, "interpreted path handed back the source collection");
        }

        /// <summary>
        /// Asking for the item type gets exactly a List&lt;T&gt;; only where nothing narrower than object
        /// was requested does the root become the List&lt;object&gt; the interpreter would build.
        /// </summary>
        [Test]
        public void RequestedItemTypeSurvives()
        {
            var holder = new ProjectionSourceHolder();

            Assert.AreEqual(typeof(List<int>),
                CompileGetter<ProjectionSourceHolder, List<int>>("Ints.!{#this + 1}").GetValue(holder).GetType());
            Assert.AreEqual(typeof(List<int>),
                CompileGetter<ProjectionSourceHolder, IList<int>>("Ints.?{#this > 1}").GetValue(holder).GetType());
            Assert.AreEqual(typeof(List<object>),
                CompileGetter<ProjectionSourceHolder, object>("Ints.!{#this + 1}").GetValue(holder).GetType());
        }

        /// <summary>
        /// The reprojection applies to the root only: a projection feeding an aggregator keeps its item
        /// type, which is what lets the aggregation stay compiled.
        /// </summary>
        /// <remarks>
        /// Asserted per backend, because the backends disagree on sum()'s result type for ints - int
        /// compiled, double interpreted. That divergence belongs to the numeric-promotion cluster, not
        /// to projections; this test pins only that the projection under the aggregator keeps working
        /// from both sides.
        /// </remarks>
        [Test]
        public void AggregatorOverAProjectionStaysCompiledAtTheItemType()
        {
            var holder = new ProjectionSourceHolder();

            var compiled = CompileGetter<ProjectionSourceHolder, object>("Ints.!{#this + 1}.sum()").GetValue(holder);
            Assert.AreEqual(typeof(int), compiled.GetType());
            Assert.AreEqual(9, compiled);

            var interpreted = InterpretGetter<ProjectionSourceHolder, object>("Ints.!{#this + 1}.sum()").GetValue(holder);
            Assert.AreEqual(typeof(double), interpreted.GetType());
            Assert.AreEqual(9.0d, interpreted);
        }
    }
}


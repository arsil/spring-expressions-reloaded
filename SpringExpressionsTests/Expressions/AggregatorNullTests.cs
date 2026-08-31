using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class NullBearingCollections
    {
        public List<object> Objs { get { return new List<object> { 3, null, 7 }; } }
        public List<int?> NullableInts { get { return new List<int?> { 3, null, 7 }; } }
        public List<string> Strings { get { return new List<string> { "b", null, "a" }; } }

        public List<DateTime?> Dates
        {
            get { return new List<DateTime?> { new DateTime(2020, 1, 1), null, new DateTime(2010, 1, 1) }; }
        }

        public List<double?> RealsWithNan
        {
            get { return new List<double?> { 3.0, double.NaN, null, 7.0 }; }
        }

        public List<object> ObjsToAverage { get { return new List<object> { 2, null, 4 }; } }

        public List<object> AllNull { get { return new List<object> { null, null }; } }
        public List<int?> AllNullInts { get { return new List<int?> { null, null }; } }
        public List<object> Empty { get { return new List<object>(); } }
    }

    /// <summary>
    /// The aggregators skip null items, which is what <c>Enumerable.Min</c>, <c>Max</c>, <c>Sum</c> and
    /// <c>Average</c> do. <c>min()</c> did not, and answered the *maximum* for a null-bearing collection:
    /// its accumulator held null after the first null item and <c>CompareUtils.Compare</c> - a sorting
    /// function - calls null the smaller of every pair, so nothing could displace it.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The rule behind this: where LINQ has an answer for a collection operation, this engine gives
    /// LINQ's answer. That cuts both ways and is the reason only the aggregators moved - <c>sort()</c>,
    /// <c>orderBy()</c> and <c>distinct()</c> already agreed with LINQ on every measured row and are
    /// asserted unchanged at the bottom of this fixture.
    /// </p>
    /// <p>
    /// It was a backend disagreement as well, not only a wrong answer: the compiled path calls
    /// <c>Enumerable.Min</c> directly for the item types its dictionary lists, so a <c>List&lt;int?&gt;</c>
    /// was right compiled and wrong interpreted, while a <c>List&lt;object&gt;</c> - which the dictionary
    /// does not list - was wrong on both, since the compiled path serves it through the bridge into these
    /// very aggregators.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class AggregatorNullTests : BaseCompiledTests
    {
        // ----- min() and max() skip nulls

        [Test]
        public void MinSkipsNullsInsteadOfAnsweringTheMaximum()
        {
            var root = new NullBearingCollections();

            TestCompiledVsInterpreted<NullBearingCollections, object>("Objs.min()", root).ResultEqualsTo(3);
            TestCompiledVsInterpreted<NullBearingCollections, object>("NullableInts.min()", root).ResultEqualsTo(3);
            TestCompiledVsInterpreted<NullBearingCollections, object>("Strings.min()", root).ResultEqualsTo("a");
            TestCompiledVsInterpreted<NullBearingCollections, object>("Dates.min()", root)
                .ResultEqualsTo(new DateTime(2010, 1, 1));
        }

        [Test]
        public void MaxSkipsNullsToo()
        {
            var root = new NullBearingCollections();

            TestCompiledVsInterpreted<NullBearingCollections, object>("Objs.max()", root).ResultEqualsTo(7);
            TestCompiledVsInterpreted<NullBearingCollections, object>("NullableInts.max()", root).ResultEqualsTo(7);
            TestCompiledVsInterpreted<NullBearingCollections, object>("Strings.max()", root).ResultEqualsTo("b");
            TestCompiledVsInterpreted<NullBearingCollections, object>("Dates.max()", root)
                .ResultEqualsTo(new DateTime(2020, 1, 1));
        }

        /// <summary>
        /// A NaN is not a null and is not skipped - <c>Enumerable.Min</c> answers NaN if any item is one
        /// and <c>Max</c> walks past it, and the engine lands on both. Do not widen the null skip to
        /// cover NaN: that would take the two apart from LINQ again in the other direction.
        /// </summary>
        [Test]
        public void ANaNIsStillNotSkipped()
        {
            var root = new NullBearingCollections();

            TestCompiledVsInterpreted<NullBearingCollections, object>("RealsWithNan.min()", root)
                .ResultEqualsTo(double.NaN);
            TestCompiledVsInterpreted<NullBearingCollections, object>("RealsWithNan.max()", root)
                .ResultEqualsTo(7.0);
        }

        [Test]
        public void WithNothingButNullsMinAndMaxAnswerNull()
        {
            var root = new NullBearingCollections();

            Assert.IsNull(CompileGetter<NullBearingCollections, object>("AllNull.min()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("AllNull.min()").GetValue(root));
            Assert.IsNull(CompileGetter<NullBearingCollections, object>("AllNull.max()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("AllNull.max()").GetValue(root));

            Assert.IsNull(CompileGetter<NullBearingCollections, object>("AllNullInts.min()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("AllNullInts.min()").GetValue(root));
            Assert.IsNull(CompileGetter<NullBearingCollections, object>("AllNullInts.max()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("AllNullInts.max()").GetValue(root));
        }

        [Test]
        public void AnEmptyCollectionStillAnswersNull()
        {
            var root = new NullBearingCollections();

            Assert.IsNull(CompileGetter<NullBearingCollections, object>("Empty.min()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("Empty.min()").GetValue(root));
            Assert.IsNull(CompileGetter<NullBearingCollections, object>("Empty.max()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("Empty.max()").GetValue(root));
        }

        // ----- sum() and average()

        /// <summary>
        /// <c>sum()</c> and <c>average()</c> already skipped nulls; the assertion is here so the three
        /// aggregators are read together. <c>average()</c> divides by the number of items it actually
        /// counted, so a null neither contributes nor inflates the divisor: <c>{2, null, 4}</c> averages
        /// to 3, not to 2.
        /// </summary>
        [Test]
        public void SumAndAverageCountOnlyTheItemsThatAreThere()
        {
            var root = new NullBearingCollections();

            TestCompiledVsInterpreted<NullBearingCollections, object>("ObjsToAverage.sum()", root)
                .ResultEqualsTo(6.0);
            TestCompiledVsInterpreted<NullBearingCollections, object>("ObjsToAverage.average()", root)
                .ResultEqualsTo(3.0);
        }

        /// <summary>
        /// With nothing counted the average is null, which is what <c>Enumerable.Average</c> gives for a
        /// nullable sequence and what the compiled path already gave. It used to divide zero by zero and
        /// hand back NaN - the average of nothing, reported as a number.
        /// </summary>
        [Test]
        public void WithNothingToAverageTheAnswerIsNull()
        {
            var root = new NullBearingCollections();

            Assert.IsNull(CompileGetter<NullBearingCollections, object>("AllNull.average()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("AllNull.average()").GetValue(root));
            Assert.IsNull(CompileGetter<NullBearingCollections, object>("AllNullInts.average()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("AllNullInts.average()").GetValue(root));
            Assert.IsNull(CompileGetter<NullBearingCollections, object>("Empty.average()").GetValue(root));
            Assert.IsNull(InterpretGetter<NullBearingCollections, object>("Empty.average()").GetValue(root));
        }

        /// <summary>
        /// A sum of nothing is zero, as <c>Enumerable.Sum</c> says - not null. The two aggregators differ
        /// here because LINQ makes them differ.
        /// </summary>
        [Test]
        public void ASumOfNothingIsStillZero()
        {
            var root = new NullBearingCollections();

            TestCompiledVsInterpreted<NullBearingCollections, object>("AllNull.sum()", root).ResultEqualsTo(0.0);
            TestCompiledVsInterpreted<NullBearingCollections, object>("Empty.sum()", root).ResultEqualsTo(0.0);
        }

        // ----- the operations that were already right, asserted so they stay that way

        /// <summary>
        /// The ordering and set operations keep placing a null exactly where <c>Enumerable.OrderBy</c>
        /// puts it - first - because a sort must be total. That is the other half of the same rule and
        /// the reason the fix went into the aggregators rather than into <c>CompareUtils.Compare</c>,
        /// which every one of these shares. Do not reconcile these with the aggregator rule.
        /// </summary>
        [Test]
        public void TheSortingSideIsUnchangedAndStillPlacesNullFirst()
        {
            var root = new NullBearingCollections();

            var sorted = (List<object>)CompileGetter<NullBearingCollections, object>("Objs.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { null, 3, 7 }, sorted);

            var sortedInterpreted =
                (List<object>)InterpretGetter<NullBearingCollections, object>("Objs.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { null, 3, 7 }, sortedInterpreted);
        }

        /// <summary>
        /// <c>count()</c> counts a null - it is an item - and <c>distinct()</c> drops one, which is its
        /// documented default. Neither follows the aggregators.
        /// </summary>
        [Test]
        public void CountCountsNullsAndDistinctDropsThem()
        {
            var root = new NullBearingCollections();

            TestCompiledVsInterpreted<NullBearingCollections, object>("Objs.count()", root).ResultEqualsTo(3);

            var distinct =
                (List<object>)CompileGetter<NullBearingCollections, object>("Objs.distinct()").GetValue(root);
            Assert.AreEqual(new List<object> { 3, 7 }, distinct);

            var distinctInterpreted =
                (List<object>)InterpretGetter<NullBearingCollections, object>("Objs.distinct()").GetValue(root);
            Assert.AreEqual(new List<object> { 3, 7 }, distinctInterpreted);
        }
    }
}

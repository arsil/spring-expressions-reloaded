using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

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

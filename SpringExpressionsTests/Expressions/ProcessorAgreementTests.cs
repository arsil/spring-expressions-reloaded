using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Holds stable instances: the two backends are compared by value, and a property handing back a
    /// fresh collection each call would fail on reference identity rather than on anything being wrong.
    /// </summary>
    public class ProcessorSourceHolder
    {
        public List<int> Ints { get; } = new List<int> { 3, 1, 2 };
        public int[] IntArray { get; } = { 4, 6, 5 };
        public List<string> Names { get; } = new List<string> { "Ola", "Ala", "Ela" };
        public List<int> Dupes { get; } = new List<int> { 3, 1, 3, 2 };
        public List<int> EmptyInts { get; } = new List<int>();
        public ArrayList Objects { get; } = new ArrayList { 1, "a", 2.5m };

        public List<GenericOnlyComparable> Comparables { get; } = new List<GenericOnlyComparable>
            { new GenericOnlyComparable(2), new GenericOnlyComparable(1), new GenericOnlyComparable(3) };
    }

    /// <summary>
    /// Comparable through IComparable&lt;T&gt; alone - no non-generic IComparable - so ordering it
    /// requires the item type's own comparer, which a boxed value compared as object never reaches.
    /// </summary>
    public class GenericOnlyComparable : System.IComparable<GenericOnlyComparable>
    {
        public GenericOnlyComparable(int rank) { Rank = rank; }

        public int Rank { get; }

        public int CompareTo(GenericOnlyComparable other) => Rank.CompareTo(other.Rank);
    }

    /// <summary>
    /// Whether the two backends agree that a processor result is a List&lt;object&gt;.
    /// </summary>
    /// <remarks>
    /// One convention for every collection the engine builds: the weakly typed path returns
    /// List&lt;object&gt;, whatever the source's item type. The interpreter processors build it
    /// directly; the compiled path keeps List&lt;T&gt; inside the tree and the root is reprojected,
    /// the same boundary a literal or a projection goes through. A typed request still gets its
    /// List&lt;T&gt; from the compiled path. sort() and distinct() used to keep the item type on both
    /// backends, and reverse()/orderBy()/sort() used to hand back the caller's own collection whenever
    /// the source was empty.
    /// </remarks>
    [TestFixture]
    public class ProcessorAgreementTests : BaseCompiledTests
    {
        [Test]
        public void ReverseOverATypedList()
        {
            var holder = new ProcessorSourceHolder();

            var result = TestCompiledVsInterpreted<ProcessorSourceHolder, object>("Ints.reverse()", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new object[] { 2, 1, 3 }, result);
        }

        [Test]
        public void ReverseOverAnArray()
        {
            var holder = new ProcessorSourceHolder();

            var result = TestCompiledVsInterpreted<ProcessorSourceHolder, object>("IntArray.reverse()", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new object[] { 5, 6, 4 }, result);
        }

        [Test]
        public void ReverseOverANonGenericCollection()
        {
            var holder = new ProcessorSourceHolder();

            var result = TestCompiledVsInterpreted<ProcessorSourceHolder, object>("Objects.reverse()", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new object[] { 2.5m, "a", 1 }, result);
        }

        /// <summary>
        /// An empty source is not returned as-is: the result is a freshly built list from both backends.
        /// The interpreter used to hand back the caller's own collection whenever Count was zero, so
        /// identity depended on the data.
        /// </summary>
        [Test]
        public void ReverseOfAnEmptyListIsAFreshList()
        {
            var holder = new ProcessorSourceHolder();

            TestCompiledVsInterpreted<ProcessorSourceHolder, object>("EmptyInts.reverse()", holder)
                .ResultEqualsTo(new List<object>());

            var compiled = CompileGetter<ProcessorSourceHolder, object>("EmptyInts.reverse()").GetValue(holder);
            var interpreted = InterpretGetter<ProcessorSourceHolder, object>("EmptyInts.reverse()").GetValue(holder);

            Assert.AreNotSame(holder.EmptyInts, compiled, "compiled path handed back the source collection");
            Assert.AreNotSame(holder.EmptyInts, interpreted, "interpreted path handed back the source collection");
        }

        [Test]
        public void SortAscendingAndDescending()
        {
            var holder = new ProcessorSourceHolder();

            var ascending = TestCompiledVsInterpreted<ProcessorSourceHolder, object>("Ints.sort()", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), ascending.GetType());
            Assert.AreEqual(new object[] { 1, 2, 3 }, ascending);

            var descending = TestCompiledVsInterpreted<ProcessorSourceHolder, object>("Ints.sort(false)", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), descending.GetType());
            Assert.AreEqual(new object[] { 3, 2, 1 }, descending);
        }

        /// <summary>
        /// Same freshness rule as reverse: sort()'s empty-source early return handed back the caller's
        /// own collection, and its non-generic branch used to return a typed array whose element type
        /// was guessed from the first non-null element.
        /// </summary>
        [Test]
        public void SortOfAnEmptyListIsAFreshList()
        {
            var holder = new ProcessorSourceHolder();

            TestCompiledVsInterpreted<ProcessorSourceHolder, object>("EmptyInts.sort()", holder)
                .ResultEqualsTo(new List<object>());

            var compiled = CompileGetter<ProcessorSourceHolder, object>("EmptyInts.sort()").GetValue(holder);
            var interpreted = InterpretGetter<ProcessorSourceHolder, object>("EmptyInts.sort()").GetValue(holder);

            Assert.AreNotSame(holder.EmptyInts, compiled, "compiled path handed back the source collection");
            Assert.AreNotSame(holder.EmptyInts, interpreted, "interpreted path handed back the source collection");
        }

        /// <summary>
        /// The result is a List&lt;object&gt;, but the ordering still comes from the item type's own
        /// comparer: IComparable&lt;T&gt;-only types sort in both backends.
        /// </summary>
        [Test]
        public void SortUsesTheItemTypesOwnComparison()
        {
            var holder = new ProcessorSourceHolder();

            var result = (List<object>)TestCompiledVsInterpreted<ProcessorSourceHolder, object>(
                    "Comparables.sort()", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new[] { 1, 2, 3 },
                new[]
                {
                    ((GenericOnlyComparable)result[0]).Rank,
                    ((GenericOnlyComparable)result[1]).Rank,
                    ((GenericOnlyComparable)result[2]).Rank,
                });
        }

        /// <summary>
        /// distinct() is an order-preserving dedup, not a set constructor: first occurrence wins and
        /// the relative order survives.
        /// </summary>
        [Test]
        public void DistinctKeepsFirstSeenOrder()
        {
            var holder = new ProcessorSourceHolder();

            var result = TestCompiledVsInterpreted<ProcessorSourceHolder, object>("Dupes.distinct()", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new object[] { 3, 1, 2 }, result);
        }

        [Test]
        public void OrderByWithALambdaComparerOverInts()
        {
            var holder = new ProcessorSourceHolder();

            var result = TestCompiledVsInterpreted<ProcessorSourceHolder, object>(
                    "Ints.orderBy({|a,b| $b - $a})", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new object[] { 3, 2, 1 }, result);
        }

        [Test]
        public void OrderByWithALambdaComparerOverStrings()
        {
            var holder = new ProcessorSourceHolder();

            var result = TestCompiledVsInterpreted<ProcessorSourceHolder, object>(
                    "Names.orderBy({|a,b| $a.CompareTo($b)})", holder)
                .Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new object[] { "Ala", "Ela", "Ola" }, result);
        }

        /// <summary>
        /// A string comparer argument is an expression evaluated per item as the sort key. Only the
        /// lambda shape has a compiled form, so this one is refused - with the CompileErrorException the
        /// weak path's fallback can see - and the interpreter returns the same List&lt;object&gt; as
        /// every other processor result.
        /// </summary>
        [Test]
        public void OrderByWithAStringComparerIsInterpreterOnlyButStillAList()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ProcessorSourceHolder, object>(
                    "Names.orderBy('ToString()')", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

            var holder = new ProcessorSourceHolder();
            var interpreted = InterpretGetter<ProcessorSourceHolder, object>("Names.orderBy('ToString()')")
                .GetValue(holder);

            Assert.AreEqual(typeof(List<object>), interpreted.GetType());
            Assert.AreEqual(new[] { "Ala", "Ela", "Ola" }, interpreted);
        }

        /// <summary>
        /// Asking the compiled path for the item type still gets exactly a List&lt;T&gt;; only where
        /// nothing narrower than object was requested does the root become the List&lt;object&gt; the
        /// interpreter would build.
        /// </summary>
        [Test]
        public void RequestedItemTypeSurvives()
        {
            var holder = new ProcessorSourceHolder();

            Assert.AreEqual(typeof(List<int>),
                CompileGetter<ProcessorSourceHolder, List<int>>("Ints.reverse()").GetValue(holder).GetType());
            Assert.AreEqual(typeof(List<int>),
                CompileGetter<ProcessorSourceHolder, IEnumerable<int>>("Ints.sort()").GetValue(holder).GetType());
            Assert.AreEqual(typeof(List<object>),
                CompileGetter<ProcessorSourceHolder, object>("Ints.distinct()").GetValue(holder).GetType());
        }
    }
}

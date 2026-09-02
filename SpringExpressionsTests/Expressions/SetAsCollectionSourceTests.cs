using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A collection processor asks for <c>IEnumerable</c>, so a <c>HashSet&lt;T&gt;</c> is a collection.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <c>ICollectionProcessor.Process</c> used to take the non-generic <c>ICollection</c>, which
    /// <c>HashSet&lt;T&gt;</c> does not implement - measured: <c>HashSet&lt;int&gt; is ICollection</c> is
    /// false while <c>SortedSet&lt;int&gt;</c>, <c>Queue&lt;int&gt;</c>, <c>Stack&lt;int&gt;</c>,
    /// <c>LinkedList&lt;int&gt;</c>, <c>List&lt;int&gt;</c> and <c>int[]</c> are all true. So every
    /// processor over a set answered on the compiled path, whose first tier asks
    /// <c>IsGenericEnumerable</c>, and threw <c>ArgumentException</c> on the interpreted one - one
    /// backend answering while the other throws, decided by the caller's declared context type rather
    /// than by anything they wrote.
    /// </p>
    /// <p>
    /// The same split upstream shipped between <c>ProjectionNode</c> (<c>IEnumerable</c>) and this
    /// interface, on a different source type. See <c>_Docs/open-issues.md</c> item 23.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class SetAsCollectionSourceTests : BaseCompiledTests
    {
        public class Root
        {
            public HashSet<int> Set { get; set; } = new HashSet<int> { 3, 1, 2 };
            public ISet<int> Declared { get; set; } = new HashSet<int> { 3, 1, 2 };
            public SortedSet<int> Sorted { get; set; } = new SortedSet<int> { 3, 1, 2 };
            public Queue<int> Queue { get; set; } = new Queue<int>(new[] { 3, 1, 2 });
            public Stack<int> Stack { get; set; } = new Stack<int>(new[] { 3, 1, 2 });
            public LinkedList<int> Linked { get; set; } = new LinkedList<int>(new[] { 3, 1, 2 });
            public IEnumerable<int> Sequence { get; set; } = new[] { 3, 1, 2 }.Select(x => x);
            public IList<int> AsIList { get; set; } = new List<int> { 3, 1, 2 };
            public ICollection<int> AsICollection { get; set; } = new List<int> { 3, 1, 2 };
            public HashSet<string> Names { get; set; } = new HashSet<string> { "c", "a", "b" };
            public HashSet<int> EmptySet { get; set; } = new HashSet<int>();
        }

        [Test]
        public void EveryProcessorRunsOverAHashSetOnBothBackends()
        {
            var root = new Root();

            Assert.AreEqual(3, CompileGetter<Root, object>("Set.count()").GetValue(root));
            Assert.AreEqual(3, InterpretGetter<Root, object>("Set.count()").GetValue(root));

            Assert.AreEqual(6, CompileGetter<Root, object>("Set.sum()").GetValue(root));
            Assert.AreEqual(6, InterpretGetter<Root, object>("Set.sum()").GetValue(root));

            Assert.AreEqual(1, CompileGetter<Root, object>("Set.min()").GetValue(root));
            Assert.AreEqual(1, InterpretGetter<Root, object>("Set.min()").GetValue(root));

            Assert.AreEqual(3, CompileGetter<Root, object>("Set.max()").GetValue(root));
            Assert.AreEqual(3, InterpretGetter<Root, object>("Set.max()").GetValue(root));

            Assert.AreEqual(2, CompileGetter<Root, object>("Set.average()").GetValue(root));
            Assert.AreEqual(2, InterpretGetter<Root, object>("Set.average()").GetValue(root));

            CollectionAssert.AreEqual(
                new object[] { 1, 2, 3 },
                (IEnumerable)CompileGetter<Root, object>("Set.sort()").GetValue(root));
            CollectionAssert.AreEqual(
                new object[] { 1, 2, 3 },
                (IEnumerable)InterpretGetter<Root, object>("Set.sort()").GetValue(root));

            // AreEquivalent, not AreEqual, for everything but sort(): a HashSet promises no
            // enumeration order, so pinning one here would pin an implementation detail of the BCL
            // rather than anything this engine decides. sort() is order-asserted because sorting is
            // what it does.
            CollectionAssert.AreEquivalent(
                new object[] { 1, 2, 3 },
                (IEnumerable)CompileGetter<Root, object>("Set.distinct()").GetValue(root));
            CollectionAssert.AreEquivalent(
                new object[] { 1, 2, 3 },
                (IEnumerable)InterpretGetter<Root, object>("Set.distinct()").GetValue(root));

            CollectionAssert.AreEquivalent(
                new object[] { 1, 2, 3 },
                (IEnumerable)CompileGetter<Root, object>("Set.reverse()").GetValue(root));
            CollectionAssert.AreEquivalent(
                new object[] { 1, 2, 3 },
                (IEnumerable)InterpretGetter<Root, object>("Set.reverse()").GetValue(root));

            CollectionAssert.AreEquivalent(
                new object[] { 1, 2, 3 },
                (IEnumerable)CompileGetter<Root, object>("Set.nonNull()").GetValue(root));
            CollectionAssert.AreEquivalent(
                new object[] { 1, 2, 3 },
                (IEnumerable)InterpretGetter<Root, object>("Set.nonNull()").GetValue(root));

            CollectionAssert.AreEquivalent(
                new object[] { 1m, 2m, 3m },
                (IEnumerable)CompileGetter<Root, object>("Set.convert(decimal)").GetValue(root));
            CollectionAssert.AreEquivalent(
                new object[] { 1m, 2m, 3m },
                (IEnumerable)InterpretGetter<Root, object>("Set.convert(decimal)").GetValue(root));
        }

        /// <summary>
        /// The shape the defect was reachable through: which backend runs is not the caller's choice, so
        /// the weakly typed route - which binds at <c>TContext = object</c> and therefore interprets -
        /// used to throw where a typed root answered.
        /// </summary>
        [Test]
        public void TheWeaklyTypedRouteAnswersToo()
        {
            var root = new Root();

            Assert.AreEqual(3, Expression.Parse("Set.count()").GetValue(root));
            Assert.AreEqual(3, ExpressionEvaluator.GetValue(root, "Set.count()"));
            Assert.AreEqual(6, ExpressionEvaluator.GetValue(root, "Set.sum()"));
            Assert.AreEqual(3, ExpressionEvaluator.GetValue(root, "Declared.count()"));
            Assert.AreEqual(3, ExpressionEvaluator.GetValue(root, "Sequence.count()"));
        }

        [Test]
        public void ADeclaredSetInterfaceIsACollectionSource()
        {
            var root = new Root();

            Assert.AreEqual(3, CompileGetter<Root, object>("Declared.count()").GetValue(root));
            Assert.AreEqual(3, InterpretGetter<Root, object>("Declared.count()").GetValue(root));
            Assert.AreEqual(6, CompileGetter<Root, object>("Declared.sum()").GetValue(root));
            Assert.AreEqual(6, InterpretGetter<Root, object>("Declared.sum()").GetValue(root));
        }

        /// <summary>
        /// A bare <c>IEnumerable&lt;T&gt;</c> has no count of its own, so the answer comes from walking
        /// it - which is the only thing that can be done for a lazy sequence, and is what both backends
        /// now do.
        /// </summary>
        [Test]
        public void ABareSequenceIsACollectionSource()
        {
            var root = new Root();

            Assert.AreEqual(3, CompileGetter<Root, object>("Sequence.count()").GetValue(root));
            Assert.AreEqual(3, InterpretGetter<Root, object>("Sequence.count()").GetValue(root));
            Assert.AreEqual(6, CompileGetter<Root, object>("Sequence.sum()").GetValue(root));
            Assert.AreEqual(6, InterpretGetter<Root, object>("Sequence.sum()").GetValue(root));
            CollectionAssert.AreEqual(
                new object[] { 1, 2, 3 },
                (IEnumerable)InterpretGetter<Root, object>("Sequence.sort()").GetValue(root));
        }

        /// <summary>
        /// The shapes that already worked, kept so the fix is shown not to have moved them. A
        /// <c>SortedSet&lt;T&gt;</c> does implement the non-generic <c>ICollection</c>, which is why it
        /// was never part of the defect despite also being a set.
        /// </summary>
        [Test]
        public void TheShapesThatAlreadyWorkedStillDo()
        {
            var root = new Root();

            foreach (var source in new[] { "Sorted", "Queue", "Stack", "Linked", "AsIList", "AsICollection" })
            {
                Assert.AreEqual(3, CompileGetter<Root, object>(source + ".count()").GetValue(root),
                    source);
                Assert.AreEqual(3, InterpretGetter<Root, object>(source + ".count()").GetValue(root),
                    source);
            }
        }

        /// <summary>
        /// <c>convert()</c>'s only compiled form is the weakly typed bridge, whose parameter was the
        /// non-generic <c>ICollection</c> - so a source declared as a generic interface, which does not
        /// statically satisfy it, had no compiled form and fell back. A refusal with a working fallback
        /// rather than a divergence, but it is gone.
        /// </summary>
        [Test]
        public void AGenericInterfaceSourceHasACompiledConvert()
        {
            var root = new Root();

            foreach (var source in new[] { "AsIList", "AsICollection", "Set", "Declared" })
            {
                CollectionAssert.AreEqual(
                    new object[] { 3m, 1m, 2m },
                    (IEnumerable)CompileGetter<Root, object>(source + ".convert(decimal)").GetValue(root),
                    source);
            }
        }

        [Test]
        public void AnEmptySetAnswersWhatAnEmptyListAnswers()
        {
            var root = new Root();

            Assert.AreEqual(0, CompileGetter<Root, object>("EmptySet.count()").GetValue(root));
            Assert.AreEqual(0, InterpretGetter<Root, object>("EmptySet.count()").GetValue(root));
            Assert.IsNull(CompileGetter<Root, object>("EmptySet.min()").GetValue(root));
            Assert.IsNull(InterpretGetter<Root, object>("EmptySet.min()").GetValue(root));
            Assert.IsNull(CompileGetter<Root, object>("EmptySet.average()").GetValue(root));
            Assert.IsNull(InterpretGetter<Root, object>("EmptySet.average()").GetValue(root));
        }

        [Test]
        public void ASetOfStringsIsACollectionSource()
        {
            var root = new Root();

            Assert.AreEqual(3, InterpretGetter<Root, object>("Names.count()").GetValue(root));
            Assert.AreEqual("a", InterpretGetter<Root, object>("Names.min()").GetValue(root));
            Assert.AreEqual("c", InterpretGetter<Root, object>("Names.max()").GetValue(root));
            CollectionAssert.AreEqual(
                new object[] { "a", "b", "c" },
                (IEnumerable)InterpretGetter<Root, object>("Names.sort()").GetValue(root));
        }

        /// <summary>
        /// <c>count()</c> answers without walking wherever the source can say so, and the two counting
        /// interfaces are both needed: a <c>HashSet&lt;int&gt;</c> implements only
        /// <c>ICollection&lt;int&gt;</c> while a <c>Queue&lt;int&gt;</c> implements only the non-generic
        /// <c>ICollection</c>. This asserts the interface facts the fast path rests on rather than
        /// timing anything, since a timing assertion would be flaky.
        /// </summary>
        [Test]
        public void TheTwoCountingInterfacesAreBothNeeded()
        {
            Assert.IsFalse(new HashSet<int>() is ICollection, "HashSet<int> is not a non-generic ICollection");
            Assert.IsTrue(new HashSet<int>() is ICollection<int>);

            Assert.IsTrue(new Queue<int>() is ICollection, "Queue<int> is a non-generic ICollection");
            Assert.IsFalse(new Queue<int>() is ICollection<int>, "Queue<int> is not an ICollection<int>");

            Assert.IsTrue(new SortedSet<int>() is ICollection, "SortedSet was never part of the defect");
        }
    }
}

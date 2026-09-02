using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A null collection has nothing in it, so it answers what "there is no answer" looks like here.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The empty-collection ruling already decided that spelling: null. An empty source and an absent
    /// one are the same situation, so <c>NullInts.min()</c> answers what <c>{}.min()</c> answers.
    /// </p>
    /// <p>
    /// Before this, the interpreter had no rule at all - null for the six processors that return a
    /// collection, <c>0</c> for <c>count()</c>, and a <c>NullReferenceException</c> for
    /// <c>min()</c>/<c>max()</c>/<c>average()</c>/<c>sum()</c>, which simply had no guard - while the
    /// compiled path threw <c>ArgumentNullException</c> out of <c>Enumerable</c> for all of them. Five
    /// divergent rows per source shape, over five shapes.
    /// </p>
    /// <p>
    /// <b>Which answer is inherited rather than chosen:</b> the frozen suite pins
    /// <c>Assert.IsNull(ExpressionEvaluator.GetValue(null, "sort()"))</c>, so making a null source an
    /// error was not available - even though a null receiver throws for member access, for an indexer
    /// and for a projection, which would have been more coherent. Open-issues item 22.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class NullCollectionSourceTests : BaseCompiledTests
    {
        public class Root
        {
            public List<int> NullInts { get; set; }
            public List<string> NullNames { get; set; }
            public List<decimal> NullDecimals { get; set; }
            public List<int?> NullNullables { get; set; }
            public string NullText { get; set; }
            public int[] NullArray { get; set; }
            public ArrayList NullLegacy { get; set; }
            public HashSet<int> NullSet { get; set; }
            public IEnumerable<int> NullSequence { get; set; }

            public List<int> Ints { get; set; } = new List<int> { 3, 1, 2 };

            public int Calls;
            public List<int> Counted() { Calls++; return null; }
        }

        static void BothNull(string expression)
        {
            var root = new Root();

            Assert.IsNull(CompileGetter<Root, object>(expression).GetValue(root), "compiled: " + expression);
            Assert.IsNull(InterpretGetter<Root, object>(expression).GetValue(root), "interpreted: " + expression);
        }

        static void BothZero(string expression)
        {
            var root = new Root();

            Assert.AreEqual(0, CompileGetter<Root, object>(expression).GetValue(root), "compiled: " + expression);
            Assert.AreEqual(0, InterpretGetter<Root, object>(expression).GetValue(root), "interpreted: " + expression);
        }

        /// <summary>
        /// The five processors that used to diverge, over every source shape that reaches the generic
        /// tier. <c>ArrayList</c> never diverged - it goes through the weakly typed bridge, so one
        /// implementation runs - and is here to show it still agrees.
        /// </summary>
        [Test]
        public void ACollectionReturningProcessorAnswersNull()
        {
            foreach (var source in new[]
                     {
                         "NullInts", "NullNames", "NullDecimals", "NullNullables",
                         "NullText", "NullArray", "NullLegacy", "NullSet", "NullSequence"
                     })
            {
                BothNull(source + ".sort()");
                BothNull(source + ".distinct()");
                BothNull(source + ".reverse()");
                BothNull(source + ".nonNull()");
                BothNull(source + ".convert(decimal)");
            }
        }

        /// <summary>
        /// <c>count()</c> answers <c>0</c>, not null - deliberately. It is what the interpreter has
        /// always answered, it is what an empty collection answers, and a count that is absent rather
        /// than zero is not what anyone means by counting nothing.
        /// </summary>
        [Test]
        public void CountAnswersZeroRatherThanNull()
        {
            foreach (var source in new[]
                     { "NullInts", "NullText", "NullArray", "NullLegacy", "NullSet", "NullSequence" })
            {
                BothZero(source + ".count()");
            }
        }

        /// <summary>
        /// <c>min()</c>, <c>max()</c> and <c>average()</c> had no null guard on either backend and
        /// crashed. They answer null now, which is the same answer they already gave for an empty
        /// source.
        /// </summary>
        [Test]
        public void MinMaxAndAverageAnswerNull()
        {
            foreach (var source in new[]
                     { "NullInts", "NullDecimals", "NullNullables", "NullArray", "NullSet", "NullSequence" })
            {
                BothNull(source + ".min()");
                BothNull(source + ".max()");
                BothNull(source + ".average()");
            }
        }

        /// <summary>
        /// <c>sum()</c> is the carve-out and still throws on both backends.
        /// </summary>
        /// <remarks>
        /// <b>Do not "fix" one side.</b> Its result type is the item type -
        /// <c>Enumerable.Sum(IEnumerable&lt;int&gt;)</c> answers <c>int</c> - so there is no null for the
        /// compiled path to return, and the two ways out are both worse than the gap: lifting every sum
        /// to <c>T?</c> makes a typed <c>int</c> request refuse it by the nullable-request ruling, and
        /// answering a zero needs an item type a null source cannot be asked for. Both backends throw
        /// today, so leaving it keeps agreement rather than creating a divergence.
        /// </remarks>
        [Test]
        public void SumStillThrowsOnBothBackends()
        {
            var root = new Root();

            foreach (var source in new[] { "NullInts", "NullDecimals", "NullArray", "NullSet" })
            {
                Assert.Catch<Exception>(
                    () => CompileGetter<Root, object>(source + ".sum()").GetValue(root), source);
                Assert.Catch<Exception>(
                    () => InterpretGetter<Root, object>(source + ".sum()").GetValue(root), source);
            }
        }

        /// <summary>
        /// The source is read into a local before the null test, so an expression with a side effect is
        /// evaluated once rather than twice.
        /// </summary>
        [Test]
        public void TheSourceIsEvaluatedOnce()
        {
            var root = new Root();

            CompileGetter<Root, object>("Counted().sort()").GetValue(root);
            Assert.AreEqual(1, root.Calls, "compiled");

            root.Calls = 0;
            InterpretGetter<Root, object>("Counted().sort()").GetValue(root);
            Assert.AreEqual(1, root.Calls, "interpreted");
        }

        /// <summary>
        /// A typed request over a null source is satisfied by null on both backends - the root reshaping
        /// passes a null through rather than dereferencing it, which is what turned the guarded answer
        /// back into a <c>NullReferenceException</c> on the first attempt.
        /// </summary>
        [Test]
        public void ATypedRequestOverANullSourceIsNull()
        {
            var root = new Root();

            Assert.IsNull(Expression.ParseGetter<Root, List<object>>("NullInts.sort()").GetValue(root));
            Assert.IsNull(Expression.ParseGetter<Root, List<int>>("NullInts.sort()").GetValue(root));
            Assert.IsNull(Expression.ParseGetter<Root, List<object>>(
                "NullInts.sort()", EvaluationMode.MustInterpret).GetValue(root));
        }

        /// <summary>
        /// A source that is not null is untouched - the guard is a runtime test, so nothing about the
        /// ordinary path changed.
        /// </summary>
        [Test]
        public void ANonNullSourceIsUnmoved()
        {
            var root = new Root();

            CollectionAssert.AreEqual(new object[] { 1, 2, 3 },
                (IEnumerable)CompileGetter<Root, object>("Ints.sort()").GetValue(root));
            Assert.AreEqual(3, CompileGetter<Root, object>("Ints.count()").GetValue(root));
            Assert.AreEqual(1, CompileGetter<Root, object>("Ints.min()").GetValue(root));
            Assert.AreEqual(3, CompileGetter<Root, object>("Ints.max()").GetValue(root));
            Assert.AreEqual(2d, CompileGetter<Root, object>("Ints.average()").GetValue(root));
            Assert.AreEqual(6, CompileGetter<Root, object>("Ints.sum()").GetValue(root));
        }

        /// <summary>
        /// The pure-language spellings, which need no root object: a null literal, a null root, and an
        /// unassigned local all reach the same rule. The <c>sort()</c> row is the frozen suite's pin.
        /// </summary>
        [Test]
        public void TheLanguageCanSayItWithoutAnyObject()
        {
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null.sort()"));
            Assert.AreEqual(0, ExpressionEvaluator.GetValue(null, "null.count()"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null.min()"));

            Assert.IsNull(ExpressionEvaluator.GetValue(null, "sort()"));
            Assert.AreEqual(0, ExpressionEvaluator.GetValue(null, "count()"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "min()"));

            Assert.IsNull(ExpressionEvaluator.GetValue(null, "$nothing.sort()"));
            Assert.AreEqual(0, ExpressionEvaluator.GetValue(null, "$nothing.count()"));
        }

        /// <summary>
        /// A null receiver still throws everywhere else, and that inconsistency is inherited rather than
        /// chosen: the frozen suite's pin is what stops the processors joining it.
        /// </summary>
        /// <remarks>
        /// <b>Do not reconcile these with the rows above.</b> Member access, indexers and projections
        /// over a null receiver are errors on both backends, which is more coherent than answering null
        /// - but changing the processors to match would break
        /// <c>Assert.IsNull(GetValue(null, "sort()"))</c> in the frozen suite, a semantic break rather
        /// than a compile-forced one.
        /// </remarks>
        [Test]
        public void ANullReceiverStillThrowsForIndexersAndProjections()
        {
            var root = new Root();

            foreach (var expression in new[]
                     { "NullInts[0]", "NullInts.!{#this}", "NullInts.?{#this > 0}", "NullText[0]" })
            {
                Assert.Catch<Exception>(
                    () => CompileGetter<Root, object>(expression).GetValue(root), expression);
                Assert.Catch<Exception>(
                    () => InterpretGetter<Root, object>(expression).GetValue(root), expression);
            }
        }
    }
}

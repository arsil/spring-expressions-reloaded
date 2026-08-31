using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class NaNCases
    {
        public double Nan { get { return double.NaN; } }
        public double One { get { return 1.0; } }
        public double AlsoOne { get { return 1.0; } }

        public float FloatNan { get { return float.NaN; } }
        public float FloatOne { get { return 1.0f; } }

        public double? NullableNan { get { return double.NaN; } }
        public double? NullableOne { get { return 1.0; } }

        public List<double> WithNan { get { return new List<double> { 3.0, double.NaN, 7.0 }; } }
        public List<object> WithNanBoxed { get { return new List<object> { 3.0, double.NaN, 7.0 }; } }
    }

    /// <summary>
    /// NaN compares and equals by IEEE 754 - every ordering comparison involving it is false, and it
    /// equals nothing, not even itself. The compiled backend always did; the interpreter answered from
    /// <c>CompareUtils.Compare</c> and <c>EqualityComparer</c> instead, which are .NET's *sorting*
    /// answers, so <c>Nan &lt; 1</c> was true, <c>Nan &lt;= Nan</c> was true and <c>Nan == Nan</c> was
    /// true - none of them what C# or IEEE says.
    /// </summary>
    /// <remarks>
    /// .NET keeps both rules on purpose, and which applies depends on the API called - a sort must be
    /// total, an operator need not be:
    /// <code>
    /// Comparer&lt;double&gt;.Default.Compare(NaN, 1)          -1        NaN &lt; 1      false
    /// Comparer&lt;double&gt;.Default.Compare(NaN, NaN)          0        NaN &lt;= NaN   false
    /// EqualityComparer&lt;double&gt;.Default.Equals(NaN, NaN) true        NaN == NaN   false
    /// </code>
    /// So the collection operations keep the sorting answers - asserted at the bottom of this fixture,
    /// because that half must not move - and the operators take the IEEE ones.
    /// </remarks>
    [TestFixture]
    public class NaNComparisonTests : BaseCompiledTests
    {
        // ----- equality

        [Test]
        public void NaNEqualsNothingNotEvenItself()
        {
            TestCompiledVsInterpreted<NaNCases, object>("Nan == Nan", new NaNCases()).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("Nan == One", new NaNCases()).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("One == Nan", new NaNCases()).ResultEqualsTo(false);
        }

        /// <summary>
        /// And <c>!=</c> follows, since it is the negation of <c>==</c>: a NaN differs from itself.
        /// </summary>
        [Test]
        public void NaNDiffersFromItself()
        {
            TestCompiledVsInterpreted<NaNCases, object>("Nan != Nan", new NaNCases()).ResultEqualsTo(true);
            TestCompiledVsInterpreted<NaNCases, object>("Nan != One", new NaNCases()).ResultEqualsTo(true);
        }

        [Test]
        public void FloatNaNBehavesTheSameWay()
        {
            TestCompiledVsInterpreted<NaNCases, object>("FloatNan == FloatNan", new NaNCases())
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("FloatNan != FloatNan", new NaNCases())
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<NaNCases, object>("FloatNan == FloatOne", new NaNCases())
                .ResultEqualsTo(false);
        }

        /// <summary>
        /// A boxed <c>double?</c> holding a value reports <c>typeof(double)</c>, so the nullable
        /// spelling reaches the same rule.
        /// </summary>
        [Test]
        public void ANullableHoldingNaNBehavesTheSameWay()
        {
            TestCompiledVsInterpreted<NaNCases, object>("NullableNan == NullableNan", new NaNCases())
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("NullableNan != NullableNan", new NaNCases())
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// Ordinary reals are untouched - the rule is about NaN, not about doubles.
        /// </summary>
        [Test]
        public void OrdinaryRealsStillCompareEqual()
        {
            TestCompiledVsInterpreted<NaNCases, object>("One == AlsoOne", new NaNCases()).ResultEqualsTo(true);
            TestCompiledVsInterpreted<NaNCases, object>("One != AlsoOne", new NaNCases()).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("FloatOne == FloatOne", new NaNCases()).ResultEqualsTo(true);
        }

        // ----- the four relational operators

        [Test]
        public void EveryOrderingComparisonWithNaNIsFalse()
        {
            var root = new NaNCases();

            TestCompiledVsInterpreted<NaNCases, object>("Nan < One", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("Nan > One", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("Nan <= One", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("Nan >= One", root).ResultEqualsTo(false);

            TestCompiledVsInterpreted<NaNCases, object>("One < Nan", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("One > Nan", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("One <= Nan", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("One >= Nan", root).ResultEqualsTo(false);
        }

        /// <summary>
        /// Including against itself: <c>NaN &lt;= NaN</c> and <c>NaN &gt;= NaN</c> are false, where the
        /// sorting answer would be true because <c>Compare(NaN, NaN)</c> is zero.
        /// </summary>
        [Test]
        public void NaNIsNotEvenOrderedAgainstItself()
        {
            var root = new NaNCases();

            TestCompiledVsInterpreted<NaNCases, object>("Nan <= Nan", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("Nan >= Nan", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("Nan < Nan", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("Nan > Nan", root).ResultEqualsTo(false);
        }

        [Test]
        public void FloatAndNullableSpellingsAreOrderedTheSameWay()
        {
            var root = new NaNCases();

            TestCompiledVsInterpreted<NaNCases, object>("FloatNan < FloatOne", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("FloatNan >= FloatNan", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("NullableNan < NullableOne", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("NullableNan >= NullableNan", root).ResultEqualsTo(false);
        }

        [Test]
        public void OrdinaryRealsStillOrder()
        {
            var root = new NaNCases();

            TestCompiledVsInterpreted<NaNCases, object>("One < 2", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<NaNCases, object>("One <= AlsoOne", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<NaNCases, object>("One > 2", root).ResultEqualsTo(false);
        }

        /// <summary>
        /// <c>between</c> reads as two comparisons and follows them.
        /// </summary>
        [Test]
        public void BetweenFollowsTheSameRule()
        {
            TestCompiledVsInterpreted<NaNCases, object>("Nan between {1, 2}", new NaNCases())
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<NaNCases, object>("One between {0, 2}", new NaNCases())
                .ResultEqualsTo(true);
        }

        // ----- the sorting half, which must not move

        /// <summary>
        /// The collection operations keep .NET's *sorting* answers for NaN, which is the whole reason
        /// the operator rule had to be applied at the four nodes rather than inside
        /// <c>CompareUtils.Compare</c>. <c>Comparer&lt;double&gt;</c> places NaN first and treats it as
        /// equal to itself, <c>Enumerable.OrderBy</c> agrees, and so does this engine.
        /// Do not "fix" these into the operator rule: a sort must be total.
        /// </summary>
        [Test]
        public void TheCollectionOperationsKeepTheSortingAnswers()
        {
            var root = new NaNCases();

            var sorted = (List<object>)InterpretGetter<NaNCases, object>("WithNan.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { double.NaN, 3.0, 7.0 }, sorted);

            var sortedCompiled = (List<object>)CompileGetter<NaNCases, object>("WithNan.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { double.NaN, 3.0, 7.0 }, sortedCompiled);

            // min() answers NaN and max() ignores it, which is what Enumerable.Min/Max do for double
            TestCompiledVsInterpreted<NaNCases, object>("WithNan.min()", root).ResultEqualsTo(double.NaN);
            TestCompiledVsInterpreted<NaNCases, object>("WithNan.max()", root).ResultEqualsTo(7.0);

            // and distinct() keeps one NaN, because EqualityComparer says a NaN equals itself
            var distinct = (List<object>)InterpretGetter<NaNCases, object>("WithNan.distinct()").GetValue(root);
            Assert.AreEqual(3, distinct.Count);
        }

        [Test]
        public void BoxedNaNSortsTheSameWay()
        {
            var root = new NaNCases();

            var sorted = (List<object>)InterpretGetter<NaNCases, object>("WithNanBoxed.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { double.NaN, 3.0, 7.0 }, sorted);
        }
    }
}

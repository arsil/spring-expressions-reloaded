using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// '+' with a dictionary on one side: what the compiled path refuses, and how it says so.
    /// </summary>
    /// <remarks>
    /// <c>OpADD</c> had two sibling branches three lines apart doing the same job with different
    /// exception types - dictionary-meets-dictionary threw <c>CannotCompile</c>, while
    /// dictionary-meets-anything-else threw a raw <c>ArgumentException</c>. The verdict was right in
    /// both cases; only one of them was sayable. An <c>ArgumentException</c> is not what the weakly
    /// typed path's fallback catches, so that branch was a hard failure at parse - including for a pair
    /// the interpreter merges perfectly well.
    /// </remarks>
    [TestFixture]
    public class DictionaryAdditionTests : BaseCompiledTests
    {
        public class Holder
        {
            public int Number { get; set; } = 45;
            public List<int> Ints { get; set; } = new List<int> { 1, 2 };
            public Hashtable Old { get; set; } = new Hashtable { { "a", 1 } };
            public Dictionary<string, int> Map { get; set; } = new Dictionary<string, int> { { "b", 2 } };
            public Dictionary<string, int> Other { get; set; } = new Dictionary<string, int> { { "c", 3 } };
        }

        /// <summary>
        /// The pair that was being lost. A non-generic Hashtable fails the generic-dictionary test, so
        /// the mixed pair fell into the branch that threw - yet the interpreter merges the two into a
        /// Dictionary&lt;object, object&gt; without complaint.
        /// </summary>
        [Test]
        public void ANonGenericDictionaryMeetingAGenericOneIsRefusedAndMergedByTheInterpreter()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>("Old + Map", EvaluationMode.MustCompile));

            var merged = (Dictionary<object, object>)Expression.Parse("Old + Map").GetValue<Holder>(new Holder());

            Assert.AreEqual(2, merged.Count);
            Assert.AreEqual(1, merged["a"]);
            Assert.AreEqual(2, merged["b"]);
        }

        [Test]
        public void TheSameHoldsWithTheOperandsTheOtherWayRound()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>("Map + Old", EvaluationMode.MustCompile));

            var merged = (Dictionary<object, object>)Expression.Parse("Map + Old").GetValue<Holder>(new Holder());

            Assert.AreEqual(2, merged.Count);
            Assert.AreEqual(1, merged["a"]);
            Assert.AreEqual(2, merged["b"]);
        }

        /// <summary>
        /// Two generic dictionaries were always refused properly - this is the sibling branch that got
        /// it right, kept here so the pair is visible together.
        /// </summary>
        [Test]
        public void TwoGenericDictionariesAreRefusedAndMergedByTheInterpreter()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>("Map + Other", EvaluationMode.MustCompile));

            var merged = (Dictionary<object, object>)Expression.Parse("Map + Other").GetValue<Holder>(new Holder());

            Assert.AreEqual(2, merged.Count);
            Assert.AreEqual(2, merged["b"]);
            Assert.AreEqual(3, merged["c"]);
        }

        /// <summary>
        /// A pair no backend can add. The point of this row is <i>which</i> exception the compile call
        /// throws: a refusal, so the weakly typed path can fall back, and the interpreter then reports
        /// the real problem at evaluation - exactly where MustInterpret reports it.
        /// </summary>
        [Test]
        public void ADictionaryMeetingSomethingUnaddableIsRefusedThenReportedByTheInterpreter()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>("Number + Map", EvaluationMode.MustCompile),
                "the compile phase must report a refusal, never ArgumentException");

            Assert.Throws<ArgumentException>(
                () => Expression.Parse("Number + Map").GetValue<Holder>(new Holder()),
                "and the interpreter then says what is actually wrong, at evaluation");

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<Holder, object>("Number + Map", EvaluationMode.MustInterpret)
                    .GetValue(new Holder()),
                "which is where MustInterpret has always reported it");
        }

        [Test]
        public void AListMeetingADictionaryBehavesTheSameWay()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>("Ints + Map", EvaluationMode.MustCompile));

            Assert.Throws<ArgumentException>(
                () => Expression.Parse("Ints + Map").GetValue<Holder>(new Holder()));
        }
    }
}

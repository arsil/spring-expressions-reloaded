using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// <c>sum()</c> is a fold of <c>+</c>, so the accumulator starts as the first item and the
    /// operator's own binary numeric promotion decides the running type.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <c>SumAggregator</c> used to seed at <c>0d</c> whatever the item type, so every collection came
    /// back a <c>Double</c> while the compiled path called <c>Enumerable.Sum(IEnumerable&lt;int&gt;)</c>
    /// and answered <c>Int32</c>. It was 19 of the sweep's 27 divergent rows.
    /// </p>
    /// <p>
    /// Nothing about the promotion is decided here. <c>NumberUtils.Add</c> runs the table generated
    /// from <c>PromoteNumericType</c>, which is the promotion the compiled path emits, so the answers
    /// below are <c>+</c>'s answers - including the two that are this fork's own rulings rather than
    /// C#'s: a decimal meeting a double promotes to <b>decimal</b>, and an int meeting a ulong is
    /// <b>refused</b>. See <c>_Docs/open-issues.md</c> item 24.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class SumAccumulatorTests : BaseCompiledTests
    {
        /// <summary>A caller's own real-valued type - an implicit conversion to decimal, no operators.</summary>
        public struct Coin
        {
            public Coin(decimal amount) { Amount = amount; }
            public decimal Amount { get; }
            public static implicit operator decimal(Coin c) { return c.Amount; }
            public override string ToString() { return Amount.ToString(); }
        }

        public class Root
        {
            public List<int> Ints { get; set; } = new List<int> { 3, 1, 2 };
            public List<long> Longs { get; set; } = new List<long> { 3, 1, 2 };
            public List<uint> UInts { get; set; } = new List<uint> { 3, 1, 2 };
            public List<float> Floats { get; set; } = new List<float> { 3, 1, 2 };
            public List<double> Doubles { get; set; } = new List<double> { 3, 1, 2 };
            public List<decimal> Decimals { get; set; } = new List<decimal> { 3, 1, 2 };
            public List<byte> Bytes { get; set; } = new List<byte> { 200, 200 };
            public List<short> Shorts { get; set; } = new List<short> { 30000, 30000 };
            public List<int?> NullableInts { get; set; } = new List<int?> { 3, null, 2 };
            public int[] Array { get; set; } = { 3, 1, 2 };
            public HashSet<int> Set { get; set; } = new HashSet<int> { 3, 1, 2 };
            public IEnumerable<int> Sequence { get; set; } = new[] { 3, 1, 2 }.Select(x => x);

            public List<object> IntAndLong { get; set; } = new List<object> { 1, 2L };
            public List<object> IntAndDouble { get; set; } = new List<object> { 1, 2.5 };
            public List<object> IntAndDecimal { get; set; } = new List<object> { 1, 2.5m };
            public List<object> DecimalAndDouble { get; set; } = new List<object> { 1m, 2.5 };
            public List<object> IntAndULong { get; set; } = new List<object> { 1, (ulong)2 };
            public List<object> LegacyMix { get; set; } = new List<object> { 5, 5.8, 12.2, 1 };

            public List<int> Overflowing { get; set; } = new List<int> { int.MaxValue, 1 };
            public List<long> OverflowingLongs { get; set; } = new List<long> { long.MaxValue, 1 };

            public List<byte> OneByte { get; set; } = new List<byte> { 3 };
            public List<Coin> Coins { get; set; } = new List<Coin> { new Coin(1.5m), new Coin(2m) };
            public List<Coin> OneCoin { get; set; } = new List<Coin> { new Coin(1.5m) };

            public List<int> NoInts { get; set; } = new List<int>();
            public List<decimal> NoDecimals { get; set; } = new List<decimal>();
            public List<double> NoDoubles { get; set; } = new List<double>();
            public List<long> NoLongs { get; set; } = new List<long>();
            public List<float> NoFloats { get; set; } = new List<float>();
            public List<int?> NoNullableInts { get; set; } = new List<int?>();
            public List<int?> AllNull { get; set; } = new List<int?> { null, null };
            public List<object> NoObjects { get; set; } = new List<object>();
            public List<string> NoNames { get; set; } = new List<string>();
            public ArrayList NoLegacy { get; set; } = new ArrayList();
            public HashSet<int> NoSet { get; set; } = new HashSet<int>();
        }

        static void Both<TExpected>(string expression, TExpected expected)
        {
            var root = new Root();

            var compiled = CompileGetter<Root, object>(expression).GetValue(root);
            Assert.AreEqual(typeof(TExpected), compiled.GetType(), "compiled type: " + expression);
            Assert.AreEqual(expected, compiled, "compiled value: " + expression);

            var interpreted = InterpretGetter<Root, object>(expression).GetValue(root);
            Assert.AreEqual(typeof(TExpected), interpreted.GetType(), "interpreted type: " + expression);
            Assert.AreEqual(expected, interpreted, "interpreted value: " + expression);
        }

        [Test]
        public void AUniformCollectionSumsToItsOwnItemType()
        {
            Both<int>("Ints.sum()", 6);
            Both<long>("Longs.sum()", 6L);
            Both<uint>("UInts.sum()", 6u);
            Both<float>("Floats.sum()", 6f);
            Both<double>("Doubles.sum()", 6d);
            Both<decimal>("Decimals.sum()", 6m);
            Both<int>("Array.sum()", 6);
            Both<int>("Set.sum()", 6);
            Both<int>("Sequence.sum()", 6);
            Both<int>("NullableInts.sum()", 5);
        }

        /// <summary>
        /// The small integers widen to <c>Int32</c> because <c>byte + byte</c> is <c>Int32</c> - C#'s
        /// own rule, already implemented for <c>+</c>. So the worry that seeding from the first item
        /// would answer <c>Byte</c> and overflow does not arise: 400 does not fit in a byte and the
        /// answer is 400.
        /// </summary>
        [Test]
        public void TheSmallIntegersWidenExactlyAsTheOperatorDoes()
        {
            Both<int>("Bytes.sum()", 400);
            Both<int>("Shorts.sum()", 60000);
        }

        /// <summary>
        /// A one-element fold is that element, with no operator applied - so a single-item byte
        /// collection answers <c>Byte</c> where a two-item one answers <c>Int32</c>. Recorded rather
        /// than smoothed over: it is what the rule says, no backend disagrees (neither <c>byte</c> nor
        /// <c>short</c> is in the compiled processor's dictionary, so only the fold runs), and forcing a
        /// widening would mean applying <c>+</c> to a single operand.
        /// </summary>
        [Test]
        public void AOneElementFoldIsThatElement()
        {
            Both<byte>("OneByte.sum()", (byte)3);
        }

        [Test]
        public void AMixedCollectionSumsByTheOperatorsPromotion()
        {
            Both<long>("IntAndLong.sum()", 3L);
            Both<double>("IntAndDouble.sum()", 3.5d);
            Both<decimal>("IntAndDecimal.sum()", 3.5m);

            // this fork's own ruling, not C#'s: the real operand converts to decimal
            Both<decimal>("DecimalAndDouble.sum()", 3.5m);
        }

        /// <summary>
        /// The one shape where a working expression stopped working, and it is deliberate: one rule for
        /// <c>+</c> and for <c>sum()</c>. <c>1 + someUlong</c> has always been refused by the promotion
        /// rules, so a collection holding both has no sum. It answered <c>Double:3</c> before, by
        /// accumulating in a type neither operand has.
        /// </summary>
        [Test]
        public void AnIntMeetingAULongIsRefusedInACollectionAsItIsInAnOperator()
        {
            var root = new Root();

            Assert.Catch<Exception>(
                () => CompileGetter<Root, object>("IntAndULong.sum()").GetValue(root));
            Assert.Catch<Exception>(
                () => InterpretGetter<Root, object>("IntAndULong.sum()").GetValue(root));

            // the same pair written as an operator, for the comparison the ruling rests on
            Assert.Catch<Exception>(
                () => InterpretGetter<Root, object>("IntAndULong[0] + IntAndULong[1]").GetValue(root));
        }

        /// <summary>
        /// Unchecked on both backends, because <c>+</c> is unchecked on both backends.
        /// </summary>
        /// <remarks>
        /// This was the sharpest row of the lot: <c>Enumerable.Sum(IEnumerable&lt;int&gt;)</c> is
        /// checked, so the compiled path threw <see cref="OverflowException"/> while the interpreter's
        /// <c>0d</c> accumulator could not overflow and answered <c>2147483648</c>. One backend throwing
        /// while the other answers, and only when the data reached the edge - which is why no sweep saw
        /// it until a <c>List&lt;int&gt;</c> holding <c>{int.MaxValue, 1}</c> was put in the corpus.
        /// The cast ruling took C#'s unchecked context for the same reason.
        /// </remarks>
        [Test]
        public void AnOverflowingSumWrapsOnBothBackends()
        {
            unchecked
            {
                Both<int>("Overflowing.sum()", int.MaxValue + 1);
                Both<long>("OverflowingLongs.sum()", long.MaxValue + 1);
            }

            // the operator this now matches
            Both<int>("Overflowing[0] + Overflowing[1]", unchecked(int.MaxValue + 1));
        }

        /// <summary>
        /// A decimal still throws, and that is agreement rather than an exception to the rule: the
        /// interpreter's decimal <c>+</c> throws too, so both sides raise
        /// <see cref="OverflowException"/> and <c>Enumerable.Sum(IEnumerable&lt;decimal&gt;)</c> was
        /// left in place.
        /// </summary>
        [Test]
        public void ADecimalOverflowStillThrowsOnBothBackends()
        {
            var root = new Root { Decimals = new List<decimal> { decimal.MaxValue, 1m } };

            Assert.Catch<Exception>(
                () => CompileGetter<Root, object>("Decimals.sum()").GetValue(root));
            Assert.Catch<Exception>(
                () => InterpretGetter<Root, object>("Decimals.sum()").GetValue(root));
        }

        /// <summary>
        /// With nothing added there is no first item, so the type comes from the source. The answers
        /// match <c>Enumerable.Sum</c> overload for overload - <c>0</c>, not null and not a throw,
        /// which is where <c>sum()</c> parts company with <c>min()</c> and <c>average()</c>.
        /// </summary>
        [Test]
        public void TheSumOfNothingIsAZeroOfTheItemType()
        {
            Both<int>("NoInts.sum()", 0);
            Both<long>("NoLongs.sum()", 0L);
            Both<float>("NoFloats.sum()", 0f);
            Both<double>("NoDoubles.sum()", 0d);
            Both<decimal>("NoDecimals.sum()", 0m);
            Both<int>("NoSet.sum()", 0);

            // a nullable item type is unwrapped: Enumerable.Sum(IEnumerable<int?>) answers 0 as well
            Both<int>("NoNullableInts.sum()", 0);
            Both<int>("AllNull.sum()", 0);
        }

        /// <summary>
        /// A source with no item type keeps the historical <c>Double:0</c>, which means the language
        /// answers two types for the sum of nothing depending on how the collection was declared.
        /// Neither diverges - each agrees with what the compiled path does for the same source - and at
        /// evaluation an empty untyped collection cannot say what it would have held.
        /// </summary>
        [Test]
        public void TheSumOfNothingUntypedIsStillADouble()
        {
            Both<double>("NoLegacy.sum()", 0d);
            Both<double>("NoObjects.sum()", 0d);
            Both<double>("NoNames.sum()", 0d);
        }

        /// <summary>
        /// A caller's own real-valued type is normalized through its implicit conversion before it
        /// becomes the seed, which is what the old <c>0d</c>-or-<c>0m</c> family choice arranged - so
        /// custom reals answer a decimal exactly as they did, single-item collections included.
        /// </summary>
        [Test]
        public void ACustomRealValuedTypeStillAccumulatesInDecimal()
        {
            Both<decimal>("Coins.sum()", 3.5m);
            Both<decimal>("OneCoin.sum()", 1.5m);
        }

        /// <summary>
        /// The frozen suite's own row, which is what made this change safe to make: its only type
        /// assertion is <c>IsInstanceOf(typeof(double))</c> over a mixed int-and-double array, and an
        /// int-then-double fold lands on double anyway.
        /// </summary>
        [Test]
        public void TheFrozenSuitesMixedArrayStillSumsToADouble()
        {
            Both<double>("LegacyMix.sum()", 24d);
        }

        /// <summary>
        /// <c>average()</c> was deliberately not touched. It divides, so it must accumulate in a real
        /// type - <c>Enumerable.Average(IEnumerable&lt;int&gt;)</c> answers a <c>double</c> too, and
        /// giving it <c>sum()</c>'s seed would have made an int collection average to <c>Int32</c> and
        /// created a divergence rather than closing one.
        /// </summary>
        [Test]
        public void AverageStillAccumulatesInADouble()
        {
            Both<double>("Ints.average()", 2d);
            Both<double>("Bytes.average()", 200d);
            Both<double>("IntAndDouble.average()", 1.75d);
        }

        /// <summary>
        /// A non-numeric item is still an error, unchanged, and it is reported before any accumulation
        /// decides a type.
        /// </summary>
        [Test]
        public void ANonNumericItemIsStillAnError()
        {
            var root = new Root { IntAndLong = new List<object> { 1, "ana" } };

            Assert.Catch<ArgumentException>(
                () => InterpretGetter<Root, object>("IntAndLong.sum()").GetValue(root));
        }
    }
}

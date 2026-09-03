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
            public List<float?> NullableFloats { get; set; } = new List<float?> { 3, null, 2 };
            public List<float> Thirds { get; set; } = new List<float> { 1, 2, 4 };

            /// <summary>
            /// The two sets that catch a float accumulation. <c>Tenths</c> is ordinary data;
            /// <c>PastTheLimit</c> starts above float's exactly-representable integer range, 2^24, so
            /// each following <c>1f</c> is lost if the running total is a float.
            /// </summary>
            public List<float> Tenths { get; set; } =
                new List<float> { .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f };

            public List<float> PastTheLimit { get; set; } =
                new List<float> { 1e8f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

            /// <summary>Below 2^24, so nothing is lost either way - it catches nothing.</summary>
            public List<float> BelowTheLimit { get; set; } =
                new List<float> { 1e7f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
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
            Both(expression, expected, new Root());
        }

        static void Both<TExpected>(string expression, TExpected expected, Root root)
        {
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
        /// <c>average()</c> did not take <c>sum()</c>'s seed. It divides, so it must accumulate in a
        /// real type - <c>Enumerable.Average(IEnumerable&lt;int&gt;)</c> answers a <c>double</c> too, and
        /// giving an int collection <c>sum()</c>'s seed would have averaged it to <c>Int32</c> and
        /// created a divergence rather than closing one.
        /// </summary>
        [Test]
        public void AverageStillAccumulatesInADouble()
        {
            Both<double>("Ints.average()", 2d);
            Both<double>("Bytes.average()", 200d);
            Both<double>("IntAndDouble.average()", 1.75d);
            Both<double>("Longs.average()", 2d);
            Both<decimal>("Decimals.average()", 2m);
        }

        /// <summary>
        /// <c>float</c> is the one family <c>average()</c> did have to learn: it answers a
        /// <c>Single</c>, matching <c>Enumerable.Average(IEnumerable&lt;float&gt;)</c> and matching what
        /// <c>Floats.sum()</c> has answered on both backends since <c>sum()</c> became a fold of
        /// <c>+</c>.
        /// </summary>
        /// <remarks>
        /// The interpreter seeded <c>0d</c> for everything but decimals, so a float collection averaged
        /// to a <c>Double</c> while the compiled path answered a <c>Single</c> - and the engine
        /// disagreed with itself, since summing the same collection gave a <c>Single</c> either way.
        /// <p>
        /// <b>The accumulation is still in <c>double</c>; only the quotient narrows.</b> That is not a
        /// detail: <c>Enumerable.Average(IEnumerable&lt;float&gt;)</c> sums in double, so accumulating
        /// in float instead diverges from it - see
        /// <see cref="AFloatAccumulationWouldDivergeAndTheseAreTheSetsThatShowIt"/>, which is the pair
        /// that caught exactly that being written here.
        /// </p>
        /// </remarks>
        [Test]
        public void AverageOverFloatsAnswersAFloat()
        {
            Both<float>("Floats.average()", 2f);
            Both<float>("NullableFloats.average()", 2.5f);
            Both<float>("Thirds.average()", 7f / 3f);

            // the engine's own precedent, asserted beside it
            Both<float>("Floats.sum()", 6f);
            Both<float>("NullableFloats.sum()", 5f);

            // and C#'s answer for the same collection
            Assert.AreEqual(new[] { 1f, 2f, 4f }.Average(), 7f / 3f);
            Assert.AreEqual(typeof(float), new[] { 1f, 2f, 4f }.Average().GetType());
        }

        /// <summary>
        /// The two sets that tell a double accumulation from a float one, and the one that does not.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>Do not "simplify" the aggregator by seeding <c>0f</c> for the float family.</b> It reads
        /// as the obvious way to make a float collection answer a float, it was written that way first,
        /// and it is wrong: <c>Enumerable.Average(IEnumerable&lt;float&gt;)</c> sums in <c>double</c>
        /// and narrows only the quotient, so a float accumulation loses addends the compiled path keeps.
        /// Measured with the seed in place:
        /// </p>
        /// <code>
        /// {0.1f x 10}      compiled Single:0.1        interpreted Single:0.10000001
        /// {1e8f, 1f x 9}   compiled Single:10000001   interpreted Single:10000000
        /// </code>
        /// <p>
        /// <c>BelowTheLimit</c> is here as the counter-example: <c>1e7</c> is under float's
        /// exactly-representable integer range (2^24 = 16,777,216), so no addend is lost either way and
        /// both routes agree. It was the only set the first measurement used, which is why the seed
        /// looked safe.
        /// </p>
        /// </remarks>
        [Test]
        public void AFloatAccumulationWouldDivergeAndTheseAreTheSetsThatShowIt()
        {
            Both<float>("Tenths.average()", 0.1f);
            Both<float>("PastTheLimit.average()", 10000001f);
            Both<float>("BelowTheLimit.average()", 1000000.9f);

            // each matches Enumerable.Average, which is the reference
            var root = new Root();
            Assert.AreEqual(root.Tenths.Average(), 0.1f);
            Assert.AreEqual(root.PastTheLimit.Average(), 10000001f);
            Assert.AreEqual(root.BelowTheLimit.Average(), 1000000.9f);

            // the fact that makes the first two sets discriminating and the third not
            Assert.IsTrue(1e8f + 1f == 1e8f, "1e8 is above float's exact-integer range");
            Assert.IsFalse(1e7f + 1f == 1e7f, "1e7 is below it, so it catches nothing");
        }

        /// <summary>
        /// The family is read from the item's <b>value</b>, not from the collection's declared item
        /// type, so an untyped collection of floats averages to a <c>Single</c> as well.
        /// </summary>
        /// <remarks>
        /// That is how the decimal family has always worked here, and it is asserted beside the float
        /// rows so the two read as one rule rather than two.
        /// <p>
        /// <b>Every item has to be a float, not just the first.</b> A float meeting anything wider is
        /// that wider type - <c>1f + 2.0</c> is a <c>double</c> - so a collection holding both averages
        /// to a double, which is what the promotion rules say and what <c>sum()</c> answers for the same
        /// items. Deciding from the first item alone narrowed <c>{1f, 2.0}</c> to a float, and a failing
        /// assertion here is what caught it.
        /// </p>
        /// </remarks>
        [Test]
        public void TheFamilyComesFromTheItemsValueNotTheDeclaredItemType()
        {
            var floats = new Root { IntAndLong = new List<object> { 1f, 2f, 4f } };
            Both<float>("IntAndLong.average()", 7f / 3f, floats);

            var decimals = new Root { IntAndLong = new List<object> { 1m, 2m, 4m } };
            Both<decimal>("IntAndLong.average()", 7m / 3m, decimals);

            // a float meeting a double is a double, so the collection averages to one
            var floatThenDouble = new Root { IntAndLong = new List<object> { 1f, 2.0 } };
            Both<double>("IntAndLong.average()", 1.5d, floatThenDouble);

            // and in the other order, so it is not "the first item decides"
            var doubleThenFloat = new Root { IntAndLong = new List<object> { 2.0, 1f } };
            Both<double>("IntAndLong.average()", 1.5d, doubleThenFloat);

            // a float meeting an int is still a float, as '1f + 2' is
            var floatAndInt = new Root { IntAndLong = new List<object> { 1f, 2 } };
            Both<double>("IntAndLong.average()", 1.5d, floatAndInt);
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

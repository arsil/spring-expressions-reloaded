using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A caller's own money-like struct: real-valued by its implicit conversion to decimal, nothing
    /// else numeric about it.
    /// </summary>
    public struct MoneyLike
    {
        private readonly decimal _value;

        public MoneyLike(decimal value) { _value = value; }

        public static implicit operator decimal(MoneyLike value) { return value._value; }
    }

    public struct SpeedLike
    {
        private readonly double _value;

        public SpeedLike(double value) { _value = value; }

        public static implicit operator double(SpeedLike value) { return value._value; }
    }

    public class CustomRealHolder
    {
        public MoneyLike Amount { get { return new MoneyLike(45.5m); } }
        public MoneyLike Fee { get { return new MoneyLike(0.5m); } }
        public SpeedLike Speed { get { return new SpeedLike(2.5); } }

        public System.Collections.Generic.List<MoneyLike> Monies { get; }
            = new System.Collections.Generic.List<MoneyLike>
                { new MoneyLike(1.5m), new MoneyLike(2.5m), new MoneyLike(2m) };
    }

    /// <summary>
    /// Custom real-valued types participate in arithmetic and comparison on both backends: the operand
    /// converts through its own implicit operator - decimal preferred over double over float - and from
    /// there the ordinary promotion rules apply. The interpreter normalizes the boxed value before its
    /// operation table; the compiled path wraps the operand expression in the conversion before
    /// promotion.
    /// </summary>
    [TestFixture]
    public class CustomRealTypesTests : BaseCompiledTests
    {
        [Test]
        public void CustomDecimalAddsToDecimal()
        {
            var holder = new CustomRealHolder();

            var result = TestCompiledVsInterpreted<CustomRealHolder, object>("Amount + 1.5m", holder)
                .Result;

            Assert.AreEqual(typeof(decimal), result.GetType());
            Assert.AreEqual(47.0m, result);
        }

        [Test]
        public void CustomDecimalComputesWithIntegers()
        {
            var holder = new CustomRealHolder();

            TestCompiledVsInterpreted<CustomRealHolder, object>("Amount * 2", holder)
                .ResultEqualsTo(91.0m);
            TestCompiledVsInterpreted<CustomRealHolder, object>("Amount - 1", holder)
                .ResultEqualsTo(44.5m);
            TestCompiledVsInterpreted<CustomRealHolder, object>("Amount / 2", holder)
                .ResultEqualsTo(22.75m);
        }

        [Test]
        public void TwoCustomDecimalsComputeTogether()
        {
            var holder = new CustomRealHolder();

            TestCompiledVsInterpreted<CustomRealHolder, object>("Amount - Fee", holder)
                .ResultEqualsTo(45.0m);
        }

        [Test]
        public void CustomDoubleComputesAsDouble()
        {
            var holder = new CustomRealHolder();

            var result = TestCompiledVsInterpreted<CustomRealHolder, object>("Speed + 0.5", holder)
                .Result;

            Assert.AreEqual(typeof(double), result.GetType());
            Assert.AreEqual(3.0d, result);
        }

        /// <summary>
        /// Power is double-only by design - Math.Pow is all the BCL offers - so a custom real converts
        /// through its implicit operator and then to double like any other numeric operand.
        /// </summary>
        [Test]
        public void CustomDecimalRaisesToAPower()
        {
            var holder = new CustomRealHolder();

            var result = TestCompiledVsInterpreted<CustomRealHolder, object>("Amount ^ 2", holder)
                .Result;

            Assert.AreEqual(typeof(double), result.GetType());
            Assert.AreEqual(2070.25d, result);
        }

        [Test]
        public void CustomDecimalCompares()
        {
            var holder = new CustomRealHolder();

            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount > 1.5m", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount < Fee", holder)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount >= 45.5m", holder)
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// Unary minus normalizes on the interpreter; the compiled path has no form for it yet and the
        /// weakly typed path falls back, so the weak result is pinned here.
        /// </summary>
        [Test]
        public void CustomDecimalNegatesOnTheWeakPath()
        {
            Assert.AreEqual(-45.5m, ExpressionEvaluator.GetValue(new CustomRealHolder(), "-Amount"));
        }

        /// <summary>
        /// The aggregators follow: sum() and average() gate items with IsNumber - which sees implicit
        /// operators now - and add through the normalizing arithmetic; min() and max() compare through
        /// the normalizing comparison and hand back the winning item itself. The compiled facade has no
        /// processors for a custom item type, so both backends run the same aggregator.
        /// </summary>
        [Test]
        public void AggregatorsWorkOverCustomDecimals()
        {
            var holder = new CustomRealHolder();

            var sum = TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.sum()", holder)
                .Result;
            Assert.AreEqual(typeof(decimal), sum.GetType());
            Assert.AreEqual(6.0m, sum);

            TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.average()", holder)
                .ResultEqualsTo(2.0m);

            var min = TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.min()", holder)
                .Result;
            Assert.AreEqual(new MoneyLike(1.5m), min);

            var max = TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.max()", holder)
                .Result;
            Assert.AreEqual(new MoneyLike(2.5m), max);
        }
    }
}

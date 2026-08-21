using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Pins the fork's decimal-meets-real promotion ruling: where C# refuses decimal against float or
    /// double at binding time, this engine converts the real operand to decimal - restoring the
    /// pre-fork behaviour for the common case of a double straying into decimal arithmetic. The edge
    /// behaviour is not custom code: it is exactly the Decimal(double) constructor, the single
    /// implementation under both backends' conversions, and these tests record it so a future change
    /// of conversion route fails visibly instead of silently changing answers.
    /// </summary>
    [TestFixture]
    public class DecimalDoublePromotionTests : BaseCompiledTests
    {
        [Test]
        public void DecimalMeetsDoubleAcrossAllFiveOperators()
        {
            TestCompiledVsInterpreted<decimal>("1m + 1.5").ResultEqualsTo(2.5m);
            TestCompiledVsInterpreted<decimal>("1000.00m - 1e4").ResultEqualsTo(-9000m);
            TestCompiledVsInterpreted<decimal>("1000.00m * 1e3").ResultEqualsTo(1000000m);
            TestCompiledVsInterpreted<decimal>("1000.00m / 1e3").ResultEqualsTo(1m);
            TestCompiledVsInterpreted<decimal>("1005.00m % 1e3").ResultEqualsTo(5m);
        }

        [Test]
        public void ResultIsDecimalWhicheverSideTheDoubleIsOn()
        {
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("1m + 1.5").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), InterpretGetter<object>("1m + 1.5").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("1.5 + 1m").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), InterpretGetter<object>("1.5 + 1m").GetValue().GetType());

            TestCompiledVsInterpreted<decimal>("1.5 + 1m").ResultEqualsTo(2.5m);
        }

        [Test]
        public void FloatMeetsDecimal()
        {
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("1.5f + 1m").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), InterpretGetter<object>("1.5f + 1m").GetValue().GetType());

            TestCompiledVsInterpreted<decimal>("1.5f + 1m").ResultEqualsTo(2.5m);
        }

        [Test]
        public void DoubleMeetsDecimalThroughDeclaredPropertyTypes()
        {
            // Property reads, not literals: the compiled path promotes from the declared types here,
            // which is the shape IllegalPromotions has always demanded must compile.
            var ctx = new Tuple<double, decimal>(3d, 3m);

            Assert.AreEqual(typeof(decimal),
                CompileGetter<Tuple<double, decimal>, object>("Item1 + Item2").GetValue(ctx).GetType());

            TestCompiledVsInterpreted<Tuple<double, decimal>, decimal>("Item1 + Item2", ctx)
                .ResultEqualsTo(6m);
        }

        [Test]
        public void FloatMeetsDecimalThroughDeclaredPropertyTypes()
        {
            var ctx = new Tuple<float, decimal>(1.5f, 1m);

            Assert.AreEqual(typeof(decimal),
                CompileGetter<Tuple<float, decimal>, object>("Item1 + Item2").GetValue(ctx).GetType());

            TestCompiledVsInterpreted<Tuple<float, decimal>, decimal>("Item1 + Item2", ctx)
                .ResultEqualsTo(2.5m);
        }

        [Test]
        public void ComparisonAndEqualitySeeTheSameRule()
        {
            TestCompiledVsInterpreted<bool>("1.5 > 1m").ResultEqualsTo(true);
            TestCompiledVsInterpreted<bool>("1.5 < 2m").ResultEqualsTo(true);
            TestCompiledVsInterpreted<bool>("1m <= 1.0").ResultEqualsTo(true);
            TestCompiledVsInterpreted<bool>("0.1 == 0.1m").ResultEqualsTo(true);
            TestCompiledVsInterpreted<bool>("2m != 1.5").ResultEqualsTo(true);
        }

        [Test]
        public void ConversionStripsTheDoublesBinaryNoise()
        {
            // 0.1 as a double is really 0.1000000000000000055511...; Decimal(double) rounds to
            // 15 significant digits, which recovers the intended 0.1m - so decimal exactness
            // survives the stray double instead of being poisoned by it.
            TestCompiledVsInterpreted<decimal>("0.2m + 0.1").ResultEqualsTo(0.3m);

            // 0.1 + 0.2 is pure double arithmetic and lands on 0.30000000000000004; converted to
            // decimal for the comparison, the noise rounds away and equality holds.
            TestCompiledVsInterpreted<bool>("0.1 + 0.2 == 0.3m").ResultEqualsTo(true);
        }

        [Test]
        public void DoubleBeyondDecimalRangeThrowsOnBothBackends()
        {
            Assert.Throws<OverflowException>(() => CompileGetter<object>("1m + 1e300").GetValue());
            Assert.Throws<OverflowException>(() => InterpretGetter<object>("1m + 1e300").GetValue());
        }

        [Test]
        public void NaNThrowsOnBothBackends()
        {
            // 0.0 / 0.0 is double NaN; decimal has no NaN, so the conversion throws at evaluation.
            Assert.Throws<OverflowException>(() => CompileGetter<object>("1m + 0.0 / 0.0").GetValue());
            Assert.Throws<OverflowException>(() => InterpretGetter<object>("1m + 0.0 / 0.0").GetValue());
        }

        [Test]
        public void DoubleBelowDecimalResolutionContributesZero()
        {
            // Decimal's smallest positive value is 1e-28; a smaller double converts to 0m, silently.
            TestCompiledVsInterpreted<decimal>("1m + 1e-30").ResultEqualsTo(1m);
        }

        [Test]
        public void ConversionKeepsFifteenSignificantDigits()
        {
            TestCompiledVsInterpreted<decimal>("0m + 0.12345678901234567")
                .ResultEqualsTo(0.123456789012346m);
        }

        [Test]
        public void NullableDecimalMeetsDoubleLiftsOnBothBackends()
        {
            var valued = new Tuple<decimal?, double>(1m, 0.5);

            Assert.AreEqual(typeof(decimal),
                CompileGetter<Tuple<decimal?, double>, object>("Item1 + Item2").GetValue(valued).GetType());

            TestCompiledVsInterpreted<Tuple<decimal?, double>, decimal?>("Item1 + Item2", valued)
                .ResultEqualsTo(1.5m);

            var nulled = new Tuple<decimal?, double>(null, 0.5);
            TestCompiledVsInterpreted<Tuple<decimal?, double>, decimal?>("Item1 + Item2", nulled)
                .ResultEqualsTo(null);
        }

        [Test]
        public void MixedDecimalAndDoubleCollectionSums()
        {
            // The accumulator seeds from the first item's family (decimal here), and the double item
            // joins it under the same promotion rule.
            var ctx = new List<object> { 1m, 2.5 };
            TestCompiledVsInterpreted<List<object>, decimal>("sum()", ctx).ResultEqualsTo(3.5m);
        }
    }
}

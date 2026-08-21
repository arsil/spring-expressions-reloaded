using System;

using NUnit.Framework;

using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Pins that interpreted comparison follows the same binary numeric promotion as arithmetic and
    /// the compiled comparison, replacing the legacy highest-TypeCode coercion it inherited. The
    /// headline shape is a negative int against a uint: both operands promote to long and compare
    /// correctly on both backends, where the legacy coercion converted the int to uint and threw
    /// OverflowException - a silent compiled-vs-interpreted divergence, because the compiled path
    /// always compared through long.
    /// </summary>
    [TestFixture]
    public class ComparisonPromotionTests : BaseCompiledTests
    {
        [Test]
        public void NegativeIntAgainstUIntComparesOnBothBackends()
        {
            var ctx = new Tuple<int, uint>(-1, 2u);

            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 < Item2", ctx).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 <= Item2", ctx).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 > Item2", ctx).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 >= Item2", ctx).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 == Item2", ctx).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 != Item2", ctx).ResultEqualsTo(true);
        }

        [Test]
        public void ValuedIntAgainstUIntComparesOnBothBackends()
        {
            var ctx = new Tuple<int, uint>(3, 5u);

            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 < Item2", ctx).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Tuple<int, uint>, bool>("Item1 == Item2", ctx).ResultEqualsTo(false);
        }

        [Test]
        public void NegativeLongAgainstUIntComparesOnBothBackends()
        {
            var ctx = new Tuple<long, uint>(-1L, 2u);

            TestCompiledVsInterpreted<Tuple<long, uint>, bool>("Item1 < Item2", ctx).ResultEqualsTo(true);
        }

        [Test]
        public void NegativeSByteAgainstByteComparesOnBothBackends()
        {
            // Both promote to int. The legacy coercion converted the sbyte to byte - the higher
            // TypeCode - and threw OverflowException for the negative value.
            var ctx = new Tuple<sbyte, byte>(-1, 200);

            TestCompiledVsInterpreted<Tuple<sbyte, byte>, bool>("Item1 < Item2", ctx).ResultEqualsTo(true);
        }

        [Test]
        public void DoubleAgainstDecimalComparesThroughDeclaredPropertyTypes()
        {
            // The decimal-meets-real ruling, seen by the comparison path through declared types
            // (the literal shapes are pinned in DecimalDoublePromotionTests).
            var ctx = new Tuple<double, decimal>(1.5, 2m);

            TestCompiledVsInterpreted<Tuple<double, decimal>, bool>("Item1 < Item2", ctx).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Tuple<double, decimal>, bool>("Item1 == Item2", ctx).ResultEqualsTo(false);
        }

        [Test]
        public void IntAgainstULongIsRefusedLikeArithmetic()
        {
            var ctx = new Tuple<int, ulong>(3, 5ul);

            // Compiled: the promotion refuses the pair while the tree is built; the strongly typed
            // path has no fallback, so the refusal surfaces at parse.
            Assert.Catch<CompileErrorException>(
                () => CompileGetter<Tuple<int, ulong>, bool>("Item1 < Item2"));

            // Interpreted: the same promotion serves evaluation, so the same refusal surfaces at
            // GetValue - exactly what arithmetic on the same operands already does. The legacy
            // coercion used to compare these through ulong, throwing only for negative values.
            Assert.Catch<CompileErrorException>(
                () => InterpretGetter<Tuple<int, ulong>, bool>("Item1 < Item2").GetValue(ctx));
        }

        [Test]
        public void NonNumericMixedPairsKeepTheirArgumentException()
        {
            // Only numeric pairs go through the promotion table; anything else still refuses with
            // the coercion ArgumentException it always threw.
            Assert.Throws<ArgumentException>(
                () => InterpretGetter<bool>("'abc' < 3").GetValue());
        }
    }
}

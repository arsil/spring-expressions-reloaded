using NUnit.Framework;

using System;
using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

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
        /// The tests above all normalize to decimal against decimal - the same-type comparison path.
        /// From here down the operand types differ after normalization, so the comparison runs the
        /// shared binary numeric promotion (the mixed-type branch of CompareUtils).
        /// </summary>
        [Test]
        public void CustomDecimalComparesWithIntegers()
        {
            var holder = new CustomRealHolder();

            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount > 45", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount <= 45", holder)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount != 45", holder)
                .ResultEqualsTo(true);
        }

        [Test]
        public void CustomDecimalComparesWithDoubles()
        {
            var holder = new CustomRealHolder();

            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount > 45.4", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount < 45.6", holder)
                .ResultEqualsTo(true);

            // The double converts through Decimal(double), which recovers the intended 45.5m
            // exactly - so equality across the custom decimal and a plain double literal holds.
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount == 45.5", holder)
                .ResultEqualsTo(true);
        }

        [Test]
        public void CustomDoubleComparesWithIntegers()
        {
            var holder = new CustomRealHolder();

            TestCompiledVsInterpreted<CustomRealHolder, bool>("Speed > 2", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Speed < 3", holder)
                .ResultEqualsTo(true);
        }

        [Test]
        public void CustomDecimalComputesWithDoubles()
        {
            var holder = new CustomRealHolder();

            var result = TestCompiledVsInterpreted<CustomRealHolder, object>("Amount + 0.5", holder)
                .Result;

            Assert.AreEqual(typeof(decimal), result.GetType());
            Assert.AreEqual(46.0m, result);
        }

        /// <summary>
        /// Two custom reals with different built-in targets: MoneyLike converts to decimal, SpeedLike
        /// to double, so the decimal-meets-real promotion cell is reached entirely through implicit
        /// conversions on both operands, in arithmetic and comparison alike.
        /// </summary>
        [Test]
        public void TwoCustomRealsWithDifferentTargetsMeet()
        {
            var holder = new CustomRealHolder();

            var sum = TestCompiledVsInterpreted<CustomRealHolder, object>("Amount + Speed", holder)
                .Result;
            Assert.AreEqual(typeof(decimal), sum.GetType());
            Assert.AreEqual(48.0m, sum);

            TestCompiledVsInterpreted<CustomRealHolder, bool>("Amount > Speed", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomRealHolder, bool>("Speed < Amount", holder)
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

        /// <summary>
        /// <c>sort()</c> orders these too, which brings it into line with the three above.
        /// </summary>
        /// <remarks>
        /// <p>
        /// It used to throw <c>InvalidOperationException</c> out of <c>Comparer&lt;T&gt;.Default</c>,
        /// which has never heard of an implicit conversion - while <c>min()</c>, <c>max()</c> and
        /// <c>between</c> answered, because they go through <c>CompareUtils.Compare</c>, which
        /// normalizes through it. The same type, the same notion of order, and two different answers
        /// depending on which function was called.
        /// </p>
        /// <p>
        /// <c>CompareUtils.RequiresConversionToOrder</c> is the one place that decides, asked by the
        /// interpreter's comparer and by the compiled <c>SortWithParam&lt;T&gt;</c>. Answering it in
        /// only one of them is what made <c>Monies.sort()</c> throw compiled while the interpreter
        /// answered - a divergence introduced and then caught while building this.
        /// </p>
        /// <p>
        /// <b><see cref="IComparable"/> is asked first, so no type that sorted before is affected.</b>
        /// Relational operators are still not consulted by any of the four: deriving an order from
        /// <c>op_LessThan</c> plus <c>op_GreaterThan</c> would call an operator the expression never
        /// wrote.
        /// </p>
        /// </remarks>
        [Test]
        public void SortOrdersCustomDecimalsToo()
        {
            var holder = new CustomRealHolder();

            var sorted = (System.Collections.IEnumerable)
                TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.sort()", holder).Result;

            CollectionAssert.AreEqual(
                new object[] { new MoneyLike(1.5m), new MoneyLike(2m), new MoneyLike(2.5m) },
                sorted);

            // reverse() and distinct() never needed an order and are unmoved
            TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.reverse()", holder);
            TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.distinct()", holder);
            TestCompiledVsInterpreted<CustomRealHolder, object>("Monies.count()", holder)
                .ResultEqualsTo(3);
        }

        /// <summary>
        /// A comparer lambda that does not answer an <c>int</c> is the caller's mistake, and is reported
        /// as one.
        /// </summary>
        /// <remarks>
        /// <c>$a - $b</c> over these yields a <c>decimal</c>, so the emitted call hands a
        /// <c>Func&lt;T,T,decimal&gt;</c> to a <c>Func&lt;T,T,int&gt;</c> parameter. The
        /// <c>ArgumentException</c> out of <c>LExpression.Call</c> used to reach the absorber, which
        /// reported the caller's own error as an internal compiler defect with "please report it"
        /// attached - the one thing the compile-failure convention says an emitter must never do. It is
        /// a plain refusal now, and the interpreter raises the real error at evaluation.
        /// </remarks>
        [Test]
        public void AComparerLambdaThatDoesNotAnswerAnIntIsTheCallersMistake()
        {
            var holder = new CustomRealHolder();

            var refusal = Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<CustomRealHolder, object>(
                    "Monies.orderBy({|a,b| $a - $b})", EvaluationMode.MustCompile));

            Assert.IsFalse(refusal.Message.Contains("internal compiler error"),
                "the caller's mistake must not be reported as ours");

            Assert.Catch<Exception>(
                () => Expression.ParseGetter<CustomRealHolder, object>(
                    "Monies.orderBy({|a,b| $a - $b})", EvaluationMode.MustInterpret).GetValue(holder));
        }
    }
}

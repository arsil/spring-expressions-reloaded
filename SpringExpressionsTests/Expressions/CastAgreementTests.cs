using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class CastCases
    {
        public object Payload { get { return "payload"; } }
        public object BoxedDate { get { return new DateTime(2024, 6, 5); } }
        public MoneyLike Amount { get { return new MoneyLike(45.5m); } }
    }

    /// <summary>
    /// Pins the cast ruling: 'x as T(...)' means C#'s cast, on both backends. The operator is this
    /// fork's own - the frozen legacy suite never uses 'as' - so the choice was free, and the
    /// compiled path (LExpression.Convert, which IS the C# cast) is the specification; the
    /// interpreter executes converters compiled from the same LExpression.Convert per type pair, so
    /// agreement is by construction. What the ruling removed, deliberately: the old interpreted
    /// Convert.ChangeType gave banker's rounding (45.6 as T(int) was 46), checked overflow, and
    /// culture-sensitive string parsing ('45' as T(int) was 45) - none of which is a cast. What it
    /// added: enum casts and user-defined conversion operators now work interpreted.
    /// </summary>
    [TestFixture]
    public class CastAgreementTests : BaseCompiledTests
    {
        [Test]
        public void RealToIntegralTruncatesLikeCSharp()
        {
            TestCompiledVsInterpreted<int>("45.6 as T(int)").ResultEqualsTo(45);
            TestCompiledVsInterpreted<int>("45.5 as T(int)").ResultEqualsTo(45);
            TestCompiledVsInterpreted<int>("-45.5 as T(int)").ResultEqualsTo(-45);
            TestCompiledVsInterpreted<int>("45.6m as T(int)").ResultEqualsTo(45);
        }

        /// <summary>
        /// The deliberate cost of "as means C#'s cast": primitive narrowing wraps silently, exactly
        /// as C#'s cast does in its default unchecked context - (short)70000 is 4464 in C# too.
        /// </summary>
        [Test]
        public void PrimitiveOverflowWrapsUncheckedLikeCSharp()
        {
            TestCompiledVsInterpreted<short>("70000 as T(short)").ResultEqualsTo((short)4464);
            TestCompiledVsInterpreted<byte>("300 as T(System.Byte)").ResultEqualsTo((byte)44);
        }

        /// <summary>
        /// Decimal is the exception in C# as well: its conversion operators always check, so a
        /// decimal source overflows loudly where a double source wraps.
        /// </summary>
        [Test]
        public void DecimalOverflowStillThrowsOnBothBackends()
        {
            Assert.Throws<OverflowException>(
                () => CompileGetter<short>("70000m as T(short)").GetValue());

            Assert.Throws<OverflowException>(
                () => InterpretGetter<short>("70000m as T(short)").GetValue());
        }

        /// <summary>
        /// New capability on the interpreted side: Convert.ChangeType could not produce an enum, so
        /// enum casts only worked compiled.
        /// </summary>
        [Test]
        public void EnumCastsWorkOnEveryPath()
        {
            TestCompiledVsInterpreted<DayOfWeek>("1 as T(System.DayOfWeek)")
                .ResultEqualsTo(DayOfWeek.Monday);

            TestCompiledVsInterpreted<int>("T(System.DayOfWeek).Friday as T(int)")
                .ResultEqualsTo(5);
        }

        /// <summary>
        /// New capability on the interpreted side: a user-defined conversion operator runs, where
        /// Convert.ChangeType demanded IConvertible and threw.
        /// </summary>
        [Test]
        public void UserConversionOperatorsRunOnEveryPath()
        {
            TestCompiledVsInterpreted<CastCases, decimal>("Amount as T(decimal)", new CastCases())
                .ResultEqualsTo(45.5m);
        }

        [Test]
        public void ReferenceDowncastWorksAndFailsLikeCSharp()
        {
            var ctx = new CastCases();

            TestCompiledVsInterpreted<CastCases, string>("Payload as T(string)", ctx)
                .ResultEqualsTo("payload");

            // a boxed DateTime is no Uri: the compiled runtime check and the interpreter report
            // the same InvalidCastException at evaluation
            Assert.Throws<InvalidCastException>(
                () => CompileGetter<CastCases, object>("BoxedDate as T(System.Uri)").GetValue(ctx));

            Assert.Throws<InvalidCastException>(
                () => InterpretGetter<CastCases, object>("BoxedDate as T(System.Uri)").GetValue(ctx));
        }

        /// <summary>
        /// String-parsing casts are gone, deliberately: C# has no cast from string to int, so the
        /// compiled path refuses the static shape (CS0030) and the interpreter reports the failed
        /// cast at evaluation. '45' as T(int) used to answer 45 interpreted - Convert.ChangeType
        /// parsing, not casting.
        /// </summary>
        [Test]
        public void StringParsingCastsAreGone()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<int>("'45' as T(int)"));

            IExpression weak = Expression.Parse("'45' as T(int)");
            Assert.Throws<InvalidCastException>(() => weak.GetValue());
        }

        [Test]
        public void NullCastsLikeCSharp()
        {
            // null casts to a reference or nullable target and stays null
            TestCompiledVsInterpreted<string>("null as T(string)").ResultEqualsTo(null);

            // unboxing null into a value type is a NullReferenceException on both backends
            Assert.Throws<NullReferenceException>(
                () => CompileGetter<object>("null as T(int)").GetValue());

            Assert.Throws<NullReferenceException>(
                () => InterpretGetter<object>("null as T(int)").GetValue());
        }

        /// <summary>
        /// An unresolvable type name used to leak TypeLoadException out of tree building; it refuses
        /// now, and the interpreter reports the TypeLoadException at evaluation.
        /// </summary>
        [Test]
        public void UnresolvableTypeNameRefusesCompiledAndThrowsAtEvaluationInterpreted()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<object>("1 as T(No.Such.TypeAtAll)"));

            IExpression weak = Expression.Parse("1 as T(No.Such.TypeAtAll)");
            Assert.Throws<TypeLoadException>(() => weak.GetValue());
        }
    }
}

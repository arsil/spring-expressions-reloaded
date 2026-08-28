using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A type's own arithmetic operators, honoured on both backends.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The engine had no notion of these. A type joined in arithmetic only by *converting* to a
    /// built-in number, so a decimal-like struct worked and everything else was refused compiled and
    /// threw interpreted - <c>TimeSpan</c> included, which is why the engine had no TimeSpan
    /// arithmetic at all. <c>TimeSpan</c> was never special; it is simply the operator-bearing struct
    /// in the BCL that people notice.
    /// </p>
    /// <p>
    /// <b>The lookup runs before the conversion path, which is C#'s order.</b> That matters for a type
    /// declaring both: it used to erase itself to the type it converts to. See
    /// <see cref="AConvertibleTypeWithOperatorsKeepsItsOwnType"/>.
    /// </p>
    /// <p>
    /// <b>Exact operand types only.</b> C#'s operator resolution is a chapter of the specification;
    /// none of it is here. A declared operator whose parameters do not match the operands exactly is
    /// not found, and both backends refuse together - see
    /// <see cref="AWidenedOperandIsNotAnExactMatchAndIsRefused"/>, which exists because the first
    /// attempt was not exact and the sweep caught it.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class UserDefinedOperatorTests : BaseCompiledTests
    {
        /// <summary>Operators, and no conversion to any built-in numeric type.</summary>
        public struct Money
        {
            public readonly decimal Amount;
            public Money(decimal amount) { Amount = amount; }

            public static Money operator +(Money a, Money b) { return new Money(a.Amount + b.Amount); }
            public static Money operator -(Money a, Money b) { return new Money(a.Amount - b.Amount); }
            public static Money operator *(Money a, int by) { return new Money(a.Amount * by); }
            public static Money operator /(Money a, int by) { return new Money(a.Amount / by); }
            public static Money operator %(Money a, Money b) { return new Money(a.Amount % b.Amount); }

            public override string ToString() { return "Money(" + Amount + ")"; }
            public override bool Equals(object o) { return o is Money m && m.Amount == Amount; }
            public override int GetHashCode() { return Amount.GetHashCode(); }
        }

        /// <summary>An implicit conversion to decimal <b>and</b> its own operators.</summary>
        public struct Convertible
        {
            public readonly decimal Amount;
            public Convertible(decimal amount) { Amount = amount; }

            public static implicit operator decimal(Convertible value) { return value.Amount; }
            public static Convertible operator +(Convertible a, Convertible b) { return new Convertible(a.Amount + b.Amount); }

            public override string ToString() { return "Convertible(" + Amount + ")"; }
            public override bool Equals(object o) { return o is Convertible c && c.Amount == Amount; }
            public override int GetHashCode() { return Amount.GetHashCode(); }
        }

        public class Root
        {
            public Money Ten { get; set; } = new Money(10);
            public Money Three { get; set; } = new Money(3);
            public Convertible ConvTen { get; set; } = new Convertible(10);
            public Convertible ConvThree { get; set; } = new Convertible(3);

            public TimeSpan TwoDays { get; set; } = TimeSpan.FromDays(2);
            public TimeSpan SixHours { get; set; } = TimeSpan.FromHours(6);
            public DateTime When { get; set; } = new DateTime(2001, 1, 1);

            public decimal Decimal { get; set; } = 10m;
            public int Int { get; set; } = 3;
            public double Double { get; set; } = 2.0;
        }

        [Test]
        public void ATypeWithOperatorsAndNoConversionJoinsInArithmetic()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Ten + Three", root).ResultEqualsTo(new Money(13));
            TestCompiledVsInterpreted<Root, object>("Ten - Three", root).ResultEqualsTo(new Money(7));
            TestCompiledVsInterpreted<Root, object>("Ten * Int", root).ResultEqualsTo(new Money(30));
            TestCompiledVsInterpreted<Root, object>("Ten % Three", root).ResultEqualsTo(new Money(1));
        }

        /// <summary>
        /// TimeSpan is the same shape, which is the whole point: it needed no special case.
        /// </summary>
        /// <remarks>
        /// Only <c>+</c> and <c>-</c> are exercised here, because they are the only TimeSpan arithmetic
        /// operators that exist on every target framework. **.NET Framework's TimeSpan declares no
        /// op_Multiply and no op_Division at all** - measured by reflection on net472, where the
        /// operator list is addition, subtraction, the six comparisons and the two unary ones; .NET
        /// Core added multiplication and division later. So <c>TwoDays / SixHours</c> compiles on
        /// net8.0 and refuses on net472, and both are correct: the engine honours whatever the BCL of
        /// the running framework declares. That is a property of the feature worth having - it tracks
        /// the platform rather than a hard-coded list - but it makes TimeSpan a poor subject for a
        /// cross-TFM test, so <see cref="Money"/> carries the multiply and divide rows instead.
        /// </remarks>
        [Test]
        public void TimeSpanArithmeticIsJustAUserDefinedOperator()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("TwoDays + SixHours", root)
                .ResultEqualsTo(TimeSpan.FromHours(54));
            TestCompiledVsInterpreted<Root, object>("TwoDays - SixHours", root)
                .ResultEqualsTo(TimeSpan.FromHours(42));
        }

        /// <summary>
        /// The row the ordering ruling exists for. With the conversion consulted first, this answered
        /// a <c>decimal</c> - the type erased itself, silently, on both backends. Nothing in either
        /// suite declared such a type, which is why it was never caught.
        /// </summary>
        [Test]
        public void AConvertibleTypeWithOperatorsKeepsItsOwnType()
        {
            TestCompiledVsInterpreted<Root, object>("ConvTen + ConvThree", new Root())
                .ResultEqualsTo(new Convertible(13));
        }

        /// <summary>
        /// A type with a conversion and *no* operator for the pair still converts, exactly as before -
        /// which is what leaves <c>CustomRealTypesTests</c> untouched: MoneyLike and SpeedLike declare
        /// a conversion and no operators at all, so this lookup finds nothing for them.
        /// </summary>
        [Test]
        public void AConvertibleTypeWithoutAMatchingOperatorStillConverts()
        {
            // Convertible declares no operator-(Convertible, Convertible), so subtraction converts
            TestCompiledVsInterpreted<Root, object>("ConvTen - ConvThree", new Root())
                .ResultEqualsTo(7m);
        }

        /// <summary>
        /// Built-in numeric pairs never reach the lookup: the promotion rules keep that whole space,
        /// which is what keeps every existing numeric result exactly as it was. decimal declares
        /// op_Addition(decimal, decimal), so without the guard this would have started intercepting
        /// ordinary decimal arithmetic.
        /// </summary>
        [Test]
        public void BuiltInNumericPairsStayOnThePromotionPath()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Decimal + Int", root).ResultEqualsTo(13m);
            TestCompiledVsInterpreted<Root, object>("Int + Int", root).ResultEqualsTo(6);
            TestCompiledVsInterpreted<Root, object>("Decimal * Int", root).ResultEqualsTo(30m);
        }

        /// <summary>
        /// The lookup is exact, and this is the test that says so - written because the first attempt
        /// was not.
        /// </summary>
        /// <remarks>
        /// <c>Type.GetMethod</c> with a null binder uses <c>Type.DefaultBinder</c>, which widens: asking
        /// for <c>(TimeSpan, int)</c> hands back <c>op_Division(TimeSpan, double)</c>. Emitting that
        /// failed inside the LINQ factory - "the operands for operator 'Divide' do not match the
        /// parameters of method 'op_Division'" - and <c>CompilationNeverLeaksTests</c> caught it as a
        /// new kind of absorbed defect. Do not "fix" this by widening the operands without ruling on
        /// it: the interpreter would have to widen identically, and reflection's Invoke widens
        /// arguments silently.
        /// </remarks>
        [Test]
        public void AWidenedOperandIsNotAnExactMatchAndIsRefused()
        {
            // Money declares operator /(Money, int) and operator *(Money, int). A double operand is
            // not an int, and is not widened to reach one - so both backends refuse together.
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Ten / Double", EvaluationMode.MustCompile));

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<Root, object>("Ten / Double", EvaluationMode.MustInterpret)
                    .GetValue(new Root()));

            // spelled with the declared parameter type, it is exact and works
            TestCompiledVsInterpreted<Root, object>("Ten / Int", new Root())
                .ResultEqualsTo(new Money(10m / 3));
        }

        /// <summary>
        /// DateTime + TimeSpan needed no branch of its own in the end - it is a user-defined operator
        /// like any other, and the same lookup finds it.
        /// </summary>
        [Test]
        public void TheBclDateTimeOperatorsAreFoundTheSameWay()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("When + TwoDays", root)
                .ResultEqualsTo(new DateTime(2001, 1, 3));
            TestCompiledVsInterpreted<Root, object>("When - TwoDays", root)
                .ResultEqualsTo(new DateTime(2000, 12, 30));
        }
    }
}

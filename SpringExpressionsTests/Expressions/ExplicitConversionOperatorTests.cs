using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringCore;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Exactly two decimal places: a decimal becomes one only explicitly, because that truncates.
    /// </summary>
    /// <remarks>
    /// Declared at namespace level, not nested in its fixture, so an expression can name it -
    /// <c>Amount as SpringExpressionsTests.Expressions.Cents</c>. A nested type's name has a <c>+</c>
    /// in it, which only <c>T(...)</c> can spell.
    /// </remarks>
    public struct Cents
    {
        private Cents(decimal amount)
        {
            Amount = amount;
        }

        public decimal Amount { get; }

        public static implicit operator decimal(Cents cents)
        {
            return cents.Amount;
        }

        public static explicit operator Cents(decimal amount)
        {
            return new Cents(decimal.Truncate(amount * 100) / 100);
        }
    }

    /// <summary>Constructed from <see cref="Cents"/>, for the constructor-argument rows.</summary>
    public class CentsBox
    {
        public CentsBox(Cents cents)
        {
            Cents = cents;
        }

        public Cents Cents { get; }
    }

    /// <summary>
    /// A type's own explicit conversion operator runs only through a cast written in the expression -
    /// <c>Amount as Cents</c> - at every typed sink, on both backends, as in C#.
    /// </summary>
    /// <remarks>
    /// <p>
    /// An operator is explicit because it can lose information. <see cref="Cents"/> holds exactly two
    /// decimal places, so converting 12.3456 into one truncates to 12.34, and applying that because a
    /// request, a parameter or a property happens to be typed <c>Cents</c> loses the caller's data
    /// without their having asked. C# refuses every such sink (CS0266, CS1503) and wants
    /// <c>(Cents)amount</c>; here the spelling is <c>as</c>.
    /// </p>
    /// <p>
    /// Without the cast, every sink is the paired refusal: refused compiled, an exception interpreted.
    /// <c>LExpression.Convert</c> resolves an explicit operator by itself, so a sink converting by
    /// asking it would apply one without a word - the requested result, a method or constructor
    /// argument and a property write are the sinks that do ask it, and each is pinned here. The
    /// implicit direction, a <c>Cents</c> read as a decimal, loses nothing and is untouched.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class ExplicitConversionOperatorTests
    {
        /// <summary>The operator on the source type: a reading becomes a decimal only explicitly.</summary>
        public struct Reading
        {
            public Reading(decimal value)
            {
                Value = value;
            }

            public decimal Value { get; }

            public static explicit operator decimal(Reading reading)
            {
                return reading.Value;
            }
        }

        public class Root
        {
            public decimal Amount { get; set; } = 12.3456m;
            public decimal? MaybeAmount { get; set; } = 12.3456m;
            public Cents Price { get; set; } = (Cents)12.3456m;
            public decimal Total { get; set; }
            public Reading Meter { get; set; } = new Reading(7.5m);
            public Dictionary<string, Cents> Prices { get; } = new Dictionary<string, Cents>();
            public decimal TakesCents(Cents cents) => cents.Amount;
            public decimal TakesDecimal(decimal value) => value;
        }

        [Test]
        public void ARequestedResultIsRefusedWithoutACast()
        {
            AssertRefusedAndTheInterpreterThrows<Cents, InvalidCastException>("Amount");
            AssertRefusedAndTheInterpreterThrows<Cents?, InvalidCastException>("Amount");
            AssertRefusedAndTheInterpreterThrows<Cents?, InvalidCastException>("MaybeAmount");
        }

        [Test]
        public void AMethodArgumentIsRefusedWithoutACast()
        {
            AssertRefusedAndTheInterpreterThrows<object, InvalidCastException>("TakesCents(Amount)");
            AssertRefusedAndTheInterpreterThrows<object, InvalidCastException>("TakesCents(MaybeAmount)");
        }

        [Test]
        public void AConstructorArgumentIsRefusedWithoutACast()
        {
            AssertRefusedAndTheInterpreterThrows<object, ArgumentException>(
                "new SpringExpressionsTests.Expressions.CentsBox(Amount).Cents");
        }

        [Test]
        public void APropertyWriteIsRefusedWithoutACast()
        {
            AssertRefusedAndTheInterpreterThrows<object, TypeMismatchException>("Price = Amount");
        }

        [Test]
        public void AnIndexerWriteIsRefusedWithoutACast()
        {
            AssertRefusedAndTheInterpreterThrows<object, InvalidPropertyException>("Prices['a'] = Amount");
        }

        [Test]
        public void ADeclaredLocalAndAnArrayElementAreRefusedWithoutACast()
        {
            AssertRefusedAndTheInterpreterThrows<object, TypeMismatchException>(
                "(SpringExpressionsTests.Expressions.Cents $c = Amount; $c)");
            AssertRefusedAndTheInterpreterThrows<object, InvalidCastException>(
                "new SpringExpressionsTests.Expressions.Cents[] {Amount}");
        }

        /// <summary>Which type declares the operator makes no difference.</summary>
        [Test]
        public void AnOperatorDeclaredOnTheSourceTypeIsRefusedWithoutACastToo()
        {
            AssertRefusedAndTheInterpreterThrows<decimal, InvalidCastException>("Meter");
            AssertRefusedAndTheInterpreterThrows<object, InvalidCastException>("TakesDecimal(Meter)");
            AssertRefusedAndTheInterpreterThrows<object, TypeMismatchException>("Total = Meter");
        }

        [Test]
        public void TheCastRunsTheOperatorAtEverySink()
        {
            var truncated = (Cents)12.3456m;

            AssertBothBackendsAnswer<Cents>(
                "Amount as SpringExpressionsTests.Expressions.Cents", truncated);
            AssertBothBackendsAnswer<object>(
                "TakesCents(Amount as SpringExpressionsTests.Expressions.Cents)", 12.34m);
            AssertBothBackendsAnswer<object>(
                "new SpringExpressionsTests.Expressions.CentsBox(Amount as SpringExpressionsTests.Expressions.Cents).Cents",
                truncated);
            AssertBothBackendsAnswer<object>(
                "Price = Amount as SpringExpressionsTests.Expressions.Cents", truncated);
            AssertBothBackendsAnswer<object>(
                "Prices['a'] = Amount as SpringExpressionsTests.Expressions.Cents", truncated);
            AssertBothBackendsAnswer<decimal>("Meter as decimal", 7.5m);
            AssertBothBackendsAnswer<object>("TakesDecimal(Meter as decimal)", 7.5m);
        }

        [Test]
        public void TheImplicitDirectionIsUntouched()
        {
            AssertBothBackendsAnswer<decimal>("Price", 12.34m);
            AssertBothBackendsAnswer<object>("TakesDecimal(Price)", 12.34m);
            AssertBothBackendsAnswer<object>("Total = Price", 12.34m);
        }

        /// <summary>The refusal says what to write, so a caller is not left guessing.</summary>
        [Test]
        public void TheRefusalNamesTheCast()
        {
            var argument = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("TakesCents(Amount)", EvaluationMode.MustCompile));
            StringAssert.Contains("explicit operator", argument.Message);
            StringAssert.Contains("'x as T'", argument.Message);

            var write = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Price = Amount", EvaluationMode.MustCompile));
            StringAssert.Contains("explicit operator", write.Message);
            StringAssert.Contains("'x as T'", write.Message);
        }

        private static void AssertBothBackendsAnswer<TResult>(string expression, TResult expected)
        {
            var compiled = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustCompile)
                .GetValue(new Root());
            var interpreted = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustInterpret)
                .GetValue(new Root());

            Assert.AreEqual(expected, compiled, "compiled: " + expression);
            Assert.AreEqual(expected, interpreted, "interpreted: " + expression);
        }

        private static void AssertRefusedAndTheInterpreterThrows<TResult, TException>(string expression)
            where TException : Exception
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustCompile),
                "compiled: " + expression);

            var interpreted = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustInterpret);
            Assert.Throws<TException>(() => interpreted.GetValue(new Root()), "interpreted: " + expression);
        }
    }
}

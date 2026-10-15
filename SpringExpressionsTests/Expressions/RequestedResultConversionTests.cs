using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// What a strongly typed getter does when the expression produces one type and the caller asked
    /// for another: it converts where C# converts without a cast - an implicit numeric widening - and
    /// refuses everything else, on both backends.
    /// </summary>
    /// <remarks>
    /// <c>ParseGetter&lt;Root, long&gt;("Number")</c> is <c>long r = root.Number;</c>.
    /// <c>ParseGetter&lt;Root, int&gt;("Flag")</c> has no C# spelling without a cast, so the compiled
    /// path refuses it at parse and the interpreter throws <c>InvalidCastException</c> at evaluation -
    /// the engine's standing paired-refusal shape. Every row asserts both halves: which backend
    /// serves a caller follows from the evaluation mode, so a conversion only one of them performs
    /// would make the answer depend on the mode - <c>LExpression.ConvertChecked</c> alone would answer
    /// <c>1</c> for <c>Flag</c> where the interpreter's cast throws.
    /// </remarks>
    [TestFixture]
    public class RequestedResultConversionTests
    {
        public enum Colour
        {
            Red,
            Green,
            Blue,
            Cyan
        }

        public struct Counter
        {
            private readonly int _value;

            public Counter(int value)
            {
                _value = value;
            }

            public static implicit operator int(Counter counter)
            {
                return counter._value;
            }
        }

        public class Root
        {
            public bool Flag { get; set; } = true;
            public int Number { get; set; } = 45;
            public long Big { get; set; } = 45L;
            public double Real { get; set; } = 45.6;
            public decimal Amount { get; set; } = 45.6m;
            public char Letter { get; set; } = 'A';
            public int Three { get; set; } = 3;
            public DayOfWeek Day { get; set; } = DayOfWeek.Wednesday;
            public int? MaybeNumber { get; set; } = 45;
            public int? NoNumber { get; set; }
            public long? MaybeBig { get; set; } = 45L;
            public Counter Counted { get; set; } = new Counter(7);
            public object Anything { get; set; }
        }

        [Test]
        public void AWideningRequestAnswersOnBothBackends()
        {
            var root = new Root();

            long asLong = root.Number;
            double asDouble = root.Number;
            decimal asDecimal = root.Number;
            float asFloat = root.Number;
            double bigAsDouble = root.Big;
            int letterAsInt = root.Letter;

            AssertBothBackendsAnswer<long>("Number", root, asLong);
            AssertBothBackendsAnswer<double>("Number", root, asDouble);
            AssertBothBackendsAnswer<decimal>("Number", root, asDecimal);
            AssertBothBackendsAnswer<float>("Number", root, asFloat);
            AssertBothBackendsAnswer<double>("Big", root, bigAsDouble);
            AssertBothBackendsAnswer<int>("Letter", root, letterAsInt);
        }

        [Test]
        public void AWideningIntoANullableRequestAnswersOnBothBackends()
        {
            var root = new Root();

            long? fromPlain = root.Number;
            long? fromNullable = root.MaybeNumber;
            long? fromNothing = root.NoNumber;

            AssertBothBackendsAnswer<long?>("Number", root, fromPlain);
            AssertBothBackendsAnswer<long?>("MaybeNumber", root, fromNullable);
            AssertBothBackendsAnswer<long?>("NoNumber", root, fromNothing);
        }

        [Test]
        public void ABooleanIsNotDeliveredAsANumber()
        {
            var root = new Root();

            AssertRefusedAndTheInterpreterThrows<int>("Flag", root);
            AssertRefusedAndTheInterpreterThrows<long>("Flag", root);
            AssertRefusedAndTheInterpreterThrows<double>("Flag", root);
            AssertRefusedAndTheInterpreterThrows<decimal>("Flag", root);
            AssertRefusedAndTheInterpreterThrows<bool>("Number", root);
        }

        [Test]
        public void ANarrowingRequestIsRefusedAndTheInterpreterThrows()
        {
            var root = new Root();

            AssertRefusedAndTheInterpreterThrows<int>("Big", root);
            AssertRefusedAndTheInterpreterThrows<short>("Number", root);
            AssertRefusedAndTheInterpreterThrows<int?>("MaybeBig", root);
        }

        [Test]
        public void ARealIsNotTruncatedIntoAnIntegralRequest()
        {
            var root = new Root();

            AssertRefusedAndTheInterpreterThrows<int>("Real", root);
            AssertRefusedAndTheInterpreterThrows<int>("Amount", root);
            AssertRefusedAndTheInterpreterThrows<long>("Real", root);
        }

        [Test]
        public void AnIntIsNotReadAsAChar()
        {
            AssertRefusedAndTheInterpreterThrows<char>("Number", new Root());
        }

        [Test]
        public void AnEnumIsNotWidenedPastItsUnderlyingType()
        {
            AssertRefusedAndTheInterpreterThrows<long>("Day", new Root());
        }

        /// <summary>
        /// A type's own conversion operator is not applied to a requested result, as it is not
        /// applied to a declared local or an array element.
        /// </summary>
        [Test]
        public void AConversionOperatorIsNotAppliedToARequestedResult()
        {
            AssertRefusedAndTheInterpreterThrows<int>("Counted", new Root());
        }

        /// <summary>
        /// The one thing C#'s cast from object does beyond widening, and the interpreter has always
        /// performed it: an enum is read as its underlying type and back.
        /// </summary>
        [Test]
        public void AnEnumAndItsUnderlyingTypeAnswerOnBothBackends()
        {
            var root = new Root();

            AssertBothBackendsAnswer<int>("Day", root, 3);
            AssertBothBackendsAnswer<DayOfWeek>("Three", root, DayOfWeek.Wednesday);
            AssertBothBackendsAnswer<Colour>("Day", root, Colour.Cyan);
        }

        /// <summary>
        /// LINQ has no conversion at all for these two pairs, and the refusal must come before it is
        /// asked: an exception out of LINQ reaches the compiler-defect absorber, which tells the
        /// caller to report their own expression as a bug. <c>Assert.Throws</c> demands the exact
        /// type, so an absorbed <c>InternalCompilerErrorException</c> fails here.
        /// </summary>
        [Test]
        public void TheRefusalNamesBothTypesAndIsNotAnInternalCompilerError()
        {
            var flagAsDecimal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, decimal>("Flag", EvaluationMode.MustCompile));
            var numberAsBool = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, bool>("Number", EvaluationMode.MustCompile));

            StringAssert.Contains("System.Boolean", flagAsDecimal.Message);
            StringAssert.Contains("System.Decimal", flagAsDecimal.Message);
            StringAssert.DoesNotContain("internal compiler error", flagAsDecimal.Message);

            StringAssert.Contains("System.Int32", numberAsBool.Message);
            StringAssert.Contains("System.Boolean", numberAsBool.Message);
            StringAssert.DoesNotContain("internal compiler error", numberAsBool.Message);
        }

        /// <summary>
        /// What a caller on the default mode sees: the refusal falls back to the interpreter, so the
        /// answer is the interpreter's exception - the same one the other two modes give.
        /// </summary>
        [Test]
        public void TheDefaultModeGivesTheSameAnswerAsTheOtherTwo()
        {
            var getter = Expression.ParseGetter<Root, int>("Flag");

            var status = Expression.GetCompilationStatus(getter);
            Assert.AreEqual(EvaluationKind.Interpreted, status.Kind);
            Assert.AreEqual(InterpretationReason.CompilationRefused, status.Reason);

            Assert.Throws<InvalidCastException>(() => getter.GetValue(new Root()));
        }

        [Test]
        public void TheEscapesAreTheTypeTheExpressionProducesOrACast()
        {
            var root = new Root();

            AssertBothBackendsAnswer<bool>("Flag", root, true);
            AssertBothBackendsAnswer<int>("Flag ? 1 : 0", root, 1);
            AssertBothBackendsAnswer<int>("Big as int", root, 45);
            AssertBothBackendsAnswer<int>("Real as int", root, 45);
            AssertBothBackendsAnswer<double>("2 ^ 10", root, 1024d);
        }

        /// <summary>
        /// An object-typed body carries no static type, so both backends decide from the value it
        /// holds, by the same rule.
        /// </summary>
        [Test]
        public void AnObjectTypedBodyIsDecidedByTheValueItHolds()
        {
            AssertBothBackendsAnswer<long>("Anything", new Root { Anything = 45 }, 45L);
            AssertBothBackendsAnswer<decimal>("Anything", new Root { Anything = 45 }, 45m);
            AssertBothBackendsAnswer<long?>("Anything", new Root { Anything = 45 }, 45L);
            AssertBothBackendsAnswer<decimal>("Anything", new Root { Anything = 6m }, 6m);
            AssertBothBackendsAnswer<int>("Anything", new Root { Anything = DayOfWeek.Wednesday }, 3);

            AssertBothBackendsThrow<int, InvalidCastException>("Anything", new Root { Anything = 45L });
            AssertBothBackendsThrow<int, InvalidCastException>("Anything", new Root { Anything = true });
            AssertBothBackendsThrow<int, InvalidCastException>("Anything", new Root { Anything = "45" });
            AssertBothBackendsThrow<decimal, InvalidCastException>("Anything", new Root { Anything = 6.0 });
            AssertBothBackendsThrow<int, NullReferenceException>("Anything", new Root { Anything = null });
        }

        private static void AssertBothBackendsAnswer<TResult>(string expression, Root root, TResult expected)
        {
            var compiled = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustCompile)
                .GetValue(root);
            var interpreted = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustInterpret)
                .GetValue(root);

            Assert.AreEqual(expected, compiled, "compiled: " + expression);
            Assert.AreEqual(expected, interpreted, "interpreted: " + expression);
        }

        private static void AssertRefusedAndTheInterpreterThrows<TResult>(string expression, Root root)
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustCompile),
                "compiled: " + expression);

            var interpreted = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustInterpret);
            Assert.Throws<InvalidCastException>(() => interpreted.GetValue(root), "interpreted: " + expression);
        }

        private static void AssertBothBackendsThrow<TResult, TException>(string expression, Root root)
            where TException : Exception
        {
            var compiled = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustCompile);
            var interpreted = Expression.ParseGetter<Root, TResult>(expression, EvaluationMode.MustInterpret);

            Assert.Throws<TException>(() => compiled.GetValue(root), "compiled: " + expression);
            Assert.Throws<TException>(() => interpreted.GetValue(root), "interpreted: " + expression);
        }
    }
}

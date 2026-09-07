using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A type's own <c>implicit operator</c> is honoured wherever a value has to become another type -
    /// on assignment and in an argument - before any conversion this engine performs.
    /// </summary>
    /// <remarks>
    /// The ordering is item 12's, ruled for operators and applied here to conversion. What made it
    /// necessary was not a missing feature but a **measured divergence**: the engine only ever knew
    /// about conversions to a <i>real</i> type (decimal, double, float), so a type with
    /// <c>implicit operator decimal</c> bound to a decimal parameter while one with
    /// <c>implicit operator int</c> did not - and <c>TakesInt(counter)</c> answered <c>7</c> compiled
    /// and threw <c>InvalidCastException</c> interpreted, because the emitter resolves any operator
    /// LINQ can see.
    /// <p>
    /// <b>Purely additive except for one carve-out.</b> Measured over four custom types and fifteen
    /// target types, the interpreter's converter already had an answer for exactly two: <c>object</c>,
    /// which assignability takes first, and <c>string</c>, which every type reaches through
    /// <c>ToString()</c>. Everything else threw, so consulting the operator first turns error space
    /// into answers and changes nothing that already worked.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class ImplicitConversionOperatorTests
    {
        public struct Money
        {
            public Money(decimal v) { _v = v; }
            private readonly decimal _v;
            public static implicit operator decimal(Money m) { return m._v; }
            public override string ToString() { return "Money(" + _v + ")"; }
        }

        public struct Counter
        {
            public Counter(int v) { _v = v; }
            private readonly int _v;
            public static implicit operator int(Counter c) { return c._v; }
            public override string ToString() { return "Counter(" + _v + ")"; }
        }

        public class Label
        {
            public Label(string v) { _v = v; }
            private readonly string _v;
            public static implicit operator string(Label l) { return l._v; }
            public override string ToString() { return "Label(" + _v + ")"; }
        }

        public class Host
        {
            public Money Money = new Money(45.5m);
            public Counter Counter = new Counter(7);
            public Label Label = new Label("hi");

            public decimal Amount { get; set; }
            public int Number { get; set; }
            public long Big { get; set; }
            public string Name { get; set; }

            public string TakesDecimal(decimal v) { return "decimal:" + v; }
            public string TakesInt(int v) { return "int:" + v; }
        }

        [Test]
        public void ANonRealConversionInAnArgumentNoLongerDiverges()
        {
            // The defect this work exists for. Before it: int:7 compiled, InvalidCastException
            // interpreted - one backend answering while the other threw, decided by whether the shape
            // happened to compile, which is not the caller's choice.
            Assert.AreEqual(
                "int:7",
                Expression.ParseGetter<Host, object>("TakesInt(Counter)", EvaluationMode.MustCompile)
                    .GetValue(new Host()));

            Assert.AreEqual(
                "int:7",
                Expression.ParseGetter<Host, object>("TakesInt(Counter)", EvaluationMode.MustInterpret)
                    .GetValue(new Host()));
        }

        [Test]
        public void ARealConversionInAnArgumentStillWorks()
        {
            // Unmoved: this is what the custom-real ruling already bought, and it is now one instance
            // of the general rule rather than its own path.
            Assert.AreEqual(
                "decimal:45,5".Replace(',', DecimalSeparator),
                Expression.ParseGetter<Host, object>(
                    "TakesDecimal(Money)", EvaluationMode.MustCompile).GetValue(new Host()));

            Assert.AreEqual(
                "decimal:45,5".Replace(',', DecimalSeparator),
                Expression.ParseGetter<Host, object>(
                    "TakesDecimal(Money)", EvaluationMode.MustInterpret).GetValue(new Host()));
        }

        [Test]
        public void AssigningThroughAConversionOperatorCompilesAndAgrees()
        {
            AssertBothLand<Money>("Amount", new Money(45.5m), 45.5m);
            AssertBothLand<Counter>("Number", new Counter(7), 7);
        }

        [Test]
        public void TheOperatorMayLandShortOfTheTargetAndWidenAfterwards()
        {
            // C# allows the user-defined step followed by a built-in widening, and the widening half
            // is the array-initialiser rule again rather than a second one: Counter -> int -> long.
            AssertBothLand<Counter>("Big", new Counter(7), 7L);
        }

        [Test]
        public void AStringTargetKeepsToStringDeliberately()
        {
            // The one carve-out, and the reason it is a carve-out rather than an omission: every type
            // already reaches a string through ToString(), so honouring the operator here would change
            // an existing answer rather than fill a gap. C# would give "hi"; this engine gives
            // "Label(hi)" and will keep doing so until that is ruled on its own.
            //
            // Do not "fix" one side of this without ruling the other - both backends agree today.
            var host = new Host();
            Expression.ParseSetter<Host, Label>("Name").SetValue(host, new Label("hi"));

            Assert.AreEqual("Label(hi)", host.Name);

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseSetter<Host, Label>("Name", EvaluationMode.MustCompile));
        }

        [Test]
        public void ATypeWithNoOperatorIsUnaffected()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseSetter<Host, Label>("Amount", EvaluationMode.MustCompile));

            Assert.Throws<SpringCore.TypeMismatchException>(
                () => Expression.ParseSetter<Host, Label>("Amount").SetValue(new Host(), new Label("x")));
        }

        private static void AssertBothLand<TValue>(string member, TValue value, object expected)
        {
            var compiledHost = new Host();
            Expression.ParseSetter<Host, TValue>(member, EvaluationMode.MustCompile)
                .SetValue(compiledHost, value);

            var interpretedHost = new Host();
            Expression.ParseSetter<Host, TValue>(member, EvaluationMode.MustInterpret)
                .SetValue(interpretedHost, value);

            var compiled = typeof(Host).GetProperty(member).GetValue(compiledHost, null);
            var interpreted = typeof(Host).GetProperty(member).GetValue(interpretedHost, null);

            Assert.AreEqual(expected, compiled, member + " compiled");
            Assert.AreEqual(expected, interpreted, member + " interpreted");
            Assert.AreEqual(compiled.GetType(), interpreted.GetType(), member + " runtime type");
        }

        private static char DecimalSeparator
        {
            get
            {
                return System.Globalization.CultureInfo.CurrentCulture
                    .NumberFormat.NumberDecimalSeparator[0];
            }
        }
    }
}

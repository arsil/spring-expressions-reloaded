using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Equality between two value types that are not the same type: refused, never answered.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The compiled path ends in a boxing tail - box both operands, call <c>object.Equals</c> - which
    /// for two unrelated types answers <b>false</b> rather than failing. So <c>45 == true</c> was
    /// <c>False</c> and <c>45 != true</c> was <c>True</c>, while the interpreter refused both pairs
    /// with ArgumentException. A plausible-looking boolean instead of an error, and which one a caller
    /// got depended on whether the shape happened to compile.
    /// </p>
    /// <p>
    /// This is the same accident the enum guard was written for - <c>Type == 1</c> answering false -
    /// found one type at a time. The rule is now general: two statically value-typed operands of
    /// different types have no compiled equality.
    /// </p>
    /// <p>
    /// Note what is deliberately <i>not</i> covered: a value type against an <c>object</c>. There the
    /// runtime value decides, and <c>Number == Anything</c> agrees on both backends when the object
    /// holds an int. Refusing it statically would break a shape that works.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class MixedValueTypeEqualityTests : BaseCompiledTests
    {
        public enum Colour { Red = 1 }
        public enum Shape { Round = 1 }

        public class Root
        {
            public bool Flag { get; set; } = true;
            public bool? NullableFlag { get; set; } = true;
            public bool? NoFlag { get; set; }
            public int Number { get; set; } = 45;
            public int? NullableNumber { get; set; } = 45;
            public double Real { get; set; } = 45;
            public char Letter { get; set; } = 'x';
            public Colour Colour { get; set; } = Colour.Red;
            public Shape Shape { get; set; } = Shape.Round;
            public DateTime When { get; set; } = new DateTime(2001, 1, 1);
            public TimeSpan Span { get; set; } = TimeSpan.FromDays(1);
            public object Anything { get; set; } = 45;
        }

        [Test]
        public void UnrelatedValueTypesAreRefusedRatherThanAnsweredFalse()
        {
            AssertRefusedThenReportedByTheInterpreter("Number == Flag");
            AssertRefusedThenReportedByTheInterpreter("Real == Flag");
            AssertRefusedThenReportedByTheInterpreter("Letter == Flag");
            AssertRefusedThenReportedByTheInterpreter("When == Span");
            AssertRefusedThenReportedByTheInterpreter("Colour == Shape");
            AssertRefusedThenReportedByTheInterpreter("Number == Letter");
            AssertRefusedThenReportedByTheInterpreter("When == Number");
        }

        /// <summary>
        /// '!=' is '==' negated, so it must move with it - otherwise the two disagree, which is the
        /// defect the enum-against-string ruling was written to prevent.
        /// </summary>
        [Test]
        public void InequalityMovesWithEquality()
        {
            AssertRefusedThenReportedByTheInterpreter("Number != Flag");
            AssertRefusedThenReportedByTheInterpreter("Colour != Shape");

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("45 != true", EvaluationMode.MustCompile),
                "this used to compile and answer True");
        }

        /// <summary>
        /// The literal forms, which is how anyone would actually meet this.
        /// </summary>
        [Test]
        public void TheLiteralFormsAreRefusedToo()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("45 == true", EvaluationMode.MustCompile),
                "this used to compile and answer False");

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("0 == false", EvaluationMode.MustCompile));

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("45.5M == true", EvaluationMode.MustCompile));
        }

        /// <summary>
        /// A nullable and its underlying type still compare, on both backends - boxing a nullable
        /// yields either the underlying boxed value or a null reference, so the tail handles it.
        /// </summary>
        [Test]
        public void ANullableStillComparesToItsUnderlyingType()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("NullableFlag == Flag", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("NoFlag == Flag", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("NullableNumber == Number", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("NullableFlag == true", root).ResultEqualsTo(true);
        }

        [Test]
        public void TheSameEnumAndComparisonsAgainstNullAreUntouched()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Colour == Colour", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Number == null", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("Flag == null", root).ResultEqualsTo(false);
        }

        /// <summary>
        /// A value type against an object-typed member is left alone: the runtime value decides, and
        /// this shape agrees on both backends. Do not "fix" it by refusing statically.
        /// </summary>
        [Test]
        public void AValueTypeAgainstAnObjectIsStillCompared()
        {
            TestCompiledVsInterpreted<Root, object>("Number == Anything", new Root()).ResultEqualsTo(true);
        }

        private static void AssertRefusedThenReportedByTheInterpreter(string expression)
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustCompile),
                expression);

            Assert.AreNotEqual(
                "InternalCompilerErrorException", refusal.GetType().Name,
                "'" + expression + "' has no compiled form; it is not a defect of ours");

            Assert.Throws<ArgumentException>(
                () => Expression.Parse(expression).GetValue<Root>(new Root()),
                "and the interpreter refuses the pair at evaluation: " + expression);
        }
    }
}

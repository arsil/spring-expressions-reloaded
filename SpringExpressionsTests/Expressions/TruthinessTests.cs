using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// What the compiled path accepts as a boolean, and what it leaves to the interpreter.
    /// </summary>
    /// <remarks>
    /// <p>
    /// C# allows only <c>bool</c> as a conditional test (or a type declaring <c>operator true</c>): a
    /// number is CS0029 and a <c>bool?</c> is CS0266. This engine's interpreter is more permissive - it
    /// runs <c>Convert.ToBoolean</c>, so <c>45 ? a : b</c> answers <c>a</c> - and that is inherited
    /// behaviour which stays. It is deliberately **not** emitted: compiling a truthiness conversion
    /// would bake into the fast path a rule this engine has never ruled on, and which C# does not have.
    /// So those shapes refuse and the interpreter serves them.
    /// </p>
    /// <p>
    /// <c>bool?</c> is the exception, and it is not truthiness: a null in a boolean context reads as
    /// false throughout this engine - the same rule that makes <c>null and true</c> false - and the
    /// conditional operator is named in that ruling. It lifts with GetValueOrDefault.
    /// </p>
    /// <p>
    /// Both nodes used to let the bad shapes reach the emitter, where LINQ threw
    /// <c>ArgumentException("Argument must be boolean")</c> and
    /// <c>InvalidOperationException("The unary operator Not is not defined for …")</c>. Those were
    /// absorbed and reported as internal compiler errors - blaming the engine for a shape that is
    /// merely uncompiled. The point of these tests is the *kind* of refusal as much as the refusal.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class TruthinessTests : BaseCompiledTests
    {
        public enum Colour { None = 0, Red = 1 }

        public class Root
        {
            public bool Flag { get; set; } = true;
            public bool? NullableFlag { get; set; } = true;
            public bool? NoFlag { get; set; }
            public int Number { get; set; } = 45;
            public double Real { get; set; } = 4.5;
            public decimal Amount { get; set; } = 45.5m;
            public string Name { get; set; } = "Ana";
            public Colour Colour { get; set; } = Colour.Red;
        }

        // ----- the conditional test

        [Test]
        public void ABooleanTestCompiles()
        {
            TestCompiledVsInterpreted<Root, object>("Flag ? 1 : 2", new Root()).ResultEqualsTo(1);
        }

        /// <summary>
        /// A nullable boolean lifts, with nothing in it reading as false - the ruled behaviour for a
        /// null in a boolean context, and the one shape here that must compile rather than refuse.
        /// </summary>
        [Test]
        public void ANullableBooleanTestCompilesAndANullReadsAsFalse()
        {
            TestCompiledVsInterpreted<Root, object>("NullableFlag ? 1 : 2", new Root()).ResultEqualsTo(1);
            TestCompiledVsInterpreted<Root, object>("NoFlag ? 1 : 2", new Root()).ResultEqualsTo(2);
        }

        /// <summary>
        /// Everything else refuses - and refuses as a missing compiled form, naming the type, not as an
        /// internal compiler error.
        /// </summary>
        [Test]
        public void AnyOtherTestIsRefusedAndInterpreted()
        {
            AssertRefusedAsAMissingCompiledForm("Number ? 1 : 2", "the conditional test is");
            AssertRefusedAsAMissingCompiledForm("Real ? 1 : 2", "the conditional test is");
            AssertRefusedAsAMissingCompiledForm("Amount ? 1 : 2", "the conditional test is");
            AssertRefusedAsAMissingCompiledForm("Colour ? 1 : 2", "the conditional test is");
            AssertRefusedAsAMissingCompiledForm("Name ? 1 : 2", "the conditional test is");
            AssertRefusedAsAMissingCompiledForm("null ? 1 : 2", "the conditional test is");

            // and the interpreter goes on reading them exactly as it always has
            var root = new Root();
            Assert.AreEqual(1, Expression.Parse("Number ? 1 : 2").GetValue<Root>(root));
            Assert.AreEqual(1, Expression.Parse("Real ? 1 : 2").GetValue<Root>(root));
            Assert.AreEqual(2, Expression.Parse("null ? 1 : 2").GetValue<Root>(root));

            Assert.Throws<FormatException>(
                () => Expression.Parse("Name ? 1 : 2").GetValue<Root>(root),
                "'Ana' is not a boolean, and that is the interpreter's answer to give");
        }

        // ----- '!'

        /// <summary>
        /// '!' is two operators sharing a spelling: logical negation for a boolean, bitwise complement
        /// for an integer or enum. Both compile, and this is inherited behaviour - C# spells the second
        /// one '~'.
        /// </summary>
        [Test]
        public void NegationAndComplementBothCompile()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("!Flag", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("!Number", root).ResultEqualsTo(-46);
            TestCompiledVsInterpreted<Root, object>("!Colour", root).ResultEqualsTo((Colour)(-2));
        }

        /// <summary>
        /// A nullable boolean negates, with nothing in it read as false - the same lift the conditional
        /// test does, and for the same ruled reason.
        /// </summary>
        [Test]
        public void ANullableBooleanNegatesAndANullReadsAsFalse()
        {
            TestCompiledVsInterpreted<Root, object>("!NullableFlag", new Root()).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("!NoFlag", new Root()).ResultEqualsTo(true);
        }

        /// <summary>
        /// A real number is neither, so it refuses - where it used to reach LExpression.Not and crash.
        /// </summary>
        /// <remarks>
        /// The interpreter's answer for these is <c>False</c>: a double is not an integer, so it falls
        /// into the logical branch and its truthiness is negated. That means '!' answers a bitwise
        /// complement for one number and a boolean for another, decided by whether the number happens
        /// to be integral - an accident rather than a design, and the reason this is refused rather
        /// than emitted. Do not "fix" it by compiling the interpreter's answer without ruling on what
        /// '!' means for a real number.
        /// </remarks>
        [Test]
        public void ARealOperandIsRefusedAndInterpreted()
        {
            AssertRefusedAsAMissingCompiledForm("!Real", "no compiled complement for");
            AssertRefusedAsAMissingCompiledForm("!Amount", "no compiled complement for");

            Assert.AreEqual(false, Expression.Parse("!Real").GetValue<Root>(new Root()));
            Assert.AreEqual(false, Expression.Parse("!Amount").GetValue<Root>(new Root()));
        }

        /// <summary>
        /// The refusal must name the shape, not the engine: an InternalCompilerErrorException here
        /// would tell the caller to report a bug about an expression that is merely uncompiled.
        /// </summary>
        private static void AssertRefusedAsAMissingCompiledForm(string expression, string expectedText)
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustCompile),
                expression);

            Assert.AreNotEqual(
                "InternalCompilerErrorException", refusal.GetType().Name,
                "'" + expression + "' has no compiled form; it is not a defect of ours");

            StringAssert.Contains(expectedText, refusal.Message, expression);
        }
    }
}

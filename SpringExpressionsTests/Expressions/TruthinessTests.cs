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
    /// <b>Ruled: a non-boolean is not a truth value, anywhere.</b> The interpreter used to run
    /// <c>Convert.ToBoolean</c>, so <c>45 ? a : b</c> answered <c>a</c> and <c>!4.5</c> answered
    /// <c>False</c>, while the compiled path had no such conversion - C# has none either
    /// (<c>5 ? a : b</c> is CS0029). The deciding argument was <c>==</c>: <c>45 == true</c> has always
    /// refused the pair rather than answering, so leaving the ternary coercing meant one expression
    /// language giving two answers to "is 45 a truth value?". Both now refuse.
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
        /// Any other test is refused at compile time and rejected by the interpreter at evaluation -
        /// the same answer <c>45 == true</c> has always given.
        /// </summary>
        [Test]
        public void AnyOtherTestIsRefusedOnBothBackends()
        {
            AssertRefusedThenRejected("Number ? 1 : 2", "the conditional test is");
            AssertRefusedThenRejected("Real ? 1 : 2", "the conditional test is");
            AssertRefusedThenRejected("Amount ? 1 : 2", "the conditional test is");
            AssertRefusedThenRejected("Colour ? 1 : 2", "the conditional test is");
            AssertRefusedThenRejected("Name ? 1 : 2", "the conditional test is");

            // 'true' is the sharpest row: it used to answer 'yes' as a test while '=='true' threw
            AssertRefusedThenRejected("'true' ? 1 : 2", "the conditional test is");
        }

        /// <summary>
        /// Null is the one carve-out, and it is not truthiness: a null in a boolean position reads as
        /// false throughout this engine, and the ruling that makes 'null and true' false names the
        /// conditional operator. The compiled path has no form for a null-typed test, so it refuses and
        /// the interpreter answers.
        /// </summary>
        [Test]
        public void ANullTestReadsAsFalse()
        {
            AssertRefusedAsAMissingCompiledForm("null ? 1 : 2", "the conditional test is");

            Assert.AreEqual(2, Expression.Parse("null ? 1 : 2").GetValue<Root>(new Root()));
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
        /// The interpreter used to answer <c>False</c>: a double is not an integer, so it fell into the
        /// logical branch and its truthiness was negated. That made '!' answer a bitwise complement for
        /// one number and a boolean for another - <c>!45</c> is <c>-46</c>, <c>!4.5</c> was
        /// <c>False</c> - with the *kind* of answer decided by whether the operand happened to be
        /// integral. Both backends reject it now.
        /// </remarks>
        [Test]
        public void ARealOperandIsRejectedOnBothBackends()
        {
            AssertRefusedThenRejected("!Real", "no compiled complement for");
            AssertRefusedThenRejected("!Amount", "no compiled complement for");
            AssertRefusedThenRejected("!Name", "no compiled complement for");
        }

        /// <summary>
        /// A null still negates to true - the carve-out, as for the conditional test.
        /// </summary>
        [Test]
        public void NegatingANullIsTrue()
        {
            AssertRefusedAsAMissingCompiledForm("!null", "no compiled complement for");

            Assert.AreEqual(true, Expression.Parse("!null").GetValue<Root>(new Root()));
        }

        /// <summary>
        /// Refused at compile time, and rejected by the interpreter at evaluation - which is what a
        /// caller on the default path actually meets, after the fallback.
        /// </summary>
        private static void AssertRefusedThenRejected(string expression, string expectedText)
        {
            AssertRefusedAsAMissingCompiledForm(expression, expectedText);

            var rejection = Assert.Throws<ArgumentException>(
                () => Expression.Parse(expression).GetValue<Root>(new Root()),
                expression);

            StringAssert.Contains("only a boolean is a truth value", rejection.Message, expression);
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

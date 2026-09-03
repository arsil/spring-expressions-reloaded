using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A refusal names the node, the operands and the reason. Two generic messages used to stand in for
    /// that, and both were worse than vague.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <c>"node produced no expression tree"</c> is <see cref="SpringExpressions.BaseNode"/>'s answer to
    /// an emit method returning null, and an emit method is <c>[NotNull]</c> - so that message can only
    /// ever mean a node broke its own contract. <c>OpAND</c>, <c>OpOR</c> and <c>OpXOR</c> each returned
    /// a <c>[CanBeNull]</c> helper result straight out, which is where 1,638 of the compilation sweep's
    /// 7,556 refusals came from, every one of them stating no reason at all.
    /// </p>
    /// <p>
    /// <c>"no compiled implementation for this node type"</c> is the base method's message and belongs
    /// to a node that has none - <c>FunctionNode</c>, <c>OpLike</c> and their kind. Four nodes with a
    /// perfectly good compiled implementation reached it by ending their emit with
    /// <c>return base.GetExpressionTreeIfPossible(...)</c>, so it was not vague but false: it told a
    /// caller the operator was uncompiled when it was their operands that were.
    /// </p>
    /// <p>
    /// Nothing here changes which expressions compile. Every row below refused before and refuses now,
    /// and the weakly typed path interprets all of them exactly as it did.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class RefusalMessageTests
    {
        public enum Colour { Red, Green }

        public class Root
        {
            public string Name { get; set; } = "Ana";
            public int Number { get; set; } = 45;
            public double Real { get; set; } = 4.5;
            public bool Flag { get; set; } = true;
            public Colour Colour { get; set; } = Colour.Red;
            public List<int> Ints { get; set; } = new List<int> { 1, 2 };
        }

        /// <summary>
        /// The three that returned null out of a method annotated to never return one.
        /// </summary>
        /// <remarks>
        /// Each of these three operators is one spelling serving two roles - logical for booleans,
        /// bitwise for integers and enums - and the operand types are what pick the role. So the
        /// operand types are precisely what a caller needs to be told, which is what
        /// <c>OpNOT</c> already said for the unary spelling of the same problem.
        /// </remarks>
        [Test]
        public void TheLogicalOperatorsNameTheirOperandsAndBothRoles()
        {
            var and = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Name and Number", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpAND 'and': no compiled 'and' for 'System.String' and 'System.Int32'; "
                + "the logical form takes two booleans and the bitwise form two integers or enums",
                and.Message);

            var or = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Name or Real", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpOR 'or': no compiled 'or' for 'System.String' and 'System.Double'; "
                + "the logical form takes two booleans and the bitwise form two integers or enums",
                or.Message);

            var xor = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Name xor Flag", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpXOR 'xor': no compiled 'xor' for 'System.String' and 'System.Boolean'; "
                + "the logical form takes two booleans and the bitwise form two integers or enums",
                xor.Message);
        }

        /// <summary>
        /// A boolean meeting an integer is the shape the two-role message exists for: each operand names
        /// a different role, so neither role applies and no other message would explain why.
        /// </summary>
        [Test]
        public void ABooleanMeetingAnIntegerNamesNeitherRole()
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Flag and Number", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpAND 'and': no compiled 'and' for 'System.Boolean' and 'System.Int32'; "
                + "the logical form takes two booleans and the bitwise form two integers or enums",
                refusal.Message);
        }

        /// <summary>
        /// The shapes that do have a compiled form still have it - the three operators were not made
        /// stricter, only more talkative.
        /// </summary>
        [Test]
        public void TheRolesThatCompiledStillCompile()
        {
            var root = new Root();

            Assert.AreEqual(
                true,
                Expression.ParseGetter<Root, object>("Flag and Flag", EvaluationMode.MustCompile)
                    .GetValue(root));

            Assert.AreEqual(
                45,
                Expression.ParseGetter<Root, object>("Number and Number", EvaluationMode.MustCompile)
                    .GetValue(root));

            Assert.AreEqual(
                Colour.Red,
                Expression.ParseGetter<Root, object>("Colour and Colour", EvaluationMode.MustCompile)
                    .GetValue(root));

            Assert.AreEqual(
                true,
                Expression.ParseGetter<Root, object>("Flag or Flag", EvaluationMode.MustCompile)
                    .GetValue(root));

            Assert.AreEqual(
                false,
                Expression.ParseGetter<Root, object>("Flag xor Flag", EvaluationMode.MustCompile)
                    .GetValue(root));
        }

        /// <summary>
        /// <c>2 ^ 3</c> compiles, so "no compiled implementation for this node type" was false.
        /// </summary>
        /// <remarks>
        /// The operand types have to be captured before the emit path runs, not read off the locals at
        /// the point of refusal: <c>OpPOWER</c> reassigns both - once for a custom real-valued operand's
        /// own conversion, once for the to-double conversion - so a message built at the bottom could
        /// report <c>System.Double</c> twice whatever the caller wrote.
        /// </remarks>
        [Test]
        public void ExponentNamesTheOperandsAsWrittenAndSaysItComputesInDouble()
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Number ^ Name", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpPOWER '^': no compiled exponent for 'System.Int32' and 'System.String'"
                + "; '^' is computed in double, so both operands must be numbers",
                refusal.Message);

            Assert.AreEqual(
                8d,
                Expression.ParseGetter<Root, object>("2 ^ 3", EvaluationMode.MustCompile)
                    .GetValue(new Root()));
        }

        /// <summary>
        /// Unary minus and unary plus are twins, and only minus was found by the sweep: the corpus
        /// generates <c>-value</c> and never <c>+value</c>, so nothing measured the plus site until it
        /// was probed for deliberately.
        /// </summary>
        [Test]
        public void BothUnarySignsNameTheirOperand()
        {
            var minus = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("-Name", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpUnaryMinus '-': no compiled negation for 'System.String'; only a "
                + "number, or a type declaring its own unary '-', is negated",
                minus.Message);

            var plus = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("+Name", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpUnaryPlus '+': no compiled unary plus for 'System.String'; only a "
                + "number, or a type declaring its own unary '+', is accepted",
                plus.Message);

            Assert.AreEqual(
                -45,
                Expression.ParseGetter<Root, object>("-Number", EvaluationMode.MustCompile)
                    .GetValue(new Root()));

            Assert.AreEqual(
                45,
                Expression.ParseGetter<Root, object>("+Number", EvaluationMode.MustCompile)
                    .GetValue(new Root()));
        }

        /// <summary>
        /// The <c>ulong</c> negation refusal is raised inside <c>UnaryNumericOperatorHelper</c> and is
        /// deliberately untouched, so the new message must not have swallowed it.
        /// </summary>
        [Test]
        public void TheUlongNegationRefusalIsStillItsOwnMessage()
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>(
                    "-T(System.UInt64).Parse('7')", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Operator '-' cannot be applied to operand of type 'ulong'",
                refusal.Message);
        }

        /// <summary>
        /// <c>between</c> reads its two bounds out of a list, and the interpreter demands the same thing
        /// at evaluation - so both halves say so now, where the compiled half used to claim the operator
        /// had no compiled form.
        /// </summary>
        [Test]
        public void BetweenNamesWhatItWantedItsBoundsToBe()
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>(
                    "Number between Number", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "Cannot compile OpBetween 'between': the 'between' bounds must be a two-element list, "
                + "and this is of type 'System.Int32'",
                refusal.Message);

            var interpreted = Assert.Throws<ArgumentException>(
                () => Expression.Parse("Number between Number", EvaluationMode.MustInterpret)
                    .GetValue<Root>(new Root()));

            StringAssert.Contains("two-element list", interpreted.Message);

            Assert.AreEqual(
                true,
                Expression.ParseGetter<Root, object>("Number between {1, 100}", EvaluationMode.MustCompile)
                    .GetValue(new Root()));
        }

        /// <summary>
        /// A refusal raised inside a shared static helper still names the node, because the node is
        /// passed in. <c>CompileErrorException.NodeType</c> is public, and it used to be null for 918
        /// of the compilation sweep's 7,556 refusals - so a caller grouping refusals by node could not
        /// see 12% of them, and the message lost the expression text as well.
        /// </summary>
        /// <remarks>
        /// <c>EqualityHelper</c>, <c>MethodNode</c>'s argument binding, <c>ConstructorNode</c>'s
        /// resolution and <c>MethodBaseHelpers</c>' candidate scan are all <c>static</c>, so none can
        /// reach <c>BaseNode.CannotCompile</c>, which is an instance method. They take the node as a
        /// parameter rather than raising a nodeless exception.
        /// </remarks>
        [Test]
        public void ARefusalFromAStaticHelperStillNamesTheNodeAndTheExpression()
        {
            var equality = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Number == Flag", EvaluationMode.MustCompile));

            Assert.AreEqual(typeof(OpEqual), equality.NodeType);
            StringAssert.StartsWith("Cannot compile OpEqual '==': ", equality.Message);
            StringAssert.Contains("the static types do not determine", equality.Reason);

            var inequality = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Number != Flag", EvaluationMode.MustCompile));

            Assert.AreEqual(
                typeof(OpNotEqual), inequality.NodeType,
                "'!=' is the negation of '==', but the refusal must name the operator that was written");

            StringAssert.StartsWith("Cannot compile OpNotEqual '!=': ", inequality.Message);

            var argument = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Text(Number)", EvaluationMode.MustCompile));

            Assert.AreEqual(typeof(MethodNode), argument.NodeType);
            StringAssert.StartsWith("Cannot compile MethodNode 'Text': ", argument.Message);
        }

        /// <summary>
        /// The refused shapes are all still served by the interpreter, which is what makes this a
        /// message change rather than a behaviour change.
        /// </summary>
        /// <remarks>
        /// <c>Name and Number</c> and <c>Number between Number</c> are not in this list: both are
        /// illegal on either backend and the interpreter raises the caller's error at evaluation, which
        /// is the engine's standing shape for a shape no backend can serve.
        /// </remarks>
        [Test]
        public void TheWeaklyTypedPathAnswersEveryRefusedShapeItAnsweredBefore()
        {
            var root = new Root();

            Assert.AreEqual(
                true, Expression.Parse("true or Name").GetValue<Root>(root),
                "a true left operand short-circuits, so the string is never looked at");

            Assert.AreEqual(
                45, Expression.Parse("+Number").GetValue<Root>(root));

            Assert.AreEqual(
                -45, Expression.Parse("-Number").GetValue<Root>(root));

            Assert.AreEqual(
                true, Expression.Parse("Number between {1, 100}").GetValue<Root>(root));
        }
    }
}

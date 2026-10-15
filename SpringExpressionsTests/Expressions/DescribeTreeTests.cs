using System;
using System.Collections.Generic;
using System.Xml;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Printing the parsed tree: <c>Expression.DescribeTree</c> for a person, <c>DescribeTreeAsXml</c> for
    /// a program, each for a strongly typed expression and for what <c>Parse</c> and <c>Wrap</c> return.
    /// </summary>
    /// <remarks>
    /// A line is the node's class, the text the caller wrote for it, and the null-conditional marker. The
    /// tree is printed as it is: the <c>Expression</c> wrapped round nearly every operand is a real node,
    /// so it is printed, and only its made-up text "expression" is not.
    /// </remarks>
    [TestFixture]
    public class DescribeTreeTests
    {
        public class Inner
        {
            public string Name { get; set; }
            public int Echo(int value) => value;
        }

        public class Holder
        {
            public Inner Inner { get; set; }
            public int Number { get; set; }
            public List<int> Ints { get; set; }
        }

        [Test]
        public void AChainIsOneExpressionNodeWithALinkPerMember()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "|--PropertyOrFieldNode 'Inner'",
                    "|--PropertyOrFieldNode 'Name'",
                    "+--PropertyOrFieldNode 'Length'",
                }),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Inner.Name.Length")));
        }

        [Test]
        public void AnOperandIsPrintedInsideTheExpressionNodeThatWrapsIt()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "OpADD '+'",
                    "|--Expression",
                    "|  +--PropertyOrFieldNode 'Number'",
                    "+--OpMULTIPLY '*'",
                    "   |--Expression",
                    "   |  +--IntLiteralNode '2'",
                    "   +--Expression",
                    "      +--IntLiteralNode '3'",
                }),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Number + 2 * 3")));
        }

        [Test]
        public void AProjectionBodyIsPrintedBelowTheProjection()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "|--PropertyOrFieldNode 'Ints'",
                    "+--ProjectionNode '!{'",
                    "   +--OpMULTIPLY '*'",
                    "      |--Expression",
                    "      |  +--VariableNode 'this'",
                    "      +--Expression",
                    "         +--IntLiteralNode '2'",
                }),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Ints.!{#this * 2}")));
        }

        [Test]
        public void ACastPrintsItsTargetType()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "CastNode 'long'",
                    "+--Expression",
                    "   +--PropertyOrFieldNode 'Number'",
                }),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Number as long")));
        }

        [Test]
        public void ANullConditionalMemberIsMarkedAndAnOrdinaryOneIsNot()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "|--PropertyOrFieldNode 'Inner'",
                    "+--MethodNode 'Echo' ?.",
                    "   +--Expression",
                    "      +--PropertyOrFieldNode 'Number'",
                }),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Inner?.Echo(Number)")));

            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "|--PropertyOrFieldNode 'Inner'",
                    "+--MethodNode 'Echo'",
                    "   +--Expression",
                    "      +--PropertyOrFieldNode 'Number'",
                }),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Inner.Echo(Number)")));
        }

        /// <summary>
        /// An indexer's text is its opening token, so a null-conditional one shows <c>?[</c> twice: once as
        /// what was written, once as the flag the node runs by.
        /// </summary>
        [Test]
        public void ANullConditionalIndexerIsMarked()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "|--PropertyOrFieldNode 'Ints'",
                    "+--IndexerNode '?[' ?[",
                    "   +--Expression",
                    "      +--IntLiteralNode '0'",
                }),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Ints?[0]")));
        }

        [Test]
        public void TextTheGrammarMadeUpIsNotPrinted()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "+--ExpressionListNode",
                    "   |--Expression",
                    "   |  +--IntLiteralNode '1'",
                    "   +--Expression",
                    "      |--PropertyOrFieldNode 'Ints'",
                    "      +--MethodNode 'orderBy'",
                    "         +--Expression",
                    "            +--LambdaExpressionNode",
                    "               |--SpringAST",
                    "               |  |--SpringAST 'a'",
                    "               |  +--SpringAST 'b'",
                    "               +--OpSUBTRACT '-'",
                    "                  |--Expression",
                    "                  |  +--LocalVariableNode 'a'",
                    "                  +--Expression",
                    "                     +--LocalVariableNode 'b'",
                }),
                Expression.DescribeTree(Expression.Parse("(1; Ints.orderBy({|a,b| $a - $b}))")));

            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "+--MapInitializerNode '{'",
                    "   +--MapEntryNode",
                    "      |--Expression",
                    "      |  +--StringLiteralNode 'a'",
                    "      +--Expression",
                    "         +--IntLiteralNode '1'",
                }),
                Expression.DescribeTree(Expression.Parse("#{'a' : 1}")));
        }

        /// <summary>
        /// The made-up texts are recognised by the node that carries them, not by the text alone, so a
        /// caller's own name that happens to match one is still printed.
        /// </summary>
        [Test]
        public void ALocalNamedLikeAMadeUpTextKeepsItsName()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "+--ExpressionListNode",
                    "   |--LocalDeclarationNode 'expression'",
                    "   |  |--QualifiedIdentifier 'int'",
                    "   |  +--Expression",
                    "   |     +--IntLiteralNode '5'",
                    "   +--Expression",
                    "      +--LocalVariableNode 'expression'",
                }),
                Expression.DescribeTree(Expression.Parse("(int $expression = 5; $expression)")));
        }

        [Test]
        public void AControlCharacterInALiteralDoesNotBreakTheLine()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "+--StringLiteralNode 'a\\nb\\\\c\\'d'",
                }),
                Expression.DescribeTree(Expression.Parse("'a\nb\\c''d'")));

            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "+--StringLiteralNode 'a\\u0001b'",
                }),
                Expression.DescribeTree(Expression.Parse("'a\u0001b'")));
        }

        [Test]
        public void AnEmptyExpressionIsOneExpressionNode()
        {
            Assert.AreEqual("Expression", Expression.DescribeTree(Expression.Parse("")));
        }

        /// <summary>
        /// The node's own <c>ToString</c> carries a hash code, which is why it is not what gets printed.
        /// </summary>
        [Test]
        public void TwoParsesOfOneExpressionDescribeIdentically()
        {
            Assert.AreEqual(
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Inner?.Echo(Number) + 2")),
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Inner?.Echo(Number) + 2")));
        }

        [Test]
        public void TheWeakAndStrongPathsDescribeTheSameTree()
        {
            Assert.AreEqual(
                Expression.DescribeTree(Expression.ParseGetter<Holder, object>("Inner?.Echo(Number) + 2")),
                Expression.DescribeTree(Expression.Parse("Inner?.Echo(Number) + 2")));

            Assert.AreEqual(
                Expression.DescribeTreeAsXml(Expression.ParseGetter<Holder, object>("Inner?.Echo(Number) + 2")),
                Expression.DescribeTreeAsXml(Expression.Parse("Inner?.Echo(Number) + 2")));
        }

        [Test]
        public void ASetterAndAVoidExpressionAreDescribed()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Expression",
                    "+--PropertyOrFieldNode 'Number'",
                }),
                Expression.DescribeTree(Expression.ParseSetter<Holder, int>("Number")));

            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "AssignNode '='",
                    "|--Expression",
                    "|  +--PropertyOrFieldNode 'Number'",
                    "+--Expression",
                    "   +--IntLiteralNode '45'",
                }),
                Expression.DescribeTree(Expression.ParseVoidExpression<Holder>("Number = 45")));
        }

        /// <summary>A node built by hand has no text of its own, so its class is all there is to print.</summary>
        [Test]
        public void AHandBuiltNodeIsDescribed()
        {
            var add = new OpADD();
            add.addChild(new IntLiteralNode("20"));
            add.addChild(new IntLiteralNode("3"));

            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "OpADD",
                    "|--IntLiteralNode '20'",
                    "+--IntLiteralNode '3'",
                }),
                Expression.DescribeTree(Expression.Wrap(add)));
        }

        /// <summary>
        /// A node keeps its sibling link when it is taken out of a tree, and antlr's own printers walk it.
        /// What <c>Wrap</c> evaluates is the node and what is below it, so that is what is described.
        /// </summary>
        [Test]
        public void AWrappedChildIsDescribedWithoutItsSiblings()
        {
            var add = new OpADD();
            var left = new IntLiteralNode("20");
            add.addChild(left);
            add.addChild(new IntLiteralNode("3"));

            Assert.AreEqual("IntLiteralNode '20'", Expression.DescribeTree(Expression.Wrap(left)));
            Assert.AreEqual("<IntLiteralNode text=\"20\" />", Expression.DescribeTreeAsXml(Expression.Wrap(left)));
        }

        [Test]
        public void TheXmlIsOneElementPerNodeNamedAfterItsClass()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "<OpADD text=\"+\">",
                    "  <Expression>",
                    "    <PropertyOrFieldNode text=\"Inner\" />",
                    "    <MethodNode text=\"Echo\" nullConditional=\"true\">",
                    "      <Expression>",
                    "        <PropertyOrFieldNode text=\"Number\" />",
                    "      </Expression>",
                    "    </MethodNode>",
                    "  </Expression>",
                    "  <Expression>",
                    "    <IntLiteralNode text=\"2\" />",
                    "  </Expression>",
                    "</OpADD>",
                }),
                Expression.DescribeTreeAsXml(Expression.ParseGetter<Holder, object>("Inner?.Echo(Number) + 2")));
        }

        [Test]
        public void TheXmlIsWellFormed()
        {
            var document = new XmlDocument();
            document.LoadXml(
                Expression.DescribeTreeAsXml(Expression.Parse("(1; Ints.orderBy({|a,b| $a - $b}); 'a<b&\"c\"')")));

            Assert.AreEqual("Expression", document.DocumentElement.Name);
            Assert.AreEqual(
                "a<b&\"c\"",
                document.SelectSingleNode("//StringLiteralNode").Attributes["text"].Value);
        }

        [Test]
        public void ACharacterXmlCannotCarryIsWrittenAsAnEscape()
        {
            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "<Expression>",
                    "  <StringLiteralNode text=\"a\\u0001b\" />",
                    "</Expression>",
                }),
                Expression.DescribeTreeAsXml(Expression.Parse("'a\u0001b'")));
        }

        [Test]
        public void NullIsRefused()
        {
            Assert.Throws<ArgumentNullException>(
                () => Expression.DescribeTree((IStronglyTypedExpression) null));
            Assert.Throws<ArgumentNullException>(
                () => Expression.DescribeTree((IWeaklyTypedExpression) null));
            Assert.Throws<ArgumentNullException>(
                () => Expression.DescribeTreeAsXml((IStronglyTypedExpression) null));
            Assert.Throws<ArgumentNullException>(
                () => Expression.DescribeTreeAsXml((IWeaklyTypedExpression) null));
        }

        /// <summary>
        /// Both marker interfaces are public, so anyone may implement them. Nothing is known about such an
        /// instance - the same answer <c>GetCompilationStatus</c> gives.
        /// </summary>
        [Test]
        public void AForeignImplementationIsRefused()
        {
            Assert.Throws<ArgumentException>(
                () => Expression.DescribeTree(new NotOurStronglyTypedExpression()));
            Assert.Throws<ArgumentException>(
                () => Expression.DescribeTreeAsXml(new NotOurStronglyTypedExpression()));
            Assert.Throws<ArgumentException>(
                () => Expression.DescribeTree(new NotOurWeaklyTypedExpression()));
            Assert.Throws<ArgumentException>(
                () => Expression.DescribeTreeAsXml(new NotOurWeaklyTypedExpression()));
        }

        private sealed class NotOurStronglyTypedExpression : IGetterExpression<Holder, string>
        {
            public string GetValue(Holder context, IDictionary<string, object> variables = null)
                => null;
        }

        private sealed class NotOurWeaklyTypedExpression : IWeaklyTypedExpression
        {
            public object GetValue() => null;

            public object GetValue<TContext>(TContext context) => null;

            public object GetValue<TContext>(TContext context, IDictionary<string, object> variables) => null;

            public void SetValue<TContext, TValue>(TContext context, TValue newValue)
            { }

            public void SetValue<TContext, TValue>(
                TContext context, IDictionary<string, object> variables, TValue newValue)
            { }
        }
    }
}

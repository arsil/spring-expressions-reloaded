using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Pins the shape of the member-access chain: in Expression.g the 'node' rule lists DOT! as one
    /// alternative among repeated items, so the dot is suppressed punctuation rather than an operator,
    /// and member access is encoded as sibling position in a flat list under a single Expression node.
    ///
    /// Two consequences depend on this and are easy to break accidentally. There is no node representing
    /// '.', so anything wanting to modify member access - the null-conditional operators, for instance -
    /// has to attach itself to the node that follows rather than to a dot. And a C#-style prefix cast
    /// cannot be added, because "(int) x" is already a valid chain meaning "(int).x".
    /// </summary>
    [TestFixture]
    public class OptionalDotTests : BaseCompiledTests
    {
        [Test]
        public void DotIsOptionalBetweenChainLinks()
        {
            TestCompiledVsInterpreted<int>("'abc'.Length").ResultEqualsTo(3);
            TestCompiledVsInterpreted<int>("'abc' Length").ResultEqualsTo(3);
        }

        [Test]
        public void DotIsOptionalAfterAParenthesisedExpression()
        {
            // This is the case that makes a C#-style prefix cast undecidable: "(x) y" is already
            // a valid access chain meaning "(x).y".
            TestCompiledVsInterpreted<int>("('abc').Length").ResultEqualsTo(3);
            TestCompiledVsInterpreted<int>("('abc') Length").ResultEqualsTo(3);
        }

        [Test]
        public void DotIsOptionalAfterATypeLiteral()
        {
            // Likewise "T(int) x" is already property access on the type literal.
            TestCompiledVsInterpreted<string>("T(string).Name").ResultEqualsTo("String");
            TestCompiledVsInterpreted<string>("T(string) Name").ResultEqualsTo("String");
        }

        [Test]
        public void DotIsOptionalAcrossSeveralLinks()
        {
            TestCompiledVsInterpreted<int>("'abc'.Length.ToString().Length").ResultEqualsTo(1);
            TestCompiledVsInterpreted<int>("'abc' Length ToString() Length").ResultEqualsTo(1);
        }
    }
}

using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Pins the shape of the member-access chain: in Expression.g the 'node' rule lists DOT! as one
    /// alternative among repeated items, so the dot is suppressed punctuation rather than an operator,
    /// and member access is encoded as sibling position in a flat list under a single Expression node.
    ///
    /// This is not a curiosity - the design in _Docs/null-conditional-operator.md depends on it (a '?.'
    /// operator cannot "replace the dot node" because there is no dot node), and
    /// _Docs/cast-and-type-syntax.md depends on it too (a C#-style prefix cast is impossible because
    /// "(int) x" already parses as "(int).x").
    ///
    /// If a grammar change ever makes the dot mandatory, these tests fail and both documents need
    /// revisiting.
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

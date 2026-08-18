using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A root that counts how often the engine reads a member, so a test can pin how many times an operand
    /// was evaluated and not merely what it evaluated to.
    /// </summary>
    /// <remarks>
    /// The member names deliberately avoid the language's keywords: "true", "false", "and", "or", "xor",
    /// "null", "is", "in", "as", "between", "like" and "matches" are all lexer literals, so a property
    /// named True would be a parse ambiguity waiting to happen.
    /// </remarks>
    public class OperandCounter
    {
        /// <summary>How many times <see cref="Counted"/> has been read.</summary>
        public int RightOperandReads;

        public bool Yes { get { return true; } }
        public bool No { get { return false; } }

        public int Zero { get { return 0; } }
        public int One { get { return 1; } }

        public string NullText { get { return null; } }

        /// <summary>The operand under observation - reading it is the side effect the tests count.</summary>
        public bool Counted
        {
            get
            {
                RightOperandReads++;
                return true;
            }
        }
    }

    /// <summary>
    /// "and" and "or" are each a single operator serving two roles - logical for boolean operands, bitwise
    /// for integer and enum ones - because the language has no separate '&amp;&amp;' and '&amp;' pair. Which
    /// role applies is decided from the left operand, and only the logical role may short-circuit: the
    /// bitwise role needs both operands by definition.
    /// </summary>
    [TestFixture]
    public class LogicalOperatorTests : BaseCompiledTests
    {
        private static int InterpretedReadsOfRightOperand(string expression)
        {
            var root = new OperandCounter();
            InterpretGetter<OperandCounter, object>(expression).GetValue(root);
            return root.RightOperandReads;
        }

        private static int CompiledReadsOfRightOperand(string expression)
        {
            var root = new OperandCounter();
            CompileGetter<OperandCounter, object>(expression).GetValue(root);
            return root.RightOperandReads;
        }

        [Test]
        public void AndSkipsTheRightOperandWhenTheLeftIsFalse()
        {
            Assert.AreEqual(0, InterpretedReadsOfRightOperand("No and Counted"), "interpreted");
            Assert.AreEqual(0, CompiledReadsOfRightOperand("No and Counted"), "compiled");
        }

        [Test]
        public void OrSkipsTheRightOperandWhenTheLeftIsTrue()
        {
            Assert.AreEqual(0, InterpretedReadsOfRightOperand("Yes or Counted"), "interpreted");
            Assert.AreEqual(0, CompiledReadsOfRightOperand("Yes or Counted"), "compiled");
        }

        [Test]
        public void AndReadsTheRightOperandOnceWhenItIsNeeded()
        {
            Assert.AreEqual(1, InterpretedReadsOfRightOperand("Yes and Counted"), "interpreted");
            Assert.AreEqual(1, CompiledReadsOfRightOperand("Yes and Counted"), "compiled");
        }

        [Test]
        public void OrReadsTheRightOperandOnceWhenItIsNeeded()
        {
            Assert.AreEqual(1, InterpretedReadsOfRightOperand("No or Counted"), "interpreted");
            Assert.AreEqual(1, CompiledReadsOfRightOperand("No or Counted"), "compiled");
        }

        /// <summary>
        /// An integer left operand with a boolean right one: the bitwise role turns out not to apply, so the
        /// operator falls back to the logical one - and the right operand must still be read once, not once
        /// to find out its type and again to use it.
        /// </summary>
        /// <remarks>
        /// Interpreter only. The compiled path has no form for mixing an integer with a boolean and refuses
        /// the shape, so there is nothing to compare against here.
        /// </remarks>
        [Test]
        public void AndReadsTheRightOperandOnceWhenTheLeftIsAnInteger()
        {
            Assert.AreEqual(1, InterpretedReadsOfRightOperand("One and Counted"));
        }

        /// <summary>
        /// As above. The left operand has to be zero for the fall-through to be reached at all: a non-zero
        /// integer makes the logical "or" true and stops there.
        /// </summary>
        [Test]
        public void OrReadsTheRightOperandOnceWhenTheLeftIsAnInteger()
        {
            Assert.AreEqual(1, InterpretedReadsOfRightOperand("Zero or Counted"));
        }

        /// <summary>
        /// The shape from SPRNET-1381. Without short-circuiting the right operand dereferences null and
        /// throws, so the two backends agreeing is itself the assertion.
        /// </summary>
        [Test]
        public void AndShortCircuitKeepsTheRightOperandFromDereferencingNull()
        {
            TestCompiledVsInterpreted<OperandCounter, bool>(
                    "NullText != null and NullText.Length == 0", new OperandCounter())
                .ResultEqualsTo(false);
        }

        [Test]
        public void OrShortCircuitKeepsTheRightOperandFromDereferencingNull()
        {
            TestCompiledVsInterpreted<OperandCounter, bool>(
                    "NullText == null or NullText.Length == 0", new OperandCounter())
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// The bitwise role, which has to keep working: it is the same token, picked by operand type. A
        /// short-circuit here would be a defect, not a feature - both operands are needed to compute a
        /// bitwise result.
        /// </summary>
        [Test]
        public void IntegerOperandsStillTakeTheBitwiseRole()
        {
            TestCompiledVsInterpreted<OperandCounter, int>("One and 3", new OperandCounter())
                .ResultEqualsTo(1 & 3);
            TestCompiledVsInterpreted<OperandCounter, int>("One or 2", new OperandCounter())
                .ResultEqualsTo(1 | 2);
        }
    }
}

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

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
    /// Nullable integers for the operator-role tests: whether a value is present is a runtime fact,
    /// so the two backends must agree on what a null one means.
    /// </summary>
    public class NullableOperands
    {
        public int? NullInt { get { return null; } }
        public int? OneInt { get { return 1; } }
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

        /// <summary>
        /// The rule for null operands, per operand type family: in the integer/enum family a null is a
        /// lifted "unknown" and propagates - "null and 3" is null, as it is for +, -, and the rest of
        /// nullable math - while in the boolean family a null coerces to false, the way a filter reads
        /// it. One rule per family, identical across and, or and xor.
        /// </summary>
        /// <remarks>
        /// A null literal leaves the role undecidable at compile time, so the compiled path refuses the
        /// shape - with the CompileErrorException the weakly typed path's fallback can see - and the
        /// interpreter evaluates it.
        /// </remarks>
        [Test]
        public void NullOperandsAreRefusedCompiledButStillEvaluate()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<object>(
                    "null and 3", CompileOptions.CompileOnParse | CompileOptions.MustCompile));
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<object>(
                    "null or 3", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null and 3"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "3 and null"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null or 3"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null xor 3"));

            Assert.AreEqual(false, ExpressionEvaluator.GetValue(null, "null and true"));
            Assert.AreEqual(false, ExpressionEvaluator.GetValue(null, "true and null"));
            Assert.AreEqual(false, ExpressionEvaluator.GetValue(null, "null and false"));
            Assert.AreEqual(true, ExpressionEvaluator.GetValue(null, "null or true"));
            Assert.AreEqual(false, ExpressionEvaluator.GetValue(null, "null or false"));
            Assert.AreEqual(true, ExpressionEvaluator.GetValue(null, "null xor true"));
            Assert.AreEqual(false, ExpressionEvaluator.GetValue(null, "null and null"));
            Assert.AreEqual(false, ExpressionEvaluator.GetValue(null, "null or null"));
        }

        /// <summary>
        /// The lifted rule holds for nullable-integer operands across all three operators, on both
        /// backends: null propagates, values compute bitwise. or and xor used to lack the nullable
        /// handling and coerced a null to a boolean - measured as a compiled-vs-interpreted divergence,
        /// since the compiled path has always lifted declared int? operands.
        /// </summary>
        [Test]
        public void NullableIntegerOperandsLiftAcrossAllThreeOperators()
        {
            var holder = new NullableOperands();

            TestCompiledVsInterpreted<NullableOperands, object>("NullInt and 3", holder).ResultEqualsTo(null);
            TestCompiledVsInterpreted<NullableOperands, object>("NullInt or 3", holder).ResultEqualsTo(null);
            TestCompiledVsInterpreted<NullableOperands, object>("NullInt xor 3", holder).ResultEqualsTo(null);

            TestCompiledVsInterpreted<NullableOperands, object>("OneInt and 3", holder).ResultEqualsTo(1 & 3);
            TestCompiledVsInterpreted<NullableOperands, object>("OneInt or 2", holder).ResultEqualsTo(1 | 2);
            TestCompiledVsInterpreted<NullableOperands, object>("OneInt xor 3", holder).ResultEqualsTo(1 ^ 3);
        }

        /// <summary>
        /// A null logical result dissolves the moment it meets a boolean context: not, the conditional
        /// operator, and the boolean family of and/or all coerce it to false rather than throwing.
        /// </summary>
        [Test]
        public void NullLogicalResultsCoerceInBooleanContexts()
        {
            var holder = new NullableOperands();

            Assert.AreEqual(true, ExpressionEvaluator.GetValue(holder, "!(NullInt and 3)"));
            Assert.AreEqual("no", ExpressionEvaluator.GetValue(holder, "(NullInt and 3) ? 'yes' : 'no'"));
            Assert.AreEqual(false, ExpressionEvaluator.GetValue(holder, "(NullInt and 3) and true"));
            Assert.AreEqual(true, ExpressionEvaluator.GetValue(holder, "(NullInt and 3) or true"));
        }
    }
}

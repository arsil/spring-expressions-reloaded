using System;

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
        /// An integer meeting a boolean is refused on both backends, and the refusal happens without
        /// reading the right operand twice.
        /// </summary>
        /// <remarks>
        /// These two used to assert that the shape *worked* - "One and Counted" answered true, by
        /// coercing the 1 - which was the truthiness the engine has since ruled out: a non-boolean is
        /// not a truth value, anywhere. The compiled path always refused the shape; now the interpreter
        /// does too, and what is left worth pinning is that the operand-reading discipline holds on the
        /// failing path as well. Do not restore the old assertions without reversing that ruling.
        /// </remarks>
        [Test]
        public void AndRefusesAnIntegerLeftOperandAgainstABoolean()
        {
            var holder = new OperandCounter();

            Assert.Throws<ArgumentException>(
                () => ExpressionEvaluator.GetValue(holder, "One and Counted"));

            Assert.AreEqual(1, holder.RightOperandReads, "the right operand is read once, not twice");
        }

        [Test]
        public void OrRefusesAnIntegerLeftOperandAgainstABoolean()
        {
            var holder = new OperandCounter();

            Assert.Throws<ArgumentException>(
                () => ExpressionEvaluator.GetValue(holder, "Zero or Counted"));

            Assert.AreEqual(1, holder.RightOperandReads, "the right operand is read once, not twice");
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
                    "null and 3", EvaluationMode.MustCompile));
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<object>(
                    "null or 3", EvaluationMode.MustCompile));

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

            // Two nothings are nothing, not false. These two used to answer false, which was the
            // boolean family's rule applied to a pair that does not name a family at all - the same
            // guess the compiled path was criticised for making the other way, since it read the
            // declared types, took the bitwise role and lifted to null. 'NoNumber and NoNumber' is the
            // shape that showed it, and one answer has to serve both.
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null and null"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null or null"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null xor null"));
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


using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public enum Colours { Red, Green }

    public class LogicalNullRoot
    {
        public int? NoNumber { get; set; }
        public int? SomeNumber { get; set; } = 6;
        public int Number { get; set; } = 3;
        public bool Flag { get; set; } = true;
        public Colours Colour { get; set; } = Colours.Red;
        public object Anything { get; set; } = 45;
    }

    /// <summary>
    /// <c>and</c>, <c>or</c> and <c>xor</c> each serve two roles - logical for booleans, bitwise for
    /// integers and enums - and the operand types pick which. Two nulls pick neither, so they answer
    /// nothing.
    /// </summary>
    /// <remarks>
    /// <p>
    /// This is the same problem <c>+</c> has, and the same answer. With two nulls the interpreter cannot
    /// tell which role applies - the operands could have been integers or booleans and both arrive as a
    /// bare null - so it used to fall into the logical branch and coerce them to <c>false</c>, while the
    /// compiled path read the declared types, took the bitwise role and lifted to null.
    /// <c>NoNumber and NoNumber</c> was <c>null</c> compiled against <c>False</c> interpreted, 18 rows
    /// of <c>EvaluationNeverDivergesTests</c> across the three operators.
    /// </p>
    /// <p>
    /// <b>The old answer was a guess in the same way the compiled path's was</b>, just in the other
    /// direction: <c>false</c> is the logical role's rule applied to a pair that names no role at all.
    /// Nothing is what the rest of arithmetic answers, and it is what the compiled path already said.
    /// </p>
    /// <p>
    /// A pair that <i>does</i> name a role is untouched, which is nearly every use - one operand
    /// carrying its type is enough.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class LogicalNullOperandTests : BaseCompiledTests
    {
        [Test]
        public void TwoNothingsAnswerNothingInAllThreeOperators()
        {
            var root = new LogicalNullRoot();

            foreach (var expression in new[]
                { "NoNumber and NoNumber", "NoNumber or NoNumber", "NoNumber xor NoNumber" })
            {
                Assert.IsNull(CompileGetter<LogicalNullRoot, object>(expression).GetValue(root), expression);
                Assert.IsNull(InterpretGetter<LogicalNullRoot, object>(expression).GetValue(root), expression);
            }

            // two null literals leave the role undecidable too, so they answer the same. The compiled
            // path has no form for them - it cannot pick a role either - and the interpreter serves it.
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null and null"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null or null"));
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "null xor null"));
        }

        /// <summary>
        /// One operand carrying a type names the role, and those were right all along: an integer beside
        /// a nothing lifts and propagates, a boolean beside a nothing coerces it to false. Both rules
        /// stand - the change was only for the pair that names neither.
        /// </summary>
        [Test]
        public void OneTypedOperandStillNamesTheRole()
        {
            var root = new LogicalNullRoot();

            // the bitwise role: nothing propagates
            Assert.IsNull(CompileGetter<LogicalNullRoot, object>("NoNumber and Number").GetValue(root));
            Assert.IsNull(InterpretGetter<LogicalNullRoot, object>("NoNumber and Number").GetValue(root));

            // the logical role: nothing reads as false
            Assert.AreEqual(false, ExpressionEvaluator.GetValue(null, "null and true"));
            Assert.AreEqual(true, ExpressionEvaluator.GetValue(null, "null or true"));
        }

        /// <summary>
        /// Two present operands are untouched, in either role.
        /// </summary>
        [Test]
        public void TwoPresentOperandsAreUnchanged()
        {
            var root = new LogicalNullRoot();

            TestCompiledVsInterpreted<LogicalNullRoot, object>("SomeNumber and Number", root)
                .ResultEqualsTo(2);
            TestCompiledVsInterpreted<LogicalNullRoot, object>("Number and Number", root)
                .ResultEqualsTo(3);
            TestCompiledVsInterpreted<LogicalNullRoot, object>("Flag and Flag", root)
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// An enum against an <c>object</c> is refused rather than guessed, which is open-issues item
        /// 21's rule reaching a third operator family.
        /// </summary>
        /// <remarks>
        /// The compiled path used to cast the object to the enum and combine. The CLR permits that for a
        /// boxed value of the enum's underlying type, so <c>Colour and Anything</c> with an <c>int</c> in
        /// the box answered an enum, while the interpreter looked at the runtime value, found the types
        /// unrelated and refused the pair. Which role applies is exactly what the static types do not
        /// determine here, so the compiled path stands aside and the interpreter answers from the value.
        /// </remarks>
        [Test]
        public void AnEnumAgainstAnUntypedOperandIsRefused()
        {
            var root = new LogicalNullRoot();

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<LogicalNullRoot, object>(
                    "Colour and Anything", EvaluationMode.MustCompile));

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<LogicalNullRoot, object>("Colour and Anything").GetValue(root));

            // the same enum on both sides still combines
            TestCompiledVsInterpreted<LogicalNullRoot, object>("Colour and Colour", root)
                .ResultEqualsTo(Colours.Red);
        }
    }
}

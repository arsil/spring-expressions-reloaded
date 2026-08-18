using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class Fallback
    {
        public virtual string Origin { get { return "Fallback"; } }
    }

    public class DerivedFallback : Fallback
    {
        public override string Origin { get { return "DerivedFallback"; } }
    }

    public class UnrelatedFallback
    {
    }

    /// <summary>
    /// Holds stable instances: the two backends are compared by value, so a property handing back a fresh
    /// object each call would fail on reference equality rather than on anything being wrong.
    /// </summary>
    public class FallbackHolder
    {
        public FallbackHolder()
        {
            Present = new Fallback();
            Derived = new DerivedFallback();
            Unrelated = new UnrelatedFallback();
        }

        public Fallback Missing { get { return null; } }
        public Fallback Present { get; private set; }
        public DerivedFallback Derived { get; private set; }
        public UnrelatedFallback Unrelated { get; private set; }

        public int? MissingNumber { get { return null; } }
        public int? PresentNumber { get { return 7; } }
        public int OtherNumber { get { return 9; } }
    }

    /// <summary>
    /// The '??' operator, where the two operands need not have the same type.
    /// </summary>
    /// <remarks>
    /// The emitted conditional is the constraint: <c>LExpression.Condition</c> requires its two branches to be
    /// of equivalent type and will not find a common base, so it rejects <c>Fallback</c> against
    /// <c>DerivedFallback</c> despite the inheritance - and rejects two siblings, and <c>int</c> against
    /// <c>long</c>. The narrower side has to be widened explicitly, which is what C# does for '??'. These pin
    /// that the widening happens, and that both backends agree afterwards.
    /// </remarks>
    [TestFixture]
    public class DefaultOperatorTests : BaseCompiledTests
    {
        [Test]
        public void RightOperandMayBeDerivedFromTheLeft()
        {
            var holder = new FallbackHolder();

            // Missing is Fallback, Derived is DerivedFallback - different types, one assignable to the other.
            var result = TestCompiledVsInterpreted<FallbackHolder, Fallback>("Missing ?? Derived", holder);

            Assert.AreSame(holder.Derived, result.Result);
        }

        [Test]
        public void LeftOperandMayBeDerivedFromTheRight()
        {
            var holder = new FallbackHolder();

            var result = TestCompiledVsInterpreted<FallbackHolder, Fallback>("Derived ?? Present", holder);

            Assert.AreSame(holder.Derived, result.Result);
        }

        [Test]
        public void SameTypeOnBothSidesStillWorks()
        {
            var holder = new FallbackHolder();

            var result = TestCompiledVsInterpreted<FallbackHolder, Fallback>("Missing ?? Present", holder);

            Assert.AreSame(holder.Present, result.Result);
        }

        /// <summary>
        /// A nullable left operand takes the branch through HasValue/Value, which has the same constraint.
        /// </summary>
        [Test]
        public void NullableLeftOperandOfTheSameUnderlyingType()
        {
            var holder = new FallbackHolder();

            TestCompiledVsInterpreted<FallbackHolder, int>("MissingNumber ?? OtherNumber", holder)
                .ResultEqualsTo(9);
            TestCompiledVsInterpreted<FallbackHolder, int>("PresentNumber ?? OtherNumber", holder)
                .ResultEqualsTo(7);
        }

        /// <summary>
        /// Neither operand type converts to the other, which C# rejects for '??' as well. The compiled form is
        /// refused - and because the refusal is a <see cref="CompileErrorException"/> rather than an
        /// ArgumentException out of the expression tree, the weakly typed path falls back to the interpreter
        /// instead of failing outright.
        /// </summary>
        [Test]
        public void UnrelatedOperandTypesHaveNoCompiledFormButStillEvaluate()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<FallbackHolder, object>(
                    "Missing ?? Unrelated", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

            var holder = new FallbackHolder();
            IExpression weak = Expression.Parse("Missing ?? Unrelated");

            Assert.AreSame(holder.Unrelated, weak.GetValue(holder));
        }
    }
}

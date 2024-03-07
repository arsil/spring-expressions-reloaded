using System;

using JetBrains.Annotations;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
        // todo: error: public?
    internal class BinaryNumericPromotionException : CompileErrorException
    {
        public BinaryNumericPromotionException([NotNull] Type left, [NotNull] Type right)
            : base(
                "Binary numeric promotion rules violation: " +
               $"Cannot apply operator to operands of type '{left}' and '{right}'.")
        {
            Left = left;
            Right = right;
        }

        [NotNull]
        public Type Left { get; }

        [NotNull]
        public Type Right { get; }
    }
}

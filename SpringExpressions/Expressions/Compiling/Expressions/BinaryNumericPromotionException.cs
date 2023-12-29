using System;

using JetBrains.Annotations;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    internal class BinaryNumericPromotionException : Exception
    {
        public BinaryNumericPromotionException([NotNull] Type left, [NotNull] Type right)
            : base($"Binary numeric promotion rules violation: '{left}'" +
                $" and/or '{right}' are not one of the supported numeric types.")
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

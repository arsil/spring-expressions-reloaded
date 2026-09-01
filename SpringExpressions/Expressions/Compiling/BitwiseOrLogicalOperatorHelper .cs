using SpringUtil;
using System;
using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;
using LBinaryExpression = System.Linq.Expressions.BinaryExpression;


namespace SpringExpressions.Expressions.Compiling
{
    static class BitwiseOrLogicalOperatorHelper
    {
        [CanBeNull]
        public static LExpression CreateAndExpression(
            [NotNull] LExpression left,
            [NotNull] LExpression right)
        {
            return CreateExpression(
                left: left, 
                right: right, 
                logicalOperatorCreator: LExpression.AndAlso, 
                bitwiseOperatorCreator: LExpression.And);
        }

        [CanBeNull]
        public static LExpression CreateOrExpression(
            [NotNull] LExpression left,
            [NotNull] LExpression right)
        {
            return CreateExpression(
                left: left,
                right: right,
                logicalOperatorCreator: LExpression.OrElse,
                bitwiseOperatorCreator: LExpression.Or);
        }

        [CanBeNull]
        public static LExpression CreateXorExpression(
            [NotNull] LExpression left,
            [NotNull] LExpression right)
        {
            return CreateExpression(
                left: left,
                right: right,
                logicalOperatorCreator: LExpression.ExclusiveOr,
                bitwiseOperatorCreator: LExpression.ExclusiveOr);
        }

        [CanBeNull]
        public static LExpression CreateExpression(
            [NotNull] LExpression left, 
            [NotNull] LExpression right,
            [NotNull] Func<LExpression, LExpression, LBinaryExpression> logicalOperatorCreator,
            [NotNull] Func<LExpression, LExpression, LBinaryExpression> bitwiseOperatorCreator)
        {
            if (left.Type == typeof(bool) && right.Type == typeof(bool))
            {
                // logical operator
                return logicalOperatorCreator(left, right);
            }

            if (left.Type.IsEnum)
            {
                var enumType = left.Type;
                var enumUnderlyingType = Enum.GetUnderlyingType(enumType);

                if (right.Type == enumType)
                {
                    return LExpression.Convert(
                        bitwiseOperatorCreator(
                            LExpression.Convert(left, enumUnderlyingType),
                            LExpression.Convert(right, enumUnderlyingType)),
                        left.Type);
                }

                // There is deliberately no branch for an object-typed right operand. It used to cast the
                // object to the enum and combine - which the CLR allows for a boxed value of the enum's
                // underlying type, so 'Colour and Anything' with an int 45 in the box answered an enum -
                // while the interpreter looked at the runtime value, found the types unrelated and
                // refused the pair. That is the shape open-issues item 21 ruled on for the comparison
                // operators: a decision the static types do not determine is refused, not guessed, so
                // the interpreter can answer from the value. Falling through to the refusal below is
                // what agrees with it.
            }

            if (ExpressionTypeHelper.IsIntegerOrNullableIntegerExpression(left, out _, out _)
                && ExpressionTypeHelper.IsIntegerOrNullableIntegerExpression(right, out _, out _))
            {
                // bitwise AND for integer types
                if (BinaryNumericOperatorHelper.TryCreate(
                    left,
                    right,
                    bitwiseOperatorCreator, out var resultExpression))
                {
                    return resultExpression;
                }
            }

            // Anything else - an object-typed operand (a null literal, say), a string, mixed
            // integer-and-bool shapes - has no compiled form. Null is the "cannot compile" signal, so
            // the weakly typed path falls back to the interpreter, which evaluates several of these
            // shapes; an ArgumentException here escaped that fallback and made them hard failures.
            return null;
        }
    }
}

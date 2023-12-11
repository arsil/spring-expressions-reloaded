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

                if (right.Type == typeof(object))
                {
                    // trying to convert object to enum
                    return LExpression.Convert(
                        bitwiseOperatorCreator(
                            LExpression.Convert(left, enumUnderlyingType),
                            LExpression.Convert(LExpression.Convert(right, enumType), enumUnderlyingType)),
                        enumType);
                }
            }

            if (ExpressionTypeHelper.IsIntegerExpression(left)
                && ExpressionTypeHelper.IsIntegerExpression(right))
            {
                // bitwise AND for integer types
                return NumericalOperatorHelper.Create(
                    left,
                    right,
                    bitwiseOperatorCreator);
            }

            return null;
        }
    }
}

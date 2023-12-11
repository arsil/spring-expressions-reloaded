using JetBrains.Annotations;
using System;

using LExpression = System.Linq.Expressions.Expression;
using LBinaryExpression = System.Linq.Expressions.BinaryExpression;

namespace SpringExpressions.Expressions.Compiling
{
    internal class NumericalOperatorHelper
    {
        [CanBeNull]
        public static LBinaryExpression Create(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] Func<
                LExpression,
                LExpression,
                LBinaryExpression> binaryFunctionCreator)
        {
            var leftExpressionType = left.Type;
            var rightExpressionType = right.Type;

            var leftTypeCode = (int)Type.GetTypeCode(leftExpressionType);

            // For Char, Boolean, DBNull, Object, Empty, DateTime and String
            if (leftTypeCode < 5 || leftTypeCode > 15 || leftExpressionType.IsEnum)
                return null;

            // TODO: konwersja user-typów
            // TODO: przetestować dziwne rzutowania... np z double na decimal

            if (leftExpressionType != rightExpressionType)
            {
                // types are different
                var rightTypeCode = (int)Type.GetTypeCode(rightExpressionType);

                // For Char, Boolean, DBNull, Object, Empty, DateTime and String
                if (rightTypeCode < 5 || rightTypeCode > 15)
                    return null;

                //   5 - sByte
                //   6 - Byte
                //   7 - Int16
                //   8 - UInt16
                //   9 - Int32
                //  10 - UInt32
                //  11 - Int64
                //  12 - UInt64
                //  13 - Single
                //  14 - Double
                //  15 - Decimal

                if (leftTypeCode > rightTypeCode)
                {
                    // left has bigger precision
                    right = LExpression.Convert(right, leftExpressionType);
                }
                else
                {
                    left = LExpression.Convert(left, rightExpressionType);
                }
            }

            return binaryFunctionCreator(left, right);
        }
    }
}

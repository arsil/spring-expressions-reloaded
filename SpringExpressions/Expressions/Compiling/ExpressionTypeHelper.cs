using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;



namespace SpringExpressions.Expressions.Compiling
{
    internal static class ExpressionTypeHelper
    {
        public static bool IsNumericExpression([NotNull] LExpression expression)
        {
            //   0 - A null reference.
            //   1 - Object
            //   2 - DBNull
            //   3 - Boolean
            //   4 - Char

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

            //  16 - DateTime	
            //  18 - String

            // todo: error!
            // For Enum types, the type code of the underlying integral type is returned.
            var code = (int)System.Type.GetTypeCode(expression.Type);
            return (code >= 5 && code <= 15);
        }

        public static bool IsIntegerExpression([NotNull] LExpression expression)
        {
            //   0 - A null reference.
            //   1 - Object
            //   2 - DBNull
            //   3 - Boolean
            //   4 - Char

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
            //  16 - DateTime	
            //  18 - String

            // todo: error!
            // For Enum types, the type code of the underlying integral type is returned.

            var code = (int)System.Type.GetTypeCode(expression.Type);
            return (code >= 5 && code <= 12);
        }
    }
}

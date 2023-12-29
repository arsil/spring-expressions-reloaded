using System;
using JetBrains.Annotations;
using SpringExpressions.Expressions.LinqExpressionHelpers;

using LExpression = System.Linq.Expressions.Expression;
using LUnaryExpression = System.Linq.Expressions.UnaryExpression;

namespace SpringExpressions.Expressions.Compiling
{
    internal static class UnaryNumericOperatorHelper
    {
        public enum UnaryOperator
        {
            UnaryPlus,
            UnaryMinus,
            UnaryNot
        }

        [ContractAnnotation(
            "=>true,resultExpression:notnull;=>false,resultExpression:null")]
        public static bool TryCreate(
            [NotNull] LExpression argument,
            UnaryOperator unaryOperator,
            out LUnaryExpression resultExpression)
        {
            if (argument.Type.IsEnum)
            {
                resultExpression = null;
                return false;
            }

            var argIsNullable = false;
            var argTypeCode = (int)Type.GetTypeCode(argument.Type);

            var argIsNumber = argTypeCode >= 5 && argTypeCode <= 15;
            if (!argIsNumber && argTypeCode == 1)
            {
                argIsNumber = argIsNullable = MethodBaseHelpers.IsNullableType(argument.Type, ref argTypeCode);
            }

            if (!argIsNumber)
            {
                resultExpression = null;
                return false;
            }

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

            if (argTypeCode >= 5 && argTypeCode <= 8)
            {
                /*
                    Unary numeric promotion simply consists of converting operands of type
                    sbyte, byte, short, ushort, or char to type int.
                */
                argument = LExpression.Convert(argument, !argIsNullable ? typeof(int) : typeof(int?));
            }
            else if (unaryOperator == UnaryOperator.UnaryMinus && argTypeCode == 10)
            {
                /*
                    Additionally, for the unary '–' operator, unary numeric promotion 
                    converts operands of type uint to type long.
                */
                argument = LExpression.Convert(argument, !argIsNullable ? typeof(long) : typeof(long?));
            }

            switch (unaryOperator)
            {
                case UnaryOperator.UnaryPlus: 
                    resultExpression = LExpression.UnaryPlus(argument);
                    return true;

                case UnaryOperator.UnaryMinus:
                    if (argTypeCode == 12)
                        throw new ArgumentException("Operator '-' cannot be applied to operand of type 'ulong'");

                    resultExpression = LExpression.Negate(argument);
                    return true;

                case UnaryOperator.UnaryNot:
                    resultExpression = LExpression.Not(argument);
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(unaryOperator), unaryOperator, null);
            }
        }
    }
}

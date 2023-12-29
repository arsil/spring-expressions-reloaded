using JetBrains.Annotations;
using System;

using SpringExpressions.Expressions.LinqExpressionHelpers;

using LExpression = System.Linq.Expressions.Expression;
using LBinaryExpression = System.Linq.Expressions.BinaryExpression;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressions.Expressions.Compiling
{
    internal static class BinaryNumericOperatorHelper
    {
        /*
        [ContractAnnotation(
            "=>true,resultExpression:notnull;=>false,resultExpression:null")]
        public static bool TryCreateForComparison(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] Func<LExpression, LExpression, LBinaryExpression> binaryFunctionCreator,
            out LExpression resultExpression)
        {
            var leftExpressionType = left.Type;
            var rightExpressionType = right.Type;

            var leftIsNullable = MethodBaseHelpers.IsNullableType(leftExpressionType, out var nullableLeftItemType);
            var rightIsNullable = MethodBaseHelpers.IsNullableType(rightExpressionType, out var nullableRightItemType);

            if (!leftIsNullable && !rightIsNullable)
            {
                Create(left, right, binaryFunctionCreator, out var resultExpressionTmp);
                resultExpression = resultExpressionTmp;
                return resultExpression != null;
            }

            if (leftIsNullable && !rightIsNullable)
            {
                var propHasValue = leftExpressionType.GetProperty("HasValue");
                var propValue = leftExpressionType.GetProperty("Value");

                if (Create(
                        LExpression.Property(left, propValue), 
                        right, 
                        binaryFunctionCreator, 
                        out var exprTmp))
                {
                    resultExpression
                        = LExpression.Condition(
                            LExpression.Property(left, propHasValue),
                            exprTmp,
                            LExpression.Constant(false, typeof(bool)));

                    return true;

                }
            }
            else if (!leftIsNullable && rightIsNullable)
            {
                var propHasValue = rightExpressionType.GetProperty("HasValue");
                var propValue = rightExpressionType.GetProperty("Value");

                if (Create(
                        left,
                        LExpression.Property(right, propValue),
                        binaryFunctionCreator,
                        out var exprTmp))
                {
                    resultExpression
                        = LExpression.Condition(
                            LExpression.Property(right, propHasValue),
                            exprTmp,
                            LExpression.Constant(false, typeof(bool)));

                    return true;

                }
            }

            // both are nullable.
            resultExpression = null;
            return false;
        }
        */

        [ContractAnnotation(
            "=>true,resultExpression:notnull;=>false,resultExpression:null")]
        public static bool TryCreate(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] Func<LExpression, LExpression, LBinaryExpression> binaryFunctionCreator,
            out LBinaryExpression resultExpression)
        {
            resultExpression = null;

            var leftExpressionType = left.Type;
            var rightExpressionType = right.Type;

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

            var leftTypeCode = (int)Type.GetTypeCode(leftExpressionType);

            var leftIsNullable = false;
            var leftIsNumber = leftTypeCode >= 5 && leftTypeCode <= 15 && !leftExpressionType.IsEnum;

            if (!leftIsNumber && leftTypeCode == 1)
            {
                leftIsNumber = leftIsNullable = MethodBaseHelpers.IsNullableType(leftExpressionType, ref leftTypeCode);
            }

            //leftIsNumber |= leftTypeCode == 1
            //    && MethodBaseHelpers.IsNullableType(leftExpressionType, ref leftTypeCode);

            if (leftIsNumber)
            {
                // todo: error: nullable types!!!!!! proper handling!--------

                // TODO: konwersja user-typów
                // TODO: przetestować dziwne rzutowania... np z double na decimal

                // todo: error głupia optymalizacja...
                //if (leftExpressionType != rightExpressionType)
                {
                    // types are different
                    var rightTypeCode = (int)Type.GetTypeCode(rightExpressionType);
                    var rightIsNumber = rightTypeCode >= 5 && rightTypeCode <= 15 && !rightExpressionType.IsEnum;
                    var rightIsNullable = false;

                    if (!rightIsNumber && rightTypeCode == 1)
                    {
                        rightIsNumber = rightIsNullable = MethodBaseHelpers.IsNullableType(rightExpressionType, ref rightTypeCode);
                    }

                    if (rightIsNumber)
                    {
                        //if (leftTypeCode != rightTypeCode)
                        //{
                            var promotedType = NumericPromotionTable[leftTypeCode, rightTypeCode];
                            if (promotedType == null)
                            {
                                throw new BinaryNumericPromotionException(
                                    left: leftExpressionType, 
                                    right: rightExpressionType);
                            }


//                            right = LExpression.Convert(right, promotedType);
//                            left = LExpression.Convert(left, promotedType);
                        //}

                        if (!leftIsNullable && !rightIsNullable)
                        {
                            right = LExpression.Convert(right, promotedType);
                            left = LExpression.Convert(left, promotedType);
                        }
                        else
                        {
                            left = LExpression.Convert(left, typeof(Nullable<>).MakeGenericType(promotedType));
                            right = LExpression.Convert(right, typeof(Nullable<>).MakeGenericType(promotedType));
                            /*
                            left = !leftIsNullable
                                ? LExpression.Convert(left, promotedType)
                                : LExpression.Convert(left, typeof(Nullable<>).MakeGenericType(promotedType));

                            right = !rightIsNullable
                                ? LExpression.Convert(right, promotedType)
                                : LExpression.Convert(right, typeof(Nullable<>).MakeGenericType(promotedType));*/
                        }
                        // todo: error: bzdura! to nie zadziała!!!! dla nullable!--------
                        /*
                        if (leftTypeCode > rightTypeCode)
                        {
                            // left has bigger precision
                            right = LExpression.Convert(right, leftExpressionType);
                        }
                        else
                        {
                            left = LExpression.Convert(left, rightExpressionType);
                        }*/
                    }
                }

                resultExpression = binaryFunctionCreator(left, right);
                return true;
            }

            /*
            bool leftIsNullable = leftTypeCode == 1
                && MethodBaseHelpers.IsNullableType(leftExpressionType, out var leftNullableItemType);


            bool rightIsNullable = rightTypeCode == 1
                && MethodBaseHelpers.IsNullableType(rightExpressionType, out var rightNullableItemType);
            */

            return false;
        }

        private static Type PromoteNumericType(TypeCode left, TypeCode right)
        {
            // If either operand is of type decimal, the other operand is converted to type decimal,
            // or a binding-time error occurs if the other operand is of type float or double.
            if (left == TypeCode.Decimal || right == TypeCode.Decimal)
            {
                if (left == TypeCode.Single || left == TypeCode.Double
                    || right == TypeCode.Single || right == TypeCode.Double)
                {
                    // Invalid
                    return null;
                }

                return typeof(decimal);
            }

            // Otherwise, if either operand is of type double, the other operand is converted to type double.
            if (left == TypeCode.Double || right == TypeCode.Double)
            {
                return typeof(double);
            }

            // Otherwise, if either operand is of type float, the other operand is converted to type float.
            if (left == TypeCode.Single || right == TypeCode.Single)
            {
                return typeof(float);
            }

            // Otherwise, if either operand is of type ulong, the other operand is converted to type ulong,
            // or a binding-time error occurs if the other operand is of type sbyte, short, int, or long.
            if (left == TypeCode.UInt64 || right == TypeCode.UInt64)
            {
                if (left == TypeCode.SByte || left == TypeCode.Int16
                    || left == TypeCode.Int32 || left == TypeCode.Int64)
                {
                    // Invalid
                    return null;
                }

                if (right == TypeCode.SByte || right == TypeCode.Int16
                    || right == TypeCode.Int32 || right == TypeCode.Int64)
                {
                    // Invalid
                    return null;
                }


                return typeof(ulong);
            }

            // Otherwise, if either operand is of type long, the other operand is converted to type long.
            if (left == TypeCode.Int64 || right == TypeCode.Int64)
            {
                return typeof(long);
            }

            // Otherwise, if either operand is of type uint and the other operand is of type
            // sbyte, short, or int, both operands are converted to type long.
            // Otherwise, if either operand is of type uint, the other operand is converted to type uint.
            if (left == TypeCode.UInt32 || right == TypeCode.UInt32)
            {
                if (left == TypeCode.SByte || left == TypeCode.Int16 || left == TypeCode.Int32)
                    return typeof(long);

                if (right == TypeCode.SByte || right == TypeCode.Int16 || right == TypeCode.Int32)
                    return typeof(long);

                return typeof(uint);
            }

            // Otherwise, both operands are converted to type int.
            return typeof(int);
        }

        static BinaryNumericOperatorHelper()
        {
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

            NumericPromotionTable = new Type[32, 32];

            for (var i = TypeCode.SByte; i <= TypeCode.Decimal; ++i)
            {
                for (var j = TypeCode.SByte; j <= TypeCode.Decimal; ++j)
                {
                    NumericPromotionTable[(int)i, (int)j] = PromoteNumericType(i, j);
                }
            }
        }

        [NotNull, ItemCanBeNull]
        private static readonly Type[,] NumericPromotionTable;
    }
}

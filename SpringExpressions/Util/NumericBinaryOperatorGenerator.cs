using SpringExpressions.Expressions.Compiling;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    internal class NumericBinaryOperatorGenerator
    {
        [NotNull]
        public static Func<object, object, object>[,]
            CreateFunctionTable(
                [NotNull] Func<LExpression, LExpression, BinaryExpression> binaryFunction)
        {
            var result = new Func<object, object, object>[32, 32];

            for (var i = TypeCode.SByte; i <= TypeCode.Decimal; ++i)
            {
                for (var j = TypeCode.SByte; j <= TypeCode.Decimal; ++j)
                {
                    try
                    {
                        result[(int)i, (int)j] = CreateFunction(i, j, binaryFunction);
                    }
                    catch (Exception e)
                    {
                        result[(int)i, (int)j] = null;
                    }
                }

            }

            return result;
        }

        [NotNull]
        public static Func<object, object, int>[,] CreateCompareFunctionTable()
        {
            var result = new Func<object, object, int>[32, 32];

            for (var i = TypeCode.SByte; i <= TypeCode.Decimal; ++i)
            {
                for (var j = TypeCode.SByte; j <= TypeCode.Decimal; ++j)
                {
                    result[(int)i, (int)j] = CreateCompareFunction(i, j);
                }
            }

            return result;
        }

        [CanBeNull]
        private static Func<object, object, int> CreateCompareFunction(TypeCode left, TypeCode right)
        {
            // The same promotion the arithmetic cells run on: null where the pair is refused.
            var promotedType = BinaryNumericOperatorHelper.GetPromotedTypeOrNull(left, right);
            if (promotedType == null)
                return null;

            var argLeft = LExpression.Parameter(typeof(object), "left");
            var argRight = LExpression.Parameter(typeof(object), "right");

            var comparerType = typeof(Comparer<>).MakeGenericType(promotedType);
            var defaultComparer = comparerType.GetProperty("Default");
            var compareMethod = comparerType.GetMethod("Compare", new[] { promotedType, promotedType });

            var body = LExpression.Call(
                LExpression.Property(null, defaultComparer),
                compareMethod,
                LExpression.Convert(LExpression.Convert(argLeft, GetTypeForCode(left)), promotedType),
                LExpression.Convert(LExpression.Convert(argRight, GetTypeForCode(right)), promotedType));

            return LExpression.Lambda<Func<object, object, int>>(body, argLeft, argRight).Compile();
        }

        [CanBeNull]
        private static Func<object, object, object> CreateFunction(
            TypeCode left, 
            TypeCode right,
            [NotNull] Func<LExpression, LExpression, BinaryExpression> binaryFunction)
        {
            var argLeft = LExpression.Parameter(typeof(object), "left");
            var argRight = LExpression.Parameter(typeof(object), "right");

            if (BinaryNumericOperatorHelper.TryCreate(
                    LExpression.Convert(argLeft, GetTypeForCode(left)),
                    LExpression.Convert(argRight, GetTypeForCode(right)),
                    binaryFunction,
                    out var resultExpression
                ))
            {
                // boxing
                var finalExpression = LExpression.Convert(resultExpression, typeof(object));
                Expression<Func<object, object, object>> lambda
                    = LExpression.Lambda<Func<object, object, object>>(finalExpression, argLeft, argRight);

                return lambda.Compile();
            }

            return null;
        }

        public static Type GetTypeForCode(TypeCode code)
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

            switch (code)
            {
                case TypeCode.SByte: return typeof(sbyte);
                case TypeCode.Byte: return typeof(byte);
                case TypeCode.Int16: return typeof(short);
                case TypeCode.UInt16: return typeof(ushort);
                case TypeCode.Int32: return typeof(int);
                case TypeCode.UInt32: return typeof(uint);
                case TypeCode.Int64: return typeof(long);
                case TypeCode.UInt64: return typeof(ulong);
                case TypeCode.Single: return typeof(float);
                case TypeCode.Double: return typeof(double);
                case TypeCode.Decimal: return typeof(decimal);
            }

            return null;
        }
    }
}

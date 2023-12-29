using JetBrains.Annotations;

using System;
using System.Collections.Concurrent;
using System.Reflection;

using SpringExpressions.Expressions.LinqExpressionHelpers;

using LExpression = System.Linq.Expressions.Expression;
using LBinaryExpression = System.Linq.Expressions.BinaryExpression;
using LConstantExpression = System.Linq.Expressions.ConstantExpression;

namespace SpringExpressions.Expressions.Compiling
{
    internal static class NullableValueTypesHelper
    {
        [ContractAnnotation(
            "=>true,resultExpression:notnull;=>false,resultExpression:null")]
        public static bool TryCreateForComparison(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] LConstantExpression oneSideReturnsNullResult,
            [NotNull] LConstantExpression bothSidesReturnsNullResult,
            [NotNull] Func<LExpression, LExpression, LExpression> 
                bothSidesReturnsNotNullBinaryFunctionCreator,
            out LExpression resultExpression
                )
        {
            var leftExpressionType = left.Type;
            var rightExpressionType = right.Type;

            var leftIsNullable = Methods.TryGetValue(leftExpressionType, out var leftNullableTypeInfo);
            var rightIsNullable = Methods.TryGetValue(rightExpressionType, out var rightNullableTypeInfo);


            if (!leftIsNullable && MethodBaseHelpers.IsNullableType(leftExpressionType))
            {
                leftIsNullable = true;
                leftNullableTypeInfo = AddMethodForType(leftExpressionType);
            }

            if (!rightIsNullable && MethodBaseHelpers.IsNullableType(rightExpressionType))
            {
                rightIsNullable = true;
                rightNullableTypeInfo = AddMethodForType(rightExpressionType);
            }



            if (!leftIsNullable && !rightIsNullable)
            {
                resultExpression = bothSidesReturnsNotNullBinaryFunctionCreator(left, right);
            }
            else if (leftIsNullable && !rightIsNullable)
            {
                var bothSidesNotNullExpression = bothSidesReturnsNotNullBinaryFunctionCreator(
                    LExpression.Property(left, leftNullableTypeInfo.Value), 
                    right);

                if (bothSidesNotNullExpression == null)
                {
                    resultExpression = null;
                    return false;
                }

                // if (left.HasValue) creator() else false
                resultExpression
                    = LExpression.Condition(
                        LExpression.Property(left, leftNullableTypeInfo.HasValue),
                        bothSidesNotNullExpression,
                        oneSideReturnsNullResult);
            }
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            else if (!leftIsNullable && rightIsNullable)
            {
                var bothSidesNotNullExpression = bothSidesReturnsNotNullBinaryFunctionCreator(
                        left,
                        LExpression.Property(right, rightNullableTypeInfo.Value));

                if (bothSidesNotNullExpression == null)
                {
                    resultExpression = null;
                    return false;
                }

                // if (right.HasValue) creator() else false
                resultExpression
                    = LExpression.Condition(
                        LExpression.Property(right, rightNullableTypeInfo.HasValue),
                        bothSidesNotNullExpression,
                        oneSideReturnsNullResult);
            }
            else
            {
                var bothSidesNotNullExpression = bothSidesReturnsNotNullBinaryFunctionCreator(
                    LExpression.Property(left, leftNullableTypeInfo.Value),
                    LExpression.Property(right, rightNullableTypeInfo.Value));

                if (bothSidesNotNullExpression == null)
                {
                    resultExpression = null;
                    return false;
                }

                resultExpression
                    // if (left.HasValue)
                    = LExpression.Condition(LExpression.Property(left, leftNullableTypeInfo.HasValue),
                            // if (right.HasValue)
                            LExpression.Condition(LExpression.Property(right, rightNullableTypeInfo.HasValue),
                                // both have values
                                bothSidesNotNullExpression,
                                // else - left HasValue but right doesn't
                                oneSideReturnsNullResult
                                ),
                        // else - left is null (does not have value)
                            // if (right.HasValue)
                            LExpression.Condition(LExpression.Property(right, rightNullableTypeInfo.HasValue),
                                // !left.HasValue && right.HasValue (so null && Value)
                                oneSideReturnsNullResult,
                                // else - !left.HasValue && !right.HasValue (so null && null)
                                bothSidesReturnsNullResult
                                )
                            );
            }


            // both are null

            return resultExpression != null;
        }

        static NullableValueTypesHelper()
        {
            AddMethodForType<int?>();
            AddMethodForType<decimal?>();
            AddMethodForType<double?>();
            AddMethodForType<float?>();
            AddMethodForType<long?>();
            AddMethodForType<DateTime?>();
            AddMethodForType<TimeSpan?>();
            AddMethodForType<ulong?>();
            AddMethodForType<uint?>();
            AddMethodForType<short?>();
            AddMethodForType<ushort?>();
            AddMethodForType<byte?>();
            AddMethodForType<sbyte?>();
            AddMethodForType<char?>();
            AddMethodForType<bool?>();
            AddMethodForType<DateTimeOffset?>();
        }

        [NotNull]
        private static NullableTypeInfo AddMethodForType([NotNull] Type t)
        {
            var result = NullableTypeInfo.ForType(t);
            Methods[t] = result;
            return result;
        }

        private static void AddMethodForType<T>() 
        {
            var type = typeof(T);
            Methods[type] = NullableTypeInfo.ForType(type);
        }

        class NullableTypeInfo
        {
            public static NullableTypeInfo ForType(Type t)
            {
                return new NullableTypeInfo(
                    t.GetProperty("HasValue"),
                    t.GetProperty("Value"),
                    t.GetGenericArguments()[0]);
            }

            private NullableTypeInfo(
                [NotNull] PropertyInfo hasValue, 
                [NotNull] PropertyInfo getValue,
                [NotNull] Type itemType)
            {
                HasValue = hasValue;
                Value = getValue;
            }

            [NotNull]
            public PropertyInfo HasValue { get; }

            [NotNull]
            public PropertyInfo Value { get; }

            public Type ItemType { get; }
        }

        private static readonly ConcurrentDictionary<Type, NullableTypeInfo> Methods
            = new ConcurrentDictionary<Type, NullableTypeInfo>();
    }
}

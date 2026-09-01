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
        /// <summary>
        /// A comparison over operands that may be <c>Nullable&lt;T&gt;</c>, with the three
        /// nothing-involved outcomes supplied by the caller.
        /// </summary>
        /// <remarks>
        /// The three used to be two constants, both <c>false</c> - C#'s rule, where any comparison with
        /// a null operand is false. That made a nullable the one kind of nothing this engine ordered
        /// differently: a null literal and a null reference already sorted first on both backends, and
        /// so did a nullable on the *interpreter*. Only the compiled path treated it as C# does, which
        /// is why <c>NullableNumber &lt; Number</c> was False compiled against True interpreted.
        /// They are three now because the sorting answer is not symmetric: which side holds the nothing
        /// decides. See open-issues item 17.
        /// </remarks>
        public static bool TryCreateForComparison(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] LExpression leftIsNothingResult,
            [NotNull] LExpression rightIsNothingResult,
            [NotNull] LExpression bothAreNothingResult,
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
                        leftIsNothingResult);
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
                        rightIsNothingResult);
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
                                rightIsNothingResult
                                ),
                        // else - left is null (does not have value)
                            // if (right.HasValue)
                            LExpression.Condition(LExpression.Property(right, rightNullableTypeInfo.HasValue),
                                // !left.HasValue && right.HasValue (so null && Value)
                                leftIsNothingResult,
                                // else - !left.HasValue && !right.HasValue (so null && null)
                                bothAreNothingResult
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

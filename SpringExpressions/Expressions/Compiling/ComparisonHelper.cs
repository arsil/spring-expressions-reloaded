using System;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using System.Collections.Generic;

using LExpression = System.Linq.Expressions.Expression;
using LBinaryExpression = System.Linq.Expressions.BinaryExpression;


namespace SpringExpressions.Expressions.Compiling
{
    internal static class ComparisonHelper
    {
        public enum ComparisonOperator
        {
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual
        }

            // todo: error: to nie zadziała dla == i !=

        [ContractAnnotation(
            "=>true,resultExpression:notnull;=>false,resultExpression:null")]
        public static bool CreateCompare(
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression,
            [NotNull] Func<
                            LExpression,
                            LExpression,
                            LBinaryExpression> comparisonExpression,
            out LExpression resultExpression)
        {
            if (NullableValueTypesHelper.TryCreateForComparison(
                    leftExpression, 
                    rightExpression,
                    LExpression.Constant(false, typeof(bool)),
                    LExpression.Constant(false, typeof(bool)),
                    (l, r) => HandleValueTypesComparison(l, r, comparisonExpression),
                    out var binaryExpression1
                    ))
            {
                resultExpression = binaryExpression1;
                return true;
            }
            // todo: error:
            // null constant?


                   // todo: error: nullable vs notNullable
            if (leftExpression.Type == rightExpression.Type)
            {
                var mi = MiCompareSameTypes.MakeGenericMethod(leftExpression.Type);

                resultExpression
                    = comparisonExpression(
                        LExpression.Call(mi, leftExpression, rightExpression),
                        LExpression.Constant(0));
                return true;
            }


             // todo; error: return false!!!

                 // todo: error: null const value handling???--------------------------------------------------------------------------------------------------

            var biedaszyb = CreateIComparableComparisonWithNullHandling(
                leftExpression,
                rightExpression,
                comparisonExpression,
                0);

            resultExpression = biedaszyb;
            return resultExpression != null;
        }

        [CanBeNull]
        private static LExpression HandleValueTypesComparison(
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression,
            Func<LExpression, LExpression, BinaryExpression> comparisonExpression)
        {
            // try numeric comparision
            if (BinaryNumericOperatorHelper.TryCreate(
                    leftExpression,
                    rightExpression,
                    comparisonExpression, out var binaryExpression))
            {
                return  binaryExpression;
            }

            // todo: error: czy to ma sens!!!!?????
            /*
                        if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
                        {
                            // left ? 1 : 0  [comparisonExpression] right ? 1 : 0
                            return comparisonExpression(
                                LExpression.Condition(leftExpression, LExpression.Constant(1), LExpression.Constant(0)),
                                LExpression.Condition(rightExpression, LExpression.Constant(1), LExpression.Constant(0)));
                        }
            */
            if (leftExpression.Type == rightExpression.Type)
            {
                var mi = MiCompareSameTypes.MakeGenericMethod(leftExpression.Type);

                return comparisonExpression(
                        LExpression.Call(mi, leftExpression, rightExpression),
                        LExpression.Constant(0));
            }

            return null;
        }

        private static int CompareSameTypes<T>(T first, T second)
        {
            return Comparer<T>.Default.Compare(first, second);
        }

        private static readonly MethodInfo MiCompareSameTypes = typeof(ComparisonHelper)
            .GetMethod(nameof(CompareSameTypes), BindingFlags.Static | BindingFlags.NonPublic);


        static LExpression CreateIComparableComparisonWithNullHandling(
            LExpression leftExpression,
            LExpression rightExpression,
            Func<
                LExpression,
                LExpression,
                LBinaryExpression> comparisonExpression,
            int comparisonValue)
        {
            if (typeof(IComparable).IsAssignableFrom(leftExpression.Type))
            {
                var result =
                    comparisonExpression(
                        LExpression.Call(
                            LExpression.Convert(leftExpression, typeof(IComparable)),
                            //leftExpression,
                            CompareToMethodInfo,
                            LExpression.Convert(rightExpression, typeof(object))
                        //rightExpression
                        ),
                        LExpression.Constant(comparisonValue)
                    );

                return result;
            }

            if (leftExpression is ConstantExpression constExpression
                && constExpression.Value == null)
            {
                if (rightExpression.Type.IsValueType)
                    rightExpression = LExpression.Convert(rightExpression, typeof(object));

                return comparisonExpression(
                    LExpression.Condition(
                        LExpression.Equal(rightExpression, LExpression.Constant(null)),
                        LExpression.Constant(0),
                        LExpression.Constant(-1)),
                    LExpression.Constant(comparisonValue));
            }

            return null;
        }

        // todo: wyrzucić może?
        private static readonly MethodInfo CompareToMethodInfo
            = typeof(IComparable).GetMethod("CompareTo", new[] { typeof(object) });

    }
}

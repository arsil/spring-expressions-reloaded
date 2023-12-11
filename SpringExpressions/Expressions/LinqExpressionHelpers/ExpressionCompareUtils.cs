using SpringExpressions.Expressions.Compiling;
using System;
using System.Linq.Expressions;
using System.Reflection;

using LExpression = System.Linq.Expressions.Expression;
using LBinaryExpression = System.Linq.Expressions.BinaryExpression;

namespace SpringExpressions.Expressions.LinqExpressionHelpers
{
    internal static class ExpressionCompareUtils
    {
        public static LExpression CreateCompare(
            LExpression leftExpression,
            LExpression rightExpression,
            Func<
                LExpression,
                LExpression,
                LBinaryExpression> comparisonExpression,
            int comparisonValue)
        {
            if (leftExpression == null || rightExpression == null)
                return null;

                      // todo: error: null const value handling???--------------------------------------------------------------------------------------------------

            if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
            {
                return comparisonExpression(
                    LExpression.Condition(leftExpression, LExpression.Constant(1), LExpression.Constant(0)),
                    LExpression.Condition(rightExpression, LExpression.Constant(1), LExpression.Constant(0)));
            }

            // try numeric comparision
            LExpression result = NumericalOperatorHelper.Create(
                leftExpression,
                rightExpression,
                comparisonExpression);

            if (result != null)
                return result;

            result = CreateIComparableComparisonWithNullHandling(
                leftExpression,
                rightExpression,
                comparisonExpression,
                comparisonValue);

            if (result != null)
                return result;

            return null;
        }

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

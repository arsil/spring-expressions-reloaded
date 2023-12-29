#region License

/*
 * Copyright © 2002-2011 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Reflection;
using System.Runtime.Serialization;
using SpringExpressions.Expressions.Compiling;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents arithmetic exponent operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpPOWER : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpPOWER():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpPOWER(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (leftExpression == null || rightExpression == null)
                return null;

            if (ExpressionTypeHelper.IsNumericOrNullableNumericExpression(
                   leftExpression, out var leftIsNullable, out var leftTypeCode)
                &&
                ExpressionTypeHelper.IsNumericOrNullableNumericExpression(
                    rightExpression, out var rightIsNullable, out var rightTypeCode))
            {
                if (leftTypeCode != TypeCode.Double)
                {
                    leftExpression =
                        LExpression.Convert(leftExpression, leftIsNullable ? typeof(double?) : typeof(double));
                }

                if (rightTypeCode != TypeCode.Double)
                {
                    rightExpression =
                        LExpression.Convert(rightExpression, rightIsNullable ? typeof(double?) : typeof(double));
                }


                if (BinaryNumericOperatorHelper.TryCreate(
                        leftExpression, rightExpression,
                        LExpression.Power, out var resultExpression))
                {
                    return resultExpression;
                }
            }


            /*
                    // todo: error: nullable!----------------- ---------------------- -------------------------------------------------------
            if (leftExpression != null 
                && rightExpression != null
                && ExpressionTypeHelper.IsNumericExpression(leftExpression)
                && ExpressionTypeHelper.IsNumericExpression(rightExpression))
            {
                //return Math.Pow(Convert.ToDouble(m), Convert.ToDouble(n));

                return LExpression.Call(
                    MathPowMethodInfo, 
                    LExpression.Convert(leftExpression, typeof(double)),
                    LExpression.Convert(rightExpression, typeof(double)));
            }
            */
            return base.GetExpressionTreeIfPossible(contextExpression, compilationContext);
        }

        /// <summary>
        /// Returns a value for the arithmetic exponent operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object leftValue = GetLeftValue( context, evalContext );
            object rightValue = GetRightValue( context, evalContext );

            var leftIsNumber = NumberUtils.IsNumber(leftValue);
            var rightIsNumber = NumberUtils.IsNumber(rightValue);

            if (leftIsNumber && rightIsNumber)
            {
                return NumberUtils.Power(leftValue, rightValue);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsNumber || rightIsNumber) && (leftValue == null || rightValue == null))
            {
                return null;
            }

            throw new ArgumentException("Cannot calculate exponent for the instances of '"
                + leftValue?.GetType().FullName
                + "' and '"
                + rightValue?.GetType().FullName
                + "'.");
            
        }

        private static readonly MethodInfo MathPowMethodInfo
            = typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) });

    }
}
#region License

/*
 * Copyright � 2002-2011 the original author or authors.
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
            LExpression evalContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, evalContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, evalContext);

            if (leftExpression != null 
                && rightExpression != null
                && IsNumericExpression(leftExpression)
                && IsNumericExpression(rightExpression))
            {
                //return Math.Pow(Convert.ToDouble(m), Convert.ToDouble(n));

                return LExpression.Call(
                    MathPowMethodInfo, 
                    LExpression.Convert(leftExpression, typeof(double)),
                    LExpression.Convert(rightExpression, typeof(double)));
            }

            return base.GetExpressionTreeIfPossible(contextExpression, evalContext);
        }

        /// <summary>
        /// Returns a value for the arithmetic exponent operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object left = GetLeftValue( context, evalContext );
            object right = GetRightValue( context, evalContext );

            if (NumberUtils.IsNumber(left) && NumberUtils.IsNumber(right))
            {
                return NumberUtils.Power(left, right);
            }
            else
            {
                throw new ArgumentException("Cannot calculate exponent for the instances of '"
                                            + left.GetType().FullName
                                            + "' and '"
                                            + right.GetType().FullName
                                            + "'.");
            }
        }

        private static readonly MethodInfo MathPowMethodInfo
            = typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) });

    }
}
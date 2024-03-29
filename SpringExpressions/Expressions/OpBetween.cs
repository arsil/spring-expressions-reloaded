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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using SpringExpressions.Expressions.Compiling;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical BETWEEN operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpBetween : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpBetween()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpBetween(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (rightExpression.Type.IsGenericType &&
                rightExpression.Type.GetGenericTypeDefinition() == typeof(List<>))
            {

                          // todo: error handling! null!
                var methodInfo = rightExpression.Type.GetMethod("get_Item");

                    // todo: error! to dzia�a tyko dla numerycznych! nie zadzia�a dla innych....
                    // todo: error! i te� musz� mie� ten sam typ!!! jak nie maj�, do te� nie dzia�a... bo nie robi si� List tylko ArrayList
                ComparisonHelper.CreateCompare(
                    leftExpression,
                    LExpression.Call(rightExpression, methodInfo, LExpression.Constant(0, typeof(int))),
                    LExpression.GreaterThanOrEqual,
                    out var greaterThanOrEqualExpression);

                ComparisonHelper.CreateCompare(
                    leftExpression,
                    LExpression.Call(rightExpression, methodInfo, LExpression.Constant(1, typeof(int))),
                    LExpression.LessThanOrEqual,
                    out var lessThanOrEqualExpression);

                // todo: exception!!!!!!!!!!!
                if (lessThanOrEqualExpression == null | greaterThanOrEqualExpression == null)
                    return null;

                return LExpression.And(
                    greaterThanOrEqualExpression,
                    lessThanOrEqualExpression);
            }

            return base.GetExpressionTreeIfPossible(contextExpression, compilationContext);
        }

        /// <summary>
        /// Returns a value for the logical IN operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>
        /// true if the left operand is contained within the right operand, false otherwise.
        /// </returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object value = GetLeftValue(context, evalContext);
            IList range = GetRightValue(context, evalContext) as IList;

            if (range == null || range.Count != 2)
            {
                throw new ArgumentException("Right operand for the 'between' operator has to be a two-element list.");
            }

            object low = range[0];
            object high = range[1];

            return (CompareUtils.Compare(value, low) >= 0 && CompareUtils.Compare(value, high) <= 0);
        }
    }
}
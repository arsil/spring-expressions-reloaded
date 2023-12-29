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
using System.Collections;
using System.Runtime.Serialization;
using SpringExpressions.Expressions.Compiling;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents arithmetic division operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpDIVIDE : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpDIVIDE()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpDIVIDE(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpr = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpr = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (leftExpr == null || rightExpr == null)
                return null;

            if (BinaryNumericOperatorHelper.TryCreate(
                    leftExpr,
                    rightExpr,
                    LExpression.Divide,
                    out var resultExpression))
            {
                return resultExpression;
            }

            return null;
        }


        /// <summary>
        /// Returns a value for the arithmetic division operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object leftValue = GetLeftValue(context, evalContext);
            object rightValue = GetRightValue(context, evalContext);

            var leftIsNumber = NumberUtils.IsNumber(leftValue);
            var rightIsNumber = NumberUtils.IsNumber(rightValue);

            if (leftIsNumber && rightIsNumber)
            {
                return NumberUtils.Divide(leftValue, rightValue);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsNumber || rightIsNumber) && (leftValue == null || rightValue == null))
            {
                return null;
            }

            throw new ArgumentException("Cannot divide instances of '"
                + leftValue?.GetType().FullName
                + "' and '"
                + rightValue?.GetType().FullName
                + "'.");
        }
    }
}
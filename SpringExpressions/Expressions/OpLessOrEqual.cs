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
using System.Runtime.Serialization;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical "less than or equal" operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpLessOrEqual : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpLessOrEqual():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpLessOrEqual(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression, 
            LExpression evalContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, evalContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, evalContext);

            if (leftExpression == null || rightExpression == null)
                return null;

            if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
            {
                return LExpression.LessThanOrEqual(
                    leftExpression,
                    rightExpression);
            }

            // numeric comparision - we do not support other types
            return CreateBinaryExpressionForAllNumericTypesForNotNullChildren(
                leftExpression,
                rightExpression,
                LExpression.LessThanOrEqual);
        }

        /// <summary>
        /// Returns a value for the logical "less than or equal" operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object left = GetLeftValue( context, evalContext );
            object right = GetRightValue( context, evalContext );

            return CompareUtils.Compare(left, right) <= 0;
        }
    }
}
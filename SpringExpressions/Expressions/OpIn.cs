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
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical IN operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpIn : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpIn():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpIn(SerializationInfo info, StreamingContext context)
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

            if (rightExpression is ConstantExpression constExpression
                && constExpression.Value == null)
            {
                return LExpression.Constant(false, typeof(bool));
            }

            if (typeof(IList).IsAssignableFrom(rightExpression.Type))
            {
                return LExpression.Call(
                    rightExpression, IListContainsMethodInfo, 
                        LExpression.Convert(leftExpression, typeof(object)));
            }

            if (typeof(IDictionary).IsAssignableFrom(rightExpression.Type))
            {
                return LExpression.Call(
                    rightExpression, IDictionaryContainsMethodInfo,
                    LExpression.Convert(leftExpression, typeof(object)));
            }

            return null;
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
            object left = GetLeftValue( context, evalContext );
            object right = GetRightValue( context, evalContext );

            if (right == null)
            {
                return false;
            }
            else if (right is IList)
            {
                return ((IList) right).Contains(left);
            }
            else if (right is IDictionary)
            {
                return ((IDictionary) right).Contains(left);
            }
            else
            {
                throw new ArgumentException(
                    "Right hand parameter for 'in' operator has to be an instance of IList or IDictionary.");
            }
        }

        private static MethodInfo IListContainsMethodInfo = typeof(IList)
            .GetMethod("Contains", new[] { typeof(object) } );
        private static MethodInfo IDictionaryContainsMethodInfo = typeof(IDictionary)
            .GetMethod("Contains", new[] { typeof(object) });

    }
}
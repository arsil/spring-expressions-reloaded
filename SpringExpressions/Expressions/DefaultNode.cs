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

using SpringExpressions.Expressions.LinqExpressionHelpers;
using System;
using System.Linq.Expressions;
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed default node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class DefaultNode : BinaryOperator
    {        
        /// <summary>
        /// Create a new instance
        /// </summary>
        public DefaultNode()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected DefaultNode(SerializationInfo info, StreamingContext context)
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

            if (leftExpression is ConstantExpression constExpr && constExpr.Value == null)
                return rightExpression;

   // todo: sprawdziæ, czy jest null!em
            if (MethodBaseHelpers.IsNullableType(leftExpression.Type))
                return leftExpression;

            if (leftExpression.Type.IsValueType)
                return leftExpression;
               // todo: error: typy musz¹ pasowaæ!


               // todo: value types!
               return LExpression.Condition(
                   LExpression.NotEqual(leftExpression, LExpression.Constant(null, leftExpression.Type)),
                   leftExpression,
                   rightExpression);
            /*
         if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
         {
             // logical AND on boolean expressions
             return LExpression.AndAlso(
                 leftExpression,
                 rightExpression);
         }

         if (NumberUtils.IsInteger(leftExpression.Type)
             && NumberUtils.IsInteger(rightExpression.Type))
         {
             // bitwise AND for integer types
             return CreateBinaryExpressionForAllNumericTypesForNotNullChildren(
                 leftExpression,
                 rightExpression,
                 LExpression.And);
         }

         // enums or conversions not supported
         return null;
                           */
        }

        /// <summary>
        /// Returns left operand if it is not null, or the right operand if it is.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object leftVal = GetValue(Left, context, evalContext);
            object rightVal = GetValue(Right, context, evalContext);

            return (leftVal != null ? leftVal : rightVal);
        }
    }
}
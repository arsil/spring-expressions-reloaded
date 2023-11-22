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
using System.Runtime.Serialization;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents OR operator (both, bitwise and logical).
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpOR : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpOR():base()
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpOR(BaseNode left, BaseNode right)
            :base(left, right)
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpOR(SerializationInfo info, StreamingContext context)
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

            if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
            {
                // logical OR on boolean expressions
                return LExpression.OrElse(
                    leftExpression,
                    rightExpression);
            }

            if (NumberUtils.IsInteger(leftExpression.Type)
                && NumberUtils.IsInteger(rightExpression.Type))
            {
                // bitwise OR for integer types
                return CreateBinaryExpressionForAllNumericTypesForNotNullChildren(
                    leftExpression,
                    rightExpression,
                    LExpression.Or);
            }

            if (leftExpression.Type.IsEnum && rightExpression.Type == leftExpression.Type)
            {
                return LExpression.Convert(
                    LExpression.Or(
                        LExpression.Convert(leftExpression, Enum.GetUnderlyingType(leftExpression.Type)),
                        LExpression.Convert(rightExpression, Enum.GetUnderlyingType(rightExpression.Type))),
                    leftExpression.Type);
            }

            // enums or conversions not supported
            return null;
        }

        /// <summary>
        /// Returns a value for the logical OR operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object l = GetLeftValue(context, evalContext);
            
            if (NumberUtils.IsInteger(l))
            {
                object r = GetRightValue(context, evalContext);
                if (NumberUtils.IsInteger(r))
                {
                    return NumberUtils.BitwiseOr(l, r);
                }
            }
            else if (l is Enum)
            {
                object r = GetRightValue(context, evalContext);
                if (l.GetType() == r.GetType())
                {
                    Type enumType = l.GetType();
                    Type integralType = Enum.GetUnderlyingType(enumType);
                    l = Convert.ChangeType(l, integralType);
                    r = Convert.ChangeType(r, integralType);
                    object result = NumberUtils.BitwiseOr(l, r);
                    return Enum.ToObject(enumType, result);
                }
            }

            return Convert.ToBoolean(l) || 
                Convert.ToBoolean(GetRightValue(context, evalContext));
        }
    }
}
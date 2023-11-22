#region License

/*
 * Copyright 2002-2010 the original author or authors.
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
using System.Linq.Expressions;
using System.Runtime.Serialization;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// </summary>
    /// <author>Erich Eichinger</author>
    [Serializable]
    public class OpXOR : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpXOR()
        { }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpXOR(BaseNode left, BaseNode right)
            :base(left, right)
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpXOR(SerializationInfo info, StreamingContext context)
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
                return LExpression.ExclusiveOr(
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
                    LExpression.ExclusiveOr);
            }

            if (leftExpression.Type.IsEnum && rightExpression.Type == leftExpression.Type)
            {
                return LExpression.Convert(
                    LExpression.ExclusiveOr(
                        LExpression.Convert(leftExpression, Enum.GetUnderlyingType(leftExpression.Type)),
                        LExpression.Convert(rightExpression, Enum.GetUnderlyingType(rightExpression.Type))),
                    leftExpression.Type);
            }

            return null;
        }

		/// <summary>
		/// Returns a value for the logical AND operator node.
		/// </summary>
		/// <param name="context">Context to evaluate expressions against.</param>
		/// <param name="evalContext">Current expression evaluation context.</param>
		/// <returns>Node's value.</returns>
		protected override object Get(object context, EvaluationContext evalContext)
        {
            object l = GetLeftValue(context, evalContext);
            object r = GetRightValue(context, evalContext);

            if (NumberUtils.IsInteger(l) && NumberUtils.IsInteger(r))
            {
                return NumberUtils.BitwiseXor(l, r);
            }
            else if (l is Enum && l.GetType() == r.GetType())
            {
                Type enumType = l.GetType();
                Type integralType = Enum.GetUnderlyingType(enumType);
                l = Convert.ChangeType(l, integralType);
                r = Convert.ChangeType(r, integralType);
                object result = NumberUtils.BitwiseXor(l, r);
                return Enum.ToObject(enumType, result);
            }
            return Convert.ToBoolean(l) ^ Convert.ToBoolean(r);
        }
    }
}
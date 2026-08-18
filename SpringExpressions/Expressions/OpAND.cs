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
using SpringExpressions.Expressions.Compiling;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents AND operator (both, bitwise and logical).
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpAND : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpAND()
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpAND(BaseNode left, BaseNode right)
            :base(left, right)
        {
        }

        
        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            return BitwiseOrLogicalOperatorHelper.CreateAndExpression(
                leftExpression, rightExpression);
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

            // One operator serves two roles here - logical for booleans, bitwise for integers and enums -
            // and the left operand decides which. A left operand that is neither null, an integer nor an
            // enum rules out the bitwise role whatever the right operand turns out to be, so this is the
            // logical operator, and the logical operator short-circuits: "false and X" never evaluates X.
            if (l != null && !NumberUtils.IsInteger(l) && !(l is Enum))
            {
                return Convert.ToBoolean(l)
                    && Convert.ToBoolean(GetRightValue(context, evalContext));
            }

            object r = GetRightValue(context, evalContext);

            var leftIsInteger = NumberUtils.IsInteger(l);
            var rightIsInteger = NumberUtils.IsInteger(r);

            if (leftIsInteger && rightIsInteger)
            {
                return NumberUtils.BitwiseAnd(l, r);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsInteger || rightIsInteger) && (l == null || r == null))
            {
                return null;
            }

            if (l is Enum)
            {
                if (l.GetType() == r.GetType())
                {
                    Type enumType = l.GetType();
                    Type integralType = Enum.GetUnderlyingType(enumType);
                    l = Convert.ChangeType(l, integralType);
                    r = Convert.ChangeType(r, integralType);
                    object result = NumberUtils.BitwiseAnd(l, r);
                    return Enum.ToObject(enumType, result);
                }
            }

            // The right operand has already been evaluated above. Reading it again here would run its
            // side effects a second time.
            return Convert.ToBoolean(l) && Convert.ToBoolean(r);
        }
    }
}
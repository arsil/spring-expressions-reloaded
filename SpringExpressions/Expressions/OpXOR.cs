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
using JetBrains.Annotations;
using SpringExpressions.Expressions.Compiling;
using SpringExpressions.Util;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// </summary>
    /// <author>Erich Eichinger</author>
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

                protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            return BitwiseOrLogicalOperatorHelper.CreateXorExpression(
                leftExpression,
                rightExpression);
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

            // Nothing combined with nothing is nothing, the same rule OpADD states in full. Two
            // nulls do not tell this side which of the operator's two roles applies - the operands
            // could have been integers or booleans and both arrive as a bare null - so it fell into
            // the logical branch below and coerced them to false, while the compiled path read the
            // declared types, took the bitwise role and lifted to null. One answer has to serve both,
            // and propagation is the one the rest of arithmetic uses.
            if (l == null && r == null)
                return null;

            var leftIsInteger = TypeCheckingUtils.IsInteger(l);
            var rightIsInteger = TypeCheckingUtils.IsInteger(r);

            if (leftIsInteger && rightIsInteger)
            {
                return NumberUtils.BitwiseXor(l, r);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsInteger || rightIsInteger) && (l == null || r == null))
            {
                return null;
            }

            if (l is Enum && l.GetType() == r.GetType())
            {
                Type enumType = l.GetType();
                Type integralType = Enum.GetUnderlyingType(enumType);
                l = Convert.ChangeType(l, integralType);
                r = Convert.ChangeType(r, integralType);
                object result = NumberUtils.BitwiseXor(l, r);
                return Enum.ToObject(enumType, result);
            }
            // See OpAND for what reaches this line and why it is refused. 'xor' never short-circuits,
            // so both operands are validated.
            return BooleanUtils.RequireBoolean(l, LogicalOperand)
                ^ BooleanUtils.RequireBoolean(r, LogicalOperand);
        }

        private const string LogicalOperand = "operator 'xor'";
    }
}
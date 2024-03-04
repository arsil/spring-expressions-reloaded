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
using System.Runtime.Serialization;
using SpringExpressions.Expressions.Compiling;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents NOT operator (both, bitwise and logical).
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpNOT : UnaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpNOT():base()
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpNOT(BaseNode operand)
            :base(operand)
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpNOT(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }


	    protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
	    {
			var operandExpression = GetExpressionTreeIfPossible((BaseNode)getFirstChild(), contextExpression, compilationContext);

			var leftTypeCode = (int)System.Type.GetTypeCode(operandExpression.Type);

                // todo: error: double or decimal!!!!!
			// For Char, DBNull, Object, Empty, DateTime and String
			if (leftTypeCode < 3 || leftTypeCode > 15 || leftTypeCode == 4)
				return null;

            if (leftTypeCode == 3)
            {
                // boolean
                return LExpression.Not(operandExpression);
            }

            if (operandExpression.Type.IsEnum)
            {
                return LExpression.Convert(
                    LExpression.Not(
                        LExpression.Convert(operandExpression, Enum.GetUnderlyingType(operandExpression.Type))),
                    operandExpression.Type);
            }


            if (UnaryNumericOperatorHelper.TryCreate(operandExpression,
                    UnaryNumericOperatorHelper.UnaryOperator.UnaryNot, out var result))
            {
                return result;
            }

            return base.GetExpressionTreeIfPossible(contextExpression, compilationContext);
        }

	    /// <summary>
        /// Returns a value for the logical NOT operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object operand = GetValue(Operand, context, evalContext);
            if (NumberUtils.IsInteger(operand))
            {
                return NumberUtils.BitwiseNot(operand);
            }
            else if (operand is Enum)
            {
                Type enumType = operand.GetType();
                Type integralType = Enum.GetUnderlyingType(enumType);
                operand = Convert.ChangeType(operand, integralType);
                object result = NumberUtils.BitwiseNot(operand);
                return Enum.ToObject(enumType, result);
            }
            else
                return !Convert.ToBoolean(operand);
        }
    }
}
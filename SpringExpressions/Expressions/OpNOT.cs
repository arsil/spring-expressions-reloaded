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
    /// Represents NOT operator (both, bitwise and logical).
    /// </summary>
    /// <author>Aleksandar Seovic</author>
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

        
	    protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
	    {
			var operandExpression = GetExpressionTreeIfPossible((BaseNode)getFirstChild(), contextExpression, compilationContext);

			var leftTypeCode = (int)System.Type.GetTypeCode(operandExpression.Type);

            // TypeCode 3 is Boolean, 4 is Char, 5..12 are the integer types, and 13..15 are Single,
            // Double and Decimal. '!' is two operators sharing a spelling - logical negation for a
            // boolean, bitwise complement for an integer or enum - so those are the two that have a
            // compiled form. An enum reports its underlying integral TypeCode, so it passes here and is
            // handled below.
            //
            // The upper bound used to be 15, which let a real number through: not boolean, not an enum,
            // so it reached LExpression.Not, which has no form for a double - InvalidOperationException
            // out of the emitter, past the refusal convention, absorbed and reported as a defect of
            // ours. The author's own marker sat on this line. The interpreter reads a real number's
            // truthiness instead ('!4.5' is False, '!0.0' is True), which is inherited behaviour and is
            // deliberately left to it rather than emitted: it makes '!' answer a bool for one number
            // and a bitwise complement for another, which is an accident nobody has ruled on.
            var operandIsBoolean = leftTypeCode == 3;
            var operandIsInteger = leftTypeCode >= 5 && leftTypeCode <= 12;

            // A nullable boolean negates with nothing in it read as false, the same lift the conditional
            // operator does for its test: a null in a boolean context reads as false throughout this
            // engine - the rule that makes 'null and true' false names '!' among the shapes it covers -
            // so this is lifting rather than any kind of conversion. Note the ordering: it is checked
            // before the TypeCode guard, because Type.GetTypeCode(typeof(bool?)) is Object, not Boolean.
            if (operandExpression.Type == typeof(bool?))
                return LExpression.Not(LExpression.Call(operandExpression, NullableBoolGetValueOrDefault));

            if (!operandIsBoolean && !operandIsInteger)
                throw CannotCompile(
                    $"no compiled complement for '{operandExpression.Type}'; only a boolean is negated "
                    + "and only an integer or enum is complemented");

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
            if (TypeCheckingUtils.IsInteger(operand))
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

        private static readonly System.Reflection.MethodInfo NullableBoolGetValueOrDefault
            = typeof(bool?).GetMethod("GetValueOrDefault", new Type[0]);
    }
}
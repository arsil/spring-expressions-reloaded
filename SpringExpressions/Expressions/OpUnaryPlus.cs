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
    /// Represents unary plus operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpUnaryPlus : UnaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpUnaryPlus():base()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var operandExpression = GetExpressionTreeIfPossible((BaseNode)getFirstChild(), contextExpression, compilationContext);

            // A type's own operator, before the numeric paths - see TryCreateUserDefinedUnary.
            var userDefined = TryCreateUserDefinedUnary(
                operandExpression, "op_UnaryPlus", LExpression.UnaryPlus);

            if (userDefined != null)
                return userDefined;

            if (UnaryNumericOperatorHelper.TryCreate(operandExpression,
                    UnaryNumericOperatorHelper.UnaryOperator.UnaryPlus, out var result))
            {
                return result;
            }
            /*
            if (ExpressionTypeHelper.IsNumericOrNullableNumericExpression(operandExpression, out _, out _))
            {
                return operandExpression;
            }*/

            // Not the base method - see OpPOWER. This is OpUnaryMinus' twin and was found by probing
            // for it: the corpus generates '-value' and never '+value', so nothing measured this site.
            throw CannotCompile(
                "no compiled unary plus for '" + operandExpression.Type
                + "'; only a number, or a type declaring its own unary '+', is accepted");
        }

        /// <summary>
        /// Returns a value for the unary plus operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object n = GetValue(Operand, context, evalContext);

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if (n == null)
                return null;

            if (TryInvokeUserDefinedUnary(n, "op_UnaryPlus", out var userDefined))
                return userDefined;

            if (!TypeCheckingUtils.IsNumber(n))
            {
                throw new ArgumentException(
                    "Specified operand is not a number. Only numbers support unary plus operator.");
            }

            return NumberUtils.UnaryPlus(n);
        }
    }
}
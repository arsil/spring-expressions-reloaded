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
using System.Collections.Generic;
using SpringExpressions.Expressions.Compiling;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical BETWEEN operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpBetween : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpBetween()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            // Any List<T>, including a subclass: the bounds come from a list literal, and only the indexer
            // is wanted here, so comparing the exact generic definition was never the right question.
            if (CollectionOperandUtils.GetListItemType(rightExpression.Type) != null)
            {

                          // todo: error handling! null!
                var methodInfo = rightExpression.Type.GetMethod("get_Item");

                    // todo: error! to działa tyko dla numerycznych! nie zadziała dla innych....
                    // todo: error! i też muszą mieć ten sam typ!!! jak nie mają, do też nie działa... bo nie robi się List tylko ArrayList

                // No operator name and no factory, which says deliberately that 'between' does NOT
                // consult a type's own relational operators. That is open-issues item 12's remaining
                // question, and passing "op_GreaterThanOrEqual" here would answer half of it in the
                // wrong place: the interpreter's 'between' goes through CompareUtils.Compare, which
                // needs an int ordering and refuses a type with no IComparable, so the compiled path
                // would start answering where the interpreter still throws. A divergence, not a fix.
                ComparisonHelper.CreateCompare(
                    leftExpression,
                    LExpression.Call(rightExpression, methodInfo, LExpression.Constant(0, typeof(int))),
                    LExpression.GreaterThanOrEqual,
                    null, null,
                    out var greaterThanOrEqualExpression);

                ComparisonHelper.CreateCompare(
                    leftExpression,
                    LExpression.Call(rightExpression, methodInfo, LExpression.Constant(1, typeof(int))),
                    LExpression.LessThanOrEqual,
                    null, null,
                    out var lessThanOrEqualExpression);

                // todo: exception!!!!!!!!!!!
                if (lessThanOrEqualExpression == null | greaterThanOrEqualExpression == null)
                    throw CannotCompile("no compiled 'between' test for these operand types");

                return LExpression.And(
                    greaterThanOrEqualExpression,
                    lessThanOrEqualExpression);
            }

            return base.GetExpressionTreeIfPossible(contextExpression, compilationContext);
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
            object value = GetLeftValue(context, evalContext);
            IList range = GetRightValue(context, evalContext) as IList;

            if (range == null || range.Count != 2)
            {
                throw new ArgumentException("Right operand for the 'between' operator has to be a two-element list.");
            }

            object low = range[0];
            object high = range[1];

            return (CompareUtils.Compare(value, low) >= 0 && CompareUtils.Compare(value, high) <= 0);
        }
    }
}
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

using SpringExpressions.Expressions.Compiling;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using System;
using System.Linq.Expressions;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed default node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class DefaultNode : BinaryOperator
    {        
        /// <summary>
        /// Create a new instance
        /// </summary>
        public DefaultNode()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (leftExpression is ConstantExpression constExpr && constExpr.Value == null)
                return rightExpression;

            if (leftExpression.Type.IsValueType
                && !MethodBaseHelpers.IsNullableType(leftExpression.Type))
            {
                // A non-nullable value type is never null, so '??' is its left operand and the right one
                // is never evaluated. That is what C# does with the same shape - it is not even legal
                // there - and what the interpreter does since it stopped evaluating the right operand
                // eagerly.
                return leftExpression;
            }

            // The left operand is mentioned twice below - once to ask whether it is there and once as
            // the answer - and mentioning an emitted operand twice evaluates it twice: 'LText() ?? R()'
            // called LText() two times compiled against once interpreted, and once in C#. The right
            // operand needs no hoisting: it appears only in the false branch, which is exactly the
            // short-circuiting '??' is for.
            return OperandLocals.UseOnce(
                leftExpression,
                left =>
                {
                    if (MethodBaseHelpers.IsNullableType(left.Type))
                    {
                        var hasValue = LExpression.Property(left, left.Type.GetProperty("HasValue"));
                        var leftValue = (LExpression)LExpression.Property(
                            left, left.Type.GetProperty("Value"));

                        var right = rightExpression;
                        UnifyOperandTypes(ref leftValue, ref right);

                        return LExpression.Condition(hasValue, leftValue, right);
                    }

                    // Built before unifying, so the null check tests the operand as it arrived rather
                    // than a widened form of it.
                    var leftIsNotNull = LExpression.NotEqual(
                        left, LExpression.Constant(null, left.Type));

                    var unifiedLeft = left;
                    var unifiedRight = rightExpression;
                    UnifyOperandTypes(ref unifiedLeft, ref unifiedRight);

                    return LExpression.Condition(leftIsNotNull, unifiedLeft, unifiedRight);
                });
            /*
         if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
         {
             // logical AND on boolean expressions
             return LExpression.AndAlso(
                 leftExpression,
                 rightExpression);
         }

         if (TypeCheckingUtils.IsInteger(leftExpression.Type)
             && TypeCheckingUtils.IsInteger(rightExpression.Type))
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
        /// Widens the narrower operand so that both have one type, as the emitted conditional requires.
        /// </summary>
        /// <remarks>
        /// <c>LExpression.Condition</c> requires its two branches to be of *equivalent* type - measured: it
        /// rejects <c>A</c> against <c>C</c> even when <c>C</c> derives from <c>A</c>, rejects two siblings,
        /// and rejects <c>int</c> against <c>long</c>. It does not look for a common base, so the narrower
        /// side has to be widened explicitly, which is what C# does for <c>??</c>: the result takes the type
        /// the other operand converts to.
        /// <p>
        /// Without this the emitter threw <c>ArgumentException("Argument types do not match")</c> - the
        /// "typy muszą pasować!" note that used to sit at the end of the method. That says nothing useful to
        /// a caller and, not being a <see cref="CompileErrorException"/>, is invisible to the interpreter
        /// fallback, so a shape the interpreter handles became a hard failure.
        /// </p>
        /// <p>
        /// The nullable branch passes the *unwrapped* left operand, since that is what it puts in the
        /// conditional; everything else passes the operands as they came.
        /// </p>
        /// </remarks>
        /// <exception cref="CompileErrorException">
        /// Neither operand type converts to the other - two siblings, or a value type against an unrelated
        /// reference type. C# rejects <c>??</c> for those as well, and unifying through <c>object</c> would
        /// silently change the type of the expression.
        /// </exception>
        private void UnifyOperandTypes(ref LExpression leftExpression, ref LExpression rightExpression)
        {
            if (leftExpression.Type == rightExpression.Type)
                return;

            if (leftExpression.Type.IsAssignableFrom(rightExpression.Type))
            {
                rightExpression = LExpression.Convert(rightExpression, leftExpression.Type);
                return;
            }

            if (rightExpression.Type.IsAssignableFrom(leftExpression.Type))
            {
                leftExpression = LExpression.Convert(leftExpression, rightExpression.Type);
                return;
            }

            throw CannotCompile(
                $"no compiled form for '??' when neither operand type converts to the other "
                + $"('{leftExpression.Type}' and '{rightExpression.Type}')");
        }

        /// <summary>
        /// Returns left operand if it is not null, or the right operand if it is.
        /// </summary>
        /// <remarks>
        /// <b>The right operand is evaluated only when it is needed</b>, which is the whole point of
        /// the operator and was not what this did: it read both and then chose, so
        /// <c>Name ?? Expensive()</c> called <c>Expensive()</c> even with a name in hand. Invisible
        /// unless an operand does something - the answer was always right - and the compiled path had
        /// short-circuited all along, since its right operand sits in the false branch of a conditional.
        /// <p>
        /// C# does the same and the six <c>??</c> rows the frozen suite pins are unaffected: every one
        /// of them asserts a value and none has a side-effecting operand.
        /// </p>
        /// </remarks>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            var leftVal = GetValue(Left, context, evalContext);

            return leftVal ?? GetValue(Right, context, evalContext);
        }
    }
}
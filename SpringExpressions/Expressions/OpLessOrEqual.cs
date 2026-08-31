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
using SpringExpressions.Expressions.Compiling;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical "less than or equal" operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpLessOrEqual : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpLessOrEqual():base()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            // A type's own operator, before anything else - see TryCreateUserDefinedComparison.
            var userDefined = TryCreateUserDefinedComparison(
                leftExpression, rightExpression, "op_LessThanOrEqual",
                (l, r, m) => LExpression.LessThanOrEqual(l, r, false, m));

            if (userDefined != null)
                return userDefined;

            if (ComparisonHelper.CreateCompare(
                leftExpression,
                rightExpression,
                LExpression.LessThanOrEqual,
                out var result))
            {
                return result;
            }

            throw CannotCompile("no compiled comparison for these operand types");
        }

        /// <summary>
        /// Returns a value for the logical "less than or equal" operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object left = GetLeftValue( context, evalContext );
            object right = GetRightValue( context, evalContext );

            // A NaN operand answers false. CompareUtils.Compare is the sorting half of .NET's
            // pair of rules and would place a NaN instead; nulls are the helper's open question.
            if (CompareUtils.RelationalComparisonIsFalse(left, right))
                return false;

            if (TryInvokeUserDefinedComparison(left, right, "op_LessThanOrEqual", out var userDefined))
                return userDefined;

            return CompareUtils.Compare(left, right) <= 0;
        }
    }
}
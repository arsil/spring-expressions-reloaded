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
using SpringExpressions.Util;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical inequality operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpNotEqual : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpNotEqual():base()
        {
        }

        
		protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
		{
			var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
			var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            return EqualityHelper.CreateNotEqualExpression(this, leftExpression, rightExpression);

            /*
			if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
				return LExpression.NotEqual(leftExpression, rightExpression);

			if (leftExpression.Type == typeof(string) || rightExpression.Type == typeof(string))
				return LExpression.NotEqual(leftExpression, rightExpression);

                // todo: error: equals robi tonę innych rzeczy!
                // todo: error: zwinąć do do compare utils!!!! ???? jak się to ma do Equal???


            // TODO: porównanie z nulle-em, czyli objectem! jak to zrobić!
            // TODO: bo... bo trzeba pewnie equals odpalić! pytanie tylko na czym!
            // TODO: tutaj null-a nie rozpoznamy! bo nie mamy wartości! tej!

            //TODO: brak obsługi np. stringów... czy charów... czy innych takich! to samo przy Less i innych operatorach!

            // numeric comparision - we do not support other types
            if (BinaryNumericOperatorHelper.Create(
				leftExpression,
				rightExpression,
				LExpression.NotEqual, out var resultExpression))
            {
                return resultExpression;
            }

            return LExpression.Not(LExpression.Equal(leftExpression, rightExpression));
            */
        }

        /// <summary>
        /// Returns a value for the logical inequality operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object leftVal = GetLeftValue( context, evalContext );
            object rightVal = GetRightValue( context, evalContext );

            if (leftVal == null)
            {
                return (rightVal != null);
            }

            if (rightVal == null)
            {
                return true;
            }

            if (leftVal.GetType() == rightVal.GetType())
            {
                if (leftVal is Array val)
                {
                    return !ArrayUtils.AreEqual(val, rightVal as Array);
                }

                return EqualityUtils.NotEqualsForObjectsOfTheSameType(leftVal, rightVal);
                //return !leftVal.Equals(rightVal);
            }

            // The exact negation of OpEqual's enum-against-string rule, which this node never had: the
            // pair answered "Type == 'One'" true and threw on "Type != 'One'".
            if (leftVal.GetType().IsEnum && rightVal is string rightName)
                return !EqualityUtils.EnumEqualsName(leftVal, rightName);

            if (rightVal.GetType().IsEnum && leftVal is string leftName)
                return !EqualityUtils.EnumEqualsName(rightVal, leftName);

            return CompareUtils.Compare(leftVal, rightVal) != 0;
        }
    }
}
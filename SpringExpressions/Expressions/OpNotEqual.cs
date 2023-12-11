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
    /// Represents logical inequality operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpNotEqual : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpNotEqual():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpNotEqual(SerializationInfo info, StreamingContext context)
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
				return LExpression.NotEqual(leftExpression, rightExpression);

			if (leftExpression.Type == typeof(string) || rightExpression.Type == typeof(string))
				return LExpression.NotEqual(leftExpression, rightExpression);

                // todo: error: equals robi tonê innych rzeczy!
                // todo: error: zwin¹æ do do compare utils!!!! ???? jak siê to ma do Equal???


            // TODO: porównanie z nulle-em, czyli objectem! jak to zrobiæ!
            // TODO: bo... bo trzeba pewnie equals odpaliæ! pytanie tylko na czym!
            // TODO: tutaj null-a nie rozpoznamy! bo nie mamy wartoœci! tej!

            //TODO: brak obs³ugi np. stringów... czy charów... czy innych takich! to samo przy Less i innych operatorach!

            // numeric comparision - we do not support other types
            var result = NumericalOperatorHelper.Create(
				leftExpression,
				rightExpression,
				LExpression.NotEqual);

            if (result != null) 
                return result;

            return LExpression.Not(LExpression.Equal(leftExpression, rightExpression));
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
            else if (rightVal == null)
            {
                return true;
            }
            else if (leftVal.GetType() == rightVal.GetType())
            {
                if (leftVal is Array)
                {
                    return !ArrayUtils.AreEqual(leftVal as Array, rightVal as Array);
                }
                else
                {
                    return !leftVal.Equals(rightVal);
                }
            }
            else
            {
                return CompareUtils.Compare(leftVal, rightVal) != 0;
            }
        }
    }
}
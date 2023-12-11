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
using System.Reflection;
using System.Runtime.Serialization;
using SpringExpressions.Expressions.Compiling;
using SpringUtil;
using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical equality operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpEqual : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpEqual()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpEqual(SerializationInfo info, StreamingContext context)
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

            var res = NumericalOperatorHelper.Create(
                leftExpression, rightExpression, LExpression.Equal);

            if (res != null)
                return res;

                  // todo: error: zwin¹æ do do compare utils!!!! ???? jak siê to ma do notEqual???


            if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
                return LExpression.Equal(leftExpression, rightExpression);

            if (leftExpression.Type == typeof(string) || rightExpression.Type == typeof(string))
                return LExpression.Equal(leftExpression, rightExpression);

            if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(DateTime))
                return LExpression.Equal(leftExpression, rightExpression);

            // TODO: upewniæ siê, ¿e to dzia³a (dla wybranych typów) tak samo jak interpretacja!
            //TODO: brak obs³ugi .. czy charów... czy innych takich! to samo przy Less i innych operatorach!

            // todo: g³upie jest to, i¿ mo¿e to nie zadzia³aæ dla boxowanych typów... oto jest pytanie...
            // todo: mo¿e nigdy nie powiniœmy eqlals jednak u¿ywaæ... do zastanowienia siê...

            if (leftExpression.Type.IsValueType)
                leftExpression = LExpression.Convert(leftExpression, typeof(object));

            if (rightExpression.Type.IsValueType)
                rightExpression = LExpression.Convert(rightExpression, typeof(object));

            return LExpression.Condition(
                    LExpression.Equal(leftExpression,
                        LExpression.Constant(null, typeof(object))),
                    // left is null - emitting (rigth == null)
                    LExpression.Equal(rightExpression,
                        LExpression.Constant(null, typeof(object))),
                    // left is not null - checking right
                    LExpression.Condition(
                        LExpression.Equal(rightExpression,
                            LExpression.Constant(null, typeof(object))),
                        // left not null; right is null => false
                        LExpression.Constant(false, typeof(bool)),
                        // left not null; right not null => emitting left.Equals(right)
                        LExpression.Call(leftExpression, objEqualsMi, rightExpression)
                        )
                );
        }

        /// <summary>
        /// Returns a value for the logical equality operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object left = GetLeftValue(context, evalContext);
            object right = GetRightValue(context, evalContext);

            if (left == null)
            {
                return (right == null);
            }
            else if (right == null)
            {
                return false;
            }
            else if (left.GetType() == right.GetType())
            {
                if (left is Array)
                {
                    return ArrayUtils.AreEqual(left as Array, right as Array);
                }
                else
                {
                    return left.Equals(right);
                }
            }
            else if (left.GetType().IsEnum && right is string)
            {
                return left.Equals(Enum.Parse(left.GetType(), (string)right));
            }
            else if (right.GetType().IsEnum && left is string)
            {
                return right.Equals(Enum.Parse(right.GetType(), (string)left));
            }
            else
            {
                return CompareUtils.Compare(left, right) == 0;
            }
        }

	    private static readonly MethodInfo objEqualsMi = typeof(object).GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public);
    }
}
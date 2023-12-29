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
using System.Reflection;
using System.Runtime.Serialization;
using SpringExpressions.Expressions.Compiling;
using SpringExpressions.Util;
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

            return EqualityHelper.CreateEqualExpression(leftExpression, rightExpression);
        }

        /// <summary>
        /// Returns a value for the logical equality operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            var leftValue = GetLeftValue(context, evalContext);
            var rightValue = GetRightValue(context, evalContext);

            if (leftValue == null)
                return rightValue == null;

            if (rightValue == null)
                return false;

            // both values are not null

            var leftType = leftValue.GetType();
            var rightType = rightValue.GetType();

            if (leftType == rightType)
            {
                      // todo: error: czy na pewno? nie ma to chyba sensu!!!!
                if (leftValue is Array array)
                {
                    return ArrayUtils.AreEqual(array, rightValue as Array);
                }

                return EqualityUtils.EqualsForObjectsOfTheSameType(leftValue, rightValue);

                // todo: error: cache methods!
                return CreateMethod(leftType)(leftValue, rightValue);
                //return left.Equals(right);
            }

            // todo: error; to nie ma sensu (te enumy)...  bo not eq tego nie robi!!!!!
            if (leftType.IsEnum && rightValue is string)
            {
                return leftValue.Equals(Enum.Parse(leftType, (string)rightValue));
            }
            
            if (rightType.IsEnum && leftValue is string)
            {
                return rightValue.Equals(Enum.Parse(rightType, (string)leftValue));
            }

            return CompareUtils.Compare(leftValue, rightValue) == 0;
        }

        private static bool EqualsUsingEqualityComparer<T>(object t1, object t2)
        {
            return EqualityComparer<T>.Default.Equals((T)t1, (T)t2);
        }

        private static readonly MethodInfo MiEqualsUsingEqualityComparer = typeof(OpEqual)
            .GetMethod(nameof(EqualsUsingEqualityComparer), BindingFlags.Static | BindingFlags.NonPublic);


        private static Func<object, object, bool> CreateMethod(Type itemType)
        {
            var genericMethod = MiEqualsUsingEqualityComparer.MakeGenericMethod(itemType);
            return (Func<object, object, bool>)Delegate
                .CreateDelegate(typeof(Func<object, object, bool>), genericMethod);
        }

    }
}
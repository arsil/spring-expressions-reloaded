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
using System.Reflection;
using System.Runtime.Serialization;
using SpringCollections;
using SpringExpressions.Expressions.Compiling;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents arithmetic subtraction operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class OpSUBTRACT : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpSUBTRACT()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpSUBTRACT(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            // TODO: dodanie char -  char daje inta...!  ???

            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (leftExpression != null && rightExpression != null)
            {
                if (BinaryNumericOperatorHelper.TryCreate(
                    leftExpression, rightExpression,
                    LExpression.Subtract, out var resultExpression))
                {
                    return resultExpression;
                }

                if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(string))
                {
                    // (DateTime) left + TimeSpan.Parse(right);
                    return LExpression.Call(
                        DateTimeMethods.DateTimeSubTimeSpanMethodInfo,
                        leftExpression,
                        LExpression.Call(
                            TimeSpanMethods.TimeSpanParseMethodInfo,
                            rightExpression));
                }

                if (leftExpression.Type == typeof(DateTime) && ExpressionTypeHelper.IsNumericExpression(rightExpression))
                {
                    // (DateTime) left + TimeSpan.FromDays(Convert.ToDouble(right));
                    return LExpression.Call(
                        DateTimeMethods.DateTimeSubTimeSpanMethodInfo,
                        leftExpression,
                        LExpression.Call(
                            TimeSpanMethods.TimeSpanFromDaysMethodInfo,
                            LExpression.Convert(rightExpression, typeof(double))));
                }

                if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(DateTime))
                {
                    // (DateTime) left + (DateTime) right;
                    return LExpression.Call(
                        DateTimeMethods.DateTimeSubDateTimeMethodInfo,
                        leftExpression,
                        rightExpression);
                }

            }

            return null;
        }

        /// <summary>
        /// Returns a value for the arithmetic subtraction operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object leftValue = GetLeftValue( context, evalContext );
            object rightValue = GetRightValue( context, evalContext );

            var leftIsNumber = NumberUtils.IsNumber(leftValue);
            var rightIsNumber = NumberUtils.IsNumber(rightValue);

            if (leftIsNumber && rightIsNumber)
            {
                return NumberUtils.Subtract(leftValue, rightValue);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsNumber || rightIsNumber) && (leftValue == null || rightValue == null))
            {
                return null;
            }

            if (leftValue is DateTime && (rightValue is TimeSpan || rightValue is string || rightIsNumber))
            {
                if (rightIsNumber)
                {
                    rightValue = TimeSpan.FromDays(Convert.ToDouble(rightValue));
                }
                else if (rightValue is string)
                {
                    rightValue = TimeSpan.Parse((string) rightValue);
                }
                return (DateTime) leftValue - (TimeSpan) rightValue;
            }

            if (leftValue is DateTime && rightValue is DateTime)
            {
                return (DateTime) leftValue - (DateTime) rightValue;
            }

            if (leftValue is IList || leftValue is ISet)
            {
                ISet leftset = new HybridSet(leftValue as ICollection);
                ISet rightset;
                if(rightValue is IList || rightValue is ISet)
                {
                    rightset = new HybridSet(rightValue as ICollection);
                }
                else if (rightValue is IDictionary)
                {
                    rightset = new HybridSet(((IDictionary) rightValue).Keys);
                }
                else
                {
                    throw new ArgumentException("Cannot subtract instances of '"
                    + leftValue.GetType().FullName
                    + "' and '"
                    + rightValue.GetType().FullName
                    + "'.");
                }
                return leftset.Minus(rightset);
            }

            if (leftValue is IDictionary)
            {
                ISet leftset = new HybridSet(((IDictionary) leftValue).Keys);
                ISet rightset;
                if (rightValue is IList || rightValue is ISet)
                {
                    rightset = new HybridSet(rightValue as ICollection);
                }
                else if (rightValue is IDictionary)
                {
                    rightset = new HybridSet(((IDictionary) rightValue).Keys);
                }
                else
                {
                    throw new ArgumentException("Cannot subtract instances of '"
                    + leftValue.GetType().FullName
                    + "' and '"
                    + rightValue.GetType().FullName
                    + "'.");
                }
                IDictionary result = new Hashtable(rightset.Count);
                foreach(object key in leftset.Minus(rightset))
                {
                    result.Add(key, ((IDictionary)leftValue)[key]);
                }
                return result;
            }

            throw new ArgumentException("Cannot subtract instances of '"
                + leftValue?.GetType().FullName
                + "' and '"
                + rightValue?.GetType().FullName
                + "'.");
        }



    }
}
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
    public class OpSUBTRACT : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpSUBTRACT()
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
                // A type's own operator first - see OpADD.
                var userDefined = TryCreateUserDefinedBinary(
                    leftExpression, rightExpression, "op_Subtraction", LExpression.Subtract);

                if (userDefined != null)
                    return userDefined;

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

            throw CannotCompile("no compiled subtraction for these operand types");
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

            // A type's own operator first - see OpADD.
            if (TryInvokeUserDefinedBinary(leftValue, rightValue, "op_Subtraction", out var userDefined))
                return userDefined;

            var leftIsNumber = TypeCheckingUtils.IsNumber(leftValue);
            var rightIsNumber = TypeCheckingUtils.IsNumber(rightValue);

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

            // IsAnySet matches the vendored non-generic ISet and any generic ISet<T>, whatever its item type.
            // Both are needed: the operators return a HashSet, so in a chained expression the second
            // operator receives the first one's result, and a caller can hand in a HashSet<int> too.
            if (leftValue is IList || CollectionOperandUtils.IsAnySet(leftValue))
            {
                var difference = CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) leftValue);

                if (rightValue is IList || CollectionOperandUtils.IsAnySet(rightValue))
                {
                    difference.ExceptWith(CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) rightValue));
                }
                else if (rightValue is IDictionary rightDictionary)
                {
                    difference.ExceptWith(CollectionOperandUtils.KeysToHashSetOfObjects(rightDictionary));
                }
                else
                {
                    throw new ArgumentException("Cannot subtract instances of '"
                    + leftValue.GetType().FullName
                    + "' and '"
                    + rightValue?.GetType().FullName
                    + "'.");
                }

                return difference;
            }

            if (leftValue is IDictionary leftDictionary)
            {
                var keys = CollectionOperandUtils.KeysToHashSetOfObjects(leftDictionary);

                if (rightValue is IList || CollectionOperandUtils.IsAnySet(rightValue))
                {
                    keys.ExceptWith(CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) rightValue));
                }
                else if (rightValue is IDictionary rightDictionary)
                {
                    keys.ExceptWith(CollectionOperandUtils.KeysToHashSetOfObjects(rightDictionary));
                }
                else
                {
                    throw new ArgumentException("Cannot subtract instances of '"
                    + leftValue.GetType().FullName
                    + "' and '"
                    + rightValue?.GetType().FullName
                    + "'.");
                }

                IDictionary result = new Dictionary<object, object>(keys.Count);
                foreach(object key in keys)
                {
                    result.Add(key, leftDictionary[key]);
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

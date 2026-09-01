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
using SpringCollections;
using SpringExpressions.Expressions.Compiling;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents arithmetic multiplication operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpMULTIPLY : BinaryOperator
    {
		/// <summary>
		/// Create a new instance
		/// </summary>
		public OpMULTIPLY():base()
        {
        }

        
		protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
		{
            var leftExpr = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpr = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (leftExpr != null && rightExpr != null)
            {
                // A type's own operator first - see OpADD.
                var userDefined = TryCreateUserDefinedBinary(
                    leftExpr, rightExpr, "op_Multiply", LExpression.Multiply);

                if (userDefined != null)
                    return userDefined;

                if (BinaryNumericOperatorHelper.TryCreate(
                    leftExpr, rightExpr,
                    LExpression.Multiply, out var resultExpression))
                {
                    return resultExpression;
                }
            }

            throw CannotCompile("no compiled multiplication for these operand types");
        }
		/// <summary>
		/// Returns a value for the arithmetic multiplication operator node.
		/// </summary>
		/// <param name="context">Context to evaluate expressions against.</param>
		/// <param name="evalContext">Current expression evaluation context.</param>
		/// <returns>Node's value.</returns>
		protected override object Get(object context, EvaluationContext evalContext)
        {
			object leftValue = GetLeftValue( context, evalContext );
            object rightValue = GetRightValue( context, evalContext );

            // Nothing combined with nothing is nothing - see OpADD, where the same gap and the
            // same reasoning are written out in full.
            if (leftValue == null && rightValue == null)
                return null;

            // A type's own operator first - see OpADD.
            if (TryInvokeUserDefinedBinary(leftValue, rightValue, "op_Multiply", out var userDefined))
                return userDefined;

            var leftIsNumber = TypeCheckingUtils.IsNumber(leftValue);
            var rightIsNumber = TypeCheckingUtils.IsNumber(rightValue);

            if (leftIsNumber && rightIsNumber)
            {
                return NumberUtils.Multiply(leftValue, rightValue);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsNumber || rightIsNumber) && (leftValue == null || rightValue == null))
            {
                return null;
            }

               // todo: error: bad idea:!!!!

            // IsAnySet matches the vendored non-generic ISet and any generic ISet<T>, whatever its item type.
            // Both are needed: the operators return a HashSet, so in a chained expression the second
            // "{1,2} * {2,3} * {2}" would fail on its second operator.
            if (leftValue is IList || CollectionOperandUtils.IsAnySet(leftValue))
            {
                var intersection = CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) leftValue);

                if (rightValue is IList || CollectionOperandUtils.IsAnySet(rightValue))
                {
                    intersection.IntersectWith(CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) rightValue));
                }
                else if (rightValue is IDictionary rightDictionary)
                {
                    intersection.IntersectWith(CollectionOperandUtils.KeysToHashSetOfObjects(rightDictionary));
                }
                else
                {
                    throw new ArgumentException("Cannot multiply instances of '"
                    + leftValue.GetType().FullName
                    + "' and '"
                    + rightValue?.GetType().FullName
                    + "'.");
                }

                return intersection;
            }

            if (leftValue is IDictionary leftDictionary)
            {
                var keys = CollectionOperandUtils.KeysToHashSetOfObjects(leftDictionary);

                if (rightValue is IList || CollectionOperandUtils.IsAnySet(rightValue))
                {
                    keys.IntersectWith(CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) rightValue));
                }
                else if (rightValue is IDictionary rightDictionary)
                {
                    keys.IntersectWith(CollectionOperandUtils.KeysToHashSetOfObjects(rightDictionary));
                }
                else
                {
                    throw new ArgumentException("Cannot multiply instances of '"
                    + leftValue.GetType().FullName
                    + "' and '"
                    + rightValue?.GetType().FullName
                    + "'.");
                }

                IDictionary result = new Dictionary<object, object>(keys.Count);
                foreach (object key in keys)
                {
                    result.Add(key, leftDictionary[key]);
                }
                return result;
            }

            throw new ArgumentException("Cannot multiply instances of '"
                + leftValue?.GetType().FullName
                + "' and '"
                + rightValue?.GetType().FullName
                + "'.");
        }
    }
}

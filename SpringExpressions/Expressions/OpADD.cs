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
    /// Represents arithmetic addition operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpADD : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpADD()
        {
        }

        
        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
// TODO: dodanie char + char daje inta...!

            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (BinaryNumericOperatorHelper.TryCreate(
                leftExpression, rightExpression,
                LExpression.Add, out var resultExpression))
            {
                return resultExpression;
            }

            if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(string))
            {
                // (DateTime) left + TimeSpan.Parse(right);
                return LExpression.Call(
                    DateTimeMethods.DateTimeAddTimeSpanMethodInfo,
                    leftExpression,
                    LExpression.Call(
                        TimeSpanMethods.TimeSpanParseMethodInfo,
                        rightExpression));
            }

            if (leftExpression.Type == typeof(DateTime) && ExpressionTypeHelper.IsNumericExpression(rightExpression))
            {
                // (DateTime) left + TimeSpan.FromDays(Convert.ToDouble(right));
                return LExpression.Call(
                    DateTimeMethods.DateTimeAddTimeSpanMethodInfo,
                    leftExpression,
                    LExpression.Call(
                        TimeSpanMethods.TimeSpanFromDaysMethodInfo,
                        LExpression.Convert(rightExpression, typeof(double))));
            }

            if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(DateTime))
            {
                // (DateTime) left + (DateTime) right;
                return LExpression.Call(
                    DateTimeMethods.DateTimeAddDateTimeMethodInfo,
                    leftExpression,
                    rightExpression);
            }

            // todo: error: coś robić dla objecta ??????? czy może ścieżka interpretacji?
            // todo: moim zdaniem jak gdzieś mamy objecta, to jest klęska i mamy w tupie taką robotę!

            /*
                if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(object))
                {
                    return LExpression.Condition(
                        LExpression.TypeIs(rightExpression, typeof(TimeSpan)),
                        leftExpression,
                        LExpression.Throw(LExpression.Constant(new InvalidOperationException("Sraczka"))));

                    return LExpression.Condition(
                        LExpression.TypeIs(rightExpression, typeof(TimeSpan)),
                        leftExpression, 
                        LExpression.Throw(LExpression.Constant(new InvalidOperationException("Sraczka"))));

                    // todo: dupa blada, bo gdy dostaniemy np. object w right, to nic nie zrobimy
                    // todo: aktualnie... tzn. musielibyśmy interpretować wartości i próbować je parsować!!!
                }*/


            // one of exp is a string expression - we use Concat
            if (leftExpression.Type == typeof(string) || rightExpression.Type == typeof(string))
            {
                if (rightExpression.Type.IsValueType)
                {
                    return LExpression.Call(
                        StrConcatObjObjMethodInfo,
                        leftExpression, 
                        LExpression.TypeAs(rightExpression, typeof(object)));
                }

                return LExpression.Call(
                    StrConcatObjObjMethodInfo,
                    leftExpression, 
                    rightExpression);
            }
            
                // todo: error: wbudowane metody? - patrz date()
                // todo: error: może jednak zrobić np. _set()
                // todo: error: i np. _convert(cośtam).To(int))
                // todo: error: i np. _cast(cośtam).To(int))
                // todo: error: i np. _cast(cośtam).To(int))
                // todo: error: może tylko sety? jednak?

            var leftIsGenericEnumerable = MethodBaseHelpers.IsGenericEnumerable(leftExpression.Type);
            var rightIsGenericEnumerable = MethodBaseHelpers.IsGenericEnumerable(rightExpression.Type);

            if (leftIsGenericEnumerable&& rightIsGenericEnumerable
                && leftExpression.Type.GetGenericArguments()[0] == rightExpression.Type.GetGenericArguments()[0]
                && MethodBaseHelpers.IsGenericEnumerableOfItemType(
                    leftExpression.Type, leftExpression.Type.GetGenericArguments()[0]))
            {
                var finalUnionMi = _genericsUnionMi.MakeGenericMethod(leftExpression.Type.GetGenericArguments()[0]);
                var typedUnion = LExpression.Call(finalUnionMi, leftExpression, rightExpression);

                compilationContext.MarkAsConstructedCollection(typedUnion);
                return typedUnion;
            }


            var leftIsGenericDictionary = MethodBaseHelpers.IsGenericDictionary(leftExpression.Type);
            var rightIsGenericDictionary = MethodBaseHelpers.IsGenericDictionary(rightExpression.Type);

            if (leftIsGenericDictionary || rightIsGenericDictionary)
            {
                if (leftIsGenericDictionary && rightIsGenericDictionary)
                {
                           // todo: error: implementation!
                    throw CannotCompile("no compiled addition for these operand types");
                }

                throw new ArgumentException(
                    $"Cannot add instances of '{leftExpression.Type.FullName}' and '{rightExpression.Type.FullName}'.");
            }

            if ( (typeof(IList).IsAssignableFrom(leftExpression.Type)
                    || typeof(ISet).IsAssignableFrom(leftExpression.Type)
                    || leftIsGenericEnumerable
                )
                && (typeof(IList).IsAssignableFrom(rightExpression.Type)
                    || typeof(ISet).IsAssignableFrom(rightExpression.Type)
                    || rightIsGenericEnumerable 
                ))
            {
                return LExpression.Call(_typelessUnionMi, leftExpression, rightExpression);
            }

            throw CannotCompile("no compiled addition for these operand types");
        }

        /// <summary>
        /// The union of two collections that share an item type, keeping that item type.
        /// </summary>
        /// <remarks>
        /// Keeping the item type is what lets sum(), average(), max(), projections and selections over a
        /// union stay compiled. Declared as the concrete HashSet&lt;T&gt; rather than ISet&lt;T&gt;, so that
        /// assigning the result to a HashSet&lt;T&gt; property compiles - the interface is not assignable to
        /// it. The caller registers the emitted call with
        /// <see cref="CompilationContext.MarkAsConstructedCollection"/>, so Compiler can tell this is a set
        /// the engine built without the value needing a type of its own.
        /// </remarks>
        private static HashSet<T> GenericsUnion<T>(IEnumerable<T> arg1, IEnumerable<T> arg2)
        {
                 // todo: null-handling
            var set1 = new HashSet<T>(arg1);
            set1.UnionWith(arg2);
            return set1;
        }

        // todo: error: check declarations of other MethodInfos!!!!
        private static MethodInfo _genericsUnionMi = typeof(OpADD).GetMethod(
               nameof(GenericsUnion), BindingFlags.Static | BindingFlags.NonPublic);

        // Counterpart of the interpreter's branch in Get: the operands have no usable common item type, so
        // the union is a HashSet<object> rather than a HybridSet. Kept in step with GenericsUnion<T> above,
        // which already returned a HashSet.
        private static HashSet<object> TypelessUnion(IEnumerable left, IEnumerable right)
        {
            var union = new HashSet<object>();

            foreach (var e in left)
                union.Add(e);

            foreach (var e in right)
                union.Add(e);

            return union;
        }

        private static MethodInfo _typelessUnionMi = typeof(OpADD).GetMethod(
            "TypelessUnion", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Returns a value for the arithmetic addition operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object leftValue = GetLeftValue(context, evalContext);
            object rightValue = GetRightValue(context, evalContext);

            var leftIsNumber = TypeCheckingUtils.IsNumber(leftValue);
            var rightIsNumber = TypeCheckingUtils.IsNumber(rightValue);

            if (leftIsNumber && rightIsNumber)
            {
                return NumberUtils.Add(leftValue, rightValue);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsNumber || rightIsNumber) && (leftValue == null || rightValue == null))
            {
                return null;
            }

            // todo: error: string???? parsing here?--------
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

                return (DateTime) leftValue + (TimeSpan) rightValue;
            }

            if (leftValue is String || rightValue is String)
            {
                return string.Concat(leftValue, rightValue);
            }

            // IsAnySet matches the vendored non-generic ISet and any generic ISet<T>, whatever its item type.
            // Both are needed: the operators return a HashSet, so in a chained expression the second
            // operator receives the first one's result, and a caller can hand in a HashSet<int> too.
            if ((leftValue is IList || CollectionOperandUtils.IsAnySet(leftValue))
                && (rightValue is IList || CollectionOperandUtils.IsAnySet(rightValue)))
            {
                var union = CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) leftValue);
                union.UnionWith(CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) rightValue));
                return union;
            }

            if (leftValue is IDictionary leftDictionary && rightValue is IDictionary rightDictionary)
            {
                var leftKeys = CollectionOperandUtils.KeysToHashSetOfObjects(leftDictionary);
                var unionKeys = CollectionOperandUtils.KeysToHashSetOfObjects(leftDictionary);
                unionKeys.UnionWith(CollectionOperandUtils.KeysToHashSetOfObjects(rightDictionary));

                IDictionary result = new Dictionary<object, object>(unionKeys.Count);
                foreach(object key in unionKeys)
                {
                    if(leftKeys.Contains(key))
                    {
                        result.Add(key, leftDictionary[key]);
                    }
                    else
                    {
                        result.Add(key, rightDictionary[key]);
                    }
                }
                return result;
            }

            throw new ArgumentException("Cannot add instances of '"
                + leftValue?.GetType().FullName
                + "' and '"
                + rightValue?.GetType().FullName
                + "'.");
        }

        private static readonly MethodInfo StrConcatObjObjMethodInfo
            = typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object) });
    }
}
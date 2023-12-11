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
    [Serializable]
    public class OpADD : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpADD()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected OpADD(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }


        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
// TODO: dodanie char + char daje inta...!

            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (leftExpression == null || rightExpression == null)
                return null;

            var exp = NumericalOperatorHelper.Create(
                leftExpression,
                rightExpression,
                LExpression.Add);

            if (exp != null)
                return exp;

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

            // todo: error: coœ robiæ dla objecta ??????? czy mo¿e œcie¿ka interpretacji?
            // todo: moim zdaniem jak gdzieœ mamy objecta, to jest klêska i mamy w tupie tak¹ robotê!

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
                    // todo: aktualnie... tzn. musielibyœmy interpretowaæ wartoœci i próbowaæ je parsowaæ!!!
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
                // todo: error: mo¿e jednak zrobiæ np. _set()
                // todo: error: i np. _convert(coœtam).To(int))
                // todo: error: i np. _cast(coœtam).To(int))
                // todo: error: i np. _cast(coœtam).To(int))
                // todo: error: mo¿e tylko sety? jednak?

            var leftIsGenericEnumerable = MethodBaseHelpers.IsGenericEnumerable(leftExpression.Type);
            var rightIsGenericEnumerable = MethodBaseHelpers.IsGenericEnumerable(rightExpression.Type);

            if (leftIsGenericEnumerable&& rightIsGenericEnumerable
                && leftExpression.Type.GetGenericArguments()[0] == rightExpression.Type.GetGenericArguments()[0]
                && MethodBaseHelpers.IsGenericEnumerableOfItemType(
                    leftExpression.Type, leftExpression.Type.GetGenericArguments()[0]))
            {
                var finalUnionMi = _genericsUnionMi.MakeGenericMethod(leftExpression.Type.GetGenericArguments()[0]);
                return LExpression.Call(finalUnionMi, leftExpression, rightExpression);
            }


            var leftIsGenericDictionary = MethodBaseHelpers.IsGenericDictionary(leftExpression.Type);
            var rightIsGenericDictionary = MethodBaseHelpers.IsGenericDictionary(rightExpression.Type);

            if (leftIsGenericDictionary || rightIsGenericDictionary)
            {
                if (leftIsGenericDictionary && rightIsGenericDictionary)
                {
                           // todo: error: implementation!
                    return null;
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

            return null;
        }

            // todo: error: return value! why ISet not IList<> ?
        private static ISet<T> GenericsUnion<T>(IEnumerable<T> arg1, IEnumerable<T> arg2)
        {
                 // todo: null-handling
            var set1 = new HashSet<T>(arg1);
            set1.UnionWith(arg2);
            return set1;
        }

        // todo: error: check declarations of other MethodInfos!!!!
        private static MethodInfo _genericsUnionMi = typeof(OpADD).GetMethod(
               nameof(GenericsUnion), BindingFlags.Static | BindingFlags.NonPublic);

        private static ISet TypelessUnion(IEnumerable left, IEnumerable right)
        {
            ISet leftset = new HybridSet();
            ISet rightset = new HybridSet();

            foreach (var e in left)
                leftset.Add(e);

            foreach (var e in right)
                rightset.Add(e);

            return leftset.Union(rightset);
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
            object left = GetLeftValue(context, evalContext);
            object right = GetRightValue(context, evalContext);

            if (NumberUtils.IsNumber(left) && NumberUtils.IsNumber(right))
            {
                return NumberUtils.Add(left, right);
            }

            else if (left is DateTime && (right is TimeSpan || right is string || NumberUtils.IsNumber(right)))
            {
                if (NumberUtils.IsNumber(right))
                {
                    right = TimeSpan.FromDays(Convert.ToDouble(right));
                }
                else if (right is string)
                {
                    right = TimeSpan.Parse((string) right);
                }

                return (DateTime) left + (TimeSpan) right;
            }
            else if (left is String || right is String)
            {
                return string.Concat(left, right);
            }
            else if ((left is IList || left is ISet) && (right is IList || right is ISet))
            {
                ISet leftset = new HybridSet(left as ICollection);
                ISet rightset = new HybridSet(right as ICollection);
                return leftset.Union(rightset);
            }
            else if (left is IDictionary && right is IDictionary)
            {
                ISet leftset = new HybridSet(((IDictionary) left).Keys);
                ISet rightset = new HybridSet(((IDictionary) right).Keys);
                ISet unionset = leftset.Union(rightset);
                
                IDictionary result = new Hashtable(unionset.Count);
                foreach(object key in unionset)
                {
                    if(leftset.Contains(key))
                    {
                        result.Add(key, ((IDictionary)left)[key]);
                    }
                    else
                    {
                        result.Add(key, ((IDictionary)right)[key]);
                    }
                }
                return result;
            }
            else
            {
                throw new ArgumentException("Cannot add instances of '"
                                            + left.GetType().FullName
                                            + "' and '"
                                            + right.GetType().FullName
                                            + "'.");
            }
        }

        private static readonly MethodInfo StrConcatObjObjMethodInfo
            = typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object) });
    }
}
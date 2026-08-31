using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class GenericProcessorsFacade
    {
        public static bool TryGetMethodInfo(
            string methodName, 
            Type collectionType, 
            Type itemType, 
            List<Type> argumentTypes, 
            out MethodInfo methodInfo)
        {
            if (_methods.TryGetValue(methodName, out var processor))
                return processor.TryGetMethodArguments(collectionType, itemType, argumentTypes, out methodInfo);

            methodInfo = null;
            return false;
        }

        /// <summary>
        /// Lifts the source to <c>IEnumerable&lt;T?&gt;</c> when the resolved processor asks for that
        /// rather than for <c>IEnumerable&lt;T&gt;</c>, and hands it back unchanged otherwise.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <c>min()</c>, <c>max()</c> and <c>average()</c> ask for the nullable form of a non-nullable
        /// value item type, because <c>Enumerable</c>'s non-nullable overloads throw
        /// <c>"Sequence contains no elements"</c> for an empty sequence while the nullable ones answer
        /// null - and null is how this engine says there is no answer.
        /// </p>
        /// <p>
        /// <b>The lift is not free syntax.</b> <c>IEnumerable&lt;T&gt;</c> is covariant, but covariance
        /// does not apply to value-type arguments, so an <c>IEnumerable&lt;int&gt;</c> is not an
        /// <c>IEnumerable&lt;int?&gt;</c> and there is no conversion to emit - each item has to be
        /// converted, which is what the <c>Select</c> below does. <c>Cast&lt;T?&gt;</c> would box every
        /// item to do the same job.
        /// </p>
        /// <p>
        /// Nothing else is touched: a reference item type needs no lift (<c>Min&lt;T&gt;</c> already
        /// answers null for an empty sequence there), an already-nullable one is asked for as it stands,
        /// and every other processor asks for the plain item type, so this returns the source untouched.
        /// </p>
        /// </remarks>
        public static LExpression LiftSourceIfNullableItemsWanted(
            MethodInfo processorMethod, LExpression source, Type itemType)
        {
            if (!itemType.IsValueType || Nullable.GetUnderlyingType(itemType) != null)
                return source;

            var nullableItemType = typeof(Nullable<>).MakeGenericType(itemType);

            if (processorMethod.GetParameters()[0].ParameterType
                != typeof(IEnumerable<>).MakeGenericType(nullableItemType))
            {
                return source;
            }

            var item = LExpression.Parameter(itemType, "item");

            return LExpression.Call(
                SelectMethod.MakeGenericMethod(itemType, nullableItemType),
                source,
                LExpression.Lambda(LExpression.Convert(item, nullableItemType), item));
        }

        /// <summary>
        /// <c>Select&lt;TSource, TResult&gt;(IEnumerable&lt;TSource&gt;, Func&lt;TSource, TResult&gt;)</c>
        /// - told apart from its indexed sibling, whose <c>Func</c> takes three type arguments.
        /// </summary>
        private static readonly MethodInfo SelectMethod = typeof(System.Linq.Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Select"
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2);

        private static readonly Dictionary<string, IGenericProcessor> _methods
            = new Dictionary<string, IGenericProcessor>
            {
                { "average", new AverageProcessor() },
                { "min", new MinProcessor() },
                { "max", new MaxProcessor() },
                { "sum", new SumProcessor() },
                { "count", new CountProcessor() },
                { "sort", new SortProcessor() },
                { "distinct", new DistinctProcessor() },
                { "nonNull", new NotNullProcessor() },
                { "reverse", new ReverseProcessor()},
                { "orderBy", new OrderByProcessor()},

                /*
                 *

            
            collectionProcessorMap.Add("orderBy", new OrderByProcessor());
            collectionProcessorMap.Add("convert", new ConversionProcessor());


            collectionProcessorMap.Add("nonNull", new NonNullProcessor());
            collectionProcessorMap.Add("distinct", new DistinctProcessor());
            collectionProcessorMap.Add("sort", new SortProcessor());
            collectionProcessorMap.Add("count", new CountAggregator());
            collectionProcessorMap.Add("sum", new SumAggregator());
            collectionProcessorMap.Add("max", new MaxAggregator());
            collectionProcessorMap.Add("min", new MinAggregator());
            collectionProcessorMap.Add("average", new AverageAggregator());
            collectionProcessorMap.Add("reverse", new ReverseProcessor());
                 */
            };
    }
}

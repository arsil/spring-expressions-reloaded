using System;
using System.Collections.Generic;
using System.Reflection;

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

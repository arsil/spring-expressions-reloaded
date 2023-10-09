using System.Collections.Generic;
using System.Linq;


namespace SpringExpressions.Expressions.GenericProcessors
{
    /**
     * DO NOT CHANGE NAMES! 
     **/
    static class DecimalProcessor
    {
        public static decimal sum(IEnumerable<decimal> collection)
        { return collection.Sum(); }

        public static decimal max(IEnumerable<decimal> collection)
        { return collection.Max(); }

        public static decimal min(IEnumerable<decimal> collection)
        { return collection.Min(); }

        public static decimal count(IEnumerable<decimal> collection)
        { return collection.Count(); }

        public static decimal average(IEnumerable<decimal> collection)
        { return collection.Average(); }

        public static List<decimal> sort(IEnumerable<decimal> collection)
        {
            return sort(collection, true);
        }

        public static List<decimal> sort(IEnumerable<decimal> collection, bool sortAscending)
        {
            var result = new List<decimal>(collection);

            result.Sort();
            if (!sortAscending)
                result.Reverse();

            return result;
        }

        public static List<decimal> distinct(IEnumerable<decimal> collection)
            => distinct(collection, false);

        public static List<decimal> distinct(IEnumerable<decimal> collection, bool includeNulls)
        {
            return new List<decimal>(collection.Distinct());
        }

        public static List<decimal> nonNull(IEnumerable<decimal> collection)
            => new List<decimal>(collection);


        /*
                             *            collectionProcessorMap.Add("count", new CountAggregator());
                                        collectionProcessorMap.Add("sum", new SumAggregator());
                                        collectionProcessorMap.Add("max", new MaxAggregator());
                                        collectionProcessorMap.Add("min", new MinAggregator());
                                        collectionProcessorMap.Add("average", new AverageAggregator());
                                        collectionProcessorMap.Add("sort", new SortProcessor());
                    collectionProcessorMap.Add("orderBy", new OrderByProcessor());
                                        collectionProcessorMap.Add("distinct", new DistinctProcessor());
                    collectionProcessorMap.Add("nonNull", new NonNullProcessor());
                    collectionProcessorMap.Add("reverse", new ReverseProcessor());
                    collectionProcessorMap.Add("convert", new ConversionProcessor());

                    extensionMethodProcessorMap.Add("date", new DateConversionProcessor());
         */
    }
}

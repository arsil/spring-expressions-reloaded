using System.Collections.Generic;
using System.Linq;


namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class StringProcessor
    {
        public static string max(IEnumerable<string> collection)
        { return collection.Max(); }

        public static string min(IEnumerable<string> collection)
        { return collection.Min(); }

        public static int count(IEnumerable<string> collection)
        { return collection.Count(); }

        public static List<string> sort(IEnumerable<string> collection)
        {
            return sort(collection, true);
        }

        public static List<string> sort(IEnumerable<string> collection, bool sortAscending)
        {
            var result = new List<string>(collection);

            result.Sort();
            if (!sortAscending)
                result.Reverse();

            return result;
        }

        public static List<string> distinct(IEnumerable<string> collection)
            => distinct(collection, false);

        public static List<string> distinct(IEnumerable<string> collection, bool includeNulls)
        {
            if (includeNulls)
                return new List<string>(collection.Distinct());

            return new List<string>(from it in collection.Distinct() where it != null select it);
        }

        public static List<string> nonNull(IEnumerable<string> collection)
            => new List<string>(from it in collection where it != null select it);

        /*
                             *             collectionProcessorMap.Add("count", new CountAggregator());
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

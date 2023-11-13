﻿using System.Collections.Generic;
using System.Linq;

namespace SpringExpressions.Expressions.GenericProcessors
{
	static class IntProcessor
	{
		public static int sum(IEnumerable<int> collection)
		{ return collection.Sum(); }

		public static int max(IEnumerable<int> collection)
		{ return collection.Max(); }

		public static int min(IEnumerable<int> collection)
		{ return collection.Min(); }

		public static int count(IEnumerable<int> collection)
		{ return collection.Count(); }

        public static double average(IEnumerable<int> collection)
        { return collection.Average(); }

        public static List<int> sort(IEnumerable<int> collection)
        {
            return sort(collection, true);
        }

        public static List<int> sort(IEnumerable<int> collection, bool sortAscending)
        {
            var result = new List<int>(collection);

            result.Sort();
            if (!sortAscending)
                result.Reverse();

            return result;
        }

        public static List<int> distinct(IEnumerable<int> collection)
            => distinct(collection, false);

        public static List<int> distinct(IEnumerable<int> collection, bool includeNulls)
        {
            return new List<int>(collection.Distinct());
        }

        public static List<int> nonNull(IEnumerable<int> collection)
            => new List<int>(collection);


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
using System.Collections;
using SpringExpressions.Processors;

// ReSharper disable InconsistentNaming

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class WeaklyTypedCollectionProcessor
    {
        public static int count(ICollection collection)
            => collection?.Count ?? 0;

        public static object sum(ICollection collection)
            => _sum.Process(collection, null);

        public static object max(ICollection collection)
            => _max.Process(collection, null);

        public static object min(ICollection collection)
            => _min.Process(collection, null);

        public static object average(ICollection collection)
            => _average.Process(collection, null);


        public static ArrayList notNull(ICollection collection)
            => (ArrayList)_nonNull.Process(collection, null);


        public static ArrayList reverse(ICollection collection)
            => (ArrayList)_reverse.Process(collection, null);

        //private ICollectionProcessor _count =  new CountAggregator();
        private static ICollectionProcessor _sum = new SumAggregator();
        private static ICollectionProcessor _max =  new MaxAggregator();
        private static ICollectionProcessor _min = new MinAggregator();
        private static ICollectionProcessor _average = new AverageAggregator();
        private static ICollectionProcessor _sort = new SortProcessor();
        private static ICollectionProcessor _orderBy = new OrderByProcessor();
        private static ICollectionProcessor _distinct = new DistinctProcessor();
        private static ICollectionProcessor _nonNull = new NonNullProcessor();
        private static ICollectionProcessor _reverse = new ReverseProcessor();
        private static ICollectionProcessor _convert = new ConversionProcessor();

        //private IMethodCallProcessor _date = new DateConversionProcessor();


    }
}

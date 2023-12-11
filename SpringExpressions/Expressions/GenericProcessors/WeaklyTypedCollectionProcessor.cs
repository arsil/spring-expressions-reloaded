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
            => _sum.Process(collection, NoParams);

        public static object max(ICollection collection)
            => _max.Process(collection, NoParams);

        public static object min(ICollection collection)
            => _min.Process(collection, NoParams);

        public static object average(ICollection collection)
            => _average.Process(collection, NoParams);

        public static object sort(ICollection collection)
            => _sort.Process(collection, NoParams);

        public static object sort(ICollection collection, bool sortAscending)
            => _sort.Process(collection, new object[] { sortAscending });


        public static object notNull(ICollection collection)
            => _nonNull.Process(collection, NoParams);


        public static object reverse(ICollection collection)
            => _reverse.Process(collection, NoParams);

        public static object distinct(ICollection collection)
            => _distinct.Process(collection, NoParams);

        public static object distinct(ICollection collection, bool includeNulls)
            => _distinct.Process(collection, new object[] { includeNulls });

        // ReSharper disable RedundantNameQualifier
        
        // todo: error:!!!!!
        //private ICollectionProcessor _count =  new CountAggregator();
        private static ICollectionProcessor _sum = new Processors.SumAggregator();
        private static ICollectionProcessor _max =  new Processors.MaxAggregator();
        private static ICollectionProcessor _min = new Processors.MinAggregator();
        private static ICollectionProcessor _average = new Processors.AverageAggregator();
        private static ICollectionProcessor _sort = new Processors.SortProcessor();
        private static ICollectionProcessor _orderBy = new Processors.OrderByProcessor();
        private static ICollectionProcessor _distinct = new Processors.DistinctProcessor();
        private static ICollectionProcessor _nonNull = new Processors.NonNullProcessor();
        private static ICollectionProcessor _reverse = new Processors.ReverseProcessor();
        private static ICollectionProcessor _convert = new Processors.ConversionProcessor();
        
        // ReSharper restore RedundantNameQualifier

        //private IMethodCallProcessor _date = new DateConversionProcessor();

        // ReSharper disable once UseArrayEmptyMethod
        private static readonly object[] NoParams = new object[0];

    }
}

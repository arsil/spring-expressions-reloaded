using System;
using System.Collections;
using SpringExpressions.Processors;

// ReSharper disable InconsistentNaming

namespace SpringExpressions.Expressions.GenericProcessors
{
    /// <summary>
    /// Every method here is resolved reflectively by the expression-language name of the processor it
    /// bridges, so each name must match that name exactly - a mismatch is not an error anywhere, it
    /// just makes the method unreachable and the shape silently fall back to the interpreter.
    /// </summary>
    internal class WeaklyTypedCollectionProcessor
    {
        public static object count(IEnumerable collection)
            => _count.Process(collection, NoParams);

        public static object sum(IEnumerable collection)
            => _sum.Process(collection, NoParams);

        public static object max(IEnumerable collection)
            => _max.Process(collection, NoParams);

        public static object min(IEnumerable collection)
            => _min.Process(collection, NoParams);

        public static object average(IEnumerable collection)
            => _average.Process(collection, NoParams);

        public static object sort(IEnumerable collection)
            => _sort.Process(collection, NoParams);

        public static object sort(IEnumerable collection, bool sortAscending)
            => _sort.Process(collection, new object[] { sortAscending });


        public static object nonNull(IEnumerable collection)
            => _nonNull.Process(collection, NoParams);

        public static object convert(IEnumerable collection, Type targetType)
            => _convert.Process(collection, new object[] { targetType });


        public static object reverse(IEnumerable collection)
            => _reverse.Process(collection, NoParams);

        public static object distinct(IEnumerable collection)
            => _distinct.Process(collection, NoParams);

        public static object distinct(IEnumerable collection, bool includeNulls)
            => _distinct.Process(collection, new object[] { includeNulls });

        // ReSharper disable RedundantNameQualifier
        
        // count() used to be `collection?.Count ?? 0` here rather than a processor call, which is why
        // the field below was commented out. It goes through CountAggregator now, so the O(1) test and
        // the walk-if-you-must fallback are written once - the bridge cannot answer differently from
        // the interpreter it delegates to.
        private static ICollectionProcessor _count = new Processors.CountAggregator();
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

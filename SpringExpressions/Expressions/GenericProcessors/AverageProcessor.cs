using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class AverageProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            return _methods.TryGetValue(itemType, out methodInfo);
        }

        public AverageProcessor()
        {
            // Non-nullable value item types map to the nullable overload so an empty collection - or one
            // holding only nulls - answers null instead of throwing "Sequence contains no elements".
            // See MinProcessor for the reasoning; the interpreter's AverageAggregator already answers
            // null there, so this is the compiled half of one rule.
            _methods = new Dictionary<Type, MethodInfo>
            {
                { typeof(int), ((Func<IEnumerable<int?>, double?>)Enumerable.Average).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal?>, decimal?>)Enumerable.Average).Method },
                { typeof(double), ((Func<IEnumerable<double?>, double?>)Enumerable.Average).Method },
                { typeof(float), ((Func<IEnumerable<float?>, float?>)Enumerable.Average).Method },
                { typeof(long), ((Func<IEnumerable<long?>, double?>)Enumerable.Average).Method },
                { typeof(uint), ((Func<IEnumerable<uint?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(short), ((Func<IEnumerable<short?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(byte), ((Func<IEnumerable<byte?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte?>, double?>)AverageUsingNullableLongs).Method },


                { typeof(int?), ((Func<IEnumerable<int?>, double?>)Enumerable.Average).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, decimal?>)Enumerable.Average).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, double?>)Enumerable.Average).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, float?>)Enumerable.Average).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, double?>)Enumerable.Average).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, double?>)AverageUsingNullableLongs).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, double?>)AverageUsingNullableLongs).Method },
            };
        }

        /// <summary>
        /// The small integer types have no <c>Enumerable.Average</c> overload of their own, so they are
        /// averaged as longs.
        /// </summary>
        /// <remarks>
        /// <c>Cast&lt;long?&gt;</c> cannot do this, and used to: <c>Cast</c> unboxes, unboxing demands an
        /// exact type match, and a boxed <c>uint</c> is not a <c>long</c> - so every <c>uint</c>,
        /// <c>short</c>, <c>ushort</c>, <c>byte</c> and <c>sbyte</c> collection died with
        /// <c>"Unable to cast object of type 'System.UInt32' to type 'System.Int64'"</c> on the compiled
        /// path while the interpreter answered correctly. <c>Convert.ToInt64</c> widens the boxed value
        /// instead, which is what was meant. A null item stays null so <c>Average</c> can skip it.
        /// </remarks>
        private static double? AverageUsingNullableLongs<T>(IEnumerable<T> source)
        {
            return source
                .Select(item => (object)item)
                .Select(item => item == null ? (long?)null : Convert.ToInt64(item))
                .Average();
        }

        private readonly Dictionary<Type, MethodInfo> _methods;

        //  12 - UInt64
        // bool
        // char
        // object?
    }
}

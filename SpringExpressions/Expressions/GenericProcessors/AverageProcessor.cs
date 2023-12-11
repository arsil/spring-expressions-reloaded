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
            _methods = new Dictionary<Type, MethodInfo>
            {
                { typeof(int), ((Func<IEnumerable<int>, double>)Enumerable.Average).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, decimal>)Enumerable.Average).Method },
                { typeof(double), ((Func<IEnumerable<double>, double>)Enumerable.Average).Method },
                { typeof(float), ((Func<IEnumerable<float>, float>)Enumerable.Average).Method },
                { typeof(long), ((Func<IEnumerable<long>, double>)Enumerable.Average).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, double>)AverageUsingLongs).Method },
                { typeof(short), ((Func<IEnumerable<short>, double>)AverageUsingLongs).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, double>)AverageUsingLongs).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, double>)AverageUsingLongs).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, double>)AverageUsingLongs).Method },


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

        private static double AverageUsingLongs<T>(IEnumerable<T> source)
            => source.Cast<long>().Average();

        private static double? AverageUsingNullableLongs<T>(IEnumerable<T> source)
            => source.Cast<long?>().Average();

        private readonly Dictionary<Type, MethodInfo> _methods;

        //  12 - UInt64
        // bool
        // char
        // object?
    }
}

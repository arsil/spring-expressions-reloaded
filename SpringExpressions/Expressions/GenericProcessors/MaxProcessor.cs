using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class MaxProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            return _methods.TryGetValue(itemType, out methodInfo);
        }

        public MaxProcessor()
        {
            _methods = new Dictionary<Type, MethodInfo>
            {
                { typeof(string), ((Func<IEnumerable<string>, string>)Enumerable.Max).Method },
                { typeof(int), ((Func<IEnumerable<int>, int>)Enumerable.Max).Method},
                { typeof(decimal), ((Func<IEnumerable<decimal>, decimal>)Enumerable.Max).Method },
                { typeof(double), ((Func<IEnumerable<double>, double>)Enumerable.Max).Method },
                { typeof(float), ((Func<IEnumerable<float>, float>)Enumerable.Max).Method },
                { typeof(long), ((Func<IEnumerable<long>, long>)Enumerable.Max).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime>, DateTime>)Enumerable.Max).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan>, TimeSpan>)Enumerable.Max).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, ulong>)Enumerable.Max).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, uint>)Enumerable.Max).Method },
                { typeof(short), ((Func<IEnumerable<short>, short>)Enumerable.Max).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, ushort>)Enumerable.Max).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, byte>)Enumerable.Max).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, sbyte>)Enumerable.Max).Method },

                { typeof(int?), ((Func<IEnumerable<int?>, int?>)Enumerable.Max).Method},
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, decimal?>)Enumerable.Max).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, double?>)Enumerable.Max).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, float?>)Enumerable.Max).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, long?>)Enumerable.Max).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, DateTime?>)Enumerable.Max).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, TimeSpan?>)Enumerable.Max).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, ulong?>)Enumerable.Max).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, uint?>)Enumerable.Max).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, short?>)Enumerable.Max).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, ushort?>)Enumerable.Max).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, byte?>)Enumerable.Max).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, sbyte?>)Enumerable.Max).Method },

            };
        }

        // todo: error: bool
        // todo: error: char
        // todo: error: List<T> as result?

        private readonly Dictionary<Type, MethodInfo> _methods;
    }
}

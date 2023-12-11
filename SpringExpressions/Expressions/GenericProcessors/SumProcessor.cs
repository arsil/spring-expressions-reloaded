using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class SumProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            return _methods.TryGetValue(itemType, out methodInfo);
        }

        public SumProcessor()
        {
            _methods = new Dictionary<Type, MethodInfo>
            {
                { typeof(int), ((Func<IEnumerable<int>, int>)Enumerable.Sum).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, decimal>)Enumerable.Sum).Method },
                { typeof(double), ((Func<IEnumerable<double>, double>)Enumerable.Sum).Method },
                { typeof(float), ((Func<IEnumerable<float>, float>)Enumerable.Sum).Method },
                { typeof(long), ((Func<IEnumerable<long>, long>)Enumerable.Sum).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, ulong>)Sum).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, uint>)Sum).Method },

                //{ typeof(short), ((Func<IEnumerable<short>, short>)Sum).Method },
                //{ typeof(ushort), ((Func<IEnumerable<ushort>, ushort>)Sum).Method },
                //{ typeof(byte), ((Func<IEnumerable<byte>, byte>)Sum).Method },
                //{ typeof(sbyte), ((Func<IEnumerable<sbyte>, sbyte>)Sum).Method },

                { typeof(int?), ((Func<IEnumerable<int?>, int?>)Enumerable.Sum).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, decimal?>)Enumerable.Sum).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, double?>)Enumerable.Sum).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, float?>)Enumerable.Sum).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, long?>)Enumerable.Sum).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, ulong?>)Sum).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, uint?>)Sum).Method },

                //{ typeof(short?), ((Func<IEnumerable<short?>, short?>)SumUsingNullableLongs).Method },
                //{ typeof(ushort?), ((Func<IEnumerable<ushort?>, ushort?>)SumUsingNullableLongs).Method },
                //{ typeof(byte?), ((Func<IEnumerable<byte?>, byte?>)SumUsingNullableLongs).Method },
                //{ typeof(sbyte?), ((Func<IEnumerable<sbyte?>, sbyte?>)SumUsingNullableLongs).Method },
            };
        }

        private static uint Sum(IEnumerable<uint> src)
        {
            uint sum = 0;
            checked { foreach (var item in src) sum += item; }
            return sum;
        }

        private static uint? Sum(IEnumerable<uint?> src)
        {
            uint? sum = 0;
            checked { foreach (var item in src) if (item != null) sum += item.Value; }
            return sum;
        }

        private static ulong Sum(IEnumerable<ulong> src)
        {
            ulong sum = 0;
            checked { foreach (var item in src) sum += item; }
            return sum;
        }

        private static ulong? Sum(IEnumerable<ulong?> src)
        {
            ulong? sum = 0;
            checked { foreach (var item in src) if (item != null) sum += item.Value; }
            return sum;
        }

        private readonly Dictionary<Type, MethodInfo> _methods;
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class ReverseProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            return _noParamsMethods.TryGetValue(itemType, out methodInfo);
        }

        public ReverseProcessor()
        {
            _noParamsMethods = new Dictionary<Type, MethodInfo>
            {
                { typeof(string), ((Func<IEnumerable<string>, List<string>>)Reverse).Method },
                { typeof(int), ((Func<IEnumerable<int>, List<int>>)Reverse).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, List<decimal>>)Reverse).Method },
                { typeof(double), ((Func<IEnumerable<double>, List<double>>)Reverse).Method },
                { typeof(float), ((Func<IEnumerable<float>, List<float>>)Reverse).Method },
                { typeof(long), ((Func<IEnumerable<long>, List<long>>)Reverse).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime>, List<DateTime>>)Reverse).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan>, List<TimeSpan>>)Reverse).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, List<ulong>>)Reverse).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, List<uint>>)Reverse).Method },
                { typeof(short), ((Func<IEnumerable<short>, List<short>>)Reverse).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, List<ushort>>)Reverse).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, List<byte>>)Reverse).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, List<sbyte>>)Reverse).Method },
                { typeof(char), ((Func<IEnumerable<char>, List<char>>)Reverse).Method },
                { typeof(bool), ((Func<IEnumerable<bool>, List<bool>>)Reverse).Method },

                { typeof(int?), ((Func<IEnumerable<int?>, List<int?>>)Reverse).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, List<decimal?>>)Reverse).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, List<double?>>)Reverse).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, List<float?>>)Reverse).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, List<long?>>)Reverse).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, List<DateTime?>>)Reverse).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, List<TimeSpan?>>)Reverse).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, List<ulong?>>)Reverse).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, List<uint?>>)Reverse).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, List<short?>>)Reverse).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, List<ushort?>>)Reverse).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, List<byte?>>)Reverse).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, List<sbyte?>>)Reverse).Method },
                { typeof(char?), ((Func<IEnumerable<char?>, List<char?>>)Reverse).Method },
                { typeof(bool?), ((Func<IEnumerable<bool?>, List<bool?>>)Reverse).Method },

                   // todo: error:
                   // bool
                   // char

                   // object?

            };
        }

        private static List<T> Reverse<T>(IEnumerable<T> collection)
        {
            var result = new List<T>(collection);
            result.Reverse();
            return result;
        }

        private readonly Dictionary<Type, MethodInfo> _noParamsMethods;
    }
}

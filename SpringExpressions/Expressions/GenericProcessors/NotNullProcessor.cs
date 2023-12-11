using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class NotNullProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            return _noParamsMethods.TryGetValue(itemType, out methodInfo);
        }

        public NotNullProcessor()
        {
            _noParamsMethods = new Dictionary<Type, MethodInfo>
            {
                { typeof(string), ((Func<IEnumerable<string>, List<string>>)NotNull).Method },
                { typeof(int), ((Func<IEnumerable<int>, List<int>>)NotNull).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, List<decimal>>)NotNull).Method },
                { typeof(double), ((Func<IEnumerable<double>, List<double>>)NotNull).Method },
                { typeof(float), ((Func<IEnumerable<float>, List<float>>)NotNull).Method },
                { typeof(long), ((Func<IEnumerable<long>, List<long>>)NotNull).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime>, List<DateTime>>)NotNull).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan>, List<TimeSpan>>)NotNull).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, List<ulong>>)NotNull).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, List<uint>>)NotNull).Method },
                { typeof(short), ((Func<IEnumerable<short>, List<short>>)NotNull).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, List<ushort>>)NotNull).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, List<byte>>)NotNull).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, List<sbyte>>)NotNull).Method },
                { typeof(char), ((Func<IEnumerable<char>, List<char>>)NotNull).Method },
                { typeof(bool), ((Func<IEnumerable<bool>, List<bool>>)NotNull).Method },


                { typeof(int?), ((Func<IEnumerable<int?>, List<int?>>)NotNull).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, List<decimal?>>)NotNull).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, List<double?>>)NotNull).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, List<float?>>)NotNull).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, List<long?>>)NotNull).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, List<DateTime?>>)NotNull).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, List<TimeSpan?>>)NotNull).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, List<ulong?>>)NotNull).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, List<uint?>>)NotNull).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, List<short?>>)NotNull).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, List<ushort?>>)NotNull).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, List<byte?>>)NotNull).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, List<sbyte?>>)NotNull).Method },
                { typeof(char?), ((Func<IEnumerable<char?>, List<char?>>)NotNull).Method },
                { typeof(bool?), ((Func<IEnumerable<bool?>, List<bool?>>)NotNull).Method },

            };
        }

        public static List<T> NotNull<T>(IEnumerable<T> collection)
            => new List<T>(from it in collection where it != null select it);

        private readonly Dictionary<Type, MethodInfo> _noParamsMethods;
    }
}

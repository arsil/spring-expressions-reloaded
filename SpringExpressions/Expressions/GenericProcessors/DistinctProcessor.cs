using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class DistinctProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            if (argumentTypes.Count == 1)
            {
                var result = _noParamsMethods.TryGetValue(itemType, out methodInfo);
                if (result)
                    return true;

                if (typeof(IEquatable<>).MakeGenericType(itemType).IsAssignableFrom(itemType))
                {
                    methodInfo = MiDistinctForEquatable.MakeGenericMethod(itemType);
                    return true;
                }

                methodInfo = null;
                return false;
            }

            if (argumentTypes.Count == 2 && argumentTypes[1] == typeof(bool))
            {
                var result = _withOrderParamMethods.TryGetValue(itemType, out methodInfo);
                if (result)
                    return true;


                if (typeof(IEquatable<>).MakeGenericType(itemType).IsAssignableFrom(itemType))
                {
                    methodInfo = MiDistinctForEquatableIncludeNullsParam.MakeGenericMethod(itemType);
                    return true;
                }

                methodInfo = null;
                return false;
            }

            if (argumentTypes.Count == 2)
                throw new ArgumentException("distinct() processor argument must be a boolean value.");

            if (argumentTypes.Count > 2)
                throw new ArgumentException("Only a single argument can be specified for a distinct() processor.");

            methodInfo = null;
            return false;
        }

        public DistinctProcessor()
        {
            _noParamsMethods = new Dictionary<Type, MethodInfo>
            {
                { typeof(int), ((Func<IEnumerable<int>, List<int>>)Distinct).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, List<decimal>>)Distinct).Method },
                { typeof(double), ((Func<IEnumerable<double>, List<double>>)Distinct).Method },
                { typeof(float), ((Func<IEnumerable<float>, List<float>>)Distinct).Method },
                { typeof(long), ((Func<IEnumerable<long>, List<long>>)Distinct).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime>, List<DateTime>>)Distinct).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan>, List<TimeSpan>>)Distinct).Method },
                { typeof(string), ((Func<IEnumerable<string>, List<string>>)Distinct).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, List<ulong>>)Distinct).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, List<uint>>)Distinct).Method },
                { typeof(short), ((Func<IEnumerable<short>, List<short>>)Distinct).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, List<ushort>>)Distinct).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, List<byte>>)Distinct).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, List<sbyte>>)Distinct).Method },
                { typeof(char), ((Func<IEnumerable<char>, List<char>>)Distinct).Method },
                { typeof(bool), ((Func<IEnumerable<bool>, List<bool>>)Distinct).Method },


                { typeof(int?), ((Func<IEnumerable<int?>, List<int?>>)Distinct).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, List<decimal?>>)Distinct).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, List<double?>>)Distinct).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, List<float?>>)Distinct).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, List<long?>>)Distinct).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, List<DateTime?>>)Distinct).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, List<TimeSpan?>>)Distinct).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, List<ulong?>>)Distinct).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, List<uint?>>)Distinct).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, List<short?>>)Distinct).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, List<ushort?>>)Distinct).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, List<byte?>>)Distinct).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, List<sbyte?>>)Distinct).Method },
                { typeof(char?), ((Func<IEnumerable<char?>, List<char?>>)Distinct).Method },
                { typeof(bool?), ((Func<IEnumerable<bool?>, List<bool?>>)Distinct).Method },

            };

            _withOrderParamMethods = new Dictionary<Type, MethodInfo>
            {
                { typeof(int), ((Func<IEnumerable<int>, bool, List<int>>)Distinct).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, bool, List<decimal>>)Distinct).Method },
                { typeof(double), ((Func<IEnumerable<double>, bool, List<double>>)Distinct).Method },
                { typeof(float), ((Func<IEnumerable<float>, bool, List<float>>)Distinct).Method },
                { typeof(long), ((Func<IEnumerable<long>, bool, List<long>>)Distinct).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime>, bool, List<DateTime>>)Distinct).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan>, bool, List<TimeSpan>>)Distinct).Method },
                { typeof(string), ((Func<IEnumerable<string>, bool, List<string>>)Distinct).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, bool, List<ulong>>)Distinct).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, bool, List<uint>>)Distinct).Method },
                { typeof(short), ((Func<IEnumerable<short>, bool, List<short>>)Distinct).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, bool, List<ushort>>)Distinct).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, bool, List<byte>>)Distinct).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, bool, List<sbyte>>)Distinct).Method },
                { typeof(char), ((Func<IEnumerable<char>, bool, List<char>>)Distinct).Method },
                { typeof(bool), ((Func<IEnumerable<bool>, bool, List<bool>>)Distinct).Method },

                { typeof(int?), ((Func<IEnumerable<int?>, bool, List<int?>>)Distinct).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, bool, List<decimal?>>)Distinct).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, bool, List<double?>>)Distinct).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, bool, List<float?>>)Distinct).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, bool, List<long?>>)Distinct).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, bool, List<DateTime?>>)Distinct).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, bool, List<TimeSpan?>>)Distinct).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, bool, List<ulong?>>)Distinct).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, bool, List<uint?>>)Distinct).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, bool, List<short?>>)Distinct).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, bool, List<ushort?>>)Distinct).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, bool, List<byte?>>)Distinct).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, bool, List<sbyte?>>)Distinct).Method },
                { typeof(char?), ((Func<IEnumerable<char?>, bool, List<char?>>)Distinct).Method },
                { typeof(bool?), ((Func<IEnumerable<bool?>, bool, List<bool?>>)Distinct).Method },

            };
        }

        private static List<T> Distinct<T>(IEnumerable<T> collection)
        {
            return Distinct<T>(collection, false);
        }

        private static List<T> Distinct<T>(IEnumerable<T> collection, bool includeNulls)
        {
            if (includeNulls)
                return new List<T>(collection.Distinct());

            return new List<T>(from it in collection.Distinct() where it != null select it);
        }

        private static List<T> DistinctForEquatable<T>(
            IEnumerable<T> collection) where T : IEquatable<T>
            => DistinctForEquatableIncludeNullsParam<T>(collection, false);

        private static List<T> DistinctForEquatableIncludeNullsParam<T>(
            IEnumerable<T> collection, bool includeNulls)  where T : IEquatable<T>
        {
            if (includeNulls)
                return new List<T>(collection.Distinct(EquatableComparer<T>.Instance));

            return new List<T>(from it in collection.Distinct(EquatableComparer<T>.Instance) where it != null select it);
        }

        private class EquatableComparer<T> : IEqualityComparer<T> where T : IEquatable<T>
        {
            public static readonly EquatableComparer<T> Instance = new EquatableComparer<T>();

            public bool Equals(T x, T y)
            {
                if (x != null)
                {
                    if (y != null)
                        return x.Equals(y);
                    return false;
                }
                if (y != null) 
                    return false;
                return true;
            }

            public int GetHashCode(T obj)
            {
                if (obj == null)
                    return 0;
                return obj.GetHashCode();
            }
        }

        private static readonly MethodInfo MiDistinctForEquatable = typeof(DistinctProcessor)
            .GetMethod(nameof(DistinctForEquatable), BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo MiDistinctForEquatableIncludeNullsParam = typeof(DistinctProcessor)
            .GetMethod(nameof(DistinctForEquatableIncludeNullsParam), BindingFlags.Static | BindingFlags.NonPublic);

        private readonly Dictionary<Type, MethodInfo> _noParamsMethods;
        private readonly Dictionary<Type, MethodInfo> _withOrderParamMethods;
    }
}

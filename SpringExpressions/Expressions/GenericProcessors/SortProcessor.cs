using System;
using System.Collections.Generic;
using System.Reflection;

using SpringUtil;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class SortProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            if (argumentTypes.Count == 1)
            {
                if (_noParamsMethods.TryGetValue(itemType, out methodInfo))
                    return true;

                methodInfo = MiSort.MakeGenericMethod(itemType);
                return true;
            }

            if (argumentTypes.Count == 2 && argumentTypes[1] == typeof(bool))
            {
                if (_withOrderParamMethods.TryGetValue(itemType, out methodInfo))

                    return true;
                methodInfo = MiSortWithParam.MakeGenericMethod(itemType);
                return true;
            }

            methodInfo = null;
            return false;

                    // todo: error: IComparable<T> here and int the old path! Sort uses IComparable<> or IComparable internally!!!!!
        }

        public SortProcessor()
        {
            _noParamsMethods = new Dictionary<Type, MethodInfo>
            {
                { typeof(string), ((Func<IEnumerable<string>, List<string>>)Sort).Method },
                { typeof(int), ((Func<IEnumerable<int>, List<int>>)Sort).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, List<decimal>>)Sort).Method },
                { typeof(double), ((Func<IEnumerable<double>, List<double>>)Sort).Method },
                { typeof(float), ((Func<IEnumerable<float>, List<float>>)Sort).Method },
                { typeof(long), ((Func<IEnumerable<long>, List<long>>)Sort).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime>, List<DateTime>>)Sort).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan>, List<TimeSpan>>)Sort).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, List<ulong>>)Sort).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, List<uint>>)Sort).Method },
                { typeof(short), ((Func<IEnumerable<short>, List<short>>)Sort).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, List<ushort>>)Sort).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, List<byte>>)Sort).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, List<sbyte>>)Sort).Method },
                { typeof(char), ((Func<IEnumerable<char>, List<char>>)Sort).Method },
                { typeof(bool), ((Func<IEnumerable<bool>, List<bool>>)Sort).Method },


                { typeof(int?), ((Func<IEnumerable<int?>, List<int?>>)Sort).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, List<decimal?>>)Sort).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, List<double?>>)Sort).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, List<float?>>)Sort).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, List<long?>>)Sort).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, List<DateTime?>>)Sort).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, List<TimeSpan?>>)Sort).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, List<ulong?>>)Sort).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, List<uint?>>)Sort).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, List<short?>>)Sort).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, List<ushort?>>)Sort).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, List<byte?>>)Sort).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, List<sbyte?>>)Sort).Method },
                { typeof(char?), ((Func<IEnumerable<char?>, List<char?>>)Sort).Method },
                { typeof(bool?), ((Func<IEnumerable<bool?>, List<bool?>>)Sort).Method },

            };

            _withOrderParamMethods = new Dictionary<Type, MethodInfo>
            {
                { typeof(string), ((Func<IEnumerable<string>, bool, List<string>>)SortWithParam).Method },
                { typeof(int), ((Func<IEnumerable<int>, bool, List<int>>)SortWithParam).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, bool, List<decimal>>)SortWithParam).Method },
                { typeof(double), ((Func<IEnumerable<double>, bool, List<double>>)SortWithParam).Method },
                { typeof(float), ((Func<IEnumerable<float>, bool, List<float>>)SortWithParam).Method },
                { typeof(long), ((Func<IEnumerable<long>, bool, List<long>>)SortWithParam).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime>, bool, List<DateTime>>)SortWithParam).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan>, bool, List<TimeSpan>>)SortWithParam).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, bool, List<ulong>>)SortWithParam).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, bool, List<uint>>)SortWithParam).Method },
                { typeof(short), ((Func<IEnumerable<short>, bool, List<short>>)SortWithParam).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort>, bool, List<ushort>>)SortWithParam).Method },
                { typeof(byte), ((Func<IEnumerable<byte>, bool, List<byte>>)SortWithParam).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte>, bool, List<sbyte>>)SortWithParam).Method },
                { typeof(char), ((Func<IEnumerable<char>, bool, List<char>>)SortWithParam).Method },
                { typeof(bool), ((Func<IEnumerable<bool>, bool, List<bool>>)SortWithParam).Method },


                { typeof(int?), ((Func<IEnumerable<int?>, bool, List<int?>>)SortWithParam).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, bool, List<decimal?>>)SortWithParam).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, bool, List<double?>>)SortWithParam).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, bool, List<float?>>)SortWithParam).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, bool, List<long?>>)SortWithParam).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, bool, List<DateTime?>>)SortWithParam).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, bool, List<TimeSpan?>>)SortWithParam).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, bool, List<ulong?>>)SortWithParam).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, bool, List<uint?>>)SortWithParam).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, bool, List<short?>>)SortWithParam).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, bool, List<ushort?>>)SortWithParam).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, bool, List<byte?>>)SortWithParam).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, bool, List<sbyte?>>)SortWithParam).Method },
                { typeof(char?), ((Func<IEnumerable<char?>, bool, List<char?>>)SortWithParam).Method },
                { typeof(bool?), ((Func<IEnumerable<bool?>, bool, List<bool?>>)SortWithParam).Method },


            };
        }

        private static List<T> Sort<T>(IEnumerable<T> collection)
        {
            return SortWithParam<T>(collection, true);
        }

        /// <remarks>
        /// The parameterless <c>List&lt;T&gt;.Sort()</c> is <c>Comparer&lt;T&gt;.Default</c>, which
        /// throws for a type this engine treats as a number without its being
        /// <see cref="IComparable"/> - a caller's own struct with an implicit conversion to a built-in
        /// real. <c>CompareUtils.RequiresConversionToOrder</c> is the one place that question is asked;
        /// the interpreter's <c>SortProcessor</c> asks it too, and answering it differently here is what
        /// made <c>Ds.sort()</c> throw compiled while the interpreter answered. Every item type in the
        /// dictionaries above is <see cref="IComparable"/>, so they never reach the test.
        /// </remarks>
        private static List<T> SortWithParam<T>(IEnumerable<T> collection, bool sortAscending)
        {
            var result = new List<T>(collection);

            if (CompareUtils.RequiresConversionToOrder(typeof(T)))
                result.Sort((a, b) => CompareUtils.Compare(a, b));
            else
                result.Sort();

            if (!sortAscending)
                result.Reverse();

            return result;
        }

        private static readonly MethodInfo MiSort = typeof(SortProcessor)
            .GetMethod(nameof(Sort), BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo MiSortWithParam = typeof(SortProcessor)
            .GetMethod(nameof(SortWithParam), BindingFlags.Static | BindingFlags.NonPublic);

        private readonly Dictionary<Type, MethodInfo> _noParamsMethods;
        private readonly Dictionary<Type, MethodInfo> _withOrderParamMethods;
    }
}

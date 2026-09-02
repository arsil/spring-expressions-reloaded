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

        /// <remarks>
        /// <p>
        /// <b>The integral sums are unchecked, which is why they are written here instead of calling
        /// <see cref="Enumerable.Sum(IEnumerable{int})"/>.</b> That overload is checked, while the
        /// interpreter folds with <c>+</c>, which is unchecked on both backends - so a
        /// <c>List&lt;int&gt;</c> holding <c>{int.MaxValue, 1}</c> threw
        /// <see cref="OverflowException"/> compiled and answered interpreted. One backend throwing
        /// while the other answers, and only when the data happened to reach the edge.
        /// </p>
        /// <p>
        /// Unchecked is the side that moved because <c>+</c> is unchecked and the cast operator
        /// deliberately takes C#'s unchecked context too (<c>70000 as T(short)</c> is <c>4464</c>).
        /// <c>sum()</c> was the odd one out.
        /// </p>
        /// <p>
        /// <b><c>double</c>, <c>float</c> and <c>decimal</c> keep <see cref="Enumerable"/>'s own
        /// overloads</b>, and that is not an inconsistency: the reals cannot overflow - they saturate to
        /// infinity, which the interpreter's <c>+</c> does as well - and
        /// <c>Sum(IEnumerable&lt;decimal&gt;)</c> throws exactly where the interpreter's decimal <c>+</c>
        /// throws, so those three already agree.
        /// </p>
        /// </remarks>
        public SumProcessor()
        {
            _methods = new Dictionary<Type, MethodInfo>
            {
                { typeof(int), ((Func<IEnumerable<int>, int>)Sum).Method },
                { typeof(decimal), ((Func<IEnumerable<decimal>, decimal>)Enumerable.Sum).Method },
                { typeof(double), ((Func<IEnumerable<double>, double>)Enumerable.Sum).Method },
                { typeof(float), ((Func<IEnumerable<float>, float>)Enumerable.Sum).Method },
                { typeof(long), ((Func<IEnumerable<long>, long>)Sum).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong>, ulong>)Sum).Method },
                { typeof(uint), ((Func<IEnumerable<uint>, uint>)Sum).Method },

                //{ typeof(short), ((Func<IEnumerable<short>, short>)Sum).Method },
                //{ typeof(ushort), ((Func<IEnumerable<ushort>, ushort>)Sum).Method },
                //{ typeof(byte), ((Func<IEnumerable<byte>, byte>)Sum).Method },
                //{ typeof(sbyte), ((Func<IEnumerable<sbyte>, sbyte>)Sum).Method },

                { typeof(int?), ((Func<IEnumerable<int?>, int?>)Sum).Method },
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, decimal?>)Enumerable.Sum).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, double?>)Enumerable.Sum).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, float?>)Enumerable.Sum).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, long?>)Sum).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, ulong?>)Sum).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, uint?>)Sum).Method },

                //{ typeof(short?), ((Func<IEnumerable<short?>, short?>)SumUsingNullableLongs).Method },
                //{ typeof(ushort?), ((Func<IEnumerable<ushort?>, ushort?>)SumUsingNullableLongs).Method },
                //{ typeof(byte?), ((Func<IEnumerable<byte?>, byte?>)SumUsingNullableLongs).Method },
                //{ typeof(sbyte?), ((Func<IEnumerable<sbyte?>, sbyte?>)SumUsingNullableLongs).Method },
            };
        }

        // Unchecked, matching the interpreter's fold with '+' - see the constructor's remarks. A null
        // item is skipped and an empty sequence answers 0, which is what the Enumerable overloads these
        // replace do, so only the overflow behaviour moved.

        private static int Sum(IEnumerable<int> src)
        {
            int sum = 0;
            unchecked { foreach (var item in src) sum += item; }
            return sum;
        }

        private static int? Sum(IEnumerable<int?> src)
        {
            int sum = 0;
            unchecked { foreach (var item in src) if (item != null) sum += item.Value; }
            return sum;
        }

        private static long Sum(IEnumerable<long> src)
        {
            long sum = 0;
            unchecked { foreach (var item in src) sum += item; }
            return sum;
        }

        private static long? Sum(IEnumerable<long?> src)
        {
            long sum = 0;
            unchecked { foreach (var item in src) if (item != null) sum += item.Value; }
            return sum;
        }

        private static uint Sum(IEnumerable<uint> src)
        {
            uint sum = 0;
            unchecked { foreach (var item in src) sum += item; }
            return sum;
        }

        private static uint? Sum(IEnumerable<uint?> src)
        {
            uint sum = 0;
            unchecked { foreach (var item in src) if (item != null) sum += item.Value; }
            return sum;
        }

        private static ulong Sum(IEnumerable<ulong> src)
        {
            ulong sum = 0;
            unchecked { foreach (var item in src) sum += item; }
            return sum;
        }

        private static ulong? Sum(IEnumerable<ulong?> src)
        {
            ulong sum = 0;
            unchecked { foreach (var item in src) if (item != null) sum += item.Value; }
            return sum;
        }

        private readonly Dictionary<Type, MethodInfo> _methods;
    }
}

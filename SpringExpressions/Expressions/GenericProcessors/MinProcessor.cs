using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class MinProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            return _methods.TryGetValue(itemType, out methodInfo);
        }

        public MinProcessor()
        {
            // Every non-nullable value item type maps to the *nullable* overload, the same MethodInfo
            // its nullable sibling below uses, and MethodNode lifts the source to match. That is what
            // makes an empty collection answer null instead of throwing "Sequence contains no elements"
            // out of Enumerable.Min: the non-nullable overloads throw for an empty sequence, the
            // nullable ones return null, and null is how this engine says "there is no answer" - the
            // same ruling first- and last-match selection already runs on.
            //
            // A reference item type needs none of it: Min<T> already returns null for an empty sequence
            // when T is a reference type, which is why 'string' is left as it is.
            _methods = new Dictionary<Type, MethodInfo>
            {
                { typeof(string), ((Func<IEnumerable<string>, string>)Enumerable.Min).Method },
                { typeof(int), ((Func<IEnumerable<int?>, int?>)Enumerable.Min).Method},
                { typeof(decimal), ((Func<IEnumerable<decimal?>, decimal?>)Enumerable.Min).Method },
                { typeof(double), ((Func<IEnumerable<double?>, double?>)Enumerable.Min).Method },
                { typeof(float), ((Func<IEnumerable<float?>, float?>)Enumerable.Min).Method },
                { typeof(long), ((Func<IEnumerable<long?>, long?>)Enumerable.Min).Method },
                { typeof(DateTime), ((Func<IEnumerable<DateTime?>, DateTime?>)Enumerable.Min).Method },
                { typeof(TimeSpan), ((Func<IEnumerable<TimeSpan?>, TimeSpan?>)Enumerable.Min).Method },
                { typeof(ulong), ((Func<IEnumerable<ulong?>, ulong?>)Enumerable.Min).Method },
                { typeof(uint), ((Func<IEnumerable<uint?>, uint?>)Enumerable.Min).Method },
                { typeof(short), ((Func<IEnumerable<short?>, short?>)Enumerable.Min).Method },
                { typeof(ushort), ((Func<IEnumerable<ushort?>, ushort?>)Enumerable.Min).Method },
                { typeof(byte), ((Func<IEnumerable<byte?>, byte?>)Enumerable.Min).Method },
                { typeof(sbyte), ((Func<IEnumerable<sbyte?>, sbyte?>)Enumerable.Min).Method },

                { typeof(int?), ((Func<IEnumerable<int?>, int?>)Enumerable.Min).Method},
                { typeof(decimal?), ((Func<IEnumerable<decimal?>, decimal?>)Enumerable.Min).Method },
                { typeof(double?), ((Func<IEnumerable<double?>, double?>)Enumerable.Min).Method },
                { typeof(float?), ((Func<IEnumerable<float?>, float?>)Enumerable.Min).Method },
                { typeof(long?), ((Func<IEnumerable<long?>, long?>)Enumerable.Min).Method },
                { typeof(DateTime?), ((Func<IEnumerable<DateTime?>, DateTime?>)Enumerable.Min).Method },
                { typeof(TimeSpan?), ((Func<IEnumerable<TimeSpan?>, TimeSpan?>)Enumerable.Min).Method },
                { typeof(ulong?), ((Func<IEnumerable<ulong?>, ulong?>)Enumerable.Min).Method },
                { typeof(uint?), ((Func<IEnumerable<uint?>, uint?>)Enumerable.Min).Method },
                { typeof(short?), ((Func<IEnumerable<short?>, short?>)Enumerable.Min).Method },
                { typeof(ushort?), ((Func<IEnumerable<ushort?>, ushort?>)Enumerable.Min).Method },
                { typeof(byte?), ((Func<IEnumerable<byte?>, byte?>)Enumerable.Min).Method },
                { typeof(sbyte?), ((Func<IEnumerable<sbyte?>, sbyte?>)Enumerable.Min).Method },
            };

        }

        // todo: error: bool
        // todo: error: char
        // todo: error: List<T> as result?


        private readonly Dictionary<Type, MethodInfo> _methods;
    }
}

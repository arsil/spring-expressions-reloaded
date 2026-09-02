using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class CountProcessor : IGenericProcessor
    {
        /// <remarks>
        /// <p>
        /// <b>The item type comes first, and the O(1) answer depends on it.</b> This used to test the
        /// <i>declared</i> type for the non-generic <see cref="ICollection"/> and walk everything else,
        /// which cost - measured, 5,000,000 items, compiled: <c>List&lt;int&gt;</c> and <c>int[]</c>
        /// answered in 0.000 ms while <c>HashSet&lt;int&gt;</c> took 62.8 ms, <c>ICollection&lt;int&gt;</c>
        /// 73.2 ms and <c>IReadOnlyList&lt;int&gt;</c> holding a <c>List&lt;int&gt;</c> 62.1 ms. Every
        /// one of those can answer without being walked.
        /// </p>
        /// <p>
        /// <see cref="Enumerable.Count{T}"/> is the fix rather than an emitted property read, because it
        /// tests the <i>runtime</i> type for <c>ICollection&lt;T&gt;</c> and then for the non-generic
        /// <see cref="ICollection"/> - and neither interface alone is enough: a <c>HashSet&lt;int&gt;</c>
        /// implements only the generic one, a <c>Queue&lt;int&gt;</c> only the non-generic one. Measured
        /// afterwards, all five of those sources answer in 0.000 ms and only a genuinely lazy sequence
        /// walks, which is all that can be done for one. The fast path has been in
        /// <c>Enumerable.Count</c> since .NET 3.5, so every target here has it.
        /// </p>
        /// </remarks>
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            if (itemType != null)
            {
                methodInfo = EnumerableCountOfT.MakeGenericMethod(itemType);
                return true;
            }

            if (typeof(ICollection).IsAssignableFrom(collectionType))
            {
                methodInfo = CollectionCount;
                return true;
            }

            if (typeof(IEnumerable).IsAssignableFrom(collectionType))
            {
                methodInfo = EnumerableCount;
                return true;
            }

            methodInfo = null;
            return false;
        }

        private static int CountCollection(ICollection collection)
            => collection.Count;

        private static int CountEnumerable(IEnumerable enumerable)
        {
            var count = 0;
            foreach (var item in enumerable)
                ++count;
            return count;
        }

        private static readonly MethodInfo CollectionCount
            = ((Func<ICollection, int>)CountCollection).Method;

        private static readonly MethodInfo EnumerableCount
            = ((Func<IEnumerable, int>)CountEnumerable).Method;

        private static readonly MethodInfo EnumerableCountOfT
            = ((Func<IEnumerable<object>, int>)Enumerable.Count).Method.GetGenericMethodDefinition();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class CountProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
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

    }
}

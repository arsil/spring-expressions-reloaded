#region License

/*
 * Copyright © 2002-2011 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

#region Imports

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using SpringCollections;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using SpringUtil;

#endregion

namespace SpringExpressions.Processors
{
    /// <summary>
    /// Implementation of the sort processor.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class SortProcessor : ICollectionProcessor
    {
        /// <summary>
        /// Sorts the source collection.
        /// </summary>
        /// <remarks>
        /// Please not that this processor requires that collection elements
        /// are of a uniform type and that they implement <see cref="IComparable"/>
        /// interface.
        /// <p/>
        /// If you want to perform custom sorting based on element properties
        /// you should consider using <see cref="OrderByProcessor"/> instead.
        /// </remarks>
        /// <param name="source">
        /// The source collection to sort.
        /// </param>
        /// <param name="args">
        /// Ignored.
        /// </param>
        /// <returns>
        /// A list containing sorted collection elements.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If <paramref name="source"/> collection is not empty and it is 
        /// neither <see cref="IList"/> nor <see cref="ISet"/>.
        /// </exception>
        public object Process(IEnumerable source, object[] args)
        {
            if (source == null)
            {
                return source;
            }

            bool sortAscending = true;
            if (args != null && args.Length == 1 && args[0] is bool)
            {
                sortAscending = (bool) args[0];
            }

            // List<object>, not an ArrayList copied into a typed array: the weakly typed path
            // returns object-typed collections for every result the engine builds, and the compiled
            // root is reshaped to match. Always a freshly built list, never the caller's own
            // collection, whatever the count.
            var list = CollectionOperandUtils.TryGetCount(source, out var capacity)
                ? new List<object>(capacity)
                : new List<object>();

            foreach (object item in source)
            {
                list.Add(item);
            }

            list.Sort(GetComparer(source).Compare);
            if (!sortAscending)
            {
                list.Reverse();
            }

            return list;
        }

        private static IComparer GetComparer(IEnumerable source)
        {
            // Comparer<T>.Default reaches IComparable<T> where the item type implements it; boxed
            // values compared as object only ever reach the non-generic IComparable. Only the
            // ordering is item-typed - the result stays a List<object>.
            if (MethodBaseHelpers.IsGenericEnumerable(source.GetType(), out Type itemType))
                return Comparers.GetOrAdd(itemType, CreateComparer);

            return Comparer.Default;
        }

        /// <remarks>
        /// A type this engine already treats as a number sorts too, even without
        /// <see cref="IComparable"/> - see <c>CompareUtils.RequiresConversionToOrder</c>, which is where
        /// the rule lives so that the compiled sort asks the same question.
        /// </remarks>
        private static IComparer CreateComparer(Type itemType)
        {
            if (CompareUtils.RequiresConversionToOrder(itemType))
                return ConvertingComparer.Instance;

            return (IComparer)typeof(Comparer<>).MakeGenericType(itemType)
                .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null, null);
        }

        /// <summary>
        /// Orders by what the items convert to, which is what <c>CompareUtils.Compare</c> does for the
        /// same values - so <c>sort()</c>, <c>min()</c>, <c>max()</c> and <c>between</c> agree on one
        /// notion of order for these types.
        /// </summary>
        private sealed class ConvertingComparer : IComparer
        {
            public static readonly ConvertingComparer Instance = new ConvertingComparer();

            public int Compare(object x, object y)
            {
                return CompareUtils.Compare(x, y);
            }
        }

        private static readonly ConcurrentDictionary<Type, IComparer> Comparers
            = new ConcurrentDictionary<Type, IComparer>();
    }
}

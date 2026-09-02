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

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using SpringCollections;
using SpringExpressions.Expressions.LinqExpressionHelpers;

namespace SpringExpressions.Processors
{
    /// <summary>
    /// Implementation of the distinct processor.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class DistinctProcessor : ICollectionProcessor
    {
        /// <summary>
        /// Returns distinct items from the collection.
        /// </summary>
        /// <param name="source">
        /// The source collection to process.
        /// </param>
        /// <param name="args">
        /// 0: boolean flag specifying whether to include <c>null</c>
        /// in the results or not. Default is false, which means that
        /// <c>null</c> values will not be included in the results.
        /// </param>
        /// <returns>
        /// A collection containing distinct source collection elements.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If there is more than one argument, or if the single optional argument 
        /// is not <b>Boolean</b>.
        /// </exception>
        public object Process(IEnumerable source, object[] args)
        {
            if (source == null)
            {
                return null;
            }
            
            bool includeNulls = false;
            if (args.Length == 1)
            {
                if (args[0] is bool)
                {
                    includeNulls = (bool) args[0];
                }
                else
                {
                    throw new ArgumentException("distinct() processor argument must be a boolean value.");
                }
            }
            else if (args.Length > 1)
            {
                throw new ArgumentException("Only a single argument can be specified for a distinct() processor.");
            }

            // List<object>: distinct() is an order-preserving dedup rather than a set constructor - this
            // was a HybridSet, the last one left in an operator or processor result. The weakly typed
            // path returns object-typed collections for every result the engine builds, and the compiled
            // root is reshaped to match.
            var seen = new HashSet<object>(GetEqualityComparer(source));
            var distinct = new List<object>();

            foreach (var element in source)
            {
                if (element == null && !includeNulls)
                    continue;

                if (seen.Add(element))
                    distinct.Add(element);
            }

            return distinct;
        }

        private static IEqualityComparer<object> GetEqualityComparer(IEnumerable source)
        {
            // EqualityComparer<T>.Default reaches IEquatable<T> where the item type implements it;
            // boxed values compared as object never would. Only the equality is item-typed - the
            // result stays a List<object>.
            if (MethodBaseHelpers.IsGenericEnumerable(source.GetType(), out Type itemType))
                return Comparers.GetOrAdd(itemType, CreateComparer);

            return EqualityComparer<object>.Default;
        }

        private static IEqualityComparer<object> CreateComparer(Type itemType)
        {
            var itemTyped = (IEqualityComparer)typeof(EqualityComparer<>).MakeGenericType(itemType)
                .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null, null);

            return new NonGenericEqualityComparerAdapter(itemTyped);
        }

        private sealed class NonGenericEqualityComparerAdapter : IEqualityComparer<object>
        {
            private readonly IEqualityComparer _itemTyped;

            public NonGenericEqualityComparerAdapter(IEqualityComparer itemTyped)
            {
                _itemTyped = itemTyped;
            }

            bool IEqualityComparer<object>.Equals(object x, object y)
                => _itemTyped.Equals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj)
                => _itemTyped.GetHashCode(obj);
        }

        private static readonly ConcurrentDictionary<Type, IEqualityComparer<object>> Comparers
            = new ConcurrentDictionary<Type, IEqualityComparer<object>>();
    }
}
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
using System.Linq;
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
        public object Process(ICollection source, object[] args)
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

            if (MethodBaseHelpers.IsGenericEnumerable(source.GetType(), out Type itemType))
            {
                // what comes as generics leaves as generics.
                var method = _methods.GetOrAdd(itemType, CreateMethod);
                return method(source, includeNulls);
            }

            HybridSet set = new HybridSet(source);
            if (!includeNulls)
            {
                set.Remove(null);
            }

            return set;
        }

        private static object DistinctNullsWithCast<T>(ICollection collection, bool includeNulls)
        {
            var cast = (IEnumerable<T>) collection;
            if (includeNulls)
                return new List<T>(cast.Distinct());

            return new List<T>(from it in cast.Distinct() where it != null select it);
        }

        private static readonly MethodInfo MiDistinctNullsWithCast = typeof(DistinctProcessor)
            .GetMethod(nameof(DistinctNullsWithCast), BindingFlags.Static | BindingFlags.NonPublic);

        static DistinctProcessor()
        {
            AddMethodForType<int>();
            AddMethodForType<decimal>();
            AddMethodForType<double>();
            AddMethodForType<float>();
            AddMethodForType<long>();
            AddMethodForType<DateTime>();
            AddMethodForType<TimeSpan>();
            AddMethodForType<string>();
            AddMethodForType<ulong>();
            AddMethodForType<uint>();
            AddMethodForType<short>();
            AddMethodForType<ushort>();
            AddMethodForType<byte>();
            AddMethodForType<sbyte>();
            AddMethodForType<char>();
            AddMethodForType<bool>();

            AddMethodForType<int?>();
            AddMethodForType<decimal?>();
            AddMethodForType<double?>();
            AddMethodForType<float?>();
            AddMethodForType<long?>();
            AddMethodForType<DateTime?>();
            AddMethodForType<TimeSpan?>();
            AddMethodForType<ulong?>();
            AddMethodForType<uint?>();
            AddMethodForType<short?>();
            AddMethodForType<ushort?>();
            AddMethodForType<byte?>();
            AddMethodForType<sbyte?>();
            AddMethodForType<char?>();
            AddMethodForType<bool?>();
        }

        private static void AddMethodForType<T>()
        {
            _methods[typeof(T)] = DistinctNullsWithCast<T>;
        }

        private static Func<ICollection, bool, object> CreateMethod(Type itemType)
        {
            var genericMethod = MiDistinctNullsWithCast.MakeGenericMethod(itemType);
            return (Func<ICollection, bool, object>)Delegate
                .CreateDelegate(typeof(Func<ICollection, bool, object>), genericMethod);
        }

        private static readonly ConcurrentDictionary<Type, Func<ICollection, bool, object>> _methods
            = new ConcurrentDictionary<Type, Func<ICollection, bool, object>>();

    }
}
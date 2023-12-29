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
using System.Linq;
using System.Reflection;
using SpringCollections;
using SpringExpressions.Expressions.LinqExpressionHelpers;

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
        /// An array containing sorted collection elements.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If <paramref name="source"/> collection is not empty and it is 
        /// neither <see cref="IList"/> nor <see cref="ISet"/>.
        /// </exception>
        public object Process(ICollection source, object[] args)
        {
            if (source == null || source.Count == 0)
            {
                return source;
            }

            bool sortAscending = true;
            if (args != null && args.Length == 1 && args[0] is bool)
            {
                sortAscending = (bool) args[0];
            }

            if (MethodBaseHelpers.IsGenericEnumerable(source.GetType(), out Type itemType))
            {
                // what comes as generics leaves as generics.
                var method = Methods.GetOrAdd(itemType, CreateMethod);
                return method(source, sortAscending);
            }


            ArrayList list = new ArrayList(source);
            list.Sort();
            if (!sortAscending)
            {
                list.Reverse();
            }

            // todo: error: why?-------------------------------------------------------------------------------------
            Type elementType = DetermineElementType(list);
            return list.ToArray(elementType);
        }

        private Type DetermineElementType(IList list)
        {
            for (int i=0; i<list.Count; i++)
            {
                object element = list[i];
                if (element != null) return element.GetType();
            }
            return typeof (object);
        }


        static SortProcessor()
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

        private static object SortWithCast<T>(ICollection collection, bool sortAscending)
        {
            var cast = (IEnumerable<T>)collection;
            var result = new List<T>(cast);
            result.Sort();

            if (!sortAscending)
                result.Reverse();

            return result;
        }

        private static readonly MethodInfo MiSortWithCast = typeof(SortProcessor)
            .GetMethod(nameof(SortWithCast), BindingFlags.Static | BindingFlags.NonPublic);

        private static void AddMethodForType<T>()
        { Methods[typeof(T)] = SortWithCast<T>; }

        private static Func<ICollection, bool, object> CreateMethod(Type itemType)
        {
            var genericMethod = MiSortWithCast.MakeGenericMethod(itemType);
            return (Func<ICollection, bool, object>)Delegate
                .CreateDelegate(typeof(Func<ICollection, bool, object>), genericMethod);
        }

        private static readonly ConcurrentDictionary<Type, Func<ICollection, bool, object>> Methods
            = new ConcurrentDictionary<Type, Func<ICollection, bool, object>>();

    }
}

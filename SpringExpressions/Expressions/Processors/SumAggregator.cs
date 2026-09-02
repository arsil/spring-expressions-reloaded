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
using SpringUtil;

namespace SpringExpressions.Processors
{
    /// <summary>
    /// Implementation of the sum aggregator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class SumAggregator : ICollectionProcessor
    {
        /// <summary>
        /// Returns the sum of the numeric values in the source collection.
        /// </summary>
        /// <param name="source">
        /// The source collection to process.
        /// </param>
        /// <param name="args">
        /// Ignored.
        /// </param>
        /// <returns>
        /// The sum of the numeric values in the source collection.
        /// </returns>
        public object Process(IEnumerable source, object[] args)
        {
            // sum() is a fold of '+', so the accumulator starts as the first item and the operator's
            // own binary numeric promotion decides the running type from there: NumberUtils.Add runs
            // the table generated from PromoteNumericType, which is the same promotion the compiled
            // path emits, so the two backends agree by construction rather than by coincidence.
            //
            // Seeding at 0d instead made every collection answer Double whatever its item type, where
            // Enumerable.Sum(IEnumerable<int>) answers Int32, as C# does. Nothing about the promotion
            // is decided here: a byte collection answers Int32 because 'byte + byte' is Int32, and a
            // decimal meeting a double answers Decimal because this fork ruled that for '+'.
            //
            // The seed is normalized for a custom real-valued type, so a caller's struct with an
            // implicit conversion to decimal accumulates in decimal - exactly what the old 0d/0m
            // family choice arranged, and the reason it called this method too.
            object total = null;
            foreach (object item in source)
            {
                if (item == null)
                    continue;

                if (!TypeCheckingUtils.IsNumber(item))
                {
                    throw new ArgumentException("Sum can only be calculated for a collection of numeric values.");
                }

                total = total == null
                    ? NumberUtils.ToBuiltInRealIfPossible(item)
                    : NumberUtils.Add(total, item);
            }

            return total ?? ZeroForEmptySource(source);
        }

        /// <summary>
        /// The sum of nothing: <c>default(T)</c> of the source's item type where that is a number, and
        /// <c>0d</c> where the source has no item type to read.
        /// </summary>
        /// <remarks>
        /// <p>
        /// A fold has no seed when nothing was added - an empty collection, or one holding only nulls -
        /// so the type has to come from the source rather than from an item. That is the one thing this
        /// aggregator asks the source's <i>type</i> about; everything else it does works on values.
        /// </p>
        /// <p>
        /// The answers match <see cref="System.Linq.Enumerable"/> overload for overload, which is what
        /// keeps the backends level: <c>Sum(IEnumerable&lt;int&gt;)</c> and
        /// <c>Sum(IEnumerable&lt;int?&gt;)</c> both answer <c>0</c> rather than throwing or answering
        /// null, unlike <c>Min</c> and <c>Average</c> - LINQ makes them differ and so does this engine.
        /// A nullable item type is unwrapped for that reason.
        /// </p>
        /// <p>
        /// <b>Only a numeric item type is used.</b> A <c>List&lt;string&gt;</c> or
        /// <c>List&lt;object&gt;</c> falls through to <c>0d</c>, as does a non-generic
        /// <see cref="System.Collections.ArrayList"/> or a bare <see cref="IEnumerable"/>, which have no
        /// item type at all. So the language answers two types for the sum of nothing depending on how
        /// the collection was declared - <c>Int32:0</c> for an empty <c>List&lt;int&gt;</c>,
        /// <c>Double:0</c> for an empty <c>ArrayList</c>. Neither diverges, since each agrees with what
        /// the compiled path does for the same source, and there is nothing better available: at
        /// evaluation an empty untyped collection cannot say what it would have held.
        /// </p>
        /// </remarks>
        private static object ZeroForEmptySource(IEnumerable source)
        {
            var itemType = CollectionOperandUtils.GetEnumerableItemType(source.GetType());
            if (itemType == null)
                return 0d;

            switch (Type.GetTypeCode(Nullable.GetUnderlyingType(itemType) ?? itemType))
            {
                case TypeCode.Int32: return 0;
                case TypeCode.Int64: return 0L;
                case TypeCode.UInt32: return 0u;
                case TypeCode.UInt64: return 0UL;
                case TypeCode.Int16: return (short)0;
                case TypeCode.UInt16: return (ushort)0;
                case TypeCode.Byte: return (byte)0;
                case TypeCode.SByte: return (sbyte)0;
                case TypeCode.Single: return 0f;
                case TypeCode.Double: return 0d;
                case TypeCode.Decimal: return 0m;
                default: return 0d;
            }
        }
    }
}

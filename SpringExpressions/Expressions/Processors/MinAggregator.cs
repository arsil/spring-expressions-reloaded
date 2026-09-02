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

using System.Collections;
using SpringUtil;

namespace SpringExpressions.Processors
{
    /// <summary>
    /// Implementation of the minimum aggregator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class MinAggregator : ICollectionProcessor
    {
        /// <summary>
        /// Returns the smallest item in the source collection.
        /// </summary>
        /// <param name="source">
        /// The source collection to process.
        /// </param>
        /// <param name="args">
        /// Ignored.
        /// </param>
        /// <returns>
        /// The smallest item in the source collection.
        /// </returns>
        public object Process(IEnumerable source, object[] args)
        {
            // A null collection has nothing to take a minimum of, and null is how this engine says
            // there is no answer - the empty-collection ruling decided that, and an empty source and an
            // absent one are the same situation. Without the guard the foreach below dereferenced the
            // null: 'null.min()' was a NullReferenceException on both backends, which is a missing
            // check rather than a decision, since the six collection-returning processors have had
            // theirs all along.
            if (source == null)
                return null;

            // A null item is skipped, as Enumerable.Min skips it. Without that the accumulator holds
            // null after the first null item and stays there: CompareUtils.Compare is a sorting
            // function, so it calls null the smaller of every pair and nothing can displace it - which
            // made min() over a null-bearing collection answer the *maximum*.
            //
            // A NaN is not skipped, and must not be: Enumerable.Min answers NaN if any item is one, and
            // the sorting convention already lands on that.
            object minItem = null;
            foreach (object item in source)
            {
                if (item == null)
                    continue;

                if (minItem == null || CompareUtils.Compare(minItem, item) > 0)
                {
                    minItem = item;
                }
            }
            return minItem;
        }
    }
}

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
    /// Implementation of the count aggregator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class CountAggregator : ICollectionProcessor
    {
        /// <summary>
        /// Returns the number of items in the source collection.
        /// </summary>
        /// <param name="source">
        /// The source collection to process.
        /// </param>
        /// <param name="args">
        /// Ignored.
        /// </param>
        /// <returns>
        /// The number of items in the source collection, 
        /// or zero if the collection is empty or <c>null</c>.
        /// </returns>
        public object Process(IEnumerable source, object[] args)
        {
            if (source == null)
            {
                return 0;
            }

            // An IEnumerable has no Count, so the O(1) answer comes from testing the runtime type for
            // the two counting interfaces - a HashSet<T> implements only the generic one, a Queue<T>
            // only the non-generic one. A source that can answer neither is walked, which is all that
            // can be done for a genuinely lazy sequence.
            if (CollectionOperandUtils.TryGetCount(source, out var count))
            {
                return count;
            }

            count = 0;
            foreach (var unused in source)
            {
                ++count;
            }

            return count;
        }
    }
}

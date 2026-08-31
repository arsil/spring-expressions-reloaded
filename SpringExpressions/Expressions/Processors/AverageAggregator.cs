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
    /// Implementation of the average aggregator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class AverageAggregator : ICollectionProcessor
    {
        /// <summary>
        /// Returns the average of the numeric values in the source collection.
        /// </summary>
        /// <param name="source">
        /// The source collection to process.
        /// </param>
        /// <param name="args">
        /// Ignored.
        /// </param>
        /// <returns>
        /// The average of the numeric values in the source collection.
        /// </returns>
        public object Process(ICollection source, object[] args)
        {
            // The accumulator is seeded from the first item's family: decimals accumulate in decimal -
            // 0d + 1.5m is exactly the decimal-double promotion the engine refuses - and everything
            // else in double, as it always did.
            int n = 0;
            object total = null;
            foreach (object item in source)
            {
                if (item != null)
                {
                    if (TypeCheckingUtils.IsNumber(item))
                    {
                        if (total == null)
                            total = NumberUtils.ToBuiltInRealIfPossible(item) is decimal ? (object)0m : (object)0d;

                        total = NumberUtils.Add(total, item);
                        n++;
                    }
                    else
                    {
                        throw new ArgumentException("Average can only be calculated for a collection of numeric values.");
                    }
                }
            }

            // With nothing counted - an empty collection, or one holding only nulls - the answer is
            // null, which is what Enumerable.Average gives for a nullable sequence and what the
            // compiled path already gave for one. Dividing by zero handed back NaN, which is not the
            // average of anything.
            if (n == 0)
                return null;

            return NumberUtils.Divide(total, n);
        }
    }
}

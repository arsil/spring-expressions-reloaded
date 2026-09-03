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
using JetBrains.Annotations;
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
        public object Process(IEnumerable source, object[] args)
        {
            // A null collection has no average - the same reasoning as MinAggregator, and it lands on
            // the answer this processor already gives when nothing was counted.
            if (source == null)
                return null;

            // Decimals accumulate in decimal - 0d + 1.5m is exactly the decimal-double promotion the
            // engine refuses - and everything else, floats included, accumulates in double.
            //
            // A float collection answers a Single, but the accumulation is NOT done in float, and the
            // difference is not academic. Enumerable.Average(IEnumerable<float>) sums in double and
            // narrows only the quotient, so accumulating in float diverges from it as soon as the
            // running total passes float's exactly-representable integer range (2^24) or simply
            // accumulates rounding:
            //
            //   {0.1f x 10}       narrowed quotient 0.1          float accumulation 0.10000001
            //   {1e8f, 1f x 9}    narrowed quotient 10000001     float accumulation 10000000
            //
            // The first is ordinary data, not a contrived edge. A float seed was written here and
            // measured against {1e7f, 1f x 9}, which proved nothing: 1e7 is *below* 2^24, so no addend
            // was lost and both routes agreed. That is why the family below names only decimal.
            int n = 0;
            var everyItemIsAFloat = true;
            object total = null;
            foreach (object item in source)
            {
                if (item != null)
                {
                    if (TypeCheckingUtils.IsNumber(item))
                    {
                        var normalized = NumberUtils.ToBuiltInRealIfPossible(item);

                        // Every item, not just the first. A float meeting anything wider is that wider
                        // type - '1f + 2.0' is a double - so a collection holding both averages to a
                        // double, which is what the promotion rules say and what sum() answers for the
                        // same items. Deciding from the first item alone narrowed {1f, 2.0} to a float.
                        everyItemIsAFloat &= normalized is float;

                        if (total == null)
                            total = normalized is decimal ? (object)0m : (object)0d;

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

            var average = NumberUtils.Divide(total, n);

            // The quotient narrows last, which is the whole of what Enumerable.Average does for floats.
            // The family is read from the items' *values*, as the decimal family always has been, so a
            // List<object> holding nothing but floats narrows too - one rule, not two.
            return everyItemIsAFloat && average is double asDouble ? (float)asDouble : average;
        }
    }
}

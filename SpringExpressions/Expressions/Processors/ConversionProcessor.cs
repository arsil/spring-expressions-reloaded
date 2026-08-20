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
using System.Collections.Generic;
using SpringCore.TypeConversion;

#endregion

namespace SpringExpressions.Processors
{
    /// <summary>
    /// Converts all elements in the input list to a given target type.
    /// </summary>
    /// <author>Erich Eichinger</author>
    public class ConversionProcessor : ICollectionProcessor
    {
        /// <summary>
        /// Processes a list of source items and returns a result.
        /// </summary>
        /// <param name="source">
        /// The source list to process.
        /// </param>
        /// <param name="args">
        /// An optional processor arguments array.
        /// </param>
        /// <returns>
        /// The processing result.
        /// </returns>
        public object Process(ICollection source, object[] args)
        {
            if (source == null)
            {
                return source;
            }

            // Argument validation before the data check: bad arguments must not pass just because the
            // source happened to be empty.
            Type targetType;
            if (args == null || args.Length == 0)
            {
                throw new ArgumentNullException("args", "convert() processor requires a Type value argument.");
            }
            else if (args.Length > 1)
            {
                throw new ArgumentException("Only a single argument can be specified for a convert() processor.");
            }
            else if (args[0] is Type)
            {
                targetType = (Type)args[0];
            }
            else
            {
                throw new ArgumentException("convert() processor argument must be a Type value.");
            }

            // List<T>, not a typed array: convert(T) is a typed request written in the expression
            // language, so it returns what every typed request returns. Always a freshly built list,
            // never the caller's own collection, whatever the Count.
            var result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(targetType));
            foreach (object val in source)
            {
                result.Add(TypeConversionUtils.ConvertValueIfNecessary(targetType, val, null));
            }

            return result;
        }
    }
}
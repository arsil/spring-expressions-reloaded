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


namespace SpringCore.TypeResolution
{
    /// <summary>
    /// Helper methods with regard to type resolution.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Not intended to be used directly by applications.
    /// </p>
    /// </remarks>
    /// <author>Bruno Baia</author>
    public sealed class TypeResolutionUtils
    {
        #region Fields

        private static readonly ITypeResolver internalTypeResolver
            = new CachedTypeResolver(new GenericTypeResolver());

        #endregion

        #region Constructor (s) / Destructor

        // CLOVER:OFF

        /// <summary>
        /// Creates a new instance of the <see cref="SpringCore.TypeResolution.TypeResolutionUtils"/> class.
        /// </summary>
        /// <remarks>
        /// <p>
        /// This is a utility class, and as such exposes no public constructors.
        /// </p>
        /// </remarks>
        private TypeResolutionUtils()
        {
        }

        // CLOVER:ON

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the supplied type name into a <see cref="System.Type"/>
        /// instance.
        /// </summary>
        /// <remarks>
        /// <p>
        /// If you require special <see cref="System.Type"/> resolution, do
        /// <b>not</b> use this method, but rather instantiate
        /// your own <see cref="SpringCore.TypeResolution.TypeResolver"/>.
        /// </p>
        /// </remarks>
        /// <param name="typeName">
        /// The (possibly partially assembly qualified) name of a
        /// <see cref="System.Type"/>.
        /// </param>
        /// <returns>
        /// A resolved <see cref="System.Type"/> instance.
        /// </returns>
        /// <exception cref="System.TypeLoadException">
        /// If the type cannot be resolved.
        /// </exception>
        public static Type ResolveType(string typeName)
        {
            // todo: error: fixme: alias[]   alias[,]    alias[][]
            // todo: error: fixme: List<alias[]>.... Map<alias, alias[][]> etc...

            return TypeRegistry.ResolveType(typeName)
                ?? internalTypeResolver.Resolve(typeName);
        }

        #endregion
    }
}
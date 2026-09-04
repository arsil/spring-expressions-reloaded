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

using JetBrains.Annotations;

using SpringExpressions;
using SpringUtil;


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

        /// <summary>
        /// Resolves a type name written in an <i>expression</i>, subject to
        /// <paramref name="sandboxPolicy"/>.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>Every type name the expression language resolves comes through here, and nothing else
        /// does.</b> <see cref="ResolveType(string)"/> stays ungated on purpose: the library's own
        /// plumbing calls it - <c>TypeConverterRegistry</c>, <c>ResourceManagerConverter</c> and
        /// <c>RuntimeTypeConverter</c> resolving converter types from configuration - and that runs at
        /// configuration time on the engineer's behalf, so policing it with a policy meant for scripts
        /// would break startup under the default. Two entry points, therefore, rather than one gate
        /// inside the existing method. See <c>_Docs/type-sandboxing.md</c> §4.1.
        /// </p>
        /// <p>
        /// <b>A <see cref="TypeRegistry"/> entry resolves unrestricted and is never asked about.</b>
        /// The registry is already the engineer's own allow-list, which is what §3.1 rules: registered
        /// names are the language's vocabulary, and a registration deliberately overrides. That is also
        /// why the gate sits between the two halves of <see cref="ResolveType(string)"/> rather than
        /// wrapping it.
        /// </p>
        /// <p>
        /// The check runs on the resolved <see cref="Type"/> every call, so
        /// <c>CachedTypeResolver</c>'s memoisation of name to type is harmless: a name resolved once
        /// under a permissive policy is still judged under the strict one next time. Generic arguments
        /// and array item types are <i>not</i> reached from here - <c>GenericTypeResolver</c> calls the
        /// ungated <see cref="ResolveType(string)"/> for each of them - so the verdict itself
        /// decomposes composite types. Measured; §4.1 assumed otherwise.
        /// </p>
        /// </remarks>
        /// <exception cref="SandboxViolationException">
        /// If the policy does not permit the resolved type, or any part of it.
        /// </exception>
        /// <exception cref="System.TypeLoadException">If the type cannot be resolved at all.</exception>
        [NotNull]
        public static Type ResolveTypeForExpression(
            [NotNull] string typeName, [NotNull] SandboxPolicy sandboxPolicy)
        {
            AssertUtils.ArgumentNotNull(sandboxPolicy, "sandboxPolicy");

            var registered = TypeRegistry.ResolveType(typeName);

            if (registered != null)
                return registered;

            var resolved = internalTypeResolver.Resolve(typeName);

            sandboxPolicy.RequirePermittedType(resolved);

            return resolved;
        }

        #endregion
    }
}
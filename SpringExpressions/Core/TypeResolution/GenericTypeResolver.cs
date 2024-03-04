#region License

/*
 * Copyright � 2002-2011 the original author or authors.
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
using System.Linq;
using SpringExpressions.Core.TypeResolution;
using SpringUtil;

#endregion

namespace SpringCore.TypeResolution
{
    /// <summary>
    /// Resolves a generic <see cref="System.Type"/> by name.
    /// </summary>
    /// <author>Bruno Baia</author>
    public class GenericTypeResolver : TypeResolver
    {
        /// <summary>
        /// Resolves the supplied generic <paramref name="typeName"/> to a
        /// <see cref="System.Type"/> instance.
        /// </summary>
        /// <param name="typeName">
        /// The unresolved (possibly generic) name of a <see cref="System.Type"/>.
        /// </param>
        /// <returns>
        /// A resolved <see cref="System.Type"/> instance.
        /// </returns>
        /// <exception cref="System.TypeLoadException">
        /// If the supplied <paramref name="typeName"/> could not be resolved
        /// to a <see cref="System.Type"/>.
        /// </exception>
        public override Type Resolve(string typeName)
        {
            if (StringUtils.IsNullOrEmpty(typeName))
                throw BuildTypeLoadException(typeName);

            Type type = null;
            try
            {
                if (GenericArgumentsHolder.TryCreateGenericArgumentsHolder(typeName, out var genericInfo))
                {
                    type = TypeResolutionUtils.ResolveType(genericInfo.GenericTypeName);
                    if (!genericInfo.IsGenericDefinition)
                    {
                        string[] unresolvedGenericArgs = genericInfo.GetGenericArguments();
                        Type[] genericArgs = new Type[unresolvedGenericArgs.Length];
                        for (int i = 0; i < unresolvedGenericArgs.Length; i++)
                        {
                            genericArgs[i] = TypeResolutionUtils.ResolveType(unresolvedGenericArgs[i]);
                        }
                        type = type.MakeGenericType(genericArgs);
                    }

                    if (genericInfo.IsArrayDeclaration)
                    {
                        typeName = $"{type.FullName}{genericInfo.GetArrayDeclarationReversed()},{type.Assembly.FullName}";
                        type = null;
                    }
                }
                else if (ArrayArgumentHolder.TryCreateArrayArgumentHolder(typeName, out var arrayArgumentHolder))
                {
                    type = TypeResolutionUtils.ResolveType(arrayArgumentHolder.ArrayItemTypeName);

                    typeName = $"{type.FullName}{arrayArgumentHolder.ArrayDeclarationReversed},{type.Assembly.FullName}";
                    type = null;

                }
            }
            catch (Exception ex)
            {
                if (ex is TypeLoadException)
                {
                    throw;
                }
                throw BuildTypeLoadException(typeName, ex);
            }

            if (type == null)
            {
                // probably not generic type - try regular type resolution
                type = base.Resolve(typeName);
            }

            return type;
        }
    }
}

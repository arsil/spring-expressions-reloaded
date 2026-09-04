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

using SpringCore.TypeResolution;
using SpringUtil;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed attribute node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class AttributeNode : ConstructorNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public AttributeNode()
        {
        }

                /// <summary>
        /// Tries to determine attribute type based on the specified
        /// attribute type name.
        /// </summary>
        /// <param name="typeName">
        /// Attribute type name to resolve.
        /// </param>
        /// <returns>
        /// Resolved attribute type.
        /// </returns>
        /// <exception cref="TypeLoadException">
        /// If type cannot be resolved.
        /// </exception>
        /// <remarks>
        /// The <c>"Attribute"</c> retry is a resolution path like any other and is gated identically -
        /// ruled at stage 2 of <c>_Docs/type-sandboxing.md</c> §8.4, which listed this node's verdict as
        /// something to decide before the type gate landed. Attribute types are a small surface, but an
        /// ungated second attempt would be a way of naming a type the first attempt was refused.
        /// </remarks>
        protected override Type GetObjectType(string typeName, [NotNull] SandboxPolicy sandboxPolicy)
        {
            Type type;

            try
            {
                type = base.GetObjectType(typeName, sandboxPolicy);
            }
            catch (TypeLoadException)
            {
                if (typeName.EndsWith("Attribute"))
                {
                    throw;
                }
                type = TypeResolutionUtils.ResolveTypeForExpression(
                    typeName + "Attribute", sandboxPolicy);
            }

            return type;
        }
    }
}
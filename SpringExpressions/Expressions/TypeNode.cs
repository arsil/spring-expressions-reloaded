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
using SpringCore.TypeResolution;
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed type node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class TypeNode : BaseNode
    {
        private Type type;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public TypeNode()
            : base()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            if (type == null)
            {
                lock (this)
                {
                    try
                    {
                        type = TypeResolutionUtils.ResolveTypeForExpression(
                            getText(), compilationContext.SandboxPolicy);
                    }
                    catch (TypeLoadException)
                    {
                        // The same refusal CastNode and ConstructorNode already give for an
                        // unresolvable name; this node was missed when they were converted. Left to
                        // escape, the TypeLoadException is absorbed as an internal compiler error - so
                        // 'T(Nope)' told the caller that *we* were broken and asked them to report a
                        // typo in their own expression. The interpreter reports the unresolvable name
                        // at evaluation, as it always has.
                        throw CannotCompile("the type name does not resolve");
                    }
                }
            }

            return LExpression.Constant(type, typeof(Type));
        }

        /// <summary>
        /// Returns node's value for the given context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            if (type == null)
            {
                lock(this)
                {
                    type = TypeResolutionUtils.ResolveTypeForExpression(
                        getText(), evalContext.SandboxPolicy);
                }
            }

            return type;
        }

        /// <summary>
        /// Overrides getText to allow easy way to get fully 
        /// qualified typename.
        /// </summary>
        /// <returns>
        /// Fully qualified typename as a string.
        /// </returns>
        public override string getText()
        {
            string tmp = base.getText();
//            if (tmp != null && TypeRegistry.ContainsAlias(tmp))
//            {
//                Type type = TypeRegistry.ResolveType(tmp);
//                if (type != null)
//                {
//                    tmp = type.AssemblyQualifiedName;
//                }                
//            }
            AST node = this.getFirstChild();
            while (node != null)
            {
                tmp += node.getText();
                node = node.getNextSibling();
            }
            return tmp;
        }
    }
}
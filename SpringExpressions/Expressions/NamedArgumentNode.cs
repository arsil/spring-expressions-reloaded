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

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed named argument node in the expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class NamedArgumentNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public NamedArgumentNode()
        {
        }

        /// <summary>
        /// A named argument has no value of its own to emit: it is a member assignment on whatever is
        /// being constructed, so only the node that constructs can express it - ConstructorNode splits
        /// these out and emits them as MemberInit bindings.
        /// </summary>
        /// <remarks>
        /// This used to return the emitted *value*, discarding the name, which the author marked "this
        /// won't work! it is a hack!!!!!!" - and it did not: the value was then passed positionally, so
        /// "new Inventor(Nationality = 'x', DOB = ..., Name = 'y')" built an Inventor with the wrong
        /// fields wherever the types happened to line up. Refusing is what makes any remaining caller
        /// fall back to the interpreter rather than build the wrong object silently.
        /// </remarks>
        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            throw CannotCompile("a named argument is only meaningful to the node that constructs");
        }

        /// <summary>
        /// Returns the value of the named argument defined by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            return GetValue(((BaseNode) this.getFirstChild()), evalContext.RootContext, evalContext);
        }
    }
}
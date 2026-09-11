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
using System.Collections.Generic;
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed expression list node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class ExpressionListNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public ExpressionListNode()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var  expressions = new List<LExpression>();
            AST node = getFirstChild();
            while (node != null)
            {
                var expression = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);

                expressions.Add(expression);

                node = node.getNextSibling();
            }

            var block = LExpression.Block(expressions);

            // A list's value is its last element's value, so if that element built a collection then
            // so did the list.
            //
            // Without this the registration is lost the moment a projection is wrapped. Compiler asks
            // whether the *root* expression was registered, and ProjectionNode registers its own
            // call - so in '(1; Words.!{#this})' the root is this block, which nobody registered, the
            // reshaping is skipped, and a List<string> escapes where the interpreter answers
            // List<object>.
            //
            // The test is on the last element having been *registered*, not on it being a collection,
            // and that is what keeps a read collection out: '(1; SomeListProperty)' hands back the
            // caller's own object, reference identity and all, exactly as 'SomeListProperty' does.
            // Claiming it would copy it.
            if (expressions.Count > 0
                && compilationContext.IsConstructedCollection(expressions[expressions.Count - 1]))
            {
                compilationContext.MarkAsConstructedCollection(block);
            }

            return block;
        }

        /// <summary>
        /// Returns a result of the last expression in a list.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Result of the last expression in a list</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object result = context;

            AST node = this.getFirstChild();
            while (node != null)
            {
                result = GetValue(((BaseNode) node), context, evalContext);
                node = node.getNextSibling();
            }
            return result;
        }
    }
}
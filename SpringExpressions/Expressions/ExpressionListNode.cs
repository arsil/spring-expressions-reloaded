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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed expression list node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class ExpressionListNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public ExpressionListNode()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected ExpressionListNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
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

                if (expression == null)
                    return null;

                expressions.Add(expression);

                node = node.getNextSibling();
            }

            return LExpression.Block(expressions);
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
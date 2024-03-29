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
using System.Runtime.Serialization;
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents ternary expression node.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class TernaryNode : BaseNode
    {
        private bool initialized = false;
        private BaseNode condition;
        private BaseNode trueExp;
        private BaseNode falseExp;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public TernaryNode():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected TernaryNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
        
        /// <summary>
        /// Returns a value for the string literal node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            if (!initialized)
            {
                lock (this)
                {
                    if (!initialized)
                    {
                        AST node = this.getFirstChild();
                        condition = (BaseNode) node;
                        node = node.getNextSibling();
                        trueExp = (BaseNode) node;
                        node = node.getNextSibling();
                        falseExp = (BaseNode) node;

                        initialized = true;
                    }
                }
            }

            if (Convert.ToBoolean(GetValue(condition, context, evalContext)))
            {
                return GetValue(trueExp, context, evalContext);
            }
            else
            {
                return GetValue(falseExp, context, evalContext);
            }
        }

        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            AST node = getFirstChild();
            var conditionExpression = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);
            if (conditionExpression == null)
                return null;

			node = node.getNextSibling();
            var trueExpression = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);
            if (trueExpression == null)
                return null;

            node = node.getNextSibling();
            var falseExpression = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);
            if (falseExpression == null)
                return null;

            return LExpression.Condition(conditionExpression, trueExpression, falseExpression);
        }
    }
}
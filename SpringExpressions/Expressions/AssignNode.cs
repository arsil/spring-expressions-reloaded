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
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed assignment node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class AssignNode : BaseNode
    {        
        /// <summary>
        /// Create a new instance
        /// </summary>
        public AssignNode()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression, CompilationContext compilationContext)
        {
            AST left = getFirstChild();
            AST right = left.getNextSibling();

            var rightExpression = GetExpressionTreeIfPossible((BaseNode)right, contextExpression, compilationContext);

               
                 // todo: error: lambda - wykrywanie lambda
                 // todo: erro: syfon jest jakiś...  bo context to chyba jest coś, do czego przypisujemy... nie? i to już jest zewaluowane? -----------------------------------------------------------------
            return GetExpressionTreeForSetterIfPossible(
                (BaseNode)left, contextExpression, compilationContext, rightExpression);
        }

        /// <summary>
        /// Assigns value of the right operand to the left one.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            AST left = getFirstChild();
            AST right = left.getNextSibling();

            object result;

            if (right.getFirstChild() is LambdaExpressionNode)
            {
                if (!(left.getFirstChild() is VariableNode))
                {
                    throw new ArgumentException("Lambda expression can only be assigned to a global variable.");
                }
                result = right.getFirstChild();
            }
            else
            {
                result = GetValue(((BaseNode)right), context, evalContext);
            }

            // An assignment evaluates to the value as the *target* holds it, not to the value as it
            // was read - which is C#'s rule ("the result has the same type as the left operand") and
            // what this engine's compiled path has always emitted, since a LINQ Assign yields the
            // assigned value. Returning the value read made 'Big = 5' into a long member answer
            // Int32 interpreted and Int64 compiled.
            return SetValue(((BaseNode)left), context, evalContext, result);
        }
    }
}
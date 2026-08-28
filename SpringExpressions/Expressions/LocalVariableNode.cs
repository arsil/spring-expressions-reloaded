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
using System.Reflection;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed variable node.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class LocalVariableNode : BaseNode
    {
        //internal const string LOCAL_VARIABLES = "__locals";
     
        /// <summary>
        /// Create a new instance
        /// </summary>
        public LocalVariableNode()
        {
        }

                /// <summary>
        /// Returns value of the local variable represented by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            string varName = this.getText();
            IDictionary locals = evalContext.LocalVariables;
            if (locals != null)
            {
                return locals[varName];
            }
            return null;
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var variableName = getText();

            // A lambda parameter is bound by the call, and the enclosing lambda already declared it.
            if (compilationContext.TryGetLocalVariable(variableName, out var variableExpression))
                return variableExpression;

            // Anything else is a free local: storage the expression owns for the length of one
            // evaluation, and one object-typed block variable per name is all that takes. It used to
            // refuse here, which meant an expression could assign to a local through the interpreter
            // and never through the compiled backend - and, since the interpreter answers null for a
            // local nothing has assigned to, refusing the read of an undefined one was refusing a
            // shape that has a perfectly good answer. An unassigned block variable is null, which is
            // that answer without a line of code.
            if (!compilationContext.TryGetLocalStorage(variableName, out var storage))
                throw CannotCompile(LocalsOutOfScopeReason);

            return storage;
        }

        protected override LExpression GetExpressionTreeForSetterIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext,
            LExpression newValueExpression)
        {
            var variableName = getText();

            // The interpreter writes a lambda parameter into the argument dictionary the call swapped
            // in, which the compiled form has no equivalent of - its parameters are the delegate's
            // own. Rather than assign to the ParameterExpression and hope the two stay level, the
            // shape is refused and the interpreter serves it.
            if (compilationContext.TryGetLocalVariable(variableName, out var _))
                throw CannotCompile("a lambda parameter is bound by the call and cannot be assigned to");

            if (!compilationContext.TryGetLocalStorage(variableName, out var storage))
                throw CannotCompile(LocalsOutOfScopeReason);

            // Assign is an expression and yields the value assigned, which is what the interpreter's
            // Set does and what '($x = 5) + $x' being ten depends on - so nothing has to be wrapped
            // around it to produce a value.
            //
            // The value is boxed on the way into the object-typed slot: LINQ inserts no boxing of its
            // own, so without this '$x = 5' would refuse while '$x = 'five'' compiled - the kind of
            // split that made the same assignment behave differently for no reason a caller could
            // see.
            return BuildAssign(storage, BoxIfValueType(newValueExpression));
        }

        private const string LocalsOutOfScopeReason
            = "a projection or selection body is compiled on its own and handed in as a delegate, so "
            + "local variables of the enclosing expression are not in scope there";

        private static LExpression BoxIfValueType([NotNull] LExpression expression)
        {
            return expression.Type.IsValueType
                ? LExpression.Convert(expression, typeof(object))
                : expression;
        }

        /// <summary>
        /// Sets value of the local variable represented by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <param name="newValue">New value for this node.</param>
        protected override void Set(object context, EvaluationContext evalContext, object newValue)
        {
            string varName = this.getText();
            IDictionary locals = evalContext.LocalVariables;
            if (locals == null)
            {
                locals = new Hashtable();
                evalContext.LocalVariables = locals;
            }
            locals[varName] = newValue;
        }
    }
}
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
using System.Linq.Expressions;
using JetBrains.Annotations;
using SpringExpressions.Expressions;
using SpringExpressions.Expressions.Compiling.Expressions;
using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Base type for all expression nodes.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    //[Serializable]
    public abstract class BaseNode : SpringAST
    {
        protected class ArgumentMismatchException : Exception
        {
            public ArgumentMismatchException(string message)
                : base(message)
            { }
        }

        #region EvaluationContext class

        /// <summary>
        /// Holds the state during evaluating an expression.
        /// </summary>
        protected internal class EvaluationContext
        {
            #region Holder classes

            private class ThisContextHolder : IDisposable
            {
                private readonly EvaluationContext owner;
                private readonly object savedThisContext;

                public ThisContextHolder(EvaluationContext owner)
                {
                    this.owner = owner;
                    this.savedThisContext = owner.ThisContext;
                }

                public void Dispose()
                {
                    owner.ThisContext = savedThisContext;
                }
            }

            private class LocalVariablesHolder : IDisposable
            {
                private readonly EvaluationContext owner;
                private readonly IDictionary savedLocalVariables;

                public LocalVariablesHolder(EvaluationContext owner, IDictionary newLocalVariables)
                {
                    this.owner = owner;
                    this.savedLocalVariables = owner.LocalVariables;
                    owner.LocalVariables = newLocalVariables;
                }

                public void Dispose()
                {
                    owner.LocalVariables = savedLocalVariables;
                }
            }

            #endregion

            /// <summary>
            /// Gets/Sets the root context of the current evaluation
            /// </summary>
            public object RootContext;

			/// <summary>
			/// Gets/Sets global variables of the current evaluation
			/// </summary>
			public IDictionary<string, object> Variables;

			/// <summary>
			/// Gets the type of the <see cref="RootContext"/>
			/// </summary>
			public Type RootContextType { get { return (RootContext == null) ? null : RootContext.GetType(); } }
            /// <summary>
            /// Gets/Sets the current context of the current evaluation
            /// </summary>
            public object ThisContext;

            /// <summary>
            /// Gets/Sets local variables of the current evaluation
            /// </summary>
            public IDictionary LocalVariables;

            /// <summary>
            /// Initializes a new EvaluationContext instance.
            /// </summary>
            /// <param name="rootContext">The root context for this evaluation</param>
            /// <param name="globalVariables">dictionary of global variables used during this evaluation</param>
            public EvaluationContext(object rootContext, IDictionary<string, object> globalVariables)
            {
                this.RootContext = rootContext;
                this.ThisContext = rootContext;
                this.Variables = globalVariables;
            }

			// An EvaluationContext is never reused across evaluations: it is mutable, so sharing one
			// between concurrent evaluations let them overwrite each other's root and variables.
			// The interpreter gets a fresh one per evaluation; compiled code needs none at all.

			/// <summary>
			/// Switches current ThisContext.
			/// </summary>
			public IDisposable SwitchThisContext()
            {
                return new ThisContextHolder(this);
            }

            /// <summary>
            /// Switches current LocalVariables.
            /// </summary>
            public IDisposable SwitchLocalVariables(IDictionary newLocalVariables)
            {
                return new LocalVariablesHolder(this, newLocalVariables);
            }
        }

        #endregion


		/// <summary>
		/// Create a new instance
		/// </summary>
		public BaseNode()
        { }

        // The object-typed context path lived here: it compiled for the runtime type of the first root
        // this node saw and reused that delegate forever. It belongs to the expression object now, which
        // keeps one compiled form per type - declared or discovered - so a second root type no longer
        // fails. See WeaklyTypedExpression.

           // todo: error?
        internal object GetValueUsingInterpreter(
            object context, EvaluationContext evaluationContext)
        {
            return Get(context, evaluationContext);
        }

        internal void SetValueUsingInterpreter(
            object context, EvaluationContext evaluationContext, object newValue)
        {
            Set(context, evaluationContext, newValue);
        }

        internal void ExecuteVoidExpressionUsingInterpreter(
            object context, EvaluationContext evaluationContext)
        {
            Get(context, evaluationContext);
        }


        // The typed compilation that used to live here - GetValue<TResult>, GetValue<TResult, TContext> and
        // the single object-typed slot they shared - is gone. A node cannot hold it: one slot serves one type
        // pair, so a second pair could only fail its cast, which is what the author's todos here said. The
        // expression object holds it now, one compiled form per context type. See WeaklyTypedExpression.

        /// <summary>
        /// Returns node's value for the given context.
        /// </summary>
        /// <returns>Node's value.</returns>
        protected abstract object Get(object context, EvaluationContext evalContext);

        /// <summary>
        /// Evaluates this node for the given context, switching local variables map to the ones specified in <paramref name="arguments"/>.
        /// </summary>
        protected virtual object Get(object context, EvaluationContext evalContext, object[] arguments)
        {
            throw new NotSupportedException("Node " + this.GetType() + " does not support evaluation with arguments");
        }

        /// <summary>
        /// Sets node's value for the given context.
        /// </summary>
        /// <param name="context">Object to evaluate node against.</param>
        /// <param name="newValue">New value for this node.</param>
        internal void SetValue(object context, object newValue)
        {
            SetValue(context, null, newValue);
        }

        /// <summary>
        /// Sets node's value for the given context.
        /// </summary>
        /// <param name="context">Object to evaluate node against.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <param name="newValue">New value for this node.</param>
        internal void SetValue(object context, IDictionary<string, object> variables, object newValue)
        {
            // No context type parameter: setting runs on the interpreter, which resolves members against the
            // runtime type, so a declared type would buy nothing here. It becomes useful once the setter is
            // compiled too.
            EvaluationContext evalContext = new EvaluationContext(context, variables);
            Set(context, evalContext, newValue);
        }

        /// <summary>
        /// Sets node's value for the given context.
        /// </summary>
        /// <remarks>
        /// <p>
        /// This is a default implementation of <c>Set</c> method, which
        /// simply throws <see cref="NotSupportedException"/>. 
        /// </p>
        /// <p>
        /// This was done in order to avoid redundant <c>Set</c> method implementations,
        /// because most of the node types do not support value setting.
        /// </p>
        /// </remarks>
        protected virtual void Set(object context, EvaluationContext evalContext, object newValue)
        {
            throw new NotSupportedException("You cannot set the value for the node of this type: [" + this.GetType().Name + "].");
        }

        /// <summary>
        /// Returns a string representation of this node instance.
        /// </summary>
        public override string ToString()
        {
            return string.Format("{0}[{1}]", this.GetType().Name, base.GetHashCode());
        }

        /// <summary>
        /// Evaluates this node, switching local variables map to the ones specified in <paramref name="arguments"/>.
        /// </summary>
        protected object GetValueWithArguments(BaseNode node, object context, EvaluationContext evalContext, object[] arguments)
        {
            return node.Get(context, evalContext, arguments);
        }

        protected object GetValue(BaseNode node, object context, EvaluationContext evalContext)
        {
            return node.Get(context, evalContext);
        }

        protected void SetValue(BaseNode node, object context, EvaluationContext evalContext, object newValue)
        {
            node.Set(context, evalContext, newValue);
        }

		[NotNull]
		protected internal static LExpression GetExpressionTreeIfPossible(
            [NotNull] BaseNode node,
            [NotNull] LExpression contextExpression,
            [NotNull] CompilationContext compilationContext)
		{
            var expression = node.GetExpressionTreeIfPossible(contextExpression, compilationContext);

            // Single enforcement point for the non-null contract. A node returning null instead of
            // throwing would otherwise be dereferenced by its caller and surface as a bare
            // NullReferenceException naming neither the node nor the reason.
            if (expression == null)
                throw new CompileErrorException(node, "node produced no expression tree");

            return expression;
		}

           // todo: rename?
        /// <summary>Builds the expression tree that reads this node's value.</summary>
        /// <remarks>Never returns null: a node that cannot be compiled throws CompileErrorException.</remarks>
        /// <exception cref="CompileErrorException">This node has no compiled implementation.</exception>
		[NotNull]
		protected virtual LExpression GetExpressionTreeIfPossible(
            [NotNull] LExpression contextExpression,
            [NotNull] CompilationContext compilationContext)
	    {
            throw CannotCompile("no compiled implementation for this node type");
        }

        /// <summary>Builds the expression tree that assigns to this node.</summary>
        /// <remarks>Never returns null; see GetExpressionTreeIfPossible.</remarks>
        /// <exception cref="CompileErrorException">This node cannot be assigned to in compiled form.</exception>
        [NotNull]
        protected virtual LExpression GetExpressionTreeForSetterIfPossible(
            [NotNull] LExpression contextExpression,
            [NotNull] CompilationContext compilationContext,
            [NotNull] LExpression newValueExpression)
        {
            throw CannotCompile("no compiled assignment implementation for this node type");
        }

        [NotNull]
        protected internal static LExpression GetExpressionTreeForSetterIfPossible(
            [NotNull] BaseNode node,
            [NotNull] LExpression contextExpression,
            [NotNull] CompilationContext compilationContext,
            [NotNull] LExpression newValueExpression)
        {
            var expression = node.GetExpressionTreeForSetterIfPossible(
                contextExpression, compilationContext, newValueExpression);

            if (expression == null)
                throw new CompileErrorException(node, "node produced no assignment expression tree");

            return expression;
        }

        /// <summary>
        /// Builds the exception reporting that this node cannot be compiled, naming the node and the reason.
        /// </summary>
        /// <param name="reason">What prevented compilation, completing "cannot compile X: ...".</param>
        [NotNull]
        protected CompileErrorException CannotCompile([NotNull] string reason)
        {
            return new CompileErrorException(this, reason);
        }

        // todo: funkcja, która na twarz dostaje kontext i go zwraca... taki dowcip...
        // todo: i jest dalej rootem do budowania!
    }
}
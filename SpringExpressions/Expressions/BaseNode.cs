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
using System.Runtime.Serialization;
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
    public abstract class BaseNode : SpringAST, IExpression
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

 //	    private Func<object, object> _compiledExpression;
		private Func<object, IDictionary<string, object>, object> _compiledExpression;


		/// <summary>
		/// Create a new instance
		/// </summary>
		public BaseNode()
        { }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected BaseNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Returns node's value.
        /// </summary>
        /// <returns>Node's value.</returns>
        public object GetValue()
        {
            return GetValue(null, null);
        }

        /// <summary>
        /// Returns node's value for the given context.
        /// </summary>
        /// <param name="context">Object to evaluate node against.</param>
        /// <returns>Node's value.</returns>
        public object GetValue(object context)
        {
			return GetValue(context, null);

		}

		/// <summary>
		/// Returns node's value for the given context.
		/// </summary>
		/// <param name="context">Object to evaluate node against.</param>
		/// <param name="variables">Expression variables map.</param>
		/// <returns>Node's value.</returns>
		public object GetValue(object context, IDictionary<string, object> variables)
        {
                     // todo: error: strongly typed context?????

			     // The lock that used to guard this block is gone, and so is the reason for it: nothing
			     // per-evaluation is stored on this instance any more. Context and variables are
			     // parameters of the compiled delegate, and two threads racing to build that delegate
			     // is benign - the two are equivalent and the field write is atomic.
	        {
                // todo: error: _compiled jest prawdzie tylko, jeśli context się nie zmienił!

		        if (_compiledExpression == null)
		        {
					// todo: zapamiętujemy zbudowane expression!
					// todo: zapamiętujemy funkcję, która dostaje na ryja obecta! z contextem!
					// todo: i go rzutuje!
					LExpression getRootContextExpression;
			        var ctxParam = LExpression.Parameter(typeof(object), "context");

			        if (context == null)
				        getRootContextExpression = LExpression.Constant(null);
			        else
				        getRootContextExpression = LExpression.Convert(ctxParam,
					        context.GetType());

					var variablesParam = LExpression.Parameter(
						typeof(IDictionary<string, object>), "variables");


					var exp = GetExpressionTreeIfPossible(
                        getRootContextExpression,
                        new CompilationContext(getRootContextExpression, variablesParam));

                    if (exp.Type == typeof(void))
                        exp = LExpression.Block(exp, LExpression.Constant(null, typeof(object)));
                    else if (exp.Type != typeof(object))
			            exp = LExpression.Convert(exp, typeof(object));

			        //var convExp = System.Linq.Expressions.Expression.Convert(expr, typeof(object));

					Expression<Func<object, IDictionary<string, object>, object>> lambda
				        = LExpression.Lambda<Func<object, IDictionary<string, object>, object>>(
					        exp, ctxParam, variablesParam);

					_compiledExpression = lambda.Compile();
		        }

				// The caller's own variables dictionary, on every call - so a #variable read or write
				// sees this evaluation's dictionary rather than whichever one arrived first.
				return _compiledExpression(context, variables);

     // todo: jeśli kompilacja się nie udała, to powinniśmy pójść starą, wolną ścieżką (interpreterem)
     // todo: - CompileOptions.TryCompileSwitchToInterpreterOnFailure - decydując o tym raz,
     // todo: przy budowaniu delegata, a nie przy każdym wywołaniu.
			}
        }

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


        private object _compiledExpressionAsObject;

 // todo: error: Getter Settter typowany nie publiczny !
        // todo: oczywiście bez sensu jest robić tyle GetXXXValue... totalnie bez sensu....
        public TResult GetValue<TResult>(IDictionary<string, object> variables = null)
        {
            return GetValue<TResult, object>(null, variables);
        }

           // todo: error: jeśli tojest w GetValue<> to przecież ktoś moze to wywołać z nowymi typami
           // todo: erorr: i całość skompilowanego kodu pójdzie się jebać!!! tej!
           // todo: error: więc jak to robnić? 

           // todo: error: jeśli więc nie całe expression będzie typowane, to lekka dupa, nie?

        public TResult GetValue<TResult, TContext>(TContext context, IDictionary<string, object> variables)
	    {
              //todo: typ dla kompiled object....
  // todo: musimy zapaiętać typy...

    // todo: error: tutaj oczywiście jest problem, bo base-node nie jest w ogóle przygotowany na typowanie... stąd problem!
		    if (_compiledExpressionAsObject == null)
		    {
                _compiledExpressionAsObject = Compiler.CompileGetter<TResult, TContext>(this);

                // dupochron
                if (_compiledExpressionAsObject == null)
                    throw new InvalidOperationException("Unknown error [_compiledExpressionAsObject == null]!");
            }

			return ((Func<TContext, IDictionary<string, object>, TResult>) _compiledExpressionAsObject)(
				context, variables);
	    }

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
        public void SetValue(object context, object newValue)
        {
            SetValue(context, null, newValue);
        }

        /// <summary>
        /// Sets node's value for the given context.
        /// </summary>
        /// <param name="context">Object to evaluate node against.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <param name="newValue">New value for this node.</param>
        public void SetValue(object context, IDictionary<string, object> variables, object newValue)
        {
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
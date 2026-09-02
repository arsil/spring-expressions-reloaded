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
using System.Reflection;
using JetBrains.Annotations;
using SpringExpressions.Expressions;
using SpringExpressions.Expressions.Compiling.Expressions;
using SpringExpressions.Util;
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
            LExpression expression;

            try
            {
                expression = node.GetExpressionTreeIfPossible(contextExpression, compilationContext);
            }
            catch (Exception e) when (InternalCompilerErrorException.ShouldAbsorb(e))
            {
                // Whatever a node's emitter does wrong, it comes out as a refusal naming that node.
                // This dispatcher is recursive, so the *innermost* failure is the one wrapped: an outer
                // node then sees a CompileErrorException, which ShouldAbsorb passes through untouched.
                // Absorbing here rather than only at the Compiler entry points is what buys the node's
                // identity - the entry points know only the root.
                throw new InternalCompilerErrorException(node, e);
            }

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
            LExpression expression;

            try
            {
                expression = node.GetExpressionTreeForSetterIfPossible(
                    contextExpression, compilationContext, newValueExpression);
            }
            catch (Exception e) when (InternalCompilerErrorException.ShouldAbsorb(e))
            {
                // The write-side twin of the reader above.
                throw new InternalCompilerErrorException(node, e);
            }

            if (expression == null)
                throw new CompileErrorException(node, "node produced no assignment expression tree");

            return expression;
        }

        /// <summary>
        /// Emits a call to the operator the operand types declare between them, or null if they declare
        /// none. Both backends run the same lookup - see <see cref="UserDefinedOperatorUtils"/>.
        /// </summary>
        /// <remarks>
        /// The resolved method is passed to the LINQ factory explicitly. Letting
        /// <c>LExpression.Add(left, right)</c> resolve for itself would use LINQ's own, more permissive
        /// rules and drift away from what the interpreter does.
        /// </remarks>
        [CanBeNull]
        protected static LExpression TryCreateUserDefinedBinary(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] string operatorMethodName,
            [NotNull] Func<LExpression, LExpression, MethodInfo, System.Linq.Expressions.BinaryExpression> factory)
        {
            if (UserDefinedOperatorUtils.IsOwnedByNumericPromotion(left.Type, right.Type))
                return null;

            var method = UserDefinedOperatorUtils.FindBinary(operatorMethodName, left.Type, right.Type);

            return method == null ? null : factory(left, right, method);
        }

        /// <summary>
        /// Emits a call to the relational operator the operand types declare between them, or null.
        /// The same lookup as <see cref="TryCreateUserDefinedBinary"/> with one rule added: a
        /// relational operator must answer a <c>bool</c>.
        /// </summary>
        /// <remarks>
        /// <p>
        /// One line, because the rule itself lives in
        /// <see cref="UserDefinedOperatorUtils.TryCreateComparison"/> - <c>ComparisonHelper</c> asks the
        /// same question of operands it has unwrapped a nullable from, and the two must not drift.
        /// </p>
        /// <p>
        /// This call site is the one that runs <b>before any conversion</b>, which is the
        /// operator-before-conversion rule: a type with both an implicit conversion to a built-in
        /// number and its own operators must answer with its own.
        /// </p>
        /// </remarks>
        [CanBeNull]
        protected static LExpression TryCreateUserDefinedComparison(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] string operatorMethodName,
            [NotNull] Func<LExpression, LExpression, MethodInfo, System.Linq.Expressions.BinaryExpression> factory)
        {
            return UserDefinedOperatorUtils.TryCreateComparison(left, right, operatorMethodName, factory);
        }

        /// <summary>
        /// The interpreter's twin of <see cref="TryCreateUserDefinedComparison"/>.
        /// </summary>
        protected static bool TryInvokeUserDefinedComparison(
            [CanBeNull] object left,
            [CanBeNull] object right,
            [NotNull] string operatorMethodName,
            out bool result)
        {
            result = false;

            if (left == null || right == null)
                return false;

            var leftType = left.GetType();
            var rightType = right.GetType();

            if (UserDefinedOperatorUtils.IsOwnedByNumericPromotion(leftType, rightType))
                return false;

            var method = UserDefinedOperatorUtils.FindBinary(operatorMethodName, leftType, rightType);

            if (method == null || method.ReturnType != typeof(bool))
                return false;

            result = (bool)method.Invoke(null, new[] { left, right });
            return true;
        }

        /// <summary>
        /// Emits a call to the unary operator the operand type declares for itself, or null.
        /// </summary>
        /// <remarks>
        /// Consulted before the numeric paths, for the reason the binary lookup is: a type declaring
        /// both an implicit conversion to a built-in real and its own operator would otherwise erase
        /// itself to the type it converts to. Built-in numerics never reach here - <c>decimal</c>
        /// declares <c>op_UnaryNegation(decimal)</c>, and the promotion rules keep that space.
        /// </remarks>
        [CanBeNull]
        protected static LExpression TryCreateUserDefinedUnary(
            [NotNull] LExpression operand,
            [NotNull] string operatorMethodName,
            [NotNull] Func<LExpression, MethodInfo, System.Linq.Expressions.UnaryExpression> factory)
        {
            if (UserDefinedOperatorUtils.IsOwnedByNumericPromotion(operand.Type, operand.Type))
                return null;

            var method = UserDefinedOperatorUtils.FindUnary(operatorMethodName, operand.Type);

            return method == null ? null : factory(operand, method);
        }

        /// <summary>
        /// The interpreter's twin of <see cref="TryCreateUserDefinedUnary"/>.
        /// </summary>
        protected static bool TryInvokeUserDefinedUnary(
            [CanBeNull] object operand,
            [NotNull] string operatorMethodName,
            out object result)
        {
            result = null;

            if (operand == null)
                return false;

            var operandType = operand.GetType();

            if (UserDefinedOperatorUtils.IsOwnedByNumericPromotion(operandType, operandType))
                return false;

            var method = UserDefinedOperatorUtils.FindUnary(operatorMethodName, operandType);

            if (method == null)
                return false;

            result = method.Invoke(null, new[] { operand });
            return true;
        }

        /// <summary>
        /// The interpreter's twin: invokes the operator the runtime operand types declare, if any.
        /// </summary>
        protected static bool TryInvokeUserDefinedBinary(
            [CanBeNull] object left,
            [CanBeNull] object right,
            [NotNull] string operatorMethodName,
            out object result)
        {
            result = null;

            if (left == null || right == null)
                return false;

            var leftType = left.GetType();
            var rightType = right.GetType();

            if (UserDefinedOperatorUtils.IsOwnedByNumericPromotion(leftType, rightType))
                return false;

            var method = UserDefinedOperatorUtils.FindBinary(operatorMethodName, leftType, rightType);

            if (method == null)
                return false;

            result = method.Invoke(null, new[] { left, right });
            return true;
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

        /// <summary>
        /// Builds an assignment, reporting a value the target cannot accept as a
        /// <see cref="CompileErrorException"/>.
        /// </summary>
        /// <remarks>
        /// LExpression.Assign validates the value against the target's type and throws ArgumentException
        /// when no conversion exists - assigning a set of int to an ISet&lt;object&gt; property, say, since
        /// ISet&lt;T&gt; is invariant. ArgumentException is not this codebase's "cannot compile" signal, so
        /// WeaklyTypedExpression's catch never sees it and the whole expression fails outright instead of
        /// falling back to the interpreter, which assigns such a value quite happily.
        /// </remarks>
        [NotNull]
        protected LExpression BuildAssign([NotNull] LExpression target, [NotNull] LExpression value)
        {
            try
            {
                return LExpression.Assign(target, value);
            }
            catch (ArgumentException ex)
            {
                throw CannotCompile(
                    $"cannot assign a value of type '{value.Type}' to a target of type '{target.Type}': {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a method call, reporting an instance or an argument the method cannot accept as a
        /// <see cref="CompileErrorException"/>.
        /// </summary>
        /// <remarks>
        /// LExpression.Call validates the instance and every argument against the method's signature and
        /// throws ArgumentException when something does not fit - an instance method resolved against a
        /// type-name context, so there is no instance to call it on, or a null argument typed object
        /// against an IFormatProvider parameter. ArgumentException is not this codebase's "cannot compile"
        /// signal, so WeaklyTypedExpression's catch never sees it and an expression the interpreter
        /// evaluates quite happily becomes a hard failure instead of falling back.
        /// </remarks>
        [NotNull]
        protected LExpression BuildCall(
            LExpression instance,
            [NotNull] MethodInfo method,
            [NotNull] IEnumerable<LExpression> arguments)
        {
            try
            {
                return LExpression.Call(instance, method, arguments);
            }
            catch (ArgumentException ex)
            {
                throw CannotCompile(
                    $"cannot call method '{method.DeclaringType}.{method.Name}' with the given instance "
                    + $"and arguments: {ex.Message}");
            }
        }

        // todo: funkcja, która na twarz dostaje kontext i go zwraca... taki dowcip...
        // todo: i jest dalej rootem do budowania!
    }
}
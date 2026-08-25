using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    /// <summary>
    /// The weakly typed face of a parsed expression: holds the AST, and per context type the evaluator built
    /// for it - compiled where that is possible, interpreted where it is not.
    /// </summary>
    /// <remarks>
    /// <p>
    /// This is what <see cref="Expression.Parse"/> and <see cref="Expression.Wrap"/> return, and it is where
    /// evaluation state belongs. One expression can be evaluated with many context types - the type comes from
    /// the call site, so it is a property of the call, not of the tree - and each type needs its own compiled
    /// form. A single slot on an AST node cannot provide that; it can only serve the first type it saw and
    /// fail for the rest.
    /// </p>
    /// <p>
    /// The type used is the one the caller declared, never one guessed from the value. A caller who declares
    /// nothing - <c>GetValue&lt;object&gt;</c>, or the untyped overloads - gets exactly that: <c>object</c>
    /// declares no members, so anything reaching into the root has no compiled form and is interpreted, which
    /// resolves members against the runtime type. That is the honest outcome for a call site that erased its
    /// own type information, rather than a guess made on the caller's behalf.
    /// </p>
    /// <p>
    /// The map is unbounded. Its keys are the declared types of call sites, so their number is fixed by the
    /// program rather than by the data flowing through it, and the map lives on one object per parsed
    /// expression rather than on every node of the tree.
    /// </p>
    /// </remarks>
    internal sealed class WeaklyTypedExpression : IWeaklyTypedExpression
    {
        public WeaklyTypedExpression(BaseNode expressionNode, EvaluationMode mode)
        {
            _expressionNode = expressionNode;
            _mode = mode;
        }

        /// <summary>The AST this evaluates. The tree itself holds no evaluation state.</summary>
        internal BaseNode ExpressionNode
        {
            get { return _expressionNode; }
        }

        public object GetValue()
        {
            return GetValue<object>(null, null);
        }

        public object GetValue<TContext>(TContext context)
        {
            return GetValue<TContext>(context, null);
        }

        public object GetValue<TContext>(TContext context, IDictionary<string, object> variables)
        {
            return GetterFor<TContext>().GetValue(context, variables);
        }

        public void SetValue<TContext>(TContext context, object newValue)
        {
            SetValue<TContext>(context, null, newValue);
        }

        public void SetValue<TContext>(
            TContext context, IDictionary<string, object> variables, object newValue)
        {
            // Setting always interprets: only four node types emit a compiled setter, against five that
            // interpret one, so routing this at the compiler would refuse shapes that work today.
            _expressionNode.SetValue(context, variables, newValue);
        }

        private IGetterExpression<TContext, object> GetterFor<TContext>()
        {
            // GetOrAdd may run the factory more than once under contention and keep only one result. Both
            // would be equivalent, so a duplicated attempt is wasted work rather than a defect.
            return (IGetterExpression<TContext, object>)_gettersByDeclaredType.GetOrAdd(
                typeof(TContext), _ => CreateGetter<TContext>());
        }

        /// <summary>
        /// Builds the evaluator for one declared context type, in this expression's
        /// <see cref="EvaluationMode"/>.
        /// </summary>
        /// <remarks>
        /// The decision is taken once, here, and never revisited: the getter this returns is the permanent
        /// strategy for that context type. Deciding at construction rather than per evaluation is what keeps
        /// the object immutable afterwards, so one expression stays safe to share across threads.
        /// <p>
        /// This method used to hand-roll <see cref="EvaluationMode.CompileOrInterpret"/> - compile inside a
        /// try, and on <see cref="CompileErrorException"/> build an interpreter instead - because there was
        /// no word for it. There is now, and the strongly typed getter honours it, so the behaviour is
        /// expressed once rather than implemented here. Most often the context type is object, which
        /// declares nothing to bind against; it is also every construct the compiled backend does not
        /// implement yet, a lambda for instance. The interpreter handles both.
        /// </p>
        /// </remarks>
        private object CreateGetter<TContext>()
        {
            return new GetterExpression<TContext, object>(_expressionNode, _mode);
        }

        private readonly BaseNode _expressionNode;
        private readonly EvaluationMode _mode;

        private readonly ConcurrentDictionary<Type, object> _gettersByDeclaredType
            = new ConcurrentDictionary<Type, object>();
    }
}

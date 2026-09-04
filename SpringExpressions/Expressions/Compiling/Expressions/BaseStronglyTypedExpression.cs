using JetBrains.Annotations;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseStronglyTypedExpression
    {
        protected BaseStronglyTypedExpression(
            [NotNull] BaseNode expressionNode,
            EvaluationMode mode,
            [NotNull] SandboxPolicy sandboxPolicy)
        {
            _expressionNode = expressionNode;
            _mode = mode;
            _sandboxPolicy = sandboxPolicy;
        }

        internal BaseNode ExpressionNode
            => _expressionNode;

        /// <summary>
        /// What this expression became. Declared here, on the non-generic base, because that is what
        /// <see cref="Expression.GetCompilationStatus"/> can cast to without knowing TRoot or TResult.
        /// </summary>
        /// <remarks>
        /// Each subclass answers from its own fields: whether its compiled delegate is null - which is
        /// the record that the expression is interpreted - the mode it was built with, and the refusal
        /// message its constructor caught. The message cannot live here: a readonly field is assignable
        /// only by its own class's constructor, and the compile attempt happens in the subclass
        /// constructor, which runs afterwards. Three readonly fields beat one mutable one on a type whose
        /// whole purpose is to hold no per-evaluation state.
        /// </remarks>
        [NotNull]
        internal abstract CompilationStatus Status { get; }

        // No per-evaluation state is kept here on purpose. Root context and variables are passed to
        // the compiled delegate as parameters, and the interpreter gets a context of its own per
        // evaluation, so one expression instance can be evaluated concurrently on many threads.
        // ReSharper disable InconsistentNaming
        protected readonly BaseNode _expressionNode;
        protected readonly EvaluationMode _mode;

        /// <summary>
        /// What this expression may reach - fixed here at construction, never read from ambient state.
        /// See <c>_Docs/type-sandboxing.md</c> §4.3.
        /// </summary>
        [NotNull]
        protected readonly SandboxPolicy _sandboxPolicy;
        // ReSharper restore InconsistentNaming
    }
}

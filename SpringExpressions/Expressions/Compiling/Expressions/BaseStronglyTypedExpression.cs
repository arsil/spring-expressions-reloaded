using JetBrains.Annotations;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseStronglyTypedExpression
    {
        protected BaseStronglyTypedExpression(
            [NotNull] BaseNode expressionNode,
            EvaluationMode mode)
        {
            _expressionNode = expressionNode;
            _mode = mode;
        }

        internal BaseNode ExpressionNode
            => _expressionNode;

        // No per-evaluation state is kept here on purpose. Root context and variables are passed to
        // the compiled delegate as parameters, and the interpreter gets a context of its own per
        // evaluation, so one expression instance can be evaluated concurrently on many threads.
        // ReSharper disable InconsistentNaming
        protected readonly BaseNode _expressionNode;
        protected readonly EvaluationMode _mode;
        // ReSharper restore InconsistentNaming
    }
}

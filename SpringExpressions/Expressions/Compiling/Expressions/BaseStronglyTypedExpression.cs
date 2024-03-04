using JetBrains.Annotations;
using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseStronglyTypedExpression
    {
        protected BaseStronglyTypedExpression(
            [NotNull] BaseNode expressionNode, 
            CompileOptions compileOptions)
        {
            _expressionNode = expressionNode;
            _compileOptions = compileOptions;
        }

        internal BaseNode ExpressionNode
            => _expressionNode;

        // ReSharper disable InconsistentNaming
        protected readonly BaseNode _expressionNode;
        protected readonly CompileOptions _compileOptions;

        protected EvaluationContext _lastEvaluationContext;
        // ReSharper restore InconsistentNaming
    }
}

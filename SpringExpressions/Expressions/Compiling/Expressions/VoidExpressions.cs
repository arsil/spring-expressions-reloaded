using System;
using System.Collections.Generic;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseVoidExpression<TRoot>
    {
        protected BaseVoidExpression(
            BaseNode expressionNode,
            CompileOptions compileOptions)
        {
            _expressionNode = expressionNode;
            _compileOptions = compileOptions;

            // todo: error handling!!!!
            if (_compileOptions.HasFlag(CompileOptions.CompileOnParse))
                _compiledExpression = Compiler.CompileExecuteWithVoidReturnType<TRoot>(_expressionNode);
        }

        protected void ExecuteInternal(
            TRoot context, IDictionary<string, object> variables)
        {
            if (_lastEvaluationContext != null)
                _lastEvaluationContext.Reuse(context, variables);
            else
                _lastEvaluationContext = new EvaluationContext(context, variables);

            if (_compileOptions.HasFlag(CompileOptions.MustUseInterpreter))
            {
                _expressionNode.ExecuteVoidExpressionUsingInterpreter(context, _lastEvaluationContext);
                return;
            }

            // todo: error handling!!!!
            if (_compiledExpression == null)
                _compiledExpression = Compiler.CompileExecuteWithVoidReturnType<TRoot>(_expressionNode);

            _compiledExpression(context, _lastEvaluationContext);
        }

        private readonly BaseNode _expressionNode;
        private Action<TRoot, EvaluationContext> _compiledExpression;

        private EvaluationContext _lastEvaluationContext;

        private readonly CompileOptions _compileOptions;
    }

    class VoidExpression<TRoot> : BaseVoidExpression<TRoot>, IVoidExpression<TRoot>
    {
        public VoidExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public void Execute(TRoot context, IDictionary<string, object> variables = null)
            => ExecuteInternal(context, variables);
    }

    class VoidExpression : BaseVoidExpression<object>, IVoidExpression
    {
        public VoidExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public void Execute(IDictionary<string, object> variables = null)
            => ExecuteInternal(null, variables);
    }
}

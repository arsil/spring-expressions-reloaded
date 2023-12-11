using System;
using System.Collections.Generic;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseGetterExpression<TRoot, TResult>
    {
        protected BaseGetterExpression(
            BaseNode expressionNode,
            CompileOptions compileOptions)
        {
            _expressionNode = expressionNode;
            _compileOptions = compileOptions;

            // todo: error handling!!!!
            if (_compileOptions.HasFlag(CompileOptions.CompileOnParse))
                _compiledExpression = Compiler.CompileGetter<TResult, TRoot>(_expressionNode);
        }

        protected TResult GetValueInternal(TRoot context, IDictionary<string, object> variables)
        {
            if (_lastEvaluationContext != null)
                _lastEvaluationContext.Reuse(context, variables);
            else
                _lastEvaluationContext = new EvaluationContext(context, variables);

            if (_compileOptions.HasFlag(CompileOptions.MustUseInterpreter))
                return (TResult)_expressionNode.GetValueUsingInterpreter(context, _lastEvaluationContext);

            // todo: error handling!!!!
            if (_compiledExpression == null)
                _compiledExpression = Compiler.CompileGetter<TResult, TRoot>(_expressionNode);

            return _compiledExpression(context, _lastEvaluationContext);

        }

        private readonly BaseNode _expressionNode;
        private Func<TRoot, EvaluationContext, TResult> _compiledExpression;

        private EvaluationContext _lastEvaluationContext;

        private readonly CompileOptions _compileOptions;
    }

    class GetterExpression<TRoot, TResult>
        : BaseGetterExpression<TRoot, TResult>
        , IGetterExpression<TRoot, TResult>
    {
        public GetterExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public TResult GetValue(TRoot context, IDictionary<string, object> variables = null)
            => GetValueInternal(context, variables);
    }

    class GetterExpression<TResult>
        : BaseGetterExpression<object, TResult>
        , IGetterExpression<TResult>
    {
        public GetterExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public TResult GetValue(IDictionary<string, object> variables = null)
            => GetValueInternal(null, variables);
    }
}

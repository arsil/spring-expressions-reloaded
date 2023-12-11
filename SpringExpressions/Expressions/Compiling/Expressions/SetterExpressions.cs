using System;
using System.Collections.Generic;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseSetterExpression<TRoot, TArgument>
    {
        protected BaseSetterExpression(
            BaseNode expressionNode,
            CompileOptions compileOptions)
        {
            _expressionNode = expressionNode;
            _compileOptions = compileOptions;

            // todo: error handling!!!!
            if (_compileOptions.HasFlag(CompileOptions.CompileOnParse))
                _compiledExpression = Compiler.CompileSetter<TRoot, TArgument>(_expressionNode);
        }

        protected void SetValueInternal(
            TRoot context, TArgument newValue, IDictionary<string, object> variables)
        {
            if (_lastEvaluationContext != null)
                _lastEvaluationContext.Reuse(context, variables);
            else
                _lastEvaluationContext = new EvaluationContext(context, variables);

            if (_compileOptions.HasFlag(CompileOptions.MustUseInterpreter))
            {
                _expressionNode.SetValueUsingInterpreter(context, _lastEvaluationContext, newValue);
                return;
            }

            // todo: error handling!!!!
            if (_compiledExpression == null)
                _compiledExpression = Compiler.CompileSetter<TRoot, TArgument>(_expressionNode);

            _compiledExpression(context, _lastEvaluationContext, newValue);
        }

        private readonly BaseNode _expressionNode;
        private Action<TRoot, EvaluationContext, TArgument> _compiledExpression;

        private EvaluationContext _lastEvaluationContext;

        private readonly CompileOptions _compileOptions;
    }

    class SetterExpression<TRoot, TArgument>
        : BaseSetterExpression<TRoot, TArgument>
        , ISetterExpression<TRoot, TArgument>
    {
        public SetterExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public void SetValue(TRoot context, TArgument newValue, IDictionary<string, object> variables = null)
            => SetValueInternal(context, newValue, variables);
    }

    class SetterExpression<TArgument>
        : BaseSetterExpression<object, TArgument>
            , ISetterExpression<TArgument>
    {
        public SetterExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public void SetValue(TArgument newValue, IDictionary<string, object> variables = null)
            => SetValueInternal(null, newValue, variables);
    }
}

using System;
using System.Collections.Generic;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseGetterExpression<TRoot, TResult> : BaseStronglyTypedExpression
    {
        protected BaseGetterExpression(
                BaseNode expressionNode,
                CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        {
            // todo: error handling!!!!
            if (_compileOptions.HasFlag(CompileOptions.CompileOnParse))
                _compiledExpression = Compiler.CompileGetter<TResult, TRoot>(_expressionNode);
        }

        protected TResult GetValueInternal(TRoot context, IDictionary<string, object> variables)
        {
            if (_compileOptions.HasFlag(CompileOptions.MustUseInterpreter))
            {
                // The interpreter mutates its context - SwitchThisContext in the projection and
                // selection nodes, SwitchLocalVariables in the lambda node - so it gets a fresh
                // one per evaluation. This is the slow path anyway.
                return (TResult)_expressionNode.GetValueUsingInterpreter(
                    context, new EvaluationContext(context, variables));
            }

            // todo: error handling!!!!
            var compiled = _compiledExpression
                ?? (_compiledExpression = Compiler.CompileGetter<TResult, TRoot>(_expressionNode));

            // Root and variables are parameters of the compiled delegate, so nothing is shared
            // between concurrent evaluations and nothing is allocated per evaluation.
            return compiled(context, variables);
        }

        private Func<TRoot, IDictionary<string, object>, TResult> _compiledExpression;
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

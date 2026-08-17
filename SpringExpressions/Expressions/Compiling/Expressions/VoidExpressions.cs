using System;
using System.Collections.Generic;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseVoidExpression<TRoot>: BaseStronglyTypedExpression
    {
        protected BaseVoidExpression(
                BaseNode expressionNode,
                CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        {
            // todo: error handling!!!!
            if (_compileOptions.HasFlag(CompileOptions.CompileOnParse))
                _compiledExpression = Compiler.CompileExecuteWithVoidReturnType<TRoot>(_expressionNode);
        }

        protected void ExecuteInternal(
            TRoot context, IDictionary<string, object> variables)
        {
            if (_compileOptions.HasFlag(CompileOptions.MustUseInterpreter))
            {
                // A context of its own per evaluation - the interpreter mutates it.
                _expressionNode.ExecuteVoidExpressionUsingInterpreter(
                    context, new EvaluationContext(context, variables));
                return;
            }

            // todo: error handling!!!!
            var compiled = _compiledExpression
                ?? (_compiledExpression = Compiler.CompileExecuteWithVoidReturnType<TRoot>(_expressionNode));

            // Root and variables are parameters of the compiled delegate, so nothing is shared
            // between concurrent evaluations and nothing is allocated per evaluation.
            compiled(context, variables);
        }

        private Action<TRoot, IDictionary<string, object>> _compiledExpression;
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

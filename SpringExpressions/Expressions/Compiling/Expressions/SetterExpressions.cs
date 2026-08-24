using System;
using System.Collections.Generic;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseSetterExpression<TRoot, TArgument>: BaseStronglyTypedExpression
    {
        protected BaseSetterExpression(
                BaseNode expressionNode,
                CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        {
            // todo: error handling!!!!
            if (_compileOptions.HasFlag(CompileOptions.CompileOnParse))
                _compiledExpression = Compiler.CompileSetter<TRoot, TArgument>(_expressionNode);
        }

        protected void SetValueInternal(
            TRoot context, TArgument newValue, IDictionary<string, object> variables)
        {
            if (_compileOptions.HasFlag(CompileOptions.MustUseInterpreter))
            {
                // A context of its own per evaluation - the interpreter mutates it.
                _expressionNode.SetValueUsingInterpreter(
                    context, new EvaluationContext(context, variables), newValue);
                return;
            }

            // todo: error handling!!!!
            var compiled = _compiledExpression ??= Compiler.CompileSetter<TRoot, TArgument>(_expressionNode);

            // Root and variables are parameters of the compiled delegate, so nothing is shared
            // between concurrent evaluations and nothing is allocated per evaluation.
            compiled(context, variables, newValue);
        }

        private Action<TRoot, IDictionary<string, object>, TArgument> _compiledExpression;
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

using System;
using System.Collections.Generic;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseVoidExpression<TRoot>: BaseStronglyTypedExpression
    {
        /// <summary>Compilation happens here, once, or not at all - see BaseGetterExpression.</summary>
        protected BaseVoidExpression(
                BaseNode expressionNode,
                EvaluationMode mode)
            : base(expressionNode, mode)
        {
            if (mode == EvaluationMode.MustInterpret)
                return;

            try
            {
                _compiledExpression = Compiler.CompileExecuteWithVoidReturnType<TRoot>(_expressionNode);
            }
            catch (CompileErrorException) when (mode == EvaluationMode.CompileOrInterpret)
            {
                // No compiled form for this shape; the interpreter executes it. MustCompile does not
                // catch: that caller asked to hear the refusal.
            }
        }

        protected void ExecuteInternal(
            TRoot context, IDictionary<string, object> variables)
        {
            if (_compiledExpression == null)
            {
                // A context of its own per evaluation - the interpreter mutates it.
                _expressionNode.ExecuteVoidExpressionUsingInterpreter(
                    context, new EvaluationContext(context, variables));
                return;
            }

            // Root and variables are parameters of the compiled delegate, so nothing is shared
            // between concurrent evaluations and nothing is allocated per evaluation.
            _compiledExpression(context, variables);
        }

        /// <summary>Null when this expression is interpreted - see the constructor.</summary>
        private readonly Action<TRoot, IDictionary<string, object>> _compiledExpression;
    }

    class VoidExpression<TRoot> : BaseVoidExpression<TRoot>, IVoidExpression<TRoot>
    {
        public VoidExpression(BaseNode expressionNode, EvaluationMode mode)
            : base(expressionNode, mode)
        { }

        public void Execute(TRoot context, IDictionary<string, object> variables = null)
            => ExecuteInternal(context, variables);
    }

    class VoidExpression : BaseVoidExpression<object>, IVoidExpression
    {
        public VoidExpression(BaseNode expressionNode, EvaluationMode mode)
            : base(expressionNode, mode)
        { }

        public void Execute(IDictionary<string, object> variables = null)
            => ExecuteInternal(null, variables);
    }
}


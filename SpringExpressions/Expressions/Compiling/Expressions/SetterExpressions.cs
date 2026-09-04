using System;
using System.Collections.Generic;

using JetBrains.Annotations;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseSetterExpression<TRoot, TArgument>: BaseStronglyTypedExpression
    {
        /// <summary>Compilation happens here, once, or not at all - see BaseGetterExpression.</summary>
        protected BaseSetterExpression(
                BaseNode expressionNode,
                EvaluationMode mode,
                [NotNull] SandboxPolicy sandboxPolicy)
            : base(expressionNode, mode, sandboxPolicy)
        {
            if (mode == EvaluationMode.MustInterpret)
                return;

            try
            {
                _compiledExpression = Compiler.CompileSetter<TRoot, TArgument>(_expressionNode, sandboxPolicy);
            }
            catch (CompileErrorException ex) when (mode == EvaluationMode.CompileOrInterpret)
            {
                // No compiled setter for this shape - only four node types emit one. The interpreter
                // sets it instead. MustCompile does not catch: that caller asked to hear the refusal.
                _refusalMessage = ex.Message;
            }
        }

        internal override CompilationStatus Status
            => _compiledExpression != null
                ? CompilationStatus.Compiled
                : _refusalMessage == null
                    ? CompilationStatus.InterpretedByRequest
                    : CompilationStatus.InterpretedAfterRefusal(_refusalMessage);

        private readonly string _refusalMessage;

        protected void SetValueInternal(
            TRoot context, TArgument newValue, IDictionary<string, object> variables)
        {
            if (_compiledExpression == null)
            {
                // A context of its own per evaluation - the interpreter mutates it.
                _expressionNode.SetValueUsingInterpreter(
                    context, new EvaluationContext(context, variables, _sandboxPolicy), newValue);
                return;
            }

            // Root and variables are parameters of the compiled delegate, so nothing is shared
            // between concurrent evaluations and nothing is allocated per evaluation.
            _compiledExpression(context, variables, newValue);
        }

        /// <summary>Null when this expression is interpreted - see the constructor.</summary>
        private readonly Action<TRoot, IDictionary<string, object>, TArgument> _compiledExpression;
    }

    class SetterExpression<TRoot, TArgument>
        : BaseSetterExpression<TRoot, TArgument>
        , ISetterExpression<TRoot, TArgument>
    {
        public SetterExpression(
            BaseNode expressionNode, EvaluationMode mode, [NotNull] SandboxPolicy sandboxPolicy)
            : base(expressionNode, mode, sandboxPolicy)
        { }

        public void SetValue(TRoot context, TArgument newValue, IDictionary<string, object> variables = null)
            => SetValueInternal(context, newValue, variables);
    }

    class SetterExpression<TArgument>
        : BaseSetterExpression<object, TArgument>
            , ISetterExpression<TArgument>
    {
        public SetterExpression(
            BaseNode expressionNode, EvaluationMode mode, [NotNull] SandboxPolicy sandboxPolicy)
            : base(expressionNode, mode, sandboxPolicy)
        { }

        public void SetValue(TArgument newValue, IDictionary<string, object> variables = null)
            => SetValueInternal(null, newValue, variables);
    }
}



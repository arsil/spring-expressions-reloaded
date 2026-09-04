using System;
using System.Collections.Generic;

using JetBrains.Annotations;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseVoidExpression<TRoot>: BaseStronglyTypedExpression
    {
        /// <summary>Compilation happens here, once, or not at all - see BaseGetterExpression.</summary>
        protected BaseVoidExpression(
                BaseNode expressionNode,
                EvaluationMode mode,
                [NotNull] SandboxPolicy sandboxPolicy)
            : base(expressionNode, mode, sandboxPolicy)
        {
            if (mode == EvaluationMode.MustInterpret)
                return;

            try
            {
                _compiledExpression =
                    Compiler.CompileExecuteWithVoidReturnType<TRoot>(_expressionNode, sandboxPolicy);
            }
            catch (CompileErrorException ex) when (mode == EvaluationMode.CompileOrInterpret)
            {
                // No compiled form for this shape; the interpreter executes it. MustCompile does not
                // catch: that caller asked to hear the refusal.
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

        protected void ExecuteInternal(
            TRoot context, IDictionary<string, object> variables)
        {
            if (_compiledExpression == null)
            {
                // A context of its own per evaluation - the interpreter mutates it.
                _expressionNode.ExecuteVoidExpressionUsingInterpreter(
                    context, new EvaluationContext(context, variables, _sandboxPolicy));
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
        public VoidExpression(
            BaseNode expressionNode, EvaluationMode mode, [NotNull] SandboxPolicy sandboxPolicy)
            : base(expressionNode, mode, sandboxPolicy)
        { }

        public void Execute(TRoot context, IDictionary<string, object> variables = null)
            => ExecuteInternal(context, variables);
    }

    class VoidExpression : BaseVoidExpression<object>, IVoidExpression
    {
        public VoidExpression(
            BaseNode expressionNode, EvaluationMode mode, [NotNull] SandboxPolicy sandboxPolicy)
            : base(expressionNode, mode, sandboxPolicy)
        { }

        public void Execute(IDictionary<string, object> variables = null)
            => ExecuteInternal(null, variables);
    }
}



using System.Collections.Generic;
using System.Linq.Expressions;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    public class CompilationContext
    {
        public CompilationContext(LExpression rootContextExpression, LExpression variablesExpression)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = rootContextExpression;
            VariablesExpression = variablesExpression;
        }

        public CompilationContext CreateWithNewThisContext(LExpression thisExpression)
        {
            return new CompilationContext(RootContextExpression, thisExpression, VariablesExpression);
        }
        // todo: error: context expression != RootExpression    !!!!  !!!!! !!!!

        private CompilationContext(
            LExpression rootContextExpression,
            LExpression thisExpression,
            LExpression variablesExpression)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = thisExpression;
            VariablesExpression = variablesExpression;
        }

        public void AddLocalVariable(string variableName, ParameterExpression variableExpression)
        {
            if (_localVariables == null)
                _localVariables = new Dictionary<string, ParameterExpression>();

            _localVariables.Add(variableName, variableExpression);
        }

        public bool TryGetLocalVariable(
            string variableName, out ParameterExpression variableExpression)
        {
            if (_localVariables == null)
            {
                variableExpression = null;
                return false;
            }

            return _localVariables.TryGetValue(variableName, out variableExpression);
        }

        public LExpression RootContextExpression { get; private set; }
        public LExpression ThisExpression { get; private set; }

        /// <summary>
        /// The caller-supplied variables dictionary, as a parameter of the compiled delegate.
        /// Only <see cref="VariableNode"/> reads it: #root and #this resolve to
        /// <see cref="RootContextExpression"/> / <see cref="ThisExpression"/> and $locals to
        /// <see cref="ParameterExpression"/>s, all at compile time. Compiled code therefore needs
        /// no <c>EvaluationContext</c> - that object exists for the interpreter, which mutates it.
        /// </summary>
        public LExpression VariablesExpression { get; private set; }

        public Dictionary<string, ParameterExpression> _localVariables;
    }
}
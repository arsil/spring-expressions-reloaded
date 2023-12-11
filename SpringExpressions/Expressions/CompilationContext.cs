using System.Collections.Generic;
using System.Linq.Expressions;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    public class CompilationContext
    {
        public CompilationContext(LExpression rootContextExpression, LExpression evalContext)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = rootContextExpression;
            EvalContext = evalContext;
        }

        public CompilationContext CreateWithNewThisContext(LExpression thisExpression)
        {
            return new CompilationContext(RootContextExpression, thisExpression, EvalContext);
        }
        // todo: error: context expression != RootExpression    !!!!  !!!!! !!!!

        private CompilationContext(
            LExpression rootContextExpression, 
            LExpression thisExpression, 
            LExpression evalContext)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = thisExpression;
            EvalContext = evalContext;
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
        public LExpression EvalContext { get; private set; }

        public Dictionary<string, ParameterExpression> _localVariables;
    }
}

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

           // todo: error: context expression != RootExpression    !!!!  !!!!! !!!!

        public LExpression RootContextExpression { get; private set; }
        public LExpression ThisExpression { get; private set; }
        public LExpression EvalContext { get; private set; }
    }
}
using SpringCore.TypeResolution;
using System;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    public class CastNode : UnaryOperator
    {
        public CastNode()
        {
        }

                protected override object Get(object context, EvaluationContext evalContext)
        {
            if (type == null)
            {
                lock (this)
                {
                    type = TypeResolutionUtils.ResolveType(getText());
                }
            }

            object operand = GetValue(Operand, context, evalContext);
            return Convert.ChangeType(operand, type);
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var operandExpression = GetExpressionTreeIfPossible(
                (BaseNode)getFirstChild(), contextExpression, compilationContext);

            // todo: error: raise condition?
            if (type == null)
            {
                lock (this)
                {
                    type = TypeResolutionUtils.ResolveType(getText());
                }
            }

            return LExpression.Convert(operandExpression, type);
        }

        private Type type;
    }
}

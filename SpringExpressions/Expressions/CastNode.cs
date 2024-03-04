using SpringCore.TypeResolution;
using System;
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    [Serializable]
    public class CastNode : UnaryOperator
    {
        public CastNode()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected CastNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
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

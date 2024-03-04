using SpringExpressions.Expressions.Compiling.Expressions;
using System.IO;

namespace SpringExpressions.Expressions
{
    public static class ExpressionInfo
    {
        public static string DescribeAsXmlString(IStronglyTypedExpression expression)
        {
            if (expression is BaseStronglyTypedExpression stronglyTypedExpression)
            {
                var springAst = stronglyTypedExpression.ExpressionNode;

                using (TextWriter tw = new StringWriter())
                {
                    springAst.xmlSerialize(tw);

                    return tw.ToString();
                }
            }

            return "error!";
        }


        public static string DescribeAsStringTree(IStronglyTypedExpression expression)
        {
            if (expression is BaseStronglyTypedExpression stronglyTypedExpression)
            {
                var springAst = stronglyTypedExpression.ExpressionNode;
                return springAst.ToTree();
            }

            return "error!";
        }

    }
}

using System;

using System.Linq.Expressions;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Expressions
{
    using static BaseNode;

    internal static class Compiler
    {
        public static Func<TContext, EvaluationContext, TResult> CompileGetter<TResult, TContext>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var getEvalContextExpression = LExpression.Parameter(typeof(EvaluationContext), "evalContext");

            LExpression getRootContextExpression;
            // todo: error: czy to ma sens?????!!!!------------------------------------------------------------------------------
            //            if (context == null)
            //                getRootContextExpression = LExpression.Constant(null, typeof(TContext));
            //          else
            //            getRootContextExpression = LExpression.Convert(ctxParam, typeof(TContext));

            getRootContextExpression = ctxParam;


            // todo: problem: w EvalContext nie mamy już typowanego roota... i to jest słabe... kurde...
            // todo: czy da się to jakoś załatwić... bo dostęp do roota mógłby być... ale potrzebowalibyśmy
            // todo: exp... dla roota... a może to powinno jeszcze inaczej działać...  może do GetExpressionTree powinniśmy
            // todo: przekaża epression? do wyciągnięcia roota? może to jednak powinien być inny evalContext!!!!!!!!!!!!!!!
            // todO:: pewnie powinien to być inny eval context....  

            var exp = GetExpressionTreeIfPossible(
                expressionNode,
                getRootContextExpression,
                new CompilationContext(getRootContextExpression, getEvalContextExpression));

            if (exp.Type.IsValueType)
            {
                var resultType = typeof(TResult);

                if (resultType == typeof(object))
                {
                    // boxing value types for TResult == object
                    exp = LExpression.Convert(exp, typeof(object));
                }
                else if (resultType != exp.Type && resultType.IsValueType)
                {
                    exp = LExpression.ConvertChecked(exp, resultType);
                }
            }

            // todo: error; a może przekazywać tutaj nie evaluationContext..  tylko coś więcej???? shit....roota przykładowo...
            Expression<Func<TContext, EvaluationContext, TResult>> lambda
                = LExpression.Lambda<Func<TContext, EvaluationContext, TResult>>(exp, ctxParam, getEvalContextExpression);

            // no i co dalej... jak 
            // todo: co z lastEvaluationContext? może nie jest potrzebny? oto jest pytanie!
            // todo: możemy go tutaj przekazać... albo po prostu utworzyć w środku... 
            // todo: pytanie, czy możemy do na rządanie utworzyć? kurde... raczej nie...
            return lambda.Compile();
        }

        public static Action<TContext, EvaluationContext, TArgument> CompileSetter<TContext, TArgument>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var newValueParam = LExpression.Parameter(typeof(TArgument), "newValue");

            var getEvalContextExpression = LExpression.Parameter(typeof(EvaluationContext), "evalContext");

            LExpression getRootContextExpression;
            // todo: error: czy to ma sens?????!!!!------------------------------------------------------------------------------
            //            if (context == null)
            //                getRootContextExpression = LExpression.Constant(null, typeof(TContext));
            //          else
            //            getRootContextExpression = LExpression.Convert(ctxParam, typeof(TContext));

            getRootContextExpression = ctxParam;

            var exp = GetExpressionTreeForSetterIfPossible(
                expressionNode,
                getRootContextExpression,
                new CompilationContext(getRootContextExpression, getEvalContextExpression),
                newValueParam);

               // todo: error; must compile!
            
               // todo: nodeType == Assign?
/*
            if (exp.Type != typeof(void))
            {
                var tree = ((SpringExpressions.Parser.antlr.collections.AST)expressionNode).ToStringTree();
                throw new InvalidOperationException($"Expression returns {exp.Type} instead of void! \n" + tree);
            }
*/
            Expression<Action<TContext, EvaluationContext, TArgument>> lambda
                = LExpression.Lambda<Action<TContext, EvaluationContext, TArgument>>(
                    exp, ctxParam, getEvalContextExpression, newValueParam);

            return lambda.Compile();
        }

        public static Action<TContext, EvaluationContext> CompileExecuteWithVoidReturnType<TContext>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var getEvalContextExpression = LExpression.Parameter(typeof(EvaluationContext), "evalContext");

            LExpression getRootContextExpression;
            // todo: error: czy to ma sens?????!!!!------------------------------------------------------------------------------
            //            if (context == null)
            //                getRootContextExpression = LExpression.Constant(null, typeof(TContext));
            //          else
            //            getRootContextExpression = LExpression.Convert(ctxParam, typeof(TContext));

            getRootContextExpression = ctxParam;

            var exp = GetExpressionTreeIfPossible(
                expressionNode,
                getRootContextExpression,
                new CompilationContext(getRootContextExpression, getEvalContextExpression));

            // todo: error:  compile error!
            // todo: error:  compile error!
            // todo: error void or Assign or Block? and last of the block is void or assign?
            // todo: error   Or Call(?) Call return void... so it is void?
            var validExpression
                = exp.Type == typeof(void)
                || exp.NodeType == ExpressionType.Assign;

            if (!validExpression)
               throw new InvalidOperationException(
                   $"Expression '{exp.NodeType}' returning '{exp.Type}' is not a void expression!");

            Expression<Action<TContext, EvaluationContext>> lambda
                = LExpression.Lambda<Action<TContext, EvaluationContext>>(exp, ctxParam, getEvalContextExpression);

            return lambda.Compile();
        }
    }
}

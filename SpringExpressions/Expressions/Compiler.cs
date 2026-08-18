using System;
using System.Collections.Generic;

using System.Linq.Expressions;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Expressions
{
    using static BaseNode;

    internal static class Compiler
    {
        public static Func<TContext, IDictionary<string, object>, TResult> CompileGetter<TResult, TContext>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var variablesParam = LExpression.Parameter(typeof(IDictionary<string, object>), "variables");

            LExpression getRootContextExpression;
            // todo: error: czy to ma sens?????!!!!------------------------------------------------------------------------------
            //            if (context == null)
            //                getRootContextExpression = LExpression.Constant(null, typeof(TContext));
            //          else
            //            getRootContextExpression = LExpression.Convert(ctxParam, typeof(TContext));

            getRootContextExpression = ctxParam;


            // The root arrives as a typed delegate parameter, so the compiled tree never needed the
            // untyped EvaluationContext.RootContext; the only thing it did need was the variables
            // dictionary, which is now the second parameter. Nothing per-evaluation is cached on the
            // expression instance, which is what makes a compiled expression safe to share.
            var exp = GetExpressionTreeIfPossible(
                expressionNode,
                getRootContextExpression,
                new CompilationContext(getRootContextExpression, variablesParam));

            // An expression whose body is void - an assignment, say - still has to produce a value when the
            // result type is object. Yielding null after it is what the weakly typed path always did.
            if (exp.Type == typeof(void) && typeof(TResult) == typeof(object))
            {
                exp = LExpression.Block(exp, LExpression.Constant(null, typeof(object)));
            }

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

            Expression<Func<TContext, IDictionary<string, object>, TResult>> lambda
                = LExpression.Lambda<Func<TContext, IDictionary<string, object>, TResult>>(
                    exp, ctxParam, variablesParam);

            return lambda.Compile();
        }

        public static Action<TContext, IDictionary<string, object>, TArgument> CompileSetter<TContext, TArgument>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var newValueParam = LExpression.Parameter(typeof(TArgument), "newValue");

            var variablesParam = LExpression.Parameter(typeof(IDictionary<string, object>), "variables");

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
                new CompilationContext(getRootContextExpression, variablesParam),
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
            Expression<Action<TContext, IDictionary<string, object>, TArgument>> lambda
                = LExpression.Lambda<Action<TContext, IDictionary<string, object>, TArgument>>(
                    exp, ctxParam, variablesParam, newValueParam);

            return lambda.Compile();
        }

        public static Action<TContext, IDictionary<string, object>> CompileExecuteWithVoidReturnType<TContext>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var variablesParam = LExpression.Parameter(typeof(IDictionary<string, object>), "variables");

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
                new CompilationContext(getRootContextExpression, variablesParam));

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

            Expression<Action<TContext, IDictionary<string, object>>> lambda
                = LExpression.Lambda<Action<TContext, IDictionary<string, object>>>(
                    exp, ctxParam, variablesParam);

            return lambda.Compile();
        }
    }
}

#region License

/*
 * Copyright © 2002-2011 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed projection node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class ProjectionNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public ProjectionNode():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected ProjectionNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            if (!typeof(IEnumerable).IsAssignableFrom(contextExpression.Type))
            {
                throw new ArgumentException(
                    "Projection can only be used on an instance of the type that implements IEnumerable.");
            }

            if (contextExpression.Type.IsGenericType)
            {
                var itemType = contextExpression.Type.GetGenericArguments()[0];
                BaseNode expressionNode = (BaseNode) getFirstChild();



                var ctxParam = LExpression.Parameter(itemType, "item");
                var getRootContextExpression = LExpression.Convert(ctxParam, itemType);

                //  todo: error: contextExpression musi byæ typu item!!!!
                //  todo: error: wtedy to mo¿e ma jakiœ sens!!!

                // todo: this? root!
                var projectionExpression = GetExpressionTreeIfPossible(
                    expressionNode, 
                    getRootContextExpression, 
                    compilationContext.CreateWithNewThisContext(getRootContextExpression));


                var finalProjectionMi = _projectionMi.MakeGenericMethod(itemType, projectionExpression.Type);
                var projectionResultType = projectionExpression.Type;

                var funcType = LExpression.GetFuncType(itemType, projectionResultType);
                //                Expression<Func<TContext, EvaluationContext, TResult>> lambda
                //                  = LExpression.Lambda<Func<TContext, EvaluationContext, TResult>>(exp, ctxParam, getEvalContextExpression);

                //var finalProjectionExpressionType = typeof(LExpression).MakeGenericType(funcType);
                //var lambdaMi

                // Expression.Lambda<>() - call
                var finalLambdaMi = _lambdaMi.MakeGenericMethod(funcType);
                var functionExpr = finalLambdaMi.Invoke(null,
                    new object[]{ projectionExpression, new ParameterExpression[] {ctxParam} });

                var compileMi = functionExpr.GetType().GetMethod("Compile", System.Type.EmptyTypes);

                // .Compile()
                var compiledFunction = compileMi.Invoke(functionExpr, new object[0]);

                //functionExpr.GetType().InvokeMember("Compile")

                return LExpression.Call(
                    finalProjectionMi, 
                    contextExpression,
                    LExpression.Constant(compiledFunction));
                /*
                var genericList = typeof(List<>).MakeGenericType(projectionResultType);
                var genericEnumerable = typeof(IEnumerable<>).MakeGenericType(projectionResultType);

                var constructor = genericList.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { genericEnumerable },
                    null
                );

                       // todo: error: jak to ogarn¹æ! tej! kurwa! mo¿e jakiegoœ helpera trzeba dopisaæ!!!----
                       // tood: error: ewentualnie co?
                var arguments = new List<LExpression>();

                return LExpression.New(
                    constructor,
                    LExpression.NewArrayInit(projectionResultType, arguments));

                */
                // todo: error: nie znamy typu zwracanego przez expression chyba! co jest fatalne w skutkach!
                //expression.GetValue<>()
            }

            return null;
        }

           // todo: error: public
        public static List<TResult> Projection<TArg, TResult>(
            IEnumerable<TArg> source, Func<TArg, TResult> projectionFunction)
        {
            return new List<TResult>(from el in source select projectionFunction(el));
        }

        private readonly MethodInfo _projectionMi = typeof(ProjectionNode).GetMethod("Projection");

        //        private readonly MethodInfo _lambdaMi = typeof(System.Linq.Expressions.Expression<>).GetMethod("Lambda",
//        private readonly MethodInfo _lambdaMi = typeof(LExpression).GetMethod("Lambda",
  //          new[] { typeof(LExpression), typeof(ParameterExpression[]) });


        private readonly MethodInfo _lambdaMi = typeof(LExpression).GetMethods().FirstOrDefault(
            x => x.Name.Equals("Lambda", StringComparison.OrdinalIgnoreCase)
                && x.IsGenericMethod && x.GetParameters().Length == 2
                && x.GetParameters()[0].ParameterType == typeof(LExpression)
                && x.GetParameters()[1].ParameterType == typeof(ParameterExpression[]));

        /// <summary>
        /// Returns a <see cref="IList"/> containing results of evaluation
        /// of projection expression against each node in the context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            IEnumerable enumerable = context as IEnumerable;
            if(enumerable == null)
            {
                throw new ArgumentException(
                    "Projection can only be used on an instance of the type that implements IEnumerable.");
            }

            BaseNode expression = (BaseNode) this.getFirstChild();
            IList projectedList = new ArrayList();
            using (evalContext.SwitchThisContext())
            {
                foreach(object o in enumerable)
                {
                    evalContext.ThisContext = o;
                    projectedList.Add(GetValue(expression, o, evalContext));
                }
            }
            return projectedList;
        }

          // todo: error: create some helper class!
        private static LExpression ForEachExpression(
            LExpression collection, 
            ParameterExpression loopVar, 
            LExpression loopContent)
        {
            var elementType = loopVar.Type;
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType); 

            var enumeratorVar = LExpression.Variable(enumeratorType, "enumerator");
            var getEnumeratorCall = LExpression.Call(collection, enumerableType.GetMethod("GetEnumerator"));
            var enumeratorAssign = LExpression.Assign(enumeratorVar, getEnumeratorCall);

            // The MoveNext method's actually on IEnumerator, not IEnumerator<T>
            var moveNextCall = LExpression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));

            var breakLabel = LExpression.Label("LoopBreak");

            var loop = LExpression.Block(new[] { enumeratorVar },
                enumeratorAssign,
                LExpression.Loop(
                    LExpression.IfThenElse(
                        LExpression.Equal(moveNextCall, LExpression.Constant(true)),
                        LExpression.Block(new[] { loopVar },
                            LExpression.Assign(loopVar, LExpression.Property(enumeratorVar, "Current")),
                            loopContent
                        ),
                        LExpression.Break(breakLabel)
                    ),
                    breakLabel)
            );

            return loop;
        }
    }
}
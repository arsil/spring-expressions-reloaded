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
    /// Represents parsed selection node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class SelectionNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public SelectionNode()
            : base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected SelectionNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            if (!typeof(IEnumerable).IsAssignableFrom(contextExpression.Type))
            {
                throw new ArgumentException(
                    "Selection can only be used on an instance of the type that implements IEnumerable "
                    + $"(which is not the case with {contextExpression.Type}).");
            }

            var collectionIsGenericType = contextExpression.Type.IsGenericType;
            var collectionIsArray = contextExpression.Type.IsArray;

            if (!collectionIsGenericType && !collectionIsArray)
            {
                         // todo: error: exception!!!!
                return null;
            }

            var itemType = collectionIsGenericType
                ? contextExpression.Type.GetGenericArguments()[0]
                : contextExpression.Type.GetElementType();

            BaseNode expressionNode = (BaseNode)getFirstChild();

            BaseNode minIndexExpression = (BaseNode)expressionNode.getNextSibling();
            if (minIndexExpression != null)
                return null;

            // selector
            var ctxParam = LExpression.Parameter(itemType, "item");
            var getRootContextExpression = LExpression.Convert(ctxParam, itemType);

            var selectionExpression = GetExpressionTreeIfPossible(
                expressionNode,
                getRootContextExpression,
                compilationContext.CreateWithNewThisContext(getRootContextExpression));

            if (selectionExpression.Type != typeof(bool))
                return null;

            var finalSelectionMi = _selectionMi.MakeGenericMethod(itemType);
            var funcType = LExpression.GetFuncType(itemType, typeof(bool));

            // Expression.Lambda<>() - call
            var finalLambdaMi = _lambdaMi.MakeGenericMethod(funcType);
            var functionExpr = finalLambdaMi.Invoke(null,
                new object[] { selectionExpression, new ParameterExpression[] { ctxParam } });

            var compileMi = functionExpr.GetType().GetMethod("Compile", System.Type.EmptyTypes);

            // .Compile()
            var compiledFunction = compileMi.Invoke(functionExpr, new object[0]);

            return LExpression.Call(
                finalSelectionMi,
                contextExpression,
                LExpression.Constant(compiledFunction));
        }

        public static List<T> Selection<T>(
            IEnumerable<T> source, Func<T, bool> whereFunction)
        {
            return new List<T>(from el in source where whereFunction(el) select el);
        }

        private readonly MethodInfo _selectionMi = typeof(SelectionNode).GetMethod("Selection");

        private readonly MethodInfo _lambdaMi = typeof(LExpression).GetMethods().FirstOrDefault(
            x => x.Name.Equals("Lambda", StringComparison.OrdinalIgnoreCase)
                && x.IsGenericMethod && x.GetParameters().Length == 2
                && x.GetParameters()[0].ParameterType == typeof(LExpression)
                && x.GetParameters()[1].ParameterType == typeof(ParameterExpression[]));


        /// <summary>
        /// Returns a <see cref="IList"/> containing results of evaluation
        /// of selection expression against each node in the context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            IEnumerable enumerable = context as IEnumerable;
            if (enumerable == null)
            {
                throw new ArgumentException(
                    "Selection can only be used on an instance of the type that implements IEnumerable.");
            }

            BaseNode expression = (BaseNode)this.getFirstChild();
            BaseNode minIndexExpression = (BaseNode)expression.getNextSibling();
            BaseNode maxIndexExpression = (minIndexExpression == null) ? null : (BaseNode)minIndexExpression.getNextSibling();

            int minIndex = (int)((minIndexExpression == null)
                                      ? Int32.MinValue
                                      : GetValue(minIndexExpression, context, evalContext));
            int maxIndex = (int)((maxIndexExpression == null)
                                      ? Int32.MaxValue
                                      : GetValue(maxIndexExpression, context, evalContext));

            IList selectionList = new ArrayList();

            using (evalContext.SwitchThisContext())
            {
                int found = 0;
                foreach (object o in enumerable)
                {
                    evalContext.ThisContext = o;
                    bool isMatch = (bool)GetValue(expression, o, evalContext);
                    if (isMatch)
                    {
                        if (minIndex <= found && found <= maxIndex)
                        {
                            selectionList.Add(o);
                        }
                        found++;

                        if (found>maxIndex)
                        {
                            break; // don't look any further
                        }
                    }
                }
            }
            return selectionList;
        }
    }
}
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

using JetBrains.Annotations;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed selection node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class SelectionLastNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public SelectionLastNode():base()
        {
        }

        /// <summary>
        /// The compiled twin of <see cref="Get"/>, and <see cref="SelectionFirstNode"/>'s mirror - see the
        /// nullable-result reasoning there, which applies verbatim.
        /// <p>
        /// Two things differ from the first-match node. The interpreter demands an <c>IList</c> here, not
        /// merely an <c>IEnumerable</c>, so a source it would reject is refused rather than compiled, and
        /// the helper walks backwards over the indexer exactly as the interpreter does - so the predicate
        /// runs on the same items, in the same order, the same number of times. LINQ's
        /// <c>LastOrDefault</c> would give the same answer while evaluating the predicate for every item,
        /// which a side-effecting predicate can tell apart.
        /// </p>
        /// </summary>
        [NotNull]
        protected override LExpression GetExpressionTreeIfPossible(
            [NotNull] LExpression contextExpression,
            [NotNull] CompilationContext compilationContext)
        {
            // A refusal, not an ArgumentException: CompileErrorException is the only signal the weakly
            // typed path can fall back on, and the interpreter reports the bad source at evaluation.
            if (!typeof(IList).IsAssignableFrom(contextExpression.Type))
                throw CannotCompile("selection of the last match requires a source that implements IList");

            // The item type is the T of the IEnumerable<T> the source implements, not its first generic
            // argument. Null means it enumerates only untyped, or ambiguously; the interpreter serves
            // those. The helper takes IList<T>, so that has to hold for the item type as well.
            var itemType = CollectionOperandUtils.GetEnumerableItemType(contextExpression.Type);

            if (itemType == null
                || !typeof(IList<>).MakeGenericType(itemType).IsAssignableFrom(contextExpression.Type))
                throw CannotCompile("no compiled selection for this source type");

            BaseNode expressionNode = (BaseNode)getFirstChild();

            if (expressionNode.getNextSibling() != null)
                throw CannotCompile("no compiled selection for this source type");

            var ctxParam = LExpression.Parameter(itemType, "item");

            var selectionExpression = GetExpressionTreeIfPossible(
                expressionNode,
                ctxParam,
                compilationContext.CreateWithNewThisContext(ctxParam));

            // A nullable predicate has no compiled form here either - the interpreter reads a null result
            // as "no match", which needs the runtime value.
            if (selectionExpression.Type != typeof(bool))
                throw CannotCompile("no compiled selection for this predicate");

            var itemTypeIsNonNullableValueType
                = itemType.IsValueType && Nullable.GetUnderlyingType(itemType) == null;

            var finalSelectionMi = (itemTypeIsNonNullableValueType ? _selectionLastOrNullMi : _selectionLastMi)
                .MakeGenericMethod(itemType);

            var funcType = LExpression.GetFuncType(itemType, typeof(bool));

            // Expression.Lambda<>() - call
            var finalLambdaMi = _lambdaMi.MakeGenericMethod(funcType);
            var functionExpr = finalLambdaMi.Invoke(null,
                new object[] { selectionExpression, new ParameterExpression[] { ctxParam } });

            var compileMi = functionExpr.GetType().GetMethod("Compile", System.Type.EmptyTypes);

            // .Compile()
            var compiledFunction = compileMi.Invoke(functionExpr, new object[0]);

            // One item, not a collection - nothing to register as constructed.
            return LExpression.Call(
                finalSelectionMi,
                contextExpression,
                LExpression.Constant(compiledFunction));
        }

        /// <summary>
        /// Reference and already-nullable item types: default(T) is null, which is what the interpreter
        /// returns when nothing matched.
        /// </summary>
        [CanBeNull]
        public static T SelectionLast<T>(
            [NotNull] IList<T> source, [NotNull] Func<T, bool> whereFunction)
        {
            for (int i = source.Count - 1; i >= 0; i--)
            {
                var item = source[i];
                if (whereFunction(item))
                    return item;
            }

            return default(T);
        }

        /// <summary>
        /// Non-nullable value item types, where default(T) would be 0 rather than null.
        /// </summary>
        // No [CanBeNull] on the return: Nullable<T> already says it, and the annotation is not valid
        // on a value type.
        public static T? SelectionLastOrNull<T>(
            [NotNull] IList<T> source, [NotNull] Func<T, bool> whereFunction)
            where T : struct
        {
            for (int i = source.Count - 1; i >= 0; i--)
            {
                var item = source[i];
                if (whereFunction(item))
                    return item;
            }

            return null;
        }

        [NotNull]
        private readonly MethodInfo _selectionLastMi
            = typeof(SelectionLastNode).GetMethod(nameof(SelectionLast));

        [NotNull]
        private readonly MethodInfo _selectionLastOrNullMi
            = typeof(SelectionLastNode).GetMethod(nameof(SelectionLastOrNull));

        [NotNull]
        private readonly MethodInfo _lambdaMi = typeof(LExpression).GetMethods().FirstOrDefault(
            x => x.Name.Equals("Lambda", StringComparison.OrdinalIgnoreCase)
                && x.IsGenericMethod && x.GetParameters().Length == 2
                && x.GetParameters()[0].ParameterType == typeof(LExpression)
                && x.GetParameters()[1].ParameterType == typeof(ParameterExpression[]));

        /// <summary>
        /// Returns the last context item that matches selection expression.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            IList list = context as IList;

            if (list == null)
            {
                throw new ArgumentException(
                    "Selection can only be used on an instance of the type that implements IList.");
            }

            using (evalContext.SwitchThisContext())
            {
                BaseNode expression = (BaseNode) this.getFirstChild();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    object listItem = list[i];
                    evalContext.ThisContext = listItem;
                    // A null predicate result counts as no match: a filter treats "unknown" as false
                    // rather than as an error - a nullable operand inside the predicate can
                    // produce null, and unboxing it would throw.
                    object match = GetValue(expression, listItem, evalContext);
                    bool isMatch = match != null && (bool)match;
                    if (isMatch)
                    {
                        return listItem;
                    }
                }
            }
            return null;
        }
    }
}
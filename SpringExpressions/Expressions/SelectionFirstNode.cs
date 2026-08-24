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

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed selection node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class SelectionFirstNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public SelectionFirstNode():base()
        {
        }

        /// <summary>
        /// The compiled twin of <see cref="Get"/>, built like <see cref="SelectionNode"/>'s: the predicate
        /// is compiled to a delegate over the item type and handed to a static helper as a constant.
        /// <p>
        /// The one thing this node has to decide that a list-returning selection does not is what "nothing
        /// matched" is worth. The interpreter answers null, because it answers in <c>object</c>; a helper
        /// returning <c>T</c> would answer <c>default(T)</c>, which is 0 for an int source and a
        /// silent disagreement. So a non-nullable value item type is served by the
        /// <c>Nullable&lt;T&gt;</c> overload instead: boxing a nullable with no value produces the null
        /// reference itself, and boxing one with a value produces a plain boxed T, so the weakly typed path
        /// - which always asks for object - agrees with the interpreter on both the value and its runtime
        /// type. Reference and already-nullable item types need none of this: default(T) is already null.
        /// </p>
        /// </summary>
        [NotNull]
        protected override LExpression GetExpressionTreeIfPossible(
            [NotNull] LExpression contextExpression,
            [NotNull] CompilationContext compilationContext)
        {
            // A refusal, not an ArgumentException: CompileErrorException is the only signal the weakly
            // typed path can fall back on, and the interpreter reports the bad source at evaluation.
            if (!typeof(IEnumerable).IsAssignableFrom(contextExpression.Type))
                throw CannotCompile("selection requires a source that implements IEnumerable");

            var collectionIsGenericType = contextExpression.Type.IsGenericType;
            var collectionIsArray = contextExpression.Type.IsArray;

            if (!collectionIsGenericType && !collectionIsArray)
                throw CannotCompile("no compiled selection for this source type");

            var itemType = collectionIsGenericType
                ? contextExpression.Type.GetGenericArguments()[0]
                : contextExpression.Type.GetElementType();

            // The first generic argument is not always the item type - a dictionary's is its key type -
            // and the helper call would not bind. Asking the question keeps that out of the emitter.
            if (!typeof(IEnumerable<>).MakeGenericType(itemType).IsAssignableFrom(contextExpression.Type))
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

            var finalSelectionMi = (itemTypeIsNonNullableValueType ? _selectionFirstOrNullMi : _selectionFirstMi)
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
        public static T SelectionFirst<T>(
            [NotNull] IEnumerable<T> source, [NotNull] Func<T, bool> whereFunction)
        {
            foreach (var item in source)
            {
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
        public static T? SelectionFirstOrNull<T>(
            [NotNull] IEnumerable<T> source, [NotNull] Func<T, bool> whereFunction)
            where T : struct
        {
            foreach (var item in source)
            {
                if (whereFunction(item))
                    return item;
            }

            return null;
        }

        [NotNull]
        private readonly MethodInfo _selectionFirstMi
            = typeof(SelectionFirstNode).GetMethod(nameof(SelectionFirst));

        [NotNull]
        private readonly MethodInfo _selectionFirstOrNullMi
            = typeof(SelectionFirstNode).GetMethod(nameof(SelectionFirstOrNull));

        [NotNull]
        private readonly MethodInfo _lambdaMi = typeof(LExpression).GetMethods().FirstOrDefault(
            x => x.Name.Equals("Lambda", StringComparison.OrdinalIgnoreCase)
                && x.IsGenericMethod && x.GetParameters().Length == 2
                && x.GetParameters()[0].ParameterType == typeof(LExpression)
                && x.GetParameters()[1].ParameterType == typeof(ParameterExpression[]));

        /// <summary>
        /// Returns the first context item that matches selection expression.
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

            BaseNode expression = (BaseNode) this.getFirstChild();
            using (evalContext.SwitchThisContext())
            {
                foreach (object o in enumerable)
                {
                    evalContext.ThisContext = o;
                    // A null predicate result counts as no match: a filter treats "unknown" as false
                    // rather than as an error - a nullable operand inside the predicate can
                    // produce null, and unboxing it would throw.
                    object match = GetValue(expression, o, evalContext);
                    bool isMatch = match != null && (bool)match;
                    if (isMatch)
                    {
                        return o;
                    }
                }
            }
            return null;
        }
    }
}
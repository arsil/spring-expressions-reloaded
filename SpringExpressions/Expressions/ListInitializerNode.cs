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
using System.Linq.Expressions;
using System.Reflection;

using JetBrains.Annotations;

using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
	/// <summary>
	/// Represents parsed list initializer node in the navigation expression.
	/// </summary>
    /// <author>Aleksandar Seovic</author>
    public class ListInitializerNode : NodeWithArguments
	{
        /// <summary>
        /// Create a new instance
        /// </summary>
        public ListInitializerNode()
        {
        }

                protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var node = getFirstChild();


            var arguments = new List<LExpression>();
            Type commonType = null;
            var nullValuesArgumentIndexes = new List<int>(8);

            while (node != null)
            {
// todo: te checki ciągle się powtarzają... czy coś z tym zrobić? ------------------------------------------------------------
                //if (node.getFirstChild() is LambdaExpressionNode)
                //{
                //	argList.Add((BaseNode)node.getFirstChild());
                //}
                //else if (node is NamedArgumentNode)
                //{
                //	namedArgs.Add(node.getText(), node);
                //}
                //else

                var arg = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);

                // An item that is itself a collection this engine built is reshaped to what the
                // interpreter would have put in the list - it builds a List<object> and so boxes every
                // item, which leaves it no item type to keep. The caller's own collection is not
                // registered and goes in as the very instance.
                arg = compilationContext.NormalizeIfConstructed(arg, typeof(object));

                arguments.Add(arg);



                if (commonType == null)
                {
                    commonType = arg.Type;
                }
                else if (arg.Type != commonType)
                {
                    // todo: error: nullable? - to musi nullable nawalać!

                    // todo: error: gdzieś jeszcze zbieramy commonType!
                    // todo: error: to nie działa dobrze? shit!
                    var nullForReferenceTypeList
                        = !commonType.IsValueType
                        && arg is ConstantExpression constExpression
                        && constExpression.Value == null;

                    if (nullForReferenceTypeList)
                        nullValuesArgumentIndexes.Add(arguments.Count - 1);
                    else
                        commonType = typeof(object);
                }

                node = node.getNextSibling();
            }

            // A literal keeps its item type on both backends, or it is not compiled at all.
            //
            // The interpreter has no static types, so it works the item type out from the items'
            // RUNTIME types (see Get). That reaches the same answer as the unification above exactly
            // when no element's static type can be narrowed by the value it holds - see
            // TypeCheckingUtils.RuntimeCanNarrow. Where it can, the two would disagree, so the compiled path
            // stands aside and the interpreter is the only backend that runs. Nothing can diverge:
            // either both compute the same item type, or only one of them computes anything.
            foreach (var argument in arguments)
            {
                if (IsNullLiteral(argument))
                    continue;

                if (TypeCheckingUtils.RuntimeCanNarrow(argument.Type))
                {
                    throw CannotCompile(
                        $"an element is statically typed '{argument.Type}', which the value it holds "
                        + "can narrow at runtime, so the interpreter would infer a different item type "
                        + "for this literal");
                }
            }

            if (commonType == null)
                commonType = typeof(object);

            ConstructorInfo constructor;

            if (commonType != typeof(object))
            {
                // strongly typed list - allows lots of optimizations

                // null arguments handling
                foreach (var argIndex in nullValuesArgumentIndexes)
                    arguments[argIndex] = LExpression.Constant(null, commonType);


                // A plain List<T>. That this list was built here rather than read out of the object graph is
                // recorded on the CompilationContext below, not in the type, so no special type can travel
                // out with the value.
                var genericList = typeof(List<>).MakeGenericType(commonType);
                var genericEnumerable = typeof(IEnumerable<>).MakeGenericType(commonType);

                constructor = genericList.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { genericEnumerable },
                    null
                );
            }
            else
            {
                // List<object>, not ArrayList: no operator or literal result carries a pre-generics
                // collection any more. Registered like the typed case below, but the boundary will find
                // nothing to do - object is already the item type the interpreter would have produced.
                constructor = typeof(List<object>).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(IEnumerable<object>) },
                    null
                );

                for (int i = 0; i < arguments.Count; ++i)
                {
                    arguments[i] = LExpression
                        .Convert(arguments[i], typeof(object));
                }
            }

            // Deliberately NOT registered as a constructed collection. Registration exists so the
            // boundary can reshape a collection into the shape the interpreter would have built; here
            // the interpreter builds this very shape, so there is nothing to reconcile and reshaping
            // would throw away the item type both backends agreed on.
            return LExpression.New(
                constructor,
                LExpression.NewArrayInit(commonType, arguments));
        }


        private static bool IsNullLiteral([NotNull] LExpression element)
        {
            return element is ConstantExpression constant && constant.Value == null;
        }

        /// <summary>
        /// Creates new instance of the list defined by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object[] values = ResolveArguments(evalContext);

            // The item type comes from the items' runtime types, which is the interpreter's version of
            // the unification the compiled path does on static types - and the compiled path declines
            // the literal whenever the two could reach different answers, so they agree by
            // construction. A literal always has at least one item ('{}' is a syntax error), so unlike
            // a computed collection there is never nothing to work from.
            //
            // Nulls contribute no type and are simply carried: the type comes from the other items, as
            // it does compiled. A null beside a value type has nowhere to live, so that falls to object
            // - which is what the compiled path's own null handling does with it.
            Type commonType = null;
            var sawNull = false;

            foreach (var value in values)
            {
                if (value == null)
                {
                    sawNull = true;
                    continue;
                }

                var valueType = value.GetType();

                if (commonType == null)
                    commonType = valueType;
                else if (commonType != valueType)
                    commonType = typeof(object);
            }

            if (commonType == null || commonType == typeof(object)
                || (sawNull && commonType.IsValueType))
            {
                return new List<object>(values);
            }

            var list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(commonType), values.Length);

            foreach (var value in values)
                list.Add(value);

            return list;
        }
    }
}

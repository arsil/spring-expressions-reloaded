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

            var literal = LExpression.New(
                constructor,
                LExpression.NewArrayInit(commonType, arguments));

            compilationContext.MarkAsConstructedCollection(literal);
            return literal;
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

            // List<object>, not ArrayList. The interpreter sees boxed values and has no item type to work
            // from, so object is all it can offer; the compiled path keeps the item type where the
            // items share a type and is reprojected to match at the boundary. Both now agree, and neither
            // yields a pre-generics collection.
            return new List<object>(values);
        }
    }
}

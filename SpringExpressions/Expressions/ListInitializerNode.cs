#region License

/*
 * Copyright � 2002-2011 the original author or authors.
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
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
	/// <summary>
	/// Represents parsed list initializer node in the navigation expression.
	/// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class ListInitializerNode : NodeWithArguments
	{
        /// <summary>
        /// Create a new instance
        /// </summary>
        public ListInitializerNode()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected ListInitializerNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
// todo: te checki ci�gle si� powtarzaj�... czy co� z tym zrobi�? ------------------------------------------------------------
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
                if (arg == null)
                    return null;

                arguments.Add(arg);



                if (commonType == null)
                {
                    commonType = arg.Type;
                }
                else if (arg.Type != commonType)
                {
                    // todo: error: nullable? - to musi nullable nawala�!

                    // todo: error: gdzie� jeszcze zbieramy commonType!
                    // todo: error: to nie dzia�a dobrze? shit!
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
                // ArrayList for objects
                // we need conversion to object
                constructor = typeof(ArrayList).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(ICollection) },
                    null
                );

                for (int i = 0; i < arguments.Count; ++i)
                {
                    arguments[i] = LExpression
                        .Convert(arguments[i], typeof(object));
                }
            }

            return LExpression.New(
                constructor,
                LExpression.NewArrayInit(commonType, arguments));
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

               // todo: error: usp�jni� z kodem kompilatora - szczeg�lnie typy!!!!!
            return new ArrayList(values);
        }
    }
}

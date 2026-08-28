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
using JetBrains.Annotations;
using SpringUtil;
using System.Reflection;
using SpringCore.TypeResolution;
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed method node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class ArrayConstructorNode : NodeWithArguments
    {
        private Type arrayType;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public ArrayConstructorNode()
        {
        }

        	    protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
	    {
			if (arrayType == null)
			{
				lock (this)
				{
					if (arrayType == null)
					{
						arrayType = TypeResolutionUtils.ResolveType(getText());
					}
				}
			}


                // todo: error: czy nulle i nullable tutaj jakoś obsługujemy? co gdy mamy string i ktoś wali null? albo object i null?

			AST rankRoot = getFirstChild();
			int dimensions = rankRoot.getNumberOfChildren();

			if (dimensions > 0)
			{
				int i = 0;
				AST rankNode = rankRoot.getFirstChild();
				var args = new List<LExpression>();
				while (rankNode != null)
				{
					args.Add(GetExpressionTreeIfPossible((BaseNode)rankNode, contextExpression, compilationContext));
					rankNode = rankNode.getNextSibling();
				}
				return LExpression.NewArrayBounds(arrayType, args);
			}

		    AST valuesRoot = getFirstChild().getNextSibling();
		    if (valuesRoot != null)
		    {
                // The array's element type is the one the caller wrote, and each item is converted to it.
                //
                // This used to emit the initializer as a whole and call ToArray() on the List<T> it
                // built, which meant the result type was whatever the *items* unified to and the
                // declared type was never read at all: 'new long[] {1,2}' produced an int[], and
                // 'new string[] {1}' produced an int[] as well. A silently wrong type, where the
                // interpreter threw. Building the array here, item by item, is what makes the declared
                // type load-bearing.
                var items = new List<LExpression>();

                for (var itemNode = valuesRoot.getFirstChild();
                    itemNode != null;
                    itemNode = itemNode.getNextSibling())
                {
                    var item = GetExpressionTreeIfPossible(
                        (BaseNode)itemNode, contextExpression, compilationContext);

                    items.Add(ConvertItemToElementType(item, arrayType));
                }

                return LExpression.NewArrayInit(arrayType, items);
		    }

		    throw CannotCompile("no compiled form for this array construction");
	    }

        /// <summary>
        /// Converts one initializer item to the array's declared element type, or refuses.
        /// </summary>
        /// <remarks>
        /// The conversions allowed are the ones C# allows in an array initializer, and they are the
        /// ones this engine already rules on elsewhere: an identity, a reference or boxing conversion,
        /// or an implicit numeric widening from
        /// <see cref="TypeCheckingUtils.IsCSharpImplicitNumericConversion"/> - the same table the
        /// overload-resolution tier uses, so array initializers gain no rule of their own.
        /// <p>
        /// Everything else refuses, which is what C# does too: 'new int[] {1L}' narrows,
        /// 'new string[] {1}' is not a conversion at all. Both used to produce an array of the wrong
        /// type instead.
        /// </p>
        /// </remarks>
        [NotNull]
        private LExpression ConvertItemToElementType([NotNull] LExpression item, [NotNull] Type elementType)
        {
            if (item.Type == elementType)
                return item;

            // A null literal carries no useful type of its own - it arrives typed object - so it is
            // retyped rather than converted, and only where a null can actually live.
            if (IsNullLiteral(item))
            {
                if (!elementType.IsValueType || Nullable.GetUnderlyingType(elementType) != null)
                    return LExpression.Constant(null, elementType);

                throw CannotCompile(
                    $"null cannot be stored in an array of '{elementType}'");
            }

            if (elementType.IsAssignableFrom(item.Type))
                return LExpression.Convert(item, elementType);

            if (TypeCheckingUtils.IsCSharpImplicitNumericConversion(item.Type, elementType))
                return LExpression.Convert(item, elementType);

            throw CannotCompile(
                $"an item of type '{item.Type}' cannot be stored in an array of '{elementType}'");
        }

        private static bool IsNullLiteral([NotNull] LExpression item)
        {
            return item is System.Linq.Expressions.ConstantExpression constant && constant.Value == null;
        }

		/// <summary>
		/// Creates new instance of the type defined by this node.
		/// </summary>
		/// <param name="context">Context to evaluate expressions against.</param>
		/// <param name="evalContext">Current expression evaluation context.</param>
		/// <returns>Node's value.</returns>
		protected override object Get(object context, EvaluationContext evalContext)
        {
            if (arrayType == null)
            {
                lock (this)
                {
                    if (arrayType == null)
                    {
                        arrayType = TypeResolutionUtils.ResolveType(getText());
                    }
                }
            }

            AST rankRoot = getFirstChild();
            int dimensions = rankRoot.getNumberOfChildren();
            int[] ranks = new int[dimensions];
            if (dimensions > 0)
            {
                int i = 0;
                AST rankNode = rankRoot.getFirstChild();
                while (rankNode != null)
                {
                    ranks[i++] = (int)GetValue((BaseNode)rankNode, context, evalContext);
                    rankNode = rankNode.getNextSibling();
                }
                return Array.CreateInstance(arrayType, ranks);
            }
            else
            {
                AST valuesRoot = getFirstChild().getNextSibling();
                if (valuesRoot != null)
                {
                    // ICollection, not ArrayList: a list literal is a List<object> now, so this has to
                    // take whatever the initializer node built.
                    //
                    // The items are converted one at a time rather than block-copied. CopyTo is
                    // Array.Copy, which for a List<object> source unboxes each element and demands an
                    // exact type match - so 'new long[] {1, 2}' threw InvalidCastException over boxed
                    // ints, where C# widens them. The engine's own implicit-conversion table decides
                    // now, the same one the compiled path uses, so both backends widen alike and
                    // refuse alike.
                    var values = (ICollection)GetValue(((BaseNode)valuesRoot), context, evalContext);
                    var array = Array.CreateInstance(arrayType, values.Count);

                    var index = 0;
                    foreach (var value in values)
                        array.SetValue(ConvertItemValueToElementType(value, arrayType), index++);

                    return array;
                }
            }

            throw new ArgumentException("You have to specify either rank or initializer for an array.");
        }

        /// <summary>
        /// The interpreter's twin of <see cref="ConvertItemToElementType"/>, deciding from the runtime
        /// value rather than the static type - and by the same rules, so the backends agree.
        /// </summary>
        /// <exception cref="InvalidCastException">
        /// The item cannot be stored in an array of this type. <c>Array.Copy</c> raised the same
        /// exception for these shapes before the conversion was made explicit, so the type a caller
        /// sees for a genuinely bad item is unchanged.
        /// </exception>
        [CanBeNull]
        private static object ConvertItemValueToElementType(
            [CanBeNull] object value, [NotNull] Type elementType)
        {
            if (value == null)
            {
                if (!elementType.IsValueType || Nullable.GetUnderlyingType(elementType) != null)
                    return null;

                throw new InvalidCastException(
                    $"null cannot be stored in an array of '{elementType}'.");
            }

            if (elementType.IsInstanceOfType(value))
                return value;

            var underlyingElementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

            if (TypeCheckingUtils.IsCSharpImplicitNumericConversion(value.GetType(), underlyingElementType))
                return Convert.ChangeType(value, underlyingElementType);

            throw new InvalidCastException(
                $"an item of type '{value.GetType()}' cannot be stored in an array of '{elementType}'.");
        }
    }
}
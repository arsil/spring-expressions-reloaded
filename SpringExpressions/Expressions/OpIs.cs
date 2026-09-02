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
using System.Linq.Expressions;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents logical IS operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpIs : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpIs():base()
        {
        }

        /// <remarks>
        /// <p>
        /// <b><c>is</c> asks about the value, so it emits a runtime test.</b> This used to emit a
        /// <see cref="LExpression.Constant(object)"/> computed from
        /// <c>target.IsAssignableFrom(leftExpression.Type)</c> - a compile-time answer from the
        /// <i>static</i> type, which never looked at what the operand actually held. It broke both
        /// ordinary uses of the operator, measured over 20 shapes with 8 diverging:
        /// </p>
        /// <code>
        /// AnyInt is T(System.Int32)     object holding 45      was False, interpreter True
        /// AsAnimal is T(Dog)           Animal holding a Dog    was False, interpreter True
        /// Ints is T(List`1[Int32])     declared IList&lt;int&gt;      was False, interpreter True
        /// NullableNumber is T(Int32)   int? holding 7          was False, interpreter True
        /// NullName is T(System.String) a null string           was TRUE,  interpreter False
        /// </code>
        /// <p>
        /// The last row is the one that shows it was the wrong question rather than a strict answer: a
        /// null string was reported as being a string, because <c>string</c> is assignable from
        /// <c>string</c>.
        /// </p>
        /// <p>
        /// <see cref="LExpression.TypeIs"/> is what C# compiles <c>is</c> to, and it needed no ruling
        /// because all three parties already agreed: measured on every shape above, it matches the
        /// interpreter and matches C#, nullables included - <c>int?</c> holding a value is an
        /// <c>int</c>, one holding nothing is not. It is not constant-folded either, even for
        /// <c>int</c> against <c>int</c>.
        /// </p>
        /// </remarks>
                protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            if (leftExpression is ConstantExpression leftConst
                && leftConst.Value == null)
            {
                return LExpression.Constant(false, typeof(bool));
            }

            // The target has to be a type known now, because that is what TypeIs takes. A right operand
            // that only produces a Type at evaluation - a #variable holding one - is refused, and the
            // interpreter serves it by reading the value. The line this replaces asked
            // rightExpression.Type.IsAssignableFrom(...), comparing System.Type itself against the left
            // operand, which could only ever answer false.
            if (!(rightExpression is ConstantExpression rightConst))
                throw CannotCompile("the type to test against is only known at evaluation");

            if (rightConst.Value == null)
                return LExpression.Constant(false, typeof(bool));

            if (!(rightConst.Value is Type target))
                throw CannotCompile("the right operand of 'is' is not a type");

            // A Nullable<T> target needs no special case, which was worth measuring rather than
            // assuming: the interpreter reads instance.GetType(), and boxing a nullable that holds a
            // value yields the plain boxed T, so it looked as though it could never answer true for
            // one. It can - typeof(int?).IsAssignableFrom(typeof(int)) is True - and TypeIs agrees with
            // it on every row, an empty nullable included, which both call false.
            return LExpression.TypeIs(leftExpression, target);
        }


        /// <summary>
        /// Returns a value for the logical IS operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>
        /// true if the left operand is contained within the right operand, false otherwise.
        /// </returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object instance = GetLeftValue( context, evalContext );
            Type type = GetRightValue( context, evalContext ) as Type;

            if (instance == null || type == null)
            {
                return false;
            }
            return type.IsAssignableFrom(instance.GetType());
        }
    }
}
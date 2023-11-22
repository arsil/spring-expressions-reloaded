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
using System.Reflection;
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Base class for binary operators.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public abstract class BinaryOperator : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        protected BinaryOperator()
        {}

        /// <summary>
        /// Create a new instance with the supplied operands
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        protected BinaryOperator(BaseNode left, BaseNode right)
        {
            base.addChild(left);
            base.addChild(right);
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected BinaryOperator(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {}
        
        /// <summary>
        /// Gets the left operand.
        /// </summary>
        /// <value>The left operand.</value>
        public BaseNode Left
        {
            get { return (BaseNode) this.getFirstChild(); }
        }

        /// <summary>
        /// Evaluate the left operand
        /// </summary>
        protected object GetLeftValue(object context, EvaluationContext evalContext)
        {
            return GetValue(Left, context, evalContext);
        }
        /// <summary>
        /// Gets the right operand.
        /// </summary>
        /// <value>The right operand.</value>
        [CLSCompliant(false)]
        public BaseNode Right
        {
            get { return (BaseNode) this.getFirstChild().getNextSibling(); }
        }

        /// <summary>
        /// Evaluate the left operand
        /// </summary>
        protected object GetRightValue(object context, EvaluationContext evalContext)
        {
            return GetValue(Right, context, evalContext);
        }

           // todo: przenie��!!!!!

              // todo: error: to si� b�dzie myli�o!!!!!
        public static LExpression CreateBinaryExpressionForAllNumericTypesForNotNullChildren(
			LExpression leftExpression,
			LExpression rightExpression,
			Func<
			    LExpression,
			    LExpression,
			    System.Linq.Expressions.BinaryExpression> binaryFunctionCreator)
	    {
			var leftExpressionType = leftExpression.Type;
			var rightExpressionType = rightExpression.Type;

			var leftTypeCode = (int)System.Type.GetTypeCode(leftExpressionType);

			// For Char, Boolean, DBNull, Object, Empty, DateTime and String
			if (leftTypeCode < 5 || leftTypeCode > 15)
			    return null;

// TODO: konwersja user-typ�w
// TODO: przetestowa� dziwne rzutowania... np z double na decimal

			if (leftExpressionType != rightExpressionType)
			{
				// types are different
				var rightTypeCode = (int)System.Type.GetTypeCode(rightExpressionType);

				// For Char, Boolean, DBNull, Object, Empty, DateTime and String
				if (rightTypeCode < 5 || rightTypeCode > 15)
					return null;

				if (leftTypeCode > rightTypeCode)
				{
					// left has bigger precision
					rightExpression = LExpression.Convert(
						rightExpression, leftExpressionType);
				}
				else
				{
					leftExpression = LExpression.Convert(
						leftExpression, rightExpressionType);
				}
			}

			return binaryFunctionCreator(leftExpression, rightExpression);
		}


		// todo: co np. z try catch - gdy si� czego� nie da skompilowa�?
		// todo: czyli fallback na star� obs�ug�!
		// todo: jako� to trzeba zrobi�!



             // todo: error: to si� b�dzie myli�o!!!!!
		protected LExpression CreateBinaryExpressionForAllNumericTypesEvaluatingChildren(
            LExpression contextExpression,
            CompilationContext compilationContext,
            Func<
                LExpression, 
                LExpression, 
                System.Linq.Expressions.BinaryExpression> binaryFunctionCreator)
	    {
			var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
			var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

			if (leftExpression != null && rightExpression != null)
			{
				return CreateBinaryExpressionForAllNumericTypesForNotNullChildren(
					leftExpression,
					rightExpression,
					binaryFunctionCreator);
			}

			return null;


			// todo: co je�li zwr�ci�y null-e? przecie� to mo�e oznacza�, i� odpalaj�
			// todo: funkcje zwracaj�ce objecty... i co wtedy? 
			// todo: czy pr�bujemy emitowa� call do czego� takiego? czy to ma sens?

			// todo: w sumie to chyba jest jeden expression do wyemitowania?
			// todo: pewnie mo�na, tylko dostaniemy na twarz object i co z nim zrobi�? jak doda�?

			// todo: dupa... to nie jest constant...  wi�c nie ma to sensu...
			// todo: raczej wywo�anie metody powinno sprawdzi�, czy jest sta�ego typu i wtedy
			// todo: wyemitowa� odpowiedni� konwersj� do typu prostego! tak sobie my�l�!

			// jak dostaniemy na ryja dwa objecty, to w og�le b�dzi� kl�ska...

		}


        protected bool IsNumericExpression(LExpression expression)
        {
            var code = (int)System.Type.GetTypeCode(expression.Type);
            return (code >= 5 && code <= 15);
        }




    }
}
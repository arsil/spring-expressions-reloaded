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

           // todo: przenieœæ!!!!!

              // todo: error: to siê bêdzie myli³o!!!!!
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

// TODO: konwersja user-typów
// TODO: przetestowaæ dziwne rzutowania... np z double na decimal

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


		// todo: co np. z try catch - gdy siê czegoœ nie da skompilowaæ?
		// todo: czyli fallback na star¹ obs³ugê!
		// todo: jakoœ to trzeba zrobiæ!



             // todo: error: to siê bêdzie myli³o!!!!!
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


			// todo: co jeœli zwróci³y null-e? przecie¿ to mo¿e oznaczaæ, i¿ odpalaj¹
			// todo: funkcje zwracaj¹ce objecty... i co wtedy? 
			// todo: czy próbujemy emitowaæ call do czegoœ takiego? czy to ma sens?

			// todo: w sumie to chyba jest jeden expression do wyemitowania?
			// todo: pewnie mo¿na, tylko dostaniemy na twarz object i co z nim zrobiæ? jak dodaæ?

			// todo: dupa... to nie jest constant...  wiêc nie ma to sensu...
			// todo: raczej wywo³anie metody powinno sprawdziæ, czy jest sta³ego typu i wtedy
			// todo: wyemitowaæ odpowiedni¹ konwersjê do typu prostego! tak sobie myœlê!

			// jak dostaniemy na ryja dwa objecty, to w ogóle bêdziê klêska...

		}


        protected bool IsNumericExpression(LExpression expression)
        {
            var code = (int)System.Type.GetTypeCode(expression.Type);
            return (code >= 5 && code <= 15);
        }




    }
}
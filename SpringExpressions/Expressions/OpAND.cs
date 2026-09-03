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
using SpringExpressions.Expressions.Compiling;
using SpringExpressions.Util;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents AND operator (both, bitwise and logical).
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpAND : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpAND()
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpAND(BaseNode left, BaseNode right)
            :base(left, right)
        {
        }

        
        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            var expression = BitwiseOrLogicalOperatorHelper.CreateAndExpression(
                leftExpression, rightExpression);

            // The helper is [CanBeNull] and answers null for "no compiled form"; this method is
            // [NotNull]. Returning that null straight out left BaseNode's dispatcher to turn it into
            // "node produced no expression tree" - a refusal naming no reason at all, and 1,638 of the
            // compilation sweep's 7,556 refusals came from these three operators doing it. The node is
            // also the only one that can name itself, which is why the refusal is thrown here rather
            // than inside the helper: CannotCompile is an instance method, and a helper in
            // Expressions/Compiling has no BaseNode to name.
            //
            // What the message has to say is which role was undecidable. 'and' is one operator serving
            // two - logical for booleans, bitwise for integers and enums - and the operand types are
            // what choose between them, so they are what the reader needs. OpNOT already words the
            // same problem this way for the unary spelling.
            if (expression == null)
                throw CannotCompile(
                    "no compiled 'and' for '" + leftExpression.Type + "' and '" + rightExpression.Type
                    + "'; the logical form takes two booleans and the bitwise form two integers or enums");

            return expression;
        }

        /// <summary>
        /// Returns a value for the logical AND operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object l = GetLeftValue(context, evalContext);

            // One operator serves two roles here - logical for booleans, bitwise for integers and enums -
            // and the left operand decides which. A left operand that is neither null, an integer nor an
            // enum rules out the bitwise role whatever the right operand turns out to be, so this is the
            // logical operator, and the logical operator short-circuits: "false and X" never evaluates X.
            if (l != null && !TypeCheckingUtils.IsInteger(l) && !(l is Enum))
            {
                return BooleanUtils.RequireBoolean(l, LogicalOperand)
                    && BooleanUtils.RequireBoolean(
                        GetRightValue(context, evalContext), LogicalOperand);
            }

            object r = GetRightValue(context, evalContext);

            // Nothing combined with nothing is nothing, the same rule OpADD states in full. Two
            // nulls do not tell this side which of the operator's two roles applies - the operands
            // could have been integers or booleans and both arrive as a bare null - so it fell into
            // the logical branch below and coerced them to false, while the compiled path read the
            // declared types, took the bitwise role and lifted to null. One answer has to serve both,
            // and propagation is the one the rest of arithmetic uses.
            if (l == null && r == null)
                return null;

            var leftIsInteger = TypeCheckingUtils.IsInteger(l);
            var rightIsInteger = TypeCheckingUtils.IsInteger(r);

            if (leftIsInteger && rightIsInteger)
            {
                return NumberUtils.BitwiseAnd(l, r);
            }

            // Nullable value types are boxed as values or nulls, so we may get
            // null values for Nullable<T>
            // Any math operation involving value and null returns null
            if ((leftIsInteger || rightIsInteger) && (l == null || r == null))
            {
                return null;
            }

            if (l is Enum)
            {
                if (l.GetType() == r.GetType())
                {
                    Type enumType = l.GetType();
                    Type integralType = Enum.GetUnderlyingType(enumType);
                    l = Convert.ChangeType(l, integralType);
                    r = Convert.ChangeType(r, integralType);
                    object result = NumberUtils.BitwiseAnd(l, r);
                    return Enum.ToObject(enumType, result);
                }
            }

            // The right operand has already been evaluated above. Reading it again here would run its
            // side effects a second time.
            //
            // Everything that reaches this line is either a boolean, a null read as false, or an
            // operand that is not a truth value at all - a bitwise pair was claimed above, and so was
            // the lifted null. '45 and true' lands here, and used to answer True by coercing the 45.
            return BooleanUtils.RequireBoolean(l, LogicalOperand)
                && BooleanUtils.RequireBoolean(r, LogicalOperand);
        }

        /// <summary>
        /// How the logical role of this operator names itself when refusing a non-boolean operand.
        /// </summary>
        private const string LogicalOperand = "operator 'and'";
    }
}
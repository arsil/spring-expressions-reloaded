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

using JetBrains.Annotations;

using SpringExpressions.Parser.antlr.collections;
using SpringExpressions.Util;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents ternary expression node.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class TernaryNode : BaseNode
    {
        private bool initialized = false;
        private BaseNode condition;
        private BaseNode trueExp;
        private BaseNode falseExp;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public TernaryNode():base()
        {
        }

                /// <summary>
        /// Returns a value for the string literal node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            if (!initialized)
            {
                lock (this)
                {
                    if (!initialized)
                    {
                        AST node = this.getFirstChild();
                        condition = (BaseNode) node;
                        node = node.getNextSibling();
                        trueExp = (BaseNode) node;
                        node = node.getNextSibling();
                        falseExp = (BaseNode) node;

                        initialized = true;
                    }
                }
            }

            // Only a boolean, or a null read as false - see BooleanUtils. This used to be
            // Convert.ToBoolean, which made '45 ? a : b' answer 'a' where the compiled path had no such
            // conversion and '45 == true' refused the pair outright.
            if (BooleanUtils.RequireBoolean(
                    GetValue(condition, context, evalContext), "the conditional test"))
            {
                return GetValue(trueExp, context, evalContext);
            }
            else
            {
                return GetValue(falseExp, context, evalContext);
            }
        }

        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            AST node = getFirstChild();
            var conditionExpression = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);

			node = node.getNextSibling();
            var trueExpression = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);

            node = node.getNextSibling();
            var falseExpression = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);

            return LExpression.Condition(
                AsConditionTest(conditionExpression), trueExpression, falseExpression);
        }

        /// <summary>
        /// The test of a compiled conditional: a bool, or a nullable bool with nothing in it read as
        /// false. Anything else has no compiled form and is refused.
        /// </summary>
        /// <remarks>
        /// <p>
        /// C# allows only <c>bool</c> here, or a type declaring <c>operator true</c> - a number is
        /// <c>CS0029</c> and a <c>bool?</c> is <c>CS0266</c>. This engine's interpreter is more
        /// permissive: it runs <c>Convert.ToBoolean</c>, so <c>45 ? a : b</c> answers <c>a</c> and
        /// <c>'Ana' ? a : b</c> throws <c>FormatException</c>. That is inherited behaviour and it stays
        /// - the interpreter serves every shape refused here - but it is deliberately **not** emitted:
        /// compiling a truthiness conversion would bake a rule this engine has never ruled into the
        /// fast path, where C# itself has no such conversion at all.
        /// </p>
        /// <p>
        /// The nullable case is different, and is the one shape that must compile: a null in a boolean
        /// context reads as false throughout this engine - the same rule that makes 'null and true'
        /// false - and the conditional operator is named in that ruling. GetValueOrDefault is lifting,
        /// not truthiness; there is no conversion here, only the absence of a value.
        /// </p>
        /// <p>
        /// Without this check LExpression.Condition raised ArgumentException("Argument must be
        /// boolean") from inside the emitter, which the absorber then reported as an internal compiler
        /// error - a defect of ours, for a shape that is merely uncompiled.
        /// </p>
        /// </remarks>
        [NotNull]
        private LExpression AsConditionTest([NotNull] LExpression conditionExpression)
        {
            if (conditionExpression.Type == typeof(bool))
                return conditionExpression;

            if (conditionExpression.Type == typeof(bool?))
                return LExpression.Call(conditionExpression, NullableBoolGetValueOrDefault);

            throw CannotCompile(
                $"the conditional test is '{conditionExpression.Type}' rather than a boolean; only the "
                + "interpreter reads other types as true or false");
        }

        private static readonly MethodInfo NullableBoolGetValueOrDefault
            = typeof(bool?).GetMethod("GetValueOrDefault", new Type[0]);
    }
}
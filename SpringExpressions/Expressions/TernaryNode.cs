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

            ReconcileBranchTypes(ref trueExpression, ref falseExpression);

            return LExpression.Condition(
                AsConditionTest(conditionExpression), trueExpression, falseExpression);
        }

        /// <summary>
        /// Brings the two branches to one type, or refuses. Only two disagreements have an answer: a
        /// null literal takes the other branch's type, and a value type meeting its own nullable form
        /// lifts.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <c>LExpression.Condition</c> demands both branches be of the same type and raises
        /// ArgumentException("Argument types do not match") otherwise - which the absorber then
        /// reported as an internal compiler error, a defect of ours for a shape that is merely
        /// uncompiled. <see cref="AsConditionTest"/> exists for the same reason on the *test* operand;
        /// the branches were the other half of the same call and were left behind.
        /// </p>
        /// <p>
        /// Everything else refuses, and a measurement forces that rather than taste. The interpreter
        /// has no common-type rule at all - it returns whichever branch ran, untouched - so
        /// <c>x ? 1 : 2.5</c> is <c>1</c> as an Int32 when the test holds and <c>2.5</c> as a Double
        /// when it does not. The result *type* follows the branch taken, so any conversion to a common
        /// type diverges from the interpreter, C#'s own numeric widening included: it would answer
        /// <c>1.0</c> where the interpreter answers <c>1</c>. That makes this deliberately stricter
        /// than C# for convertible pairs, and exactly as strict for incompatible ones
        /// (<c>x ? 'a' : 0</c> is CS0173).
        /// </p>
        /// <p>
        /// Boxing both branches to <c>object</c> was considered and rejected. It would compile more
        /// shapes and preserve every value, but an object-typed result carries no type for anything
        /// downstream - so the conditional becomes the thing needing a cast - and it turns branches
        /// that disagree, usually a mistake, into something plausible. Casting a *branch* is the
        /// escape and it yields a real type: <c>x ? 1.0 : 2.5</c>.
        /// </p>
        /// </remarks>
        private void ReconcileBranchTypes(
            [NotNull] ref LExpression trueExpression, [NotNull] ref LExpression falseExpression)
        {
            if (trueExpression.Type == falseExpression.Type)
                return;

            // A null literal carries no type of its own - it arrives typed object - so it is retyped
            // rather than converted, exactly as MethodNode.ConvertParameters retypes a null argument
            // and ArrayElementConversions a null array item.
            if (TryRetypeNullLiteral(ref trueExpression, falseExpression.Type)
                || TryRetypeNullLiteral(ref falseExpression, trueExpression.Type))
            {
                return;
            }

            // A value type meeting its own nullable form lifts, which is this engine's standing
            // policy for nullable operands (NullableValueTypesHelper, NullableMathTests) applied in
            // its mildest form: nothing propagates here, the branch value is returned untouched, and
            // lifting only widens the static type enough to hold both possibilities. Boxing a
            // nullable yields the plain boxed T or the null reference, so no value on the heap can
            // tell the two backends apart.
            if (TryLiftToNullable(ref trueExpression, falseExpression.Type)
                || TryLiftToNullable(ref falseExpression, trueExpression.Type))
            {
                return;
            }

            throw CannotCompile(
                $"the conditional branches are '{trueExpression.Type}' and '{falseExpression.Type}'; "
                + "there is no compiled form for a conditional whose branches differ, because the "
                + "interpreter returns whichever branch ran without converting it. Cast a branch to "
                + "give both the same type");
        }

        private static bool TryRetypeNullLiteral(
            [NotNull] ref LExpression branch, [NotNull] Type otherBranchType)
        {
            if (!(branch is ConstantExpression constant) || constant.Value != null)
                return false;

            if (otherBranchType.IsValueType && Nullable.GetUnderlyingType(otherBranchType) == null)
                return false;

            branch = LExpression.Constant(null, otherBranchType);
            return true;
        }

        private static bool TryLiftToNullable(
            [NotNull] ref LExpression branch, [NotNull] Type otherBranchType)
        {
            if (Nullable.GetUnderlyingType(otherBranchType) != branch.Type)
                return false;

            branch = LExpression.Convert(branch, otherBranchType);
            return true;
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
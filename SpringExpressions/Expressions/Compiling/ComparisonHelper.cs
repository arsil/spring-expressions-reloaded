using System;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using System.Collections.Generic;
using SpringExpressions.Util;

using LExpression = System.Linq.Expressions.Expression;
using LBinaryExpression = System.Linq.Expressions.BinaryExpression;


namespace SpringExpressions.Expressions.Compiling
{
    internal static class ComparisonHelper
    {
        public enum ComparisonOperator
        {
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual
        }

            // todo: error: to nie zadziała dla == i !=

        [ContractAnnotation(
            "=>true,resultExpression:notnull;=>false,resultExpression:null")]
        public static bool CreateCompare(
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression,
            [NotNull] Func<
                            LExpression,
                            LExpression,
                            LBinaryExpression> comparisonExpression,
            [CanBeNull] string operatorMethodName,
            [CanBeNull] Func<LExpression, LExpression, MethodInfo, LBinaryExpression> userDefinedFactory,
            out LExpression resultExpression)
        {
            // A custom real-valued operand converts through its own implicit operator before the
            // comparison rules run, so a caller's decimal-like struct compares like the built-in real
            // it converts to - on this backend and the interpreter alike.
            leftExpression = BinaryNumericOperatorHelper.ConvertCustomReal(leftExpression);
            rightExpression = BinaryNumericOperatorHelper.ConvertCustomReal(rightExpression);

            // Nothing sorts before everything, and a nullable holding no value is one of the kinds of
            // nothing. The three outcomes are the operator applied to the *sort order* the two operands
            // would have - so the rule is written once here rather than as twelve booleans, and
            // '<=' and '>=' come out true for two nothings without anyone having to say so:
            //
            //   nothing vs a value   -1      so  '<' is true, '>' is false
            //   a value vs nothing   +1      so  '>' is true, '<' is false
            //   nothing vs nothing    0      so  '<=' and '>=' are true, '<' and '>' are false
            //
            // These used to be two constants, both false - C#'s rule, where any comparison against a
            // null is false. That made a nullable the only kind of nothing this engine ordered
            // differently: a null literal and a null reference already sorted first on both backends,
            // and so did a nullable on the interpreter. Only the compiled path followed C#, which is
            // the whole of open-issues item 17 - 116 rows of the evaluation sweep.
            //
            // Note this is deliberately *not* what the NaN ruling did, where the operators were taken
            // off the sorting answer. The two are told apart by the frozen suite: it pins null sorting
            // first for a null literal ('null < xyz' is True) and says nothing about NaN, so for null
            // the sorting answer is inherited behaviour and for NaN it never was.
            LExpression SortOrderAnswer(int order)
                => comparisonExpression(LExpression.Constant(order), LExpression.Constant(0));

            if (NullableValueTypesHelper.TryCreateForComparison(
                    leftExpression,
                    rightExpression,
                    SortOrderAnswer(-1),
                    SortOrderAnswer(1),
                    SortOrderAnswer(0),
                    (l, r) => HandleValueTypesComparison(
                        l, r, comparisonExpression, operatorMethodName, userDefinedFactory),
                    out var binaryExpression1
                    ))
            {
                resultExpression = binaryExpression1;
                return true;
            }
            // todo: error:
            // null constant?


                   // todo: error: nullable vs notNullable
            if (leftExpression.Type == rightExpression.Type)
            {
                var mi = MiCompareSameTypes.MakeGenericMethod(leftExpression.Type);

                resultExpression
                    = comparisonExpression(
                        LExpression.Call(mi, leftExpression, rightExpression),
                        LExpression.Constant(0));
                return true;
            }


             // todo; error: return false!!!

                 // todo: error: null const value handling???--------------------------------------------------------------------------------------------------

            var biedaszyb = CreateIComparableComparisonWithNullHandling(
                leftExpression,
                rightExpression,
                comparisonExpression,
                0);

            resultExpression = biedaszyb;
            return resultExpression != null;
        }

        /// <remarks>
        /// <p>
        /// <b>The operands arrive here with any nullable already unwrapped</b> - <c>left.Value</c> and
        /// <c>right.Value</c>, supplied by <see cref="NullableValueTypesHelper.TryCreateForComparison"/>
        /// - which is why the user-defined operator lookup has to be repeated here and not only in the
        /// node. The node asks about the operands as written, so <c>Ordered? &lt; Ordered</c> finds
        /// nothing there: the lookup demands exact operand types and <c>Nullable&lt;Ordered&gt;</c>
        /// declares no operators of its own.
        /// </p>
        /// <p>
        /// Without it the pair fell through to the <c>Comparer&lt;T&gt;.Default</c> branch below, which
        /// <b>compiles and then throws at evaluation</b> for a type that declares operators but no
        /// <see cref="IComparable"/> - a hard failure in every mode, since compilation succeeded and the
        /// weakly typed fallback was long finished. Measured: <c>NullableTwo &lt; One</c> threw
        /// <see cref="ArgumentException"/> on every path while the interpreter, which sees a boxed
        /// <c>Ordered</c> and never a wrapper, answered correctly. Open-issues item 18.
        /// </p>
        /// <p>
        /// <b>The null behaviour is not decided here and must not be.</b> The caller's three
        /// sort-order outcomes already say what an empty nullable answers - nothing sorts before
        /// everything, item 17's ruling - so this only has to produce the comparison for two present
        /// values. That is also why the factory passed in uses <c>liftToNull: false</c> harmlessly: by
        /// this point neither operand can be null. Lifting through
        /// <c>LExpression.LessThan(l, r, liftToNull: true, m)</c> instead would hand C#'s rule
        /// (any comparison against a null is false) back to a question item 17 settled the other way.
        /// </p>
        /// </remarks>
        [CanBeNull]
        private static LExpression HandleValueTypesComparison(
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression,
            Func<LExpression, LExpression, BinaryExpression> comparisonExpression,
            [CanBeNull] string operatorMethodName,
            [CanBeNull] Func<LExpression, LExpression, MethodInfo, LBinaryExpression> userDefinedFactory)
        {
            // A type's own operator, before anything else - the same rule and the same code the four
            // comparison nodes run, just on operands a nullable has been unwrapped from. A caller that
            // passes no operator is saying it does not want one: 'between' is the only such caller, and
            // its reason is at that call site.
            if (operatorMethodName != null && userDefinedFactory != null)
            {
                var userDefined = UserDefinedOperatorUtils.TryCreateComparison(
                    leftExpression, rightExpression, operatorMethodName, userDefinedFactory);

                if (userDefined != null)
                    return userDefined;
            }

            // try numeric comparision
            if (BinaryNumericOperatorHelper.TryCreate(
                    leftExpression,
                    rightExpression,
                    comparisonExpression, out var binaryExpression))
            {
                return  binaryExpression;
            }

            // todo: error: czy to ma sens!!!!?????
            /*
                        if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
                        {
                            // left ? 1 : 0  [comparisonExpression] right ? 1 : 0
                            return comparisonExpression(
                                LExpression.Condition(leftExpression, LExpression.Constant(1), LExpression.Constant(0)),
                                LExpression.Condition(rightExpression, LExpression.Constant(1), LExpression.Constant(0)));
                        }
            */
            if (leftExpression.Type == rightExpression.Type)
            {
                var mi = MiCompareSameTypes.MakeGenericMethod(leftExpression.Type);

                return comparisonExpression(
                        LExpression.Call(mi, leftExpression, rightExpression),
                        LExpression.Constant(0));
            }

            return null;
        }

        private static int CompareSameTypes<T>(T first, T second)
        {
            return Comparer<T>.Default.Compare(first, second);
        }

        private static readonly MethodInfo MiCompareSameTypes = typeof(ComparisonHelper)
            .GetMethod(nameof(CompareSameTypes), BindingFlags.Static | BindingFlags.NonPublic);


        static LExpression CreateIComparableComparisonWithNullHandling(
            LExpression leftExpression,
            LExpression rightExpression,
            Func<
                LExpression,
                LExpression,
                LBinaryExpression> comparisonExpression,
            int comparisonValue)
        {
            // This branch emits ((IComparable)left).CompareTo((object)right), which succeeds only when
            // the right operand's *runtime* type is one the left's CompareTo accepts - in practice, its
            // own type. Asking only whether the left is IComparable is therefore a guess about the
            // right, and it showed:
            //
            //   Age < Anything, an int against an object holding 50L or 50.5, threw ArgumentException
            //   compiled where the interpreter promoted and answered. The same expression with an int
            //   in the box was correct, so the guess was right exactly as often as the box happened to
            //   match.
            //
            // It also produced an asymmetry that gives the accident away: 'Age < Anything' compiled
            // while 'Anything < Age' did not, because only the left operand was ever consulted. The
            // refusing side was the correct one.
            //
            // Requiring the right operand to be assignable to the left is what the static types can
            // actually promise. Everything else refuses and the interpreter compares the runtime values,
            // which is open-issues item 21's rule: a comparison the static types do not determine is
            // refused, not guessed. Numeric pairs never reach here - BinaryNumericOperatorHelper claims
            // them above - and same-typed pairs are handled by the caller, so what this turns away is
            // genuinely undecidable: an object-typed operand, or two unrelated types C# would reject.
            // A null literal on the right is allowed through: CompareTo(null) answers "greater than
            // null" for every value type, which is the inherited null-sorts-first rule the frozen suite
            // pins - '123 < null' is False, 'null < xyz' is True. That is settled semantics, not a
            // guess, and it is not item 17's question either (which is about a *nullable value type*
            // holding nothing, not a null literal).
            if (typeof(IComparable).IsAssignableFrom(leftExpression.Type)
                && (leftExpression.Type.IsAssignableFrom(rightExpression.Type)
                    || IsNullConstant(rightExpression)))
            {
                LExpression comparison =
                    LExpression.Call(
                        LExpression.Convert(leftExpression, typeof(IComparable)),
                        CompareToMethodInfo,
                        LExpression.Convert(rightExpression, typeof(object)));

                // A left operand that can hold null gets the same first lines CompareUtils.Compare has
                // always had: null equals null, and null is below anything else. Without them the
                // emitted CompareTo was invoked *on* the null - 'Name < null' with a null Name was a
                // NullReferenceException compiled where the interpreter answered False.
                //
                // Only the left needs it. A null on the right is CompareTo's own business and it
                // answers correctly: every value is greater than null, which is the same rule.
                //
                // This is a null *reference*, whose ordering is settled and pinned in the frozen suite.
                // It is not item 17, which asks what a nullable value type holding nothing should do -
                // those operands never reach here, NullableValueTypesHelper claims them first.
                if (!leftExpression.Type.IsValueType)
                {
                    var leftIsNull = LExpression.ReferenceEqual(
                        LExpression.Convert(leftExpression, typeof(object)),
                        LExpression.Constant(null, typeof(object)));

                    var rightIsNull = LExpression.ReferenceEqual(
                        LExpression.Convert(rightExpression, typeof(object)),
                        LExpression.Constant(null, typeof(object)));

                    comparison = LExpression.Condition(
                        leftIsNull,
                        LExpression.Condition(
                            rightIsNull,
                            LExpression.Constant(0),
                            LExpression.Constant(-1)),
                        comparison);
                }

                return comparisonExpression(comparison, LExpression.Constant(comparisonValue));
            }

            if (IsNullConstant(leftExpression))
            {
                if (rightExpression.Type.IsValueType)
                    rightExpression = LExpression.Convert(rightExpression, typeof(object));

                return comparisonExpression(
                    LExpression.Condition(
                        LExpression.Equal(rightExpression, LExpression.Constant(null)),
                        LExpression.Constant(0),
                        LExpression.Constant(-1)),
                    LExpression.Constant(comparisonValue));
            }

            return null;
        }

        private static bool IsNullConstant(LExpression expression)
        {
            return expression is ConstantExpression constant && constant.Value == null;
        }

        // todo: wyrzucić może?
        private static readonly MethodInfo CompareToMethodInfo
            = typeof(IComparable).GetMethod("CompareTo", new[] { typeof(object) });

    }
}

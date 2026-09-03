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
using System.Reflection;
using JetBrains.Annotations;
using SpringCollections;
using SpringExpressions.Expressions.Compiling;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents arithmetic addition operator.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class OpADD : BinaryOperator
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public OpADD()
        {
        }

        
        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
// TODO: dodanie char + char daje inta...!

            var leftExpression = GetExpressionTreeIfPossible(Left, contextExpression, compilationContext);
            var rightExpression = GetExpressionTreeIfPossible(Right, contextExpression, compilationContext);

            // A type's own operator is found before any conversion is considered, which is C#'s order.
            // Without it a type that both converts to a number and declares 'operator +' erased itself:
            // 'a + b' answered the number it converts to rather than the type. Built-in numeric pairs
            // never reach here - the promotion rules keep that whole space.
            var userDefined = TryCreateUserDefinedBinary(
                leftExpression, rightExpression, "op_Addition", LExpression.Add);

            if (userDefined != null)
                return userDefined;

            if (BinaryNumericOperatorHelper.TryCreate(
                leftExpression, rightExpression,
                LExpression.Add, out var resultExpression))
            {
                return resultExpression;
            }

            if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(string))
            {
                // A null span propagates rather than reaching TimeSpan.Parse, which answers a null with
                // ArgumentNullException. The interpreter has no such branch for a null - it is not a
                // string, so the pair falls to the propagation rule above and answers null - and this
                // is the same rule seen from the compiled side. The result becomes DateTime? to carry
                // it, which boxes to a DateTime or to nothing and so is invisible at an object root.
                return AddDateTimeAndSpanIfPresent(
                    leftExpression,
                    rightExpression,
                    span => LExpression.Equal(span, LExpression.Constant(null, typeof(string))),
                    span => LExpression.Call(TimeSpanMethods.TimeSpanParseMethodInfo, span));
            }

            if (leftExpression.Type == typeof(DateTime)
                && ExpressionTypeHelper.IsNumericOrNullableNumericExpression(
                    rightExpression, out var daysAreNullable, out _))
            {
                if (!daysAreNullable)
                {
                    // (DateTime) left + TimeSpan.FromDays(Convert.ToDouble(right));
                    return LExpression.Call(
                        DateTimeMethods.DateTimeAddTimeSpanMethodInfo,
                        leftExpression,
                        LExpression.Call(
                            TimeSpanMethods.TimeSpanFromDaysMethodInfo,
                            LExpression.Convert(rightExpression, typeof(double))));
                }

                // 'When + NullableNumber' had no compiled form at all, because the guard above asked
                // IsNumericExpression, which an 'int?' fails. Nothing else was missing: a nothing added
                // to a date is nothing, which is the standing rule for every arithmetic operator, and
                // the string branch a few lines up already produces a DateTime? for exactly that
                // reason. The interpreter answered all along - it sees an unwrapped value or a bare
                // null - so no answer changes here; the shape simply compiles now.
                return AddDateTimeAndSpanIfPresent(
                    leftExpression,
                    rightExpression,
                    days => LExpression.Not(LExpression.Property(days, "HasValue")),
                    days => LExpression.Call(
                        TimeSpanMethods.TimeSpanFromDaysMethodInfo,
                        LExpression.Convert(
                            LExpression.Property(days, "Value"), typeof(double))));
            }

            // There is deliberately no DateTime + DateTime branch. The BCL has no such operation -
            // DateTime.Add takes a TimeSpan - so the reflective lookup this branch used returned null
            // at type-initialisation and every 'When + When' died on the null MethodInfo. The
            // interpreter has always rejected the pair ("Cannot add instances of 'System.DateTime' and
            // 'System.DateTime'"), and falling through to the refusal below is what agrees with it.
            // The neighbouring branches are the real operations: DateTime + TimeSpan, DateTime + a
            // number of days, and DateTime + a parseable TimeSpan string.

            // todo: error: coś robić dla objecta ??????? czy może ścieżka interpretacji?
            // todo: moim zdaniem jak gdzieś mamy objecta, to jest klęska i mamy w tupie taką robotę!

            /*
                if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(object))
                {
                    return LExpression.Condition(
                        LExpression.TypeIs(rightExpression, typeof(TimeSpan)),
                        leftExpression,
                        LExpression.Throw(LExpression.Constant(new InvalidOperationException("Sraczka"))));

                    return LExpression.Condition(
                        LExpression.TypeIs(rightExpression, typeof(TimeSpan)),
                        leftExpression, 
                        LExpression.Throw(LExpression.Constant(new InvalidOperationException("Sraczka"))));

                    // todo: dupa blada, bo gdy dostaniemy np. object w right, to nic nie zrobimy
                    // todo: aktualnie... tzn. musielibyśmy interpretować wartości i próbować je parsować!!!
                }*/


            // one of exp is a string expression - we use Concat
            if (leftExpression.Type == typeof(string) || rightExpression.Type == typeof(string))
            {
                // '+' concatenates only when at least one operand is an actual string at run time;
                // otherwise null propagates, as it does everywhere else in arithmetic.
                //
                // The rule exists because the interpreter cannot see what the compiled path can. With a
                // null on the left it holds a bare null reference and has no idea the declared type was
                // string, so 'NullName + Number' was "5" compiled and null interpreted, and
                // 'NullName + NullName' was "" compiled and an ArgumentException interpreted. Making
                // both concatenate is not available - only one backend has the information - so the
                // agreement has to be reached the other way.
                //
                // What is *not* affected is any concatenation with a real string in it, which is nearly
                // all of them: 'NullName + Text' and 'Text + NullName' are "b" on both backends today
                // and stay that way, because the interpreter can see the string it does have.
                //
                // Two deviations from C# come with it, both confined to nulls: '(string)null + 5' is
                // "5" in C# and null here, and '(string)null + (string)null' is "" in C# and null here.
                // When the static types already guarantee a real string, no test is emitted and each
                // operand is mentioned once - so nothing is hoisted and the tree is what it always was.
                if (AtLeastOneIsARealString(leftExpression, rightExpression) == null)
                    return Concatenate(leftExpression, rightExpression);

                // Otherwise the test and the concatenation both need the operands, and mentioning an
                // operand twice evaluates it twice: 'Text() + Text()' read one of them three times.
                // The block assigns each once, left before right.
                return OperandLocals.UseOnce(
                    leftExpression,
                    rightExpression,
                    (left, right) => LExpression.Condition(
                        AtLeastOneIsARealString(left, right),
                        Concatenate(left, right),
                        LExpression.Constant(null, typeof(string))));
            }
            
                // todo: error: wbudowane metody? - patrz date()
                // todo: error: może jednak zrobić np. _set()
                // todo: error: i np. _convert(cośtam).To(int))
                // todo: error: i np. _cast(cośtam).To(int))
                // todo: error: i np. _cast(cośtam).To(int))
                // todo: error: może tylko sety? jednak?

            // Dictionaries are matched BEFORE the enumerable union below, and the order is load-bearing:
            // a Dictionary<K, V> enumerates as KeyValuePair<K, V>, so the set branch would happily union
            // two dictionaries into a set of pairs and lose the mapping. The order used to be the other
            // way round and survived only by accident - the item type was read as the *key* type, which
            // then failed the IsGenericEnumerableOfItemType guard and fell through to here. Reading the
            // item type correctly removed that accident, so the guard has to be real.
            var leftIsGenericDictionary = MethodBaseHelpers.IsGenericDictionary(leftExpression.Type);
            var rightIsGenericDictionary = MethodBaseHelpers.IsGenericDictionary(rightExpression.Type);

            if (leftIsGenericDictionary || rightIsGenericDictionary)
            {
                if (leftIsGenericDictionary && rightIsGenericDictionary)
                {
                           // todo: error: implementation!
                    throw CannotCompile("no compiled addition for these operand types");
                }

                // A dictionary meeting anything else. The verdict is right and was always right; it
                // used to be delivered as an ArgumentException, which the fallback cannot see - so a
                // pair the interpreter merges quite happily (a non-generic Hashtable meeting a
                // Dictionary<,>, whose left operand fails the generic test above) was a hard failure,
                // and a pair the interpreter also rejects failed at parse instead of at evaluation.
                throw CannotCompile(
                    $"no compiled addition of '{leftExpression.Type}' and '{rightExpression.Type}'");
            }

            var leftIsGenericEnumerable = MethodBaseHelpers.IsGenericEnumerable(leftExpression.Type);
            var rightIsGenericEnumerable = MethodBaseHelpers.IsGenericEnumerable(rightExpression.Type);

            // The item type is read from what the operand enumerates as, not from its own generic
            // arguments: an array implements IEnumerable<T> without being a generic type at all, so
            // GetGenericArguments() on int[] is *empty* and indexing it threw IndexOutOfRangeException -
            // which is how 'Array + Ints' failed, while the interpreter unioned the two quite happily.
            // GetEnumerableItemType is the same helper the projection and selection nodes were given
            // when they had this exact defect; it also returns null for an ambiguous source rather than
            // guessing, and a null item type simply falls through to the typeless union below.
            var leftItemType = leftIsGenericEnumerable
                ? CollectionOperandUtils.GetEnumerableItemType(leftExpression.Type)
                : null;
            var rightItemType = rightIsGenericEnumerable
                ? CollectionOperandUtils.GetEnumerableItemType(rightExpression.Type)
                : null;

            if (leftItemType != null
                && leftItemType == rightItemType
                && MethodBaseHelpers.IsGenericEnumerableOfItemType(leftExpression.Type, leftItemType))
            {
                var finalUnionMi = _genericsUnionMi.MakeGenericMethod(leftItemType);
                var typedUnion = LExpression.Call(finalUnionMi, leftExpression, rightExpression);

                compilationContext.MarkAsConstructedCollection(typedUnion);
                return typedUnion;
            }

            if ( (typeof(IList).IsAssignableFrom(leftExpression.Type)
                    || typeof(ISet).IsAssignableFrom(leftExpression.Type)
                    || leftIsGenericEnumerable
                )
                && (typeof(IList).IsAssignableFrom(rightExpression.Type)
                    || typeof(ISet).IsAssignableFrom(rightExpression.Type)
                    || rightIsGenericEnumerable 
                ))
            {
                return LExpression.Call(_typelessUnionMi, leftExpression, rightExpression);
            }

            throw CannotCompile("no compiled addition for these operand types");
        }

        /// <summary>
        /// Adds a span to a date where the span's source carries one, answering a
        /// <c>DateTime?</c> holding nothing where it does not.
        /// </summary>
        /// <remarks>
        /// <p>
        /// Two operand shapes share this: a <c>string</c> to be parsed as a TimeSpan, and a nullable
        /// number of days. Both have to answer "nothing" for an absent operand, and neither can do it
        /// with a plain <c>DateTime</c>, so both results are <c>DateTime?</c> - which boxes to a
        /// <c>DateTime</c> or to the null reference and is therefore invisible to a caller.
        /// </p>
        /// <p>
        /// <b>Both operands go into locals first, and that is the point of the method.</b> Written as a
        /// bare conditional over the operand expressions - which is what the string branch did - the
        /// right operand is emitted twice and evaluated twice, so <c>When + Span()</c> called
        /// <c>Span()</c> two times compiled against one interpreted; and the left operand, appearing
        /// only inside the true branch, was not evaluated at all when the right turned out to be
        /// absent. Measured both ways. That is the same defect <c>OpAND</c> and <c>OpOR</c> had, where
        /// <c>0 or SideEffect()</c> ran the side effect twice, and only a side-effecting operand can
        /// see it. The block assigns left then right, once each, in the order
        /// <see cref="Get"/> evaluates them.
        /// </p>
        /// </remarks>
        [NotNull]
        private static LExpression AddDateTimeAndSpanIfPresent(
            [NotNull] LExpression dateExpression,
            [NotNull] LExpression spanSourceExpression,
            [NotNull] Func<LExpression, LExpression> isAbsent,
            [NotNull] Func<LExpression, LExpression> toTimeSpan)
        {
            var date = LExpression.Variable(dateExpression.Type, "date");
            var spanSource = LExpression.Variable(spanSourceExpression.Type, "spanSource");

            var added = LExpression.Call(
                DateTimeMethods.DateTimeAddTimeSpanMethodInfo, date, toTimeSpan(spanSource));

            return LExpression.Block(
                new[] { date, spanSource },
                LExpression.Assign(date, dateExpression),
                LExpression.Assign(spanSource, spanSourceExpression),
                LExpression.Condition(
                    isAbsent(spanSource),
                    LExpression.Constant(null, typeof(DateTime?)),
                    LExpression.Convert(added, typeof(DateTime?))));
        }

        /// <summary>
        /// The union of two collections that share an item type, keeping that item type.
        /// </summary>
        /// <remarks>
        /// Keeping the item type is what lets sum(), average(), max(), projections and selections over a
        /// union stay compiled. Declared as the concrete HashSet&lt;T&gt; rather than ISet&lt;T&gt;, so that
        /// assigning the result to a HashSet&lt;T&gt; property compiles - the interface is not assignable to
        /// it. The caller registers the emitted call with
        /// <see cref="CompilationContext.MarkAsConstructedCollection"/>, so Compiler can tell this is a set
        /// the engine built without the value needing a type of its own.
        /// </remarks>
        private static HashSet<T> GenericsUnion<T>(IEnumerable<T> arg1, IEnumerable<T> arg2)
        {
                 // todo: null-handling
            var set1 = new HashSet<T>(arg1);
            set1.UnionWith(arg2);
            return set1;
        }

        // todo: error: check declarations of other MethodInfos!!!!
        private static MethodInfo _genericsUnionMi = typeof(OpADD).GetMethod(
               nameof(GenericsUnion), BindingFlags.Static | BindingFlags.NonPublic);

        // Counterpart of the interpreter's branch in Get: the operands have no usable common item type, so
        // the union is a HashSet<object> rather than a HybridSet. Kept in step with GenericsUnion<T> above,
        // which already returned a HashSet.
        private static HashSet<object> TypelessUnion(IEnumerable left, IEnumerable right)
        {
            var union = new HashSet<object>();

            foreach (var e in left)
                union.Add(e);

            foreach (var e in right)
                union.Add(e);

            return union;
        }

        private static MethodInfo _typelessUnionMi = typeof(OpADD).GetMethod(
            "TypelessUnion", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Returns a value for the arithmetic addition operator node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object leftValue = GetLeftValue(context, evalContext);
            object rightValue = GetRightValue(context, evalContext);

            // The operand types' own operator, before any conversion is considered - the compiled
            // path's first question too, so the two agree by running the same lookup.
            if (TryInvokeUserDefinedBinary(leftValue, rightValue, "op_Addition", out var userDefined))
                return userDefined;

            var leftIsNumber = TypeCheckingUtils.IsNumber(leftValue);
            var rightIsNumber = TypeCheckingUtils.IsNumber(rightValue);

            if (leftIsNumber && rightIsNumber)
            {
                return NumberUtils.Add(leftValue, rightValue);
            }

            // '+' concatenates only when at least one operand is an actual string; otherwise a null
            // propagates, as it does everywhere else in arithmetic. This is the interpreter's half of
            // the rule the concatenation branch of the compiled path implements, and the two are
            // written to mirror each other deliberately.
            //
            // It used to be narrower - "a null beside a *number* propagates" - which left every other
            // pairing to fall off the end of the method and report that the two cannot be added. So
            // 'NoNumber + NoNumber' and 'NullName + NullName' threw where the compiled path answered
            // null and "", and so did 'NullName + Flag'.
            //
            // Null is the answer rather than "" because this side cannot tell which it is looking at:
            // two nulls are two nulls whether they were strings or ints. Only one answer can serve
            // both, and propagation is the one the rest of arithmetic already uses.
            //
            // A real string on either side is untouched: 'NullName + Text' still concatenates to "b",
            // here and compiled, because there is a string to concatenate to.
            if ((leftValue == null || rightValue == null)
                && !(leftValue is string)
                && !(rightValue is string))
            {
                return null;
            }

            // todo: error: string???? parsing here?--------
            if (leftValue is DateTime && (rightValue is TimeSpan || rightValue is string || rightIsNumber))
            {
                if (rightIsNumber)
                {
                    rightValue = TimeSpan.FromDays(Convert.ToDouble(rightValue));
                }
                else if (rightValue is string)
                {
                    rightValue = TimeSpan.Parse((string) rightValue);
                }

                return (DateTime) leftValue + (TimeSpan) rightValue;
            }

            if (leftValue is String || rightValue is String)
            {
                return string.Concat(leftValue, rightValue);
            }

            // IsAnySet matches the vendored non-generic ISet and any generic ISet<T>, whatever its item type.
            // Both are needed: the operators return a HashSet, so in a chained expression the second
            // operator receives the first one's result, and a caller can hand in a HashSet<int> too.
            if ((leftValue is IList || CollectionOperandUtils.IsAnySet(leftValue))
                && (rightValue is IList || CollectionOperandUtils.IsAnySet(rightValue)))
            {
                var union = CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) leftValue);
                union.UnionWith(CollectionOperandUtils.ToHashSetOfObjects((IEnumerable) rightValue));
                return union;
            }

            if (leftValue is IDictionary leftDictionary && rightValue is IDictionary rightDictionary)
            {
                var leftKeys = CollectionOperandUtils.KeysToHashSetOfObjects(leftDictionary);
                var unionKeys = CollectionOperandUtils.KeysToHashSetOfObjects(leftDictionary);
                unionKeys.UnionWith(CollectionOperandUtils.KeysToHashSetOfObjects(rightDictionary));

                IDictionary result = new Dictionary<object, object>(unionKeys.Count);
                foreach(object key in unionKeys)
                {
                    if(leftKeys.Contains(key))
                    {
                        result.Add(key, leftDictionary[key]);
                    }
                    else
                    {
                        result.Add(key, rightDictionary[key]);
                    }
                }
                return result;
            }

            throw new ArgumentException("Cannot add instances of '"
                + leftValue?.GetType().FullName
                + "' and '"
                + rightValue?.GetType().FullName
                + "'.");
        }

        /// <summary>
        /// <c>String.Concat(left, right)</c>, with each operand boxed on its own merits.
        /// </summary>
        /// <remarks>
        /// Both parameters of <c>String.Concat(object, object)</c> are <c>object</c>. Boxing only the
        /// right one made the operator asymmetric: <c>'Ana' + 45</c> compiled, because a string needs no
        /// boxing, while <c>45 + 'Ana'</c> handed an unboxed int to an <c>object</c> parameter and
        /// <c>LExpression.Call</c> threw.
        /// </remarks>
        [NotNull]
        private LExpression Concatenate([NotNull] LExpression left, [NotNull] LExpression right)
        {
            return BuildCall(
                null,
                StrConcatObjObjMethodInfo,
                new[] { BoxIfValueType(left), BoxIfValueType(right) });
        }

        /// <summary>
        /// Boxes a value-typed operand so it fits an <c>object</c> parameter; anything else is already
        /// one and is passed through, so no gratuitous conversion is emitted.
        /// </summary>
        /// <remarks>
        /// A <c>Nullable&lt;T&gt;</c> holding nothing boxes to a null reference, which
        /// <c>String.Concat</c> renders as the empty string - the same answer the interpreter gives,
        /// since it hands Concat the boxed value it already has.
        /// </remarks>
        [NotNull]
        private static LExpression BoxIfValueType([NotNull] LExpression expression)
        {
            return expression.Type.IsValueType
                ? LExpression.Convert(expression, typeof(object))
                : expression;
        }

        /// <summary>
        /// A test for "at least one of these is an actual string once the expression runs", or null when
        /// the static types already guarantee it and no test is needed.
        /// </summary>
        /// <remarks>
        /// An operand declared <c>string</c> qualifies when it is not null; one declared <c>object</c>
        /// might hold a string, so it is asked at run time; anything else cannot be a string and is
        /// ruled out at compile time. A non-null string constant settles the question on its own, which
        /// is what keeps <c>'a' + 'b'</c> and every literal-bearing concatenation free of the branch.
        /// </remarks>
        [CanBeNull]
        private static LExpression AtLeastOneIsARealString(
            [NotNull] LExpression left, [NotNull] LExpression right)
        {
            if (IsCertainlyARealString(left) || IsCertainlyARealString(right))
                return null;

            return LExpression.OrElse(MightBeARealString(left), MightBeARealString(right));
        }

        private static bool IsCertainlyARealString([NotNull] LExpression operand)
        {
            return operand is System.Linq.Expressions.ConstantExpression constant
                && constant.Value is string;
        }

        private static LExpression MightBeARealString([NotNull] LExpression operand)
        {
            if (operand.Type == typeof(string))
                return LExpression.NotEqual(operand, LExpression.Constant(null, typeof(string)));

            // an object-typed operand can still be holding one
            if (!operand.Type.IsValueType)
                return LExpression.TypeIs(operand, typeof(string));

            return LExpression.Constant(false);
        }

        private static readonly MethodInfo StrConcatObjObjMethodInfo
            = typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object) });
    }
}
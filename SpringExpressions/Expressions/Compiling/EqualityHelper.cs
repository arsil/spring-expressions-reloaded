using System;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;

using SpringExpressions.Util;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Expressions.Compiling
{
    internal static class EqualityHelper
    {
        /// <param name="node">
        /// The node being compiled, so a refusal can name it. Without it every refusal below arrived
        /// with a null <see cref="Expressions.CompileErrorException.NodeType"/> and no
        /// "Cannot compile OpEqual '=='" prefix - 888 of the compilation sweep's refusals, and the
        /// reason a caller grouping refusals by node could not see 12% of them.
        /// </param>
        [NotNull]
        public static LExpression CreateEqualExpression(
            [NotNull] BaseNode node,
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression)
        {
            if (BinaryNumericOperatorHelper.TryCreate(
                leftExpression, rightExpression, LExpression.Equal, out var resultExpression))
            {
                return resultExpression;
            }

            /*
                LExpression.Equal(left, right)
                If the Type property of either left or right represents a user-defined type that overloads 
                the equality operator, the MethodInfo that represents that method is the implementing method.
             */
            // todo: error: nullable types!!!!!

            // not a number

            // todo: error: zwinąć do do compare utils!!!! ???? jak się to ma do notEqual???
            if (leftExpression.Type == typeof(bool) && rightExpression.Type == typeof(bool))
                return LExpression.Equal(leftExpression, rightExpression);

            // An enum against a string compares by member name, through the very method the interpreter
            // runs - so the two backends cannot drift, exception included.
            if (TryCreateEnumAgainstName(leftExpression, rightExpression, out var byName))
                return byName;

            // An enum against anything else - "Type == 1" - has no compiled form: the interpreter
            // refuses the pair (CompareUtils cannot coerce them), while the boxing tail below would
            // silently answer false, which is not an answer anybody chose.
            if (leftExpression.Type.IsEnum ^ rightExpression.Type.IsEnum)
            {
                var enumType = leftExpression.Type.IsEnum ? leftExpression.Type : rightExpression.Type;
                var otherType = leftExpression.Type.IsEnum ? rightExpression.Type : leftExpression.Type;

                if (Nullable.GetUnderlyingType(otherType) != enumType)
                {
                    throw new Expressions.CompileErrorException(
                        node,
                        $"no compiled equality between the enum [{enumType.FullName}] and "
                        + $"[{otherType.FullName}]; an enum compares to the same enum or to a member name.");
                }
            }

            // Both sides string-comparable. This used to accept *either* side being a string and hand
            // the pair to LExpression.Equal, which throws InvalidOperationException out of the emitter
            // for a pair it has no operator for - past the compile-refusal convention, so the weak path
            // could not fall back and a shape the interpreter serves became a hard failure.
            if (leftExpression.Type == typeof(string) || rightExpression.Type == typeof(string))
            {
                // Two strings compare by value - String declares op_Equality and LExpression.Equal
                // resolves it - and a null literal is fine against either. Anything else is refused,
                // including a string against an `object`, which C# *does* permit as reference equality.
                //
                // This engine does not, and the reason is measurable rather than stylistic. Reference
                // equality on strings answers by interning:
                //
                //   Name == Anything, both holding the literal "Ana"        True
                //   Name == Anything, the right one built at run time       False
                //
                // Same characters, same expression, and the answer turns on whether the CLR happened to
                // intern the string - invisible to the caller, and it flips the day the data comes from
                // a database instead of a constant. The interpreter compares by value and answers True
                // for both. Every earlier probe of this shape used a literal, which is why it read as
                // harmless for so long.
                //
                // It is also the odd one out: this engine's '==' promotes numbers, reads an enum against
                // a string as a member name, honours a type's own op_Equality and compares strings by
                // value. It is a value-equality operator, so reference identity in one corner is an
                // accident rather than a rule. Open-issues item 21 stage 4.
                if (leftExpression.Type != rightExpression.Type
                    && !IsNullConstant(leftExpression)
                    && !IsNullConstant(rightExpression))
                {
                    throw new Expressions.CompileErrorException(
                        node,
                        $"no compiled equality between [{leftExpression.Type.FullName}] and "
                        + $"[{rightExpression.Type.FullName}]; a string compares to a string by value, "
                        + "and comparing it to an untyped operand would answer by reference identity.");
                }

                return LExpression.Equal(leftExpression, rightExpression);
            }

            if (leftExpression.Type == typeof(DateTime) && rightExpression.Type == typeof(DateTime))
                return LExpression.Equal(leftExpression, rightExpression);

            if (leftExpression.Type == rightExpression.Type)
            {
                // A type's own op_Equality, where it declares one - the general rule the three
                // special cases above are instances of. Without it, only numerics, string and
                // DateTime ever reached their operator and every other same-typed pair went to
                // EqualityComparer: Guid, TimeSpan, DateTimeOffset, Uri, Version and anything a
                // caller wrote. EqualityUtils.CreateMethod is the interpreter's twin, so the two
                // backends decide from one rule.
                //
                // The resolved MethodInfo is passed explicitly rather than letting
                // LExpression.Equal(left, right) resolve for itself, which is this engine's standing
                // habit: LINQ's own resolution is more permissive and would drift from the
                // interpreter. Built-in numerics never arrive here anyway - they are handled above -
                // but the lookup excludes them regardless, which is what keeps double/float NaN out
                // of this change.
                var userDefined = UserDefinedOperatorUtils.IsOwnedByNumericPromotion(
                        leftExpression.Type, rightExpression.Type)
                    ? null
                    : UserDefinedOperatorUtils.FindBinary(
                        "op_Equality", leftExpression.Type, rightExpression.Type);

                if (userDefined != null && userDefined.ReturnType == typeof(bool))
                {
                    return GuardNullsAroundOperator(
                        leftExpression,
                        rightExpression,
                        LExpression.Equal(leftExpression, rightExpression, false, userDefined));
                }

                var mi = MiEqualityComparerEquals.MakeGenericMethod(leftExpression.Type);
                return LExpression.Call(mi, leftExpression, rightExpression);
            }

            // A comparison the static types do not determine is refused, not guessed. The tail below
            // boxes both operands and calls object.Equals, which *always answers* - and answers false
            // for any pair it cannot actually compare. That is a guess dressed as a result: the
            // interpreter, which sees the runtime values, either promotes them and answers or refuses
            // the pair, and it never invents. Measured by EvaluationNeverDivergesTests, the tail was
            // 1,084 of 1,441 divergences, four of them outright wrong answers rather than
            // refuse-versus-answer mismatches ('Big == Anything', a long against an object holding the
            // same value as an int, was False compiled and True interpreted).
            //
            // The rule is C#'s, measured slice by slice: it rejects 510 of the 540 pairs per operator
            // with CS0019. What it permits, and therefore what may still reach the tail:
            //
            //   * two *reference* types where one is assignable to the other - that is C#'s predefined
            //     reference equality, and the tail's object.Equals implements it. 'Name == Anything'
            //     lives here. Whether this engine should permit it at all is a separate question: its
            //     '==' promotes numbers, reads an enum against a string as a member name and honours a
            //     type's own op_Equality, so it is a value-equality operator and reference identity in
            //     one corner reads as an accident. Open-issues item 21 stage 4.
            //
            //   * a null literal against anything - the tail's null branches handle it, and C# allows
            //     'someInt == null' too (always false, with a warning).
            //
            // Everything else refuses and the interpreter serves the expression, so no answer changes
            // on the default path - it is already the interpreter that answers these whenever the shape
            // did not compile. A value type against an object refuses along with the rest: it agrees
            // today only when the boxed type happens to match, which is luck rather than a rule, and
            // the two wrong answers are exactly that luck running out.
            //
            // This widens the guard the '45 == true' ruling added for two value types, which was this
            // same defect found in the one slice static types were enough to see.
            //   * a nullable against its own underlying type - 'bool? == bool'. That is lifting, not a
            //     guess: boxing a nullable yields either the underlying boxed value or a null
            //     reference, so the tail sees exactly what the interpreter sees.
            var leftUnwrapped = Nullable.GetUnderlyingType(leftExpression.Type) ?? leftExpression.Type;
            var rightUnwrapped = Nullable.GetUnderlyingType(rightExpression.Type) ?? rightExpression.Type;

            // Stage 4 removed the last allowance here - two reference types where one was assignable to
            // the other, which let 'Inner == Anything' reach the tail and answer from whatever Equals
            // the left happened to inherit. C# permits that pair as reference equality; this engine does
            // not, for the reason spelled out in the string branch above. What is left is exactly the
            // two shapes the tail can answer as the interpreter would: a null literal, and a nullable
            // against its own underlying type.
            if (!IsNullConstant(leftExpression)
                && !IsNullConstant(rightExpression)
                && leftUnwrapped != rightUnwrapped)
            {
                throw new Expressions.CompileErrorException(
                    node,
                    $"no compiled equality between [{leftExpression.Type.FullName}] and "
                    + $"[{rightExpression.Type.FullName}]; the static types do not determine which "
                    + "comparison applies, so the interpreter decides from the runtime values.");
            }

            // todo: głupie jest to, iż może to nie zadziałać dla boxowanych typów... oto jest pytanie...
            // todo: może nigdy nie powiniśmy eqlals jednak używać... do zastanowienia się...

            // todo: error: bieda! bieda!
            if (leftExpression.Type.IsValueType)
                leftExpression = LExpression.Convert(leftExpression, typeof(object));

            if (rightExpression.Type.IsValueType)
                rightExpression = LExpression.Convert(rightExpression, typeof(object));

            return LExpression.Condition(
                    LExpression.Equal(leftExpression,
                        LExpression.Constant(null, typeof(object))),
                    // left is null - emitting (right == null)
                    LExpression.Equal(rightExpression,
                        LExpression.Constant(null, typeof(object))),
                    // left is not null - checking right
                    LExpression.Condition(
                        LExpression.Equal(rightExpression,
                            LExpression.Constant(null, typeof(object))),
                        // left not null; right is null => false
                        LExpression.Constant(false, typeof(bool)),
                        // left not null; right not null => emitting left.Equals(right)
                        LExpression.Call(leftExpression, objEqualsMi, rightExpression)
                        )
                );
        }

        /// <param name="node">
        /// The OpNotEqual node, so a refusal names the operator the caller actually wrote - '!=' is
        /// the exact negation of '==' here, but the message should not claim the caller wrote '=='.
        /// </param>
        [NotNull]
        public static LExpression CreateNotEqualExpression(
            [NotNull] BaseNode node,
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression)
        {
               // todo: error: not exactly???? ----------------------------- operator != can be different than == ?
               // todo: error: LExpression.NotEqual() can be different than NOT LExpression.Equal()

               return LExpression.Not(CreateEqualExpression(node, leftExpression, rightExpression));
        }

        /// <summary>
        /// "enumValue == 'MemberName'", in either order, emitted as a call to the interpreter's own
        /// <see cref="Util.EqualityUtils.EnumEqualsName"/> - so the rule, and the ArgumentException a
        /// string that names no member raises, are the same on both backends by construction.
        /// </summary>
        private static bool TryCreateEnumAgainstName(
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression,
            out LExpression result)
        {
            LExpression enumExpression = null;
            LExpression nameExpression = null;

            if (leftExpression.Type.IsEnum && rightExpression.Type == typeof(string))
            {
                enumExpression = leftExpression;
                nameExpression = rightExpression;
            }
            else if (rightExpression.Type.IsEnum && leftExpression.Type == typeof(string))
            {
                enumExpression = rightExpression;
                nameExpression = leftExpression;
            }

            if (enumExpression == null)
            {
                result = null;
                return false;
            }

            result = LExpression.Call(
                MiEnumEqualsName,
                LExpression.Convert(enumExpression, typeof(object)),
                nameExpression);

            return true;
        }

        [NotNull]
        private static readonly MethodInfo MiEnumEqualsName = typeof(Util.EqualityUtils)
            .GetMethod(nameof(Util.EqualityUtils.EnumEqualsName));

        /// <summary>
        /// For a reference type, answers the null cases before the operator is reached: two nulls are
        /// equal, one null is not. Value types need none of this and are returned unchanged.
        /// </summary>
        /// <remarks>
        /// <c>OpEqual.Get</c> has always done exactly this before it consults anything else, so the
        /// interpreter never hands a null to an operator. Without the same guard here the two backends
        /// part company on a type whose operator is not null-safe - measured:
        /// <c>value == null</c> gave <c>NullReferenceException</c> compiled and <c>False</c>
        /// interpreted, because the compiled call reached the operator and the interpreted one did
        /// not. C# would also throw, but agreeing with the interpreter matters more here: a caller who
        /// writes a comparison against null is asking a question about null, not asking to run the
        /// operator.
        /// <p>
        /// The null tests are <c>ReferenceEqual</c>, not <c>Equal</c> - <c>Equal</c> would resolve the
        /// very operator being guarded and recurse.
        /// </p>
        /// </remarks>
        [NotNull]
        /// <summary>
        /// A reference type, which for this purpose means anything a null can be: not a value type, and
        /// not a <c>Nullable&lt;T&gt;</c> either - boxing one yields the underlying boxed value, so it
        /// carries a value type's identity into the tail rather than a reference's.
        /// </summary>
        private static bool IsReferenceType(Type type)
        {
            return !type.IsValueType;
        }

        /// <summary>
        /// The null literal, which the tail's own null branches handle and which C# permits against
        /// anything.
        /// </summary>
        private static bool IsNullConstant(LExpression expression)
        {
            return expression is System.Linq.Expressions.ConstantExpression constant
                && constant.Value == null;
        }

        private static LExpression GuardNullsAroundOperator(
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression,
            [NotNull] LExpression operatorCall)
        {
            if (leftExpression.Type.IsValueType)
                return operatorCall;

            var leftIsNull = LExpression.ReferenceEqual(
                leftExpression, LExpression.Constant(null, leftExpression.Type));
            var rightIsNull = LExpression.ReferenceEqual(
                rightExpression, LExpression.Constant(null, rightExpression.Type));

            return LExpression.Condition(
                leftIsNull,
                rightIsNull,
                LExpression.Condition(
                    rightIsNull,
                    LExpression.Constant(false),
                    operatorCall));
        }

        private static bool EqualityComparerEquals<T>(T t1, T t2)
        {
            return EqualityComparer<T>.Default.Equals(t1, t2);
        }

        private static readonly MethodInfo MiEqualityComparerEquals = typeof(EqualityHelper)
            .GetMethod(nameof(EqualityComparerEquals), BindingFlags.Static | BindingFlags.NonPublic);


        private static readonly MethodInfo objEqualsMi
            = typeof(object).GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public);
    }

}

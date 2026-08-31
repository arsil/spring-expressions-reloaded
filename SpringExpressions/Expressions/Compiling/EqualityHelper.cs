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
        [NotNull]
        public static LExpression CreateEqualExpression(
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
                if (!leftExpression.Type.IsAssignableFrom(rightExpression.Type)
                    && !rightExpression.Type.IsAssignableFrom(leftExpression.Type))
                {
                    throw new Expressions.CompileErrorException(
                        $"no compiled equality between [{leftExpression.Type.FullName}] and "
                        + $"[{rightExpression.Type.FullName}].");
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

            // Two value types that are not the same type have no compiled equality. The tail below
            // would box both and call object.Equals, which sees two unrelated types and answers
            // *false* - '45 == true' was False, '45 != true' was True, and neither is an answer anybody
            // chose. The interpreter refuses every such pair with ArgumentException, so this is the
            // compiled path failing to implement a rule that already existed.
            //
            // This generalises the enum guard above, which was the same accident found one type at a
            // time. Nullables unwrap first, so 'bool? == bool' and 'int? == int' keep comparing - those
            // agree with the interpreter today, since boxing a nullable yields either the underlying
            // boxed value or a null reference.
            //
            // Deliberately not extended to a value type against an *object*: there the runtime value
            // decides, and 'Number == Anything' agrees on both backends when the object holds an int.
            // That is the standing object-typed-operand story, and a static refusal would break it.
            var leftValueType = Nullable.GetUnderlyingType(leftExpression.Type) ?? leftExpression.Type;
            var rightValueType = Nullable.GetUnderlyingType(rightExpression.Type) ?? rightExpression.Type;

            if (leftValueType.IsValueType
                && rightValueType.IsValueType
                && leftValueType != rightValueType)
            {
                throw new Expressions.CompileErrorException(
                    $"no compiled equality between [{leftExpression.Type.FullName}] and "
                    + $"[{rightExpression.Type.FullName}].");
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

        [NotNull]
        public static LExpression CreateNotEqualExpression(
            [NotNull] LExpression leftExpression,
            [NotNull] LExpression rightExpression)
        {
               // todo: error: not exactly???? ----------------------------- operator != can be different than == ?
               // todo: error: LExpression.NotEqual() can be different than NOT LExpression.Equal()

               return LExpression.Not(CreateEqualExpression(leftExpression, rightExpression));
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

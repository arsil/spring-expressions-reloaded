using System;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;
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
                var mi = MiEqualityComparerEquals.MakeGenericMethod(leftExpression.Type);
                return LExpression.Call(mi, leftExpression, rightExpression);
            }

            // todo: error: equatable<>

            // TODO: upewnić się, że to działa (dla wybranych typów) tak samo jak interpretacja!
            //TODO: brak obsługi .. czy charów... czy innych takich! to samo przy Less i innych operatorach!

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

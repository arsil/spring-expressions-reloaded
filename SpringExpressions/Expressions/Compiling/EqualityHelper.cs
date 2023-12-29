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

            if (leftExpression.Type == typeof(string) || rightExpression.Type == typeof(string))
                return LExpression.Equal(leftExpression, rightExpression);

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

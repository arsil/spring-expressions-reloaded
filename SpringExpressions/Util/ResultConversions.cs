using System;

using JetBrains.Annotations;

using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// The one rule for handing a strongly typed getter's value over as the type its caller asked
    /// for, on both backends: the value as it is, C#'s implicit numeric widening, or C#'s cast from
    /// <c>object</c>. A requested result is a typed sink, like a declared local or a property, and
    /// converts where C# would convert it without a cast.
    /// </summary>
    /// <remarks>
    /// <p>
    /// So <c>ParseGetter&lt;Root, long&gt;("IntValue")</c> means <c>long r = root.IntValue;</c> and
    /// answers on both backends, while <c>ParseGetter&lt;Root, int&gt;("BoolValue")</c>, a long
    /// narrowed to an int, a real truncated to an integral and an int read as a char have no such
    /// spelling: the emitted half refuses them and the interpreted half throws the cast's
    /// <c>InvalidCastException</c>. The escapes are C#'s - request the type the expression produces,
    /// or write the cast in the expression.
    /// </p>
    /// <p>
    /// The widening is the table <see cref="TypeCheckingUtils.IsCSharpImplicitNumericConversion"/>
    /// transcribes, the one <see cref="ArrayElementConversions"/> already applies to an array
    /// element, a <c>params</c> element, a declared local and a property write. Both halves run the
    /// same <c>LExpression.Convert</c> for it - emitted inline, or through
    /// <see cref="CastOperations"/> for a boxed value - so the two cannot reach different numbers.
    /// </p>
    /// <p>
    /// Deliberately NOT here: a type's own implicit conversion operator. A declared local and an
    /// array element do not honour one either, so a getter request does not; both halves refuse it.
    /// </p>
    /// </remarks>
    internal static class ResultConversions
    {
        /// <summary>
        /// The emitted half, for a body whose static type is a value type other than
        /// <paramref name="resultType"/>: the body converted to the request, or false where the
        /// request is refused.
        /// </summary>
        public static bool TryConvertExpression(
            [NotNull] LExpression body, [NotNull] Type resultType, [CanBeNull] out LExpression converted)
        {
            // Identity, a value into its own nullable, and the implicit numeric widenings.
            if (ArrayElementConversions.TryConvertExpression(body, resultType, out converted))
                return true;

            var resultUnderlyingType = Nullable.GetUnderlyingType(resultType);
            var bodyUnderlyingType = Nullable.GetUnderlyingType(body.Type);

            // The same widenings into a nullable request, from a plain body and from a nullable
            // one: C#'s 'long? r = someInt;' and its lifted 'long? r = someNullableInt;'.
            if (resultUnderlyingType != null
                && TypeCheckingUtils.IsCSharpImplicitNumericConversion(
                    bodyUnderlyingType ?? body.Type, resultUnderlyingType))
            {
                converted = LExpression.Convert(body, resultType);
                return true;
            }

            // The one thing C#'s cast from object does beyond those, and the interpreted half runs
            // that cast: an enum unboxes as its underlying integral type and back.
            if (resultUnderlyingType == null
                && bodyUnderlyingType == null
                && HaveTheSameEnumRepresentation(body.Type, resultType))
            {
                converted = LExpression.Convert(body, resultType);
                return true;
            }

            converted = null;
            return false;
        }

        /// <summary>
        /// The interpreted half, and what the emitted form calls for an object-typed body, where
        /// only the runtime value can say what it is: the value as it is, widened where C# widens,
        /// or cast.
        /// </summary>
        [CanBeNull]
        public static TResult ConvertValue<TResult>([CanBeNull] object value)
        {
            if (value != null && !(value is TResult))
            {
                var resultUnderlyingType = Nullable.GetUnderlyingType(typeof(TResult)) ?? typeof(TResult);

                if (TypeCheckingUtils.IsCSharpImplicitNumericConversion(value.GetType(), resultUnderlyingType))
                    return (TResult)CastOperations.Cast(value, resultUnderlyingType);
            }

            // C#'s cast from object: an exact or nullable match, an enum against its underlying
            // type, NullReferenceException for a null into a value type, InvalidCastException for
            // anything else.
            return (TResult)value;
        }

        /// <summary>
        /// Whether an unbox would accept a boxed <paramref name="first"/> as <paramref name="second"/>
        /// although they are different types: one of them an enum, both with the same integral
        /// representation - the CLR checks the representation, not the type.
        /// </summary>
        private static bool HaveTheSameEnumRepresentation([NotNull] Type first, [NotNull] Type second)
        {
            return (first.IsEnum || second.IsEnum)
                && RepresentationOf(first) == RepresentationOf(second);
        }

        [NotNull]
        private static Type RepresentationOf([NotNull] Type type)
        {
            return type.IsEnum ? Enum.GetUnderlyingType(type) : type;
        }
    }
}

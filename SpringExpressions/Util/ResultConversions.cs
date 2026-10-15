using System;
using System.Reflection;

using JetBrains.Annotations;

using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// The one rule for handing a strongly typed getter's value over as the type its caller asked
    /// for, on both backends: the value as it is, C#'s implicit numeric widening, a type's own
    /// conversion operator, or C#'s cast from <c>object</c>.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>Between built-in types</b> a requested result converts only where C# converts without a
    /// cast. <c>ParseGetter&lt;Root, long&gt;("IntValue")</c> is <c>long r = root.IntValue;</c> and
    /// answers on both backends, while <c>ParseGetter&lt;Root, int&gt;("BoolValue")</c>, a long
    /// narrowed to an int, a real truncated to an integral and an int read as a char are refused by
    /// the emitted half and throw the cast's <c>InvalidCastException</c> interpreted. The escapes are
    /// C#'s - request the type the expression produces, or write the cast in the expression.
    /// </p>
    /// <p>
    /// <b>A type's own implicit conversion operator is honoured</b>:
    /// <c>ParseGetter&lt;Root, decimal&gt;("Money")</c> answers through <c>Money</c>'s operator. It
    /// may be followed by the widening C# allows after it (<c>implicit operator int</c> reaches a
    /// <c>decimal</c> request), may target the request's nullable form or its underlying type, and
    /// lifts over a nullable body into a nullable request.
    /// </p>
    /// <p>
    /// <b>An explicit operator is not</b>, whichever type declares it. It is explicit because it can
    /// lose information - <c>ParseGetter&lt;Root, Cents&gt;("Amount")</c> through
    /// <c>explicit operator Cents(decimal)</c> would truncate 12.3456 to 12.34 - and, as in C#, it
    /// runs only through a cast written in the expression: <c>Amount as Cents</c>. Without one the
    /// request is refused compiled and throws <c>InvalidCastException</c> interpreted, like any other
    /// conversion C# would not make implicitly. Method arguments and property writes follow the
    /// same rule (<c>ExpressionTypeHelper.AppliesUserDefinedExplicitOperator</c>).
    /// </p>
    /// <p>
    /// Both halves ask the same questions in the same order and convert through
    /// <c>LExpression.Convert</c> - emitted inline, or through <see cref="CastOperations"/> for a
    /// boxed value - so the two cannot reach different answers. A reference-typed custom body has
    /// no emitted conversion, since a null would reach its operator compiled and not interpreted;
    /// the interpreter serves it.
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
            if (TryConvertBuiltIn(body, resultType, out converted))
                return true;

            var bodyUnderlyingType = Nullable.GetUnderlyingType(body.Type);
            var resultUnderlyingType = Nullable.GetUnderlyingType(resultType);

            // A nullable body reaches an operator only lifted, into a nullable request: nothing in,
            // nothing out, which is also what the interpreter answers, since it returns a null
            // before any conversion is looked for.
            if (bodyUnderlyingType != null && resultUnderlyingType == null)
                return false;

            var operatorSource = bodyUnderlyingType ?? body.Type;

            var conversion = FindOperator(operatorSource, resultType);
            if (conversion == null)
                return false;

            LExpression applied;
            if (bodyUnderlyingType != null)
            {
                if (!conversion.ReturnType.IsValueType || Nullable.GetUnderlyingType(conversion.ReturnType) != null)
                    return false;

                applied = LExpression.Convert(
                    body, typeof(Nullable<>).MakeGenericType(conversion.ReturnType), conversion);
            }
            else
            {
                applied = LExpression.Convert(body, conversion.ReturnType, conversion);
            }

            return TryConvertBuiltIn(applied, resultType, out converted);
        }

        /// <summary>
        /// The interpreted half, and what the emitted form calls for an object-typed body, where
        /// only the runtime value can say what it is: the value as it is, widened where C# widens,
        /// converted by its type's own operator, or cast.
        /// </summary>
        [CanBeNull]
        public static TResult ConvertValue<TResult>([CanBeNull] object value)
        {
            if (value != null && !(value is TResult) && typeof(TResult).IsValueType)
            {
                var resultUnderlyingType = Nullable.GetUnderlyingType(typeof(TResult)) ?? typeof(TResult);
                var valueType = value.GetType();

                if (TypeCheckingUtils.IsCSharpImplicitNumericConversion(valueType, resultUnderlyingType))
                    return (TResult)CastOperations.Cast(value, resultUnderlyingType);

                // CastOperations runs the operator and the widening after it in one converter - the
                // same LExpression.Convert steps the emitted half builds.
                var conversion = FindOperator(valueType, typeof(TResult));
                if (conversion != null)
                    return (TResult)CastOperations.Cast(value, OperatorTarget(conversion, typeof(TResult)));
            }

            // C#'s cast from object: an exact or nullable match, an enum against its underlying
            // type, NullReferenceException for a null into a value type, InvalidCastException for
            // anything else.
            return (TResult)value;
        }

        /// <summary>
        /// A type's own implicit conversion operator from <paramref name="source"/> towards
        /// <paramref name="resultType"/>, in the order both halves ask: one to the request itself,
        /// then one to the request's underlying type. Null when there is none - and an explicit
        /// operator is never the answer, since it runs only through a cast written in the expression.
        /// </summary>
        [CanBeNull]
        private static MethodInfo FindOperator([NotNull] Type source, [NotNull] Type resultType)
        {
            var resultUnderlyingType = Nullable.GetUnderlyingType(resultType);

            // Between two built-in types the widening table is the whole rule. decimal declares its
            // own op_Explicit to int and to double, and honouring those would truncate 45.6m to 45 -
            // the narrowing this rule refuses for every other built-in pair.
            if (IsBuiltIn(source) && IsBuiltIn(resultUnderlyingType ?? resultType))
                return null;

            MethodInfo conversion;

            if (TypeCheckingUtils.TryGetImplicitConversion(source, resultType, out conversion))
                return conversion;

            if (resultUnderlyingType != null
                && TypeCheckingUtils.TryGetImplicitConversion(source, resultUnderlyingType, out conversion))
                return conversion;

            return null;
        }

        /// <summary>
        /// A type the CLR and C# know by type code - the numbers, <c>bool</c>, <c>char</c>,
        /// <c>DateTime</c>, <c>string</c>, and an enum through its underlying type - as against a
        /// struct or class whose conversions are its own operators.
        /// </summary>
        private static bool IsBuiltIn([NotNull] Type type)
        {
            return Type.GetTypeCode(type) != TypeCode.Object;
        }

        /// <summary>
        /// The type the interpreted half asks <see cref="CastOperations"/> for: the request itself
        /// when the operator answers it exactly, otherwise its underlying type, which the operator,
        /// or the operator and a widening, reach.
        /// </summary>
        [NotNull]
        private static Type OperatorTarget([NotNull] MethodInfo conversion, [NotNull] Type resultType)
        {
            return conversion.ReturnType == resultType
                ? resultType
                : Nullable.GetUnderlyingType(resultType) ?? resultType;
        }

        /// <summary>
        /// Identity, a value into its own nullable, the implicit numeric widenings - plain or into a
        /// nullable - and an enum against its underlying type.
        /// </summary>
        private static bool TryConvertBuiltIn(
            [NotNull] LExpression body, [NotNull] Type resultType, [CanBeNull] out LExpression converted)
        {
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

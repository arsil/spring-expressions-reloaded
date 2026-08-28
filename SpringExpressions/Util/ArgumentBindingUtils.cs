using System;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// How a call's arguments bind to one candidate's parameter list. The order of the values is the
    /// order resolution prefers them in, and it is C#'s: a candidate that takes the arguments as
    /// written beats one that had to fill a default, which beats one that had to build a params array.
    /// </summary>
    internal enum ArgumentBinding
    {
        /// <summary>The arguments do not fit this candidate at all.</summary>
        NotApplicable,

        /// <summary>
        /// One argument per parameter, nothing filled and nothing packed. A params parameter handed an
        /// array it accepts is this too - <c>Join(Names)</c> passes the caller's own array through.
        /// </summary>
        Exact,

        /// <summary>
        /// Trailing parameters the call left out were filled from their declared defaults -
        /// <c>Round(x)</c> against <c>Round(decimal, int digits = 0)</c>.
        /// </summary>
        WithOmittedOptionals,

        /// <summary>
        /// The trailing arguments were built into the params array - <c>Join('a', 'b')</c>. Defaults
        /// may have been filled on the way, which does not make it any better a match: expanding is
        /// the last resort either way.
        /// </summary>
        Expanded,

        /// <summary>
        /// Which of these this is cannot be decided from static types, because the single trailing
        /// argument might be null at runtime - and a null in a params slot is the array itself, not an
        /// element of it. Only the emitted half ever answers this; the interpreter is looking at the
        /// value and has nothing to be undecided about.
        /// </summary>
        Undecidable
    }

    /// <summary>
    /// Binds a call's arguments to a parameter list, for both backends: fills omitted optional
    /// parameters from their declared defaults, and builds the trailing <c>params</c> array.
    /// </summary>
    /// <remarks>
    /// <p>
    /// C#'s order is preserved throughout. The arguments as written are tried first; only then are
    /// defaults filled; only then is an array built. Both backends run this one implementation, so
    /// a call that compiles can only bind the way the interpreter binds it.
    /// </p>
    /// <p>
    /// Elements of the params array convert by <see cref="ArrayElementConversions"/> - the rule
    /// <c>new T[] {...}</c> already runs - so a params array is not a second kind of array
    /// construction with rules of its own.
    /// </p>
    /// </remarks>
    internal static class ArgumentBindingUtils
    {
        /// <summary>
        /// The element type of the trailing <c>params</c> array, or null if the last parameter is not
        /// one.
        /// </summary>
        [CanBeNull]
        public static Type GetParamArrayElementType([NotNull, ItemNotNull] ParameterInfo[] parameters)
        {
            if (parameters.Length == 0)
                return null;

            var lastParameter = parameters[parameters.Length - 1];

            if (lastParameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length == 0)
                return null;

            return lastParameter.ParameterType.GetElementType();
        }

        /// <summary>
        /// Whether this parameter list could take <paramref name="argumentCount"/> arguments at all.
        /// Candidate gathering asks this, having only the count: it decides which methods are worth
        /// binding, never which one wins.
        /// </summary>
        public static bool CouldTakeArgumentCount(
            [NotNull, ItemNotNull] ParameterInfo[] parameters, int argumentCount)
        {
            if (parameters.Length == argumentCount)
                return true;

            var hasParamArray = GetParamArrayElementType(parameters) != null;

            if (argumentCount > parameters.Length)
                return hasParamArray;

            for (var i = argumentCount; i < parameters.Length; i++)
            {
                // The params array is the one parameter that fills itself, with no elements.
                if (hasParamArray && i == parameters.Length - 1)
                    continue;

                object unused;
                if (!TryGetOmittedArgumentValue(parameters[i], out unused))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The value a parameter takes when the call leaves it out, or false if it cannot be left out.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The metadata is less uniform than it looks, and every branch below was measured rather than
        /// assumed. <c>ParameterAttributes.HasDefault</c> is *not* the test: a
        /// <c>decimal d = 1.5m</c> parameter carries only <c>Optional</c>, its value living in a
        /// <c>DecimalConstantAttribute</c> that <c>DefaultValue</c> reads for us. A
        /// <c>DateTime d = default(DateTime)</c> parameter does carry <c>HasDefault</c>, and its
        /// <c>DefaultValue</c> is <b>null</b> - meaning <c>default(T)</c>, not a null reference, so
        /// handing that null to a DateTime is an error rather than a value.
        /// </p>
        /// <p>
        /// <c>[Optional]</c> with no default at all reports <c>Missing.Value</c> and is refused: C#
        /// substitutes <c>default(T)</c> there by rules of its own for COM interop, and this engine
        /// has no reason to guess at them. A parameter that is not optional reports
        /// <c>DBNull.Value</c>.
        /// </p>
        /// </remarks>
        public static bool TryGetOmittedArgumentValue(
            [NotNull] ParameterInfo parameter, [CanBeNull] out object value)
        {
            value = null;

            if ((parameter.Attributes & ParameterAttributes.Optional) == 0)
                return false;

            object declared;
            try
            {
                declared = parameter.DefaultValue;
            }
            catch (Exception)
            {
                // Reading the constant can fail where the constant is nevertheless known. Measured:
                // 'DateTime d = default(DateTime)' throws FormatException("Encountered an invalid
                // type for a default value") from both DefaultValue and RawDefaultValue on net472 and
                // netcoreapp2.1, and reads back as null on net8.0 - a special case for DateTime
                // alone, since TimeSpan reads as null everywhere. HasDefault is set either way, and a
                // null constant on a value type means default(T), so nothing has to be read to know
                // it. Without this the same expression compiled on one framework and refused on
                // another.
                if ((parameter.Attributes & ParameterAttributes.HasDefault) == 0)
                    return false;

                declared = null;
            }

            if (declared is Missing || declared is DBNull)
                return false;

            var parameterType = parameter.ParameterType;

            if (declared == null)
            {
                if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null)
                    return true;

                // A null constant against a non-nullable value type is C#'s 'default(T)'.
                value = Activator.CreateInstance(parameterType);
                return true;
            }

            if (parameterType.IsInstanceOfType(declared))
            {
                value = declared;
                return true;
            }

            var underlyingType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

            if (underlyingType.IsEnum && declared.GetType().IsPrimitive)
            {
                value = Enum.ToObject(underlyingType, declared);
                return true;
            }

            if (underlyingType.IsInstanceOfType(declared))
            {
                // The declared value of a Nullable<T> parameter arrives as a bare T.
                value = declared;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The emitted half.
        /// </summary>
        public static ArgumentBinding TryBind(
            [NotNull, ItemNotNull] ParameterInfo[] parameters,
            [NotNull, ItemNotNull] LExpression[] arguments,
            [CanBeNull] out LExpression[] bound)
        {
            bound = null;

            var elementType = GetParamArrayElementType(parameters);

            if (arguments.Length == parameters.Length)
            {
                if (elementType == null)
                {
                    bound = arguments;
                    return ArgumentBinding.Exact;
                }

                var lastArgument = arguments[arguments.Length - 1];
                var lastParameterType = parameters[parameters.Length - 1].ParameterType;

                if (lastParameterType.IsAssignableFrom(lastArgument.Type) || IsNullLiteral(lastArgument))
                {
                    bound = arguments;
                    return ArgumentBinding.Exact;
                }

                // A null arriving here is the array, not an element of it - which is what the
                // interpreter reads it as, from the value. Static types cannot tell a reference that
                // happens to be null from one that is not, so the shape is left to the interpreter
                // rather than compiled into whichever reading was guessed.
                if (!IsProvablyNotNull(lastArgument))
                    return ArgumentBinding.Undecidable;
            }
            else if (arguments.Length > parameters.Length && elementType == null)
            {
                return ArgumentBinding.NotApplicable;
            }

            var fixedCount = elementType == null ? parameters.Length : parameters.Length - 1;
            var result = new LExpression[parameters.Length];
            var anyOmitted = false;

            for (var i = 0; i < fixedCount; i++)
            {
                if (i < arguments.Length)
                {
                    result[i] = arguments[i];
                    continue;
                }

                object defaultValue;
                if (!TryGetOmittedArgumentValue(parameters[i], out defaultValue))
                    return ArgumentBinding.NotApplicable;

                try
                {
                    result[i] = LExpression.Constant(defaultValue, parameters[i].ParameterType);
                }
                catch (ArgumentException)
                {
                    return ArgumentBinding.NotApplicable;
                }

                anyOmitted = true;
            }

            if (elementType == null)
            {
                bound = result;
                return anyOmitted ? ArgumentBinding.WithOmittedOptionals : ArgumentBinding.Exact;
            }

            var items = new List<LExpression>();

            for (var i = fixedCount; i < arguments.Length; i++)
            {
                LExpression item;
                if (!ArrayElementConversions.TryConvertExpression(arguments[i], elementType, out item))
                    return ArgumentBinding.NotApplicable;

                items.Add(item);
            }

            result[result.Length - 1] = LExpression.NewArrayInit(elementType, items);

            bound = result;
            return ArgumentBinding.Expanded;
        }

        /// <summary>
        /// The interpreted half, answering the same question of runtime values.
        /// </summary>
        public static ArgumentBinding TryBind(
            [NotNull, ItemNotNull] ParameterInfo[] parameters,
            [NotNull, ItemCanBeNull] object[] argValues,
            [CanBeNull] out object[] bound)
        {
            bound = null;

            var elementType = GetParamArrayElementType(parameters);

            if (argValues.Length == parameters.Length)
            {
                if (elementType == null)
                {
                    bound = argValues;
                    return ArgumentBinding.Exact;
                }

                var lastValue = argValues[argValues.Length - 1];
                var lastParameterType = parameters[parameters.Length - 1].ParameterType;

                if (lastValue == null || lastParameterType.IsInstanceOfType(lastValue))
                {
                    bound = argValues;
                    return ArgumentBinding.Exact;
                }
            }
            else if (argValues.Length > parameters.Length && elementType == null)
            {
                return ArgumentBinding.NotApplicable;
            }

            var fixedCount = elementType == null ? parameters.Length : parameters.Length - 1;
            var result = new object[parameters.Length];
            var anyOmitted = false;

            for (var i = 0; i < fixedCount; i++)
            {
                if (i < argValues.Length)
                {
                    result[i] = argValues[i];
                    continue;
                }

                object defaultValue;
                if (!TryGetOmittedArgumentValue(parameters[i], out defaultValue))
                    return ArgumentBinding.NotApplicable;

                result[i] = defaultValue;
                anyOmitted = true;
            }

            if (elementType == null)
            {
                bound = result;
                return anyOmitted ? ArgumentBinding.WithOmittedOptionals : ArgumentBinding.Exact;
            }

            var array = Array.CreateInstance(elementType, Math.Max(0, argValues.Length - fixedCount));

            for (var i = fixedCount; i < argValues.Length; i++)
            {
                object item;
                if (!ArrayElementConversions.TryConvertValue(argValues[i], elementType, out item))
                    return ArgumentBinding.NotApplicable;

                array.SetValue(item, i - fixedCount);
            }

            result[result.Length - 1] = array;

            bound = result;
            return ArgumentBinding.Expanded;
        }

        private static bool IsNullLiteral([NotNull] LExpression argument)
        {
            return argument is System.Linq.Expressions.ConstantExpression constant
                && constant.Value == null;
        }

        private static bool IsProvablyNotNull([NotNull] LExpression argument)
        {
            if (argument.Type.IsValueType)
                return Nullable.GetUnderlyingType(argument.Type) == null;

            return argument is System.Linq.Expressions.ConstantExpression constant
                && constant.Value != null;
        }
    }
}

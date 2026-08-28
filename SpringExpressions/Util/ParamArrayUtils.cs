using System;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// How a call's arguments bind to a method or constructor whose last parameter is a
    /// <c>params</c> array.
    /// </summary>
    internal enum ParamArrayBinding
    {
        /// <summary>The arguments do not fit this candidate at all.</summary>
        NotApplicable,

        /// <summary>
        /// One argument per parameter, the last of them already an array the parameter accepts, so
        /// nothing is packed - <c>Join(Names)</c> passes the caller's own array straight through.
        /// </summary>
        NormalForm,

        /// <summary>
        /// The trailing arguments are built into an array of the element type - <c>Join('a', 'b')</c>.
        /// </summary>
        Expanded,

        /// <summary>
        /// Which of the two forms this is cannot be decided from static types, because the single
        /// trailing argument might be null at runtime - and a null there is the array itself, not an
        /// element of it. Only the emitted half ever answers this; the interpreter is looking at the
        /// value and has nothing to be undecided about.
        /// </summary>
        Undecidable
    }

    /// <summary>
    /// Binds a call's arguments to a <c>params</c> parameter list, for both backends.
    /// </summary>
    /// <remarks>
    /// <p>
    /// C#'s order is preserved: the normal form is tried first and only then the expanded one. Both
    /// backends used to expand unconditionally, which meant passing an actual array to a
    /// <c>params</c> parameter tried to store the array inside a fresh one-element array of its own
    /// element type and died - <c>InvalidCastException</c> interpreted, on an expression the compiled
    /// path was answering correctly.
    /// </p>
    /// <p>
    /// The expanded form accepts one argument fewer than there are parameters, which is how
    /// <c>Join()</c> reaches a <c>params</c>-only method with an empty array. The interpreter's two
    /// routes disagreed about that: the unambiguous-name route allowed it while the candidate scan
    /// demanded at least as many arguments as parameters, so the very same call resolved or did not
    /// depending on whether the method name happened to be overloaded.
    /// </p>
    /// <p>
    /// Element conversions are <see cref="ArrayElementConversions"/>' - the rule
    /// <c>new T[] {...}</c> already runs - so a <c>params</c> array is not a second kind of array
    /// construction with rules of its own.
    /// </p>
    /// </remarks>
    internal static class ParamArrayUtils
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
        /// The emitted half.
        /// </summary>
        public static ParamArrayBinding TryBind(
            [NotNull, ItemNotNull] ParameterInfo[] parameters,
            [NotNull, ItemNotNull] LExpression[] arguments,
            [CanBeNull] out LExpression[] bound)
        {
            bound = null;

            var elementType = GetParamArrayElementType(parameters);
            if (elementType == null)
                return ParamArrayBinding.NotApplicable;

            var lastParameterType = parameters[parameters.Length - 1].ParameterType;

            // The two forms compete only when there is exactly one argument per parameter; with more
            // arguments, or with the last one missing, expansion is the only reading and no null
            // question arises.
            if (arguments.Length == parameters.Length)
            {
                var lastArgument = arguments[arguments.Length - 1];

                if (lastParameterType.IsAssignableFrom(lastArgument.Type) || IsNullLiteral(lastArgument))
                {
                    bound = arguments;
                    return ParamArrayBinding.NormalForm;
                }

                // A null arriving here is the array, not an element of it - which is what the
                // interpreter reads it as, from the value. Static types cannot tell a reference that
                // happens to be null from one that is not, so the shape is left to the interpreter
                // rather than compiled into whichever reading was guessed.
                if (!IsProvablyNotNull(lastArgument))
                    return ParamArrayBinding.Undecidable;
            }

            if (arguments.Length < parameters.Length - 1)
                return ParamArrayBinding.NotApplicable;

            var result = new LExpression[parameters.Length];

            for (var i = 0; i < parameters.Length - 1; i++)
                result[i] = arguments[i];

            var items = new List<LExpression>();

            for (var i = parameters.Length - 1; i < arguments.Length; i++)
            {
                if (!ArrayElementConversions.TryConvertExpression(arguments[i], elementType, out var item))
                    return ParamArrayBinding.NotApplicable;

                items.Add(item);
            }

            result[result.Length - 1] = LExpression.NewArrayInit(elementType, items);

            bound = result;
            return ParamArrayBinding.Expanded;
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

        /// <summary>
        /// The interpreted half, answering the same question of runtime values.
        /// </summary>
        public static ParamArrayBinding TryBind(
            [NotNull, ItemNotNull] ParameterInfo[] parameters,
            [NotNull, ItemCanBeNull] object[] argValues,
            [CanBeNull] out object[] bound)
        {
            bound = null;

            var elementType = GetParamArrayElementType(parameters);
            if (elementType == null)
                return ParamArrayBinding.NotApplicable;

            var lastParameterType = parameters[parameters.Length - 1].ParameterType;

            if (argValues.Length == parameters.Length)
            {
                var lastValue = argValues[argValues.Length - 1];

                if (lastValue == null || lastParameterType.IsInstanceOfType(lastValue))
                {
                    bound = argValues;
                    return ParamArrayBinding.NormalForm;
                }
            }

            if (argValues.Length < parameters.Length - 1)
                return ParamArrayBinding.NotApplicable;

            var result = new object[parameters.Length];

            for (var i = 0; i < parameters.Length - 1; i++)
                result[i] = argValues[i];

            var array = Array.CreateInstance(elementType, argValues.Length - (parameters.Length - 1));

            for (var i = parameters.Length - 1; i < argValues.Length; i++)
            {
                if (!ArrayElementConversions.TryConvertValue(argValues[i], elementType, out var item))
                    return ParamArrayBinding.NotApplicable;

                array.SetValue(item, i - (parameters.Length - 1));
            }

            result[result.Length - 1] = array;

            bound = result;
            return ParamArrayBinding.Expanded;
        }
    }
}

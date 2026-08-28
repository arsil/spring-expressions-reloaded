using System;
using System.Linq.Expressions;

using JetBrains.Annotations;

using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// The one rule for storing a value into an array of a declared element type, in both backends:
    /// an identity conversion, a reference or boxing conversion, or an implicit numeric widening from
    /// <see cref="TypeCheckingUtils.IsCSharpImplicitNumericConversion"/> - the same table the
    /// overload-resolution tier uses. Anything else is refused, as C# refuses it.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Written for <c>new T[] {...}</c>, where the declared element type used to be ignored outright.
    /// A <c>params</c> array is the same construction with the brackets left out, so it asks the same
    /// question and gets the same answer here rather than inventing a second rule - which is what
    /// <c>Array.SetValue</c> was doing: it widens by the CLR's primitive table, which has no
    /// <c>decimal</c> in it, so <c>Sum(1, 2)</c> into a <c>params decimal[]</c> threw where C# widens.
    /// </p>
    /// <p>
    /// Both halves answer the same question of different things - a static type on the way in for the
    /// emitted form, a boxed runtime value for the interpreted one - and neither throws: a caller
    /// refusing a whole expression and a caller merely rejecting one overload candidate need
    /// different reactions to the same "no".
    /// </p>
    /// </remarks>
    internal static class ArrayElementConversions
    {
        /// <summary>
        /// The emitted half: the expression converted to <paramref name="elementType"/>, or false.
        /// </summary>
        public static bool TryConvertExpression(
            [NotNull] LExpression item, [NotNull] Type elementType, out LExpression converted)
        {
            if (item.Type == elementType)
            {
                converted = item;
                return true;
            }

            if (item is ConstantExpression constant && constant.Value == null)
            {
                if (!elementType.IsValueType || Nullable.GetUnderlyingType(elementType) != null)
                {
                    converted = LExpression.Constant(null, elementType);
                    return true;
                }

                converted = null;
                return false;
            }

            if (elementType.IsAssignableFrom(item.Type)
                || TypeCheckingUtils.IsCSharpImplicitNumericConversion(item.Type, elementType))
            {
                converted = LExpression.Convert(item, elementType);
                return true;
            }

            converted = null;
            return false;
        }

        /// <summary>
        /// The interpreted half: the value converted to <paramref name="elementType"/>, or false.
        /// </summary>
        public static bool TryConvertValue(
            [CanBeNull] object value, [NotNull] Type elementType, [CanBeNull] out object converted)
        {
            converted = null;

            if (value == null)
                return !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) != null;

            if (elementType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            var underlyingElementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

            if (TypeCheckingUtils.IsCSharpImplicitNumericConversion(value.GetType(), underlyingElementType))
            {
                converted = Convert.ChangeType(value, underlyingElementType);
                return true;
            }

            return false;
        }
    }
}

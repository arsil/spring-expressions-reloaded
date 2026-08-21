using System;
using System.Collections.Concurrent;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// C#'s cast between boxed values, for the interpreter. Each (runtime source, target) pair
    /// compiles LExpression.Convert once - the very conversion the compiled backend emits - so the
    /// interpreter executes the compiled semantics and the backends agree by construction:
    /// truncation toward zero for real-to-integral, unchecked overflow wrap for the primitive
    /// conversions (decimal's own operators still check, as they do in C#), enum conversions,
    /// user-defined conversion operators, and runtime-checked reference conversions.
    ///
    /// The cast operator is this fork's own - the frozen legacy suite never uses 'as' - so there is
    /// no upstream behaviour to preserve. The ruling is "as means C#'s cast" and the compiled path
    /// is the specification; the interpreter's old Convert.ChangeType gave banker's rounding,
    /// checked overflow and culture-sensitive string parsing, none of which is a cast.
    /// </summary>
    internal static class CastOperations
    {
        [CanBeNull]
        public static object Cast([CanBeNull] object value, [NotNull] Type targetType)
        {
            if (value == null)
            {
                // C#: a null casts to any reference or nullable target and stays null; unboxing a
                // null into a value type is the NullReferenceException the compiled unbox throws.
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return null;

                throw new NullReferenceException(
                    $"Cannot cast a null value to the value type '{targetType}'.");
            }

            var sourceType = value.GetType();

            // Reference-compatible values - upcasts, interfaces, the identity case - pass through;
            // this is also what a compiled runtime-checked downcast does when the value fits.
            if (targetType.IsInstanceOfType(value))
                return value;

            var converter = Converters.GetOrAdd(Tuple.Create(sourceType, targetType), BuildConverter);

            if (converter == null)
            {
                // No conversion exists between the runtime type and the target - the same
                // InvalidCastException the compiled backend's runtime check reports.
                throw new InvalidCastException(
                    $"Unable to cast object of type '{sourceType}' to type '{targetType}'.");
            }

            return converter(value);
        }

        [CanBeNull]
        private static Func<object, object> BuildConverter([NotNull] Tuple<Type, Type> key)
        {
            try
            {
                var value = LExpression.Parameter(typeof(object), "value");

                var body = LExpression.Convert(
                    LExpression.Convert(
                        LExpression.Convert(value, key.Item1),
                        key.Item2),
                    typeof(object));

                return LExpression.Lambda<Func<object, object>>(body, value).Compile();
            }
            catch (InvalidOperationException)
            {
                // C# has no such cast (CS0030).
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static readonly ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>> Converters
            = new ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>>();
    }
}

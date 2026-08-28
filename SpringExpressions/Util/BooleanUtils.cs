using System;

using JetBrains.Annotations;

namespace SpringExpressions.Util
{
    /// <summary>
    /// What may stand in a boolean position, for the operators that need one.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The interpreter used to run <c>Convert.ToBoolean</c> here, so <c>45 ? a : b</c> answered
    /// <c>a</c>, <c>0.0 ? a : b</c> answered <c>b</c>, <c>'true' ? a : b</c> answered <c>a</c> and
    /// <c>'Ana' ? a : b</c> threw <c>FormatException</c>. The compiled path never had any of that -
    /// there is no such conversion in a LINQ expression tree, and none in C#, where <c>5 ? a : b</c> is
    /// CS0029. So the two backends disagreed, and which one a caller got depended on whether the shape
    /// happened to compile.
    /// </p>
    /// <p>
    /// Ruled: <b>a non-boolean is not a truth value, anywhere.</b> That is what <c>==</c> has always
    /// said - <c>45 == true</c> refuses the pair rather than answering - and the conditional operator
    /// and <c>!</c> now say the same. The alternative left <c>45 == true</c> throwing while
    /// <c>45 ? a : b</c> answered, in one expression language.
    /// </p>
    /// <p>
    /// Null is the one carve-out, and it is not truthiness: a null in a boolean position reads as
    /// false throughout this engine - the rule that makes <c>null and true</c> false names the
    /// conditional operator and <c>!</c> among the shapes it covers. A <c>bool?</c> needs no special
    /// case here, because the interpreter sees the boxed <c>bool</c> it holds, or a null.
    /// </p>
    /// </remarks>
    public static class BooleanUtils
    {
        /// <summary>
        /// Reads a value standing in a boolean position: a boolean is itself, a null is false, and
        /// anything else is the caller's error.
        /// </summary>
        /// <param name="value">The evaluated operand.</param>
        /// <param name="operatorDescription">
        /// How to name the position in the message - "the conditional test", "operator '!'".
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is neither a boolean nor null. The compiled path refuses these
        /// shapes at compile time, so this is what a caller hears after the fallback.
        /// </exception>
        public static bool RequireBoolean([CanBeNull] object value, [NotNull] string operatorDescription)
        {
            if (value is bool booleanValue)
                return booleanValue;

            if (value == null)
                return false;

            throw new ArgumentException(
                operatorDescription + ": only a boolean is a truth value, and this is of type '"
                + value.GetType().FullName + "'.");
        }
    }
}

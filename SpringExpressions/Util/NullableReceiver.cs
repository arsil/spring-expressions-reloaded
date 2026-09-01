using System;

using JetBrains.Annotations;

using SpringCore;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// A member written after a <c>Nullable&lt;T&gt;</c> is read from the value inside it, not from the
    /// nullable wrapper - <c>ShippedOn.Year</c> is the year of the date, and it fails when there is no
    /// date.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The interpreter always did this, because it never sees a wrapper: a boxed nullable holding a
    /// value <i>is</i> a boxed <c>DateTime</c>. The compiled path saw the wrapper and resolved members
    /// against <c>Nullable&lt;T&gt;</c>, so the two were reading different objects, and it showed in
    /// both directions:
    /// </p>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Seventeen ordinary shapes had no compiled form at all</b> - <c>SomeDate.Year</c>,
    /// <c>SomeDate.AddDays(1)</c>, <c>SomeSpan.TotalMinutes</c>, <c>SomeMoney.Amount</c> on a caller's
    /// own struct - because <c>Nullable&lt;T&gt;</c> declares none of them. They fell back and the
    /// interpreter answered every one.
    /// </description></item>
    /// <item><description>
    /// <b>And the members it does declare diverged</b>: <c>NoNumber.ToString()</c> was <c>""</c>
    /// compiled - which is what C# gives - and an exception interpreted, because the interpreter has a
    /// bare null and no <c>ToString</c> to find on it.
    /// </description></item>
    /// </list>
    /// <p>
    /// <b>What this costs, and it is the only cost:</b> <c>HasValue</c> and <c>Value</c> stop resolving
    /// on the compiled path, since the receiver is the underlying value now and an <c>int</c> has
    /// neither. They never worked interpreted, so no weakly typed caller could rely on them; a caller
    /// who wants the question writes <c>SomeNumber != null</c>.
    /// </p>
    /// </remarks>
    public static class NullableReceiver
    {
        /// <summary>
        /// Raised when the member is read and the nullable holds nothing - the same exception, and the
        /// same shape of message, the interpreter raises for a null in the middle of a path. Generic so
        /// it can stand in a conditional beside the member access it replaces, whatever that answers.
        /// </summary>
        [UsedImplicitly]
        public static T Fail<T>(string memberName)
        {
            throw new NullValueInNestedPathException(
                "Cannot retrieve the value of a field or property '" + memberName
                + "', because the value it is read from is null.");
        }

        /// <summary>
        /// <c>receiver.HasValue ? memberOfTheValue : Fail&lt;T&gt;(name)</c>.
        /// </summary>
        [NotNull]
        public static LExpression GuardWithHasValue(
            [NotNull] LExpression nullableReceiver,
            [NotNull] LExpression memberOfTheValue,
            [NotNull] string memberName)
        {
            return LExpression.Condition(
                LExpression.Property(nullableReceiver, "HasValue"),
                memberOfTheValue,
                LExpression.Call(
                    MiFail.MakeGenericMethod(memberOfTheValue.Type),
                    LExpression.Constant(memberName)));
        }

        private static readonly System.Reflection.MethodInfo MiFail
            = typeof(NullableReceiver).GetMethod(nameof(Fail));
    }
}

using System;

using JetBrains.Annotations;

namespace SpringExpressions.Util
{
    /// <summary>
    /// What a string means when it meets a <c>char</c> in a comparison: the single implementation both
    /// backends run, so the rule and the exception cannot drift.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>This language has no char literal <i>token</i>.</b> <c>Expression.g</c> has one quoted-literal
    /// token and it builds a <c>string</c>, so <c>'A'</c> is a string. Meanwhile the object graph an
    /// expression walks is full of chars: <c>Name[0]</c> is one, a string as a collection source yields
    /// them, and any model may declare a <c>char</c> member. So <c>Letter == 'A'</c> - the obvious thing
    /// to write - threw on both backends.
    /// </p>
    /// <p>
    /// <b>What this rule buys is the short spelling, and the first draft of this comment overstated it
    /// as a missing capability.</b> A char constant could always be written - <c>'A'[0]</c>, indexing
    /// the one-character string, measured working on both backends and in every position a char works -
    /// so <c>Letter == 'A'[0]</c> and <c>Letter between {'A'[0],'Z'[0]}</c> were available before this
    /// existed. The case for the rule is that nobody guesses <c>'A'[0]</c>, not that nothing else
    /// worked. Recorded because the wrong version was written down first.
    /// </p>
    /// <p>
    /// <b>The string is read as a char, not the char as a string</b>, and that direction is the ruling.
    /// Reading the char as a string would have to reach the whole ordering surface - <c>sort()</c>,
    /// <c>min()</c>, <c>max()</c> compare through the same path, and letting <c>&lt;</c> order as
    /// strings while <c>sort()</c> ordered ordinally is exactly the incoherence the custom-decimal
    /// ruling removed - and it would make char ordering locale-dependent, since this engine orders
    /// strings with <c>Comparer&lt;string&gt;.Default</c>, which is CurrentCulture. Measured:
    /// <c>'a' &lt; 'B'</c> is false as chars and true as strings. Converting the string instead is
    /// strictly additive: every row that works today is untouched, and no locale enters.
    /// </p>
    /// <p>
    /// It is the same shape as the rule already shipped for enums - <c>Type == 'One'</c> does not turn
    /// the enum into a string, it reads the string as a member name
    /// (<see cref="EqualityUtils.EnumEqualsName"/>). The string is the thing this language can spell, so
    /// the string is the thing that adapts.
    /// </p>
    /// <p>
    /// <b>Not in scope, deliberately:</b> arithmetic and the bitwise operators. <c>Letter + 1</c>,
    /// <c>Letter and 3</c>, <c>!Letter</c> and <c>Letters.sum()</c> still refuse on both backends, and
    /// open-issues item 3 rules that they stay that way: the idiom people want is C#'s
    /// <c>(char)('a' + 1)</c>, and its translation <c>('a'[0] as int + 1) as char</c> answers
    /// <c>'b'</c> here today - with the same cast C# itself requires. So is <c>in</c>, which does not
    /// route through equality at all: <c>OpIn</c> calls <c>IList.Contains</c>, and a non-generic
    /// <c>Contains</c> on a <c>List&lt;string&gt;</c> rejects a boxed char before comparing anything.
    /// </p>
    /// </remarks>
    internal static class CharTextUtils
    {
        /// <summary>
        /// The char a string stands for when it meets one, or null for a null string.
        /// </summary>
        /// <remarks>
        /// A string of any length other than one names no char and is an <see cref="ArgumentException"/>
        /// on both backends - the enum rule's answer to the same question, where a string that names no
        /// member throws rather than answering false. Answering false instead would make
        /// <c>Letter == 'AB'</c> read as a genuine comparison that happened to fail, which is the
        /// silent-wrong-answer class this fork keeps removing.
        /// </remarks>
        public static char? TextAsCharOrNull([CanBeNull] string text)
        {
            if (text == null)
                return null;

            if (text.Length != 1)
            {
                throw new ArgumentException(
                    $"'{text}' does not name a character; comparing a char to a string compares it to "
                    + "a string of exactly one character.");
            }

            return text[0];
        }

        /// <summary>
        /// Whether a char equals the character the string names - emitted by the compiled path and
        /// reached by the interpreter through <c>CompareUtils.Compare</c>, both by way of
        /// <see cref="TextAsCharOrNull"/>.
        /// </summary>
        /// <remarks>
        /// A null string equals no char, which is what <c>OpEqual.Get</c> answers for any null operand
        /// before it consults anything else. Answering it here keeps the compiled path from reaching
        /// the conversion with a null and lets one rule serve both.
        /// </remarks>
        [MustUseReturnValue]
        public static bool CharEqualsText(char value, [CanBeNull] string text)
        {
            var other = TextAsCharOrNull(text);

            return other.HasValue && other.Value == value;
        }

        /// <summary>
        /// Whether this pair is a char meeting a string, in either order.
        /// </summary>
        public static bool IsCharAgainstText([NotNull] Type first, [NotNull] Type second)
        {
            return (first == typeof(char) && second == typeof(string))
                || (first == typeof(string) && second == typeof(char));
        }
    }
}

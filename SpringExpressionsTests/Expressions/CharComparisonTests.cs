using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class CharComparisonCases
    {
        public char Letter { get; set; } = 'A';

        public char Other { get; set; } = 'B';

        public char Lower { get; set; } = 'a';

        public string A { get; set; } = "A";

        public string Nope { get; set; }

        public List<char> Letters { get { return new List<char> { 'c', 'a', 'b' }; } }
    }

    /// <summary>
    /// A string meeting a <c>char</c> in a comparison is read as the character it names.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>This language has no char literal <i>token</i>.</b> <c>'A'</c> is a string - the grammar has
    /// one quoted-literal token - while the object graph hands chars out freely: <c>Name[0]</c> is one,
    /// a string as a collection source yields them, and any model may declare a <c>char</c> member.
    /// <c>Letter == 'A'</c>, the obvious thing to write, threw on both backends.
    /// </p>
    /// <p>
    /// <b>What this buys is the short spelling, and the first version of this comment claimed more.</b>
    /// A char constant could always be written as <c>'A'[0]</c> - indexing the one-character string -
    /// so <c>Letter == 'A'[0]</c> and <c>Letter between {'A'[0],'Z'[0]}</c> worked before the rule
    /// existed. <see cref="ACharLiteralCouldAlwaysBeWrittenTheLongWay"/> is the record.
    /// </p>
    /// <p>
    /// <b>The string converts, not the char</b>, and the direction is the ruling. Treating the char as
    /// a string would have to reach <c>sort()</c>, <c>min()</c> and <c>max()</c> as well - they compare
    /// through the same path, and letting <c>&lt;</c> order as strings while <c>sort()</c> ordered
    /// ordinally is the incoherence the custom-decimal ruling removed - and it would make char ordering
    /// locale-dependent, since strings order by <c>Comparer&lt;string&gt;.Default</c>, which is
    /// CurrentCulture. Converting the string is strictly additive instead: every row that worked before
    /// is untouched.
    /// </p>
    /// <p>
    /// It is the enum rule's shape one type over: <c>Type == 'One'</c> reads the string as a member
    /// name rather than turning the enum into a string.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class CharComparisonTests : BaseCompiledTests
    {
        [Test]
        public void ACharEqualsTheCharacterAStringNames()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter == 'A'", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        [Test]
        public void TheStringMayBeOnEitherSide()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "'A' == Letter", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        [Test]
        public void ADifferentCharacterIsNotEqual()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter == 'B'", new CharComparisonCases())
                .ResultEqualsTo(false);
        }

        /// <summary>
        /// Inequality is the exact negation of equality, as it is for the enum rule - the standing rule
        /// that kept <c>Type == 'One'</c> and <c>Type != 'One'</c> from disagreeing.
        /// </summary>
        [Test]
        public void InequalityIsEqualityNegated()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter != 'A'", new CharComparisonCases())
                .ResultEqualsTo(false);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter != 'B'", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// Not only a literal: the rule is about a string meeting a char, wherever the string came from.
        /// </summary>
        [Test]
        public void AStringPropertyWorksTheSameWayAsALiteral()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter == A", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "A == Letter", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        [Test]
        public void ACharIsLessThanTheCharacterAStringNames()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter < 'B'", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "'B' < Letter", new CharComparisonCases())
                .ResultEqualsTo(false);
        }

        [Test]
        public void TheOtherThreeRelationalOperatorsFollow()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter <= 'A'", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter > 'B'", new CharComparisonCases())
                .ResultEqualsTo(false);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter >= 'A'", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        [Test]
        public void BetweenTakesStringBoundsToo()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter between {'A','Z'}", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter between {'B','Z'}", new CharComparisonCases())
                .ResultEqualsTo(false);
        }

        /// <summary>
        /// Ordering stays ordinal, which is the whole reason the string converts rather than the char.
        /// <c>'a'</c> is 97 and <c>'B'</c> is 66, so a char comparison says false; the same pair compared
        /// as strings says true, because <c>Comparer&lt;string&gt;.Default</c> is culture-sensitive. This
        /// pin is what fails if anyone ever reverses the conversion.
        /// </summary>
        [Test]
        public void OrderingIsOrdinalAndNotCultureSensitive()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Lower < 'B'", new CharComparisonCases())
                .ResultEqualsTo(false);

            Assert.AreEqual(-1, Comparer<string>.Default.Compare("a", "B"),
                "the string ordering this rule deliberately does not use");
        }

        // ---------------------------------------------------------------------------------------
        // Nulls, and a string that names no character
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// A null string equals no char, which is what every null operand answers before anything else
        /// is consulted.
        /// </summary>
        [Test]
        public void ANullStringEqualsNoChar()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter == Nope", new CharComparisonCases())
                .ResultEqualsTo(false);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Nope == Letter", new CharComparisonCases())
                .ResultEqualsTo(false);
        }

        /// <summary>
        /// And in ordering a null still sorts before everything - item 17's ruling, reached here because
        /// the string becomes a <c>char?</c> rather than a <c>char</c>, so the nullable machinery answers
        /// the three sort-order outcomes exactly as it does for any other kind of nothing.
        /// </summary>
        [Test]
        public void ANullStringStillSortsBeforeEverything()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter < Nope", new CharComparisonCases())
                .ResultEqualsTo(false);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter > Nope", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// A string of any other length names no character and throws on both backends - the enum rule's
        /// answer to the same question, where a string naming no member throws rather than answering
        /// false. Answering false would make <c>Letter == 'AB'</c> read as a comparison that happened to
        /// fail, which is the silent-wrong-answer class this fork keeps removing.
        /// </summary>
        [Test]
        public void AStringOfAnyOtherLengthNamesNoCharacter()
        {
            var root = new CharComparisonCases();

            Assert.Catch<ArgumentException>(
                () => CompileGetter<CharComparisonCases, object>("Letter == 'AB'").GetValue(root));
            Assert.Catch<ArgumentException>(
                () => InterpretGetter<CharComparisonCases, object>("Letter == 'AB'").GetValue(root));

            Assert.Catch<ArgumentException>(
                () => CompileGetter<CharComparisonCases, object>("Letter == ''").GetValue(root));
            Assert.Catch<ArgumentException>(
                () => InterpretGetter<CharComparisonCases, object>("Letter == ''").GetValue(root));

            Assert.Catch<ArgumentException>(
                () => CompileGetter<CharComparisonCases, object>("Letter < 'AB'").GetValue(root));
            Assert.Catch<ArgumentException>(
                () => InterpretGetter<CharComparisonCases, object>("Letter < 'AB'").GetValue(root));
        }

        // ---------------------------------------------------------------------------------------
        // Unchanged
        // ---------------------------------------------------------------------------------------

        [Test]
        public void CharAgainstCharIsUnchanged()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter == Letter", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter < Other", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter == null", new CharComparisonCases())
                .ResultEqualsTo(false);
        }

        [Test]
        public void StringAgainstStringIsUnchanged()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "'A' == 'A'", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "'AB' == 'AB'", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "'A' < 'B'", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// Concatenation already read a char as text and is untouched - it is where the engine first
        /// said a char is a string, and the inconsistency this rule removes was that <c>==</c> did not
        /// agree with it.
        /// </summary>
        [Test]
        public void ConcatenationIsUnchanged()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter + 'x'", new CharComparisonCases())
                .ResultEqualsTo("Ax");
        }

        [Test]
        public void SortingAndTheAggregatorsAreUnchanged()
        {
            var sorted = (List<object>)TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letters.sort()", new CharComparisonCases()).Result;
            Assert.AreEqual(new List<object> { 'a', 'b', 'c' }, sorted);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letters.min()", new CharComparisonCases())
                .ResultEqualsTo('a');

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letters.max()", new CharComparisonCases())
                .ResultEqualsTo('c');
        }

        // ---------------------------------------------------------------------------------------
        // Out of scope - do not fix one side
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Arithmetic and the bitwise operators still refuse a char on both backends. That is the
        /// numeric-tower half of open-issues item 3 and it is a separate ruling: admitting char to
        /// <c>IsInteger</c> reaches binary numeric promotion, comparison, equality, the bitwise
        /// operators and the overload-widening tables, each of which has a recent ruling to re-check.
        /// <b>Do not fix one side</b> - the compiled path refuses and the interpreter raises the error
        /// at evaluation, which is the standing paired shape.
        /// </summary>
        /// <summary>
        /// The long spelling of a char constant, which has always worked and which this rule exists to
        /// shorten rather than to replace. Kept as a test because the ruling's own justification was
        /// written down wrongly first - as "the language cannot spell a char" - and this is what
        /// disproves it.
        /// </summary>
        [Test]
        public void ACharLiteralCouldAlwaysBeWrittenTheLongWay()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "'A'[0]", new CharComparisonCases())
                .ResultEqualsTo('A');

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter == 'A'[0]", new CharComparisonCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter between {'A'[0],'Z'[0]}", new CharComparisonCases())
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// And the reason the numeric tower is ruled <b>won't do</b> rather than left open: the idiom it
        /// was wanted for is C#'s <c>(char)('a' + 1)</c>, and this is that expression, answering
        /// <c>'b'</c> with the same cast C# itself requires. C# does not answer <c>'b'</c> for
        /// <c>'a' + 1</c> either - it answers <c>98</c>.
        /// </summary>
        [Test]
        public void TheNextCharacterIdiomAlreadyWorks()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "('a'[0] as int + 1) as char", new CharComparisonCases())
                .ResultEqualsTo('b');

            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "(Letter as int + 1) as char", new CharComparisonCases())
                .ResultEqualsTo('B');

            Assert.AreEqual(98, 'a' + 1, "C# answers an int, not a char");
        }

        [Test]
        public void ArithmeticOnACharIsStillRefused()
        {
            var root = new CharComparisonCases();

            Assert.Catch<CompileErrorException>(
                () => CompileGetter<CharComparisonCases, object>("Letter + 1"));

            Assert.Catch<ArgumentException>(
                () => InterpretGetter<CharComparisonCases, object>("Letter + 1").GetValue(root));

            Assert.Catch<ArgumentException>(
                () => InterpretGetter<CharComparisonCases, object>("Letter and 3").GetValue(root));
        }

        /// <summary>
        /// <c>in</c> does not route through equality and is deliberately untouched: <c>OpIn</c> calls
        /// <c>IList.Contains</c>, and a non-generic <c>Contains</c> on a <c>List&lt;string&gt;</c>
        /// rejects a boxed char before comparing anything - so this answers <c>False</c> rather than
        /// throwing, on both backends.
        /// <p>
        /// <b>Do not fix one side.</b> Making <c>in</c> use this engine's own equality is a wider
        /// ruling than this one: it would change what <c>in</c> means for every operand pair, not only
        /// for chars - <c>45 in {45L}</c> is false today for exactly the same reason.
        /// </p>
        /// </summary>
        [Test]
        public void InDoesNotUseThisRuleAndStillAnswersFalse()
        {
            TestCompiledVsInterpreted<CharComparisonCases, object>(
                "Letter in {'A','B'}", new CharComparisonCases())
                .ResultEqualsTo(false);
        }
    }
}

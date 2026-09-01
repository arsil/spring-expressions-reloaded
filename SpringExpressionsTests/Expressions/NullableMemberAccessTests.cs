using System;

using NUnit.Framework;

using SpringCore;
using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public struct Coin
    {
        public decimal Amount;
        public Coin(decimal amount) { Amount = amount; }
        public decimal Doubled() { return Amount * 2; }
    }

    public class NullableMemberRoot
    {
        public DateTime? SomeDate { get; set; } = new DateTime(2020, 3, 4, 5, 6, 7);
        public DateTime? NoDate { get; set; }
        public TimeSpan? SomeSpan { get; set; } = TimeSpan.FromHours(3);
        public int? SomeNumber { get; set; } = 7;
        public int? NoNumber { get; set; }
        public Coin? SomeCoin { get; set; } = new Coin(5m);
        public Coin? NoCoin { get; set; }
    }

    /// <summary>
    /// A member written after a <c>Nullable&lt;T&gt;</c> is read from the value inside it, not from the
    /// nullable wrapper. <c>ShippedOn.Year</c> is the year of the date, and it fails when there is no
    /// date.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The interpreter always did this and had no choice: a boxed nullable holding a value <i>is</i> a
    /// boxed <c>DateTime</c>, so it never sees a wrapper. The compiled path saw one and resolved members
    /// against <c>Nullable&lt;T&gt;</c>, so the two were reading different objects - and it cost in both
    /// directions at once.
    /// </p>
    /// <p>
    /// <b>Mostly it cost compiled forms.</b> Seventeen ordinary shapes had none: <c>SomeDate.Year</c>,
    /// <c>SomeDate.AddDays(1)</c>, <c>SomeSpan.TotalMinutes</c>, and a caller's own struct through a
    /// nullable. All fell back and the interpreter answered every one, so this ruling is mostly a
    /// feature rather than a fix.
    /// </p>
    /// <p>
    /// <b>And the members <c>Nullable&lt;T&gt;</c> does declare diverged.</b> <c>NoNumber.ToString()</c>
    /// was <c>""</c> compiled - which is what C# gives, since the wrapper has a <c>ToString</c> - and an
    /// exception interpreted.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class NullableMemberAccessTests : BaseCompiledTests
    {
        /// <summary>
        /// Properties of the underlying type, which had no compiled form at all before.
        /// </summary>
        [Test]
        public void APropertyOfTheUnderlyingTypeIsReadFromTheValue()
        {
            var root = new NullableMemberRoot();

            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeDate.Year", root).ResultEqualsTo(2020);
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeDate.Month", root).ResultEqualsTo(3);
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeDate.DayOfWeek", root)
                .ResultEqualsTo(DayOfWeek.Wednesday);
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeDate.Date", root)
                .ResultEqualsTo(new DateTime(2020, 3, 4));
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeSpan.TotalMinutes", root)
                .ResultEqualsTo(180.0);

            // a caller's own struct reached through a nullable
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeCoin.Amount", root).ResultEqualsTo(5m);
        }

        /// <summary>
        /// And methods, which had no compiled form either.
        /// </summary>
        [Test]
        public void AMethodOfTheUnderlyingTypeIsCalledOnTheValue()
        {
            var root = new NullableMemberRoot();

            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeDate.AddDays(1)", root)
                .ResultEqualsTo(new DateTime(2020, 3, 5, 5, 6, 7));
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeDate.ToString('yyyy')", root)
                .ResultEqualsTo("2020");
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeCoin.Doubled()", root)
                .ResultEqualsTo(10m);
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeNumber.ToString()", root)
                .ResultEqualsTo("7");
        }

        /// <summary>
        /// With nothing there, the member cannot be read and both backends say so - the compiled path
        /// through the same <c>NullValueInNestedPathException</c> the interpreter raises for a null in
        /// the middle of a path, emitted as a call to one shared helper so the message cannot drift.
        /// </summary>
        [Test]
        public void WithNoValueTheMemberCannotBeRead()
        {
            var root = new NullableMemberRoot();

            Assert.Throws<NullValueInNestedPathException>(
                () => CompileGetter<NullableMemberRoot, object>("NoDate.Year").GetValue(root));
            Assert.Throws<NullValueInNestedPathException>(
                () => InterpretGetter<NullableMemberRoot, object>("NoDate.Year").GetValue(root));

            Assert.Throws<NullValueInNestedPathException>(
                () => CompileGetter<NullableMemberRoot, object>("NoCoin.Amount").GetValue(root));
            Assert.Throws<NullValueInNestedPathException>(
                () => InterpretGetter<NullableMemberRoot, object>("NoCoin.Amount").GetValue(root));
        }

        /// <summary>
        /// <c>NoNumber.ToString()</c> used to be the divergence this ruling was found through - <c>""</c>
        /// compiled, an exception interpreted. Both fail now. The compiled answer was C#'s and was
        /// arguably the better one; predictability won, as it has everywhere else, because which backend
        /// runs is decided by the caller's declared context type rather than by anything they wrote.
        /// </summary>
        [Test]
        public void AMethodOnNothingFailsOnBothBackendsNow()
        {
            var root = new NullableMemberRoot();

            Assert.Catch<Exception>(
                () => CompileGetter<NullableMemberRoot, object>("NoNumber.ToString()").GetValue(root));
            Assert.Catch<Exception>(
                () => InterpretGetter<NullableMemberRoot, object>("NoNumber.ToString()").GetValue(root));

            // C#, for contrast: the wrapper has a ToString and it answers the empty string
            int? nothing = null;
            Assert.AreEqual(string.Empty, nothing.ToString());
        }

        /// <summary>
        /// The only cost: <c>HasValue</c> and <c>Value</c> no longer resolve, because the receiver is the
        /// underlying value and an <c>int</c> has neither. They never worked interpreted, so no weakly
        /// typed caller could have relied on them - and the compiled path answering <c>True</c> while the
        /// interpreter refused was itself a divergence. A caller who wants the question writes
        /// <c>SomeNumber != null</c>, asserted below as the replacement.
        /// </summary>
        [Test]
        public void HasValueAndValueNoLongerResolveAndThatIsTheCost()
        {
            var root = new NullableMemberRoot();

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<NullableMemberRoot, object>(
                    "SomeNumber.HasValue", EvaluationMode.MustCompile));
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<NullableMemberRoot, object>(
                    "SomeNumber.Value", EvaluationMode.MustCompile));

            // the spelling that works, on both backends
            TestCompiledVsInterpreted<NullableMemberRoot, object>("SomeNumber != null", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<NullableMemberRoot, object>("NoNumber != null", root)
                .ResultEqualsTo(false);
        }
    }
}

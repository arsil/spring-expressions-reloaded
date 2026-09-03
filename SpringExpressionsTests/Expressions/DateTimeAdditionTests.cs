using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// What a DateTime can be added to, and what it cannot.
    /// </summary>
    /// <remarks>
    /// The compiled path carried a branch for <c>DateTime + DateTime</c> that could never have worked:
    /// it called a <c>MethodInfo</c> looked up as <c>op_Addition(DateTime, DateTime)</c>, which the BCL
    /// does not declare - adding two points in time is meaningless - so the field was null from
    /// type-initialisation onward and every use died with <c>ArgumentNullException: Value cannot be null
    /// (Parameter 'method')</c>. Nothing named it, nothing tested it, and it had been there since it was
    /// written. The branch is gone; the pair falls through to the refusal, which is what the interpreter
    /// has always said about it.
    /// </remarks>
    [TestFixture]
    public class DateTimeAdditionTests : BaseCompiledTests
    {
        public class Holder
        {
            public DateTime When { get; set; } = new DateTime(2001, 1, 1);
            public DateTime Later { get; set; } = new DateTime(2001, 1, 5);
            public TimeSpan Span { get; set; } = TimeSpan.FromDays(2);
            public int Days { get; set; } = 5;

            // A nullable number of days, and a nullable real one - 0.5 days is twelve hours. Both
            // shapes had no compiled form until the guard below stopped asking IsNumericExpression.
            public int? MaybeDays { get; set; } = 5;
            public int? NoDays { get; set; }
            public decimal? HalfDay { get; set; } = 0.5m;
        }

        [Test]
        public void ADateTimePlusAnotherDateTimeIsRefusedByBothBackends()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>("When + Later", EvaluationMode.MustCompile),
                "the compiled path must refuse, not die on a null MethodInfo");

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<Holder, object>("When + Later", EvaluationMode.MustInterpret)
                    .GetValue(new Holder()),
                "and the interpreter reports what is actually wrong");

            Assert.Throws<ArgumentException>(
                () => Expression.Parse("When + Later").GetValue<Holder>(new Holder()),
                "so the weak path refuses, falls back, and ends at the interpreter's answer");
        }

        /// <summary>
        /// The neighbouring branches are the real operations, and they were never broken - kept here so
        /// that removing the dead one is visibly a removal and not a loss.
        /// </summary>
        [Test]
        public void ADateTimePlusTheThingsItCanBeAddedTo()
        {
            var holder = new Holder();

            TestCompiledVsInterpreted<Holder, object>("When + Days", holder)
                .ResultEqualsTo(new DateTime(2001, 1, 6));

            TestCompiledVsInterpreted<Holder, object>("When + '02:00:00'", holder)
                .ResultEqualsTo(new DateTime(2001, 1, 1, 2, 0, 0));
        }

        /// <summary>
        /// A DateTime plus a TimeSpan - the most natural spelling of the operation - and the one that
        /// had no compiled form for longest.
        /// </summary>
        /// <remarks>
        /// OpADD emitted for DateTime + string (parsed as a TimeSpan) and DateTime + a number of days
        /// and had no branch for the TimeSpan itself, though the MethodInfo those two call was sitting
        /// right there. It needed no branch in the end: <c>DateTime.op_Addition(DateTime, TimeSpan)</c>
        /// is a user-defined operator like any other, and the operator lookup finds it. The same lookup
        /// is why <c>Span + Other</c> works.
        /// </remarks>
        [Test]
        public void ADateTimePlusATimeSpanIsTheBclOperator()
        {
            TestCompiledVsInterpreted<Holder, object>("When + Span", new Holder())
                .ResultEqualsTo(new DateTime(2001, 1, 3));
        }

        /// <summary>
        /// TimeSpan arithmetic, which the engine had none of - for the same reason, and fixed by the
        /// same lookup.
        /// </summary>
        [Test]
        public void TimeSpansAddToEachOther()
        {
            TestCompiledVsInterpreted<Holder, object>("Span + Span", new Holder())
                .ResultEqualsTo(TimeSpan.FromDays(4));
        }

        /// <summary>
        /// Subtraction is the real asymmetry: DateTime - DateTime *is* defined and yields a TimeSpan,
        /// which is why only the addition lookup was null.
        /// </summary>
        [Test]
        public void ADateTimeMinusAnotherDateTimeIsATimeSpan()
        {
            TestCompiledVsInterpreted<Holder, object>("Later - When", new Holder())
                .ResultEqualsTo(TimeSpan.FromDays(4));
        }

        /// <summary>
        /// A nullable number of days. Nothing in it means nothing out, which is the standing rule for
        /// every arithmetic operator here - so this was a missing compiled form and not a question.
        /// </summary>
        /// <remarks>
        /// <c>OpADD</c>'s day-adding branch asked <c>IsNumericExpression</c>, which an <c>int?</c>
        /// fails, so <c>When + MaybeDays</c> refused and fell back. The interpreter answered all along:
        /// it sees an unwrapped value or a bare null and never a wrapper. Nothing else was needed - the
        /// result is a <c>DateTime?</c>, exactly as the string branch beside it already produced for
        /// the same reason, and a <c>DateTime?</c> boxes to a <c>DateTime</c> or to the null reference,
        /// so no value on the heap can tell the two backends apart.
        /// </remarks>
        [Test]
        public void ADateTimePlusANullableNumberOfDays()
        {
            var holder = new Holder();

            TestCompiledVsInterpreted<Holder, object>("When + MaybeDays", holder)
                .ResultEqualsTo(new DateTime(2001, 1, 6));

            TestCompiledVsInterpreted<Holder, object>("When + NoDays", holder)
                .ResultEqualsTo(null);

            TestCompiledVsInterpreted<Holder, object>("When + HalfDay", holder)
                .ResultEqualsTo(new DateTime(2001, 1, 1, 12, 0, 0));
        }

        /// <summary>
        /// Each operand is evaluated exactly once, left before right - and the compiled path used to get
        /// both halves of that wrong for <c>DateTime + string</c>.
        /// </summary>
        /// <remarks>
        /// The branch was a bare conditional over the operand expressions, so the right operand was
        /// emitted twice and ran twice, while the left - appearing only inside the true branch - did not
        /// run at all when the right turned out to be null. Only a side-effecting operand can see
        /// either, which is why it survived; it is the same defect <c>OpAND</c> and <c>OpOR</c> had,
        /// where <c>0 or SideEffect()</c> ran the side effect twice. Both operands go into block
        /// variables now, assigned in the order <c>Get</c> evaluates them.
        /// </remarks>
        [Test]
        public void EachOperandOfADateSpanAdditionIsEvaluatedExactlyOnce()
        {
            foreach (var expression in new[]
                { "Date() + Span()", "Date() + NoSpan()", "Date() + MaybeDayCount()", "Date() + NoDayCount()" })
            {
                var compiled = new Counter();
                Expression.ParseGetter<Counter, object>(expression, EvaluationMode.MustCompile)
                    .GetValue(compiled);

                var interpreted = new Counter();
                Expression.ParseGetter<Counter, object>(expression, EvaluationMode.MustInterpret)
                    .GetValue(interpreted);

                Assert.AreEqual(1, compiled.DateReads, expression + " - compiled left operand");
                Assert.AreEqual(1, compiled.SpanReads, expression + " - compiled right operand");

                Assert.AreEqual(
                    interpreted.DateReads, compiled.DateReads,
                    expression + " - the backends must read the left operand the same number of times");

                Assert.AreEqual(
                    interpreted.SpanReads, compiled.SpanReads,
                    expression + " - and the right operand too");
            }
        }

        /// <summary>
        /// A root whose operands count their own reads, for the test above. Separate from
        /// <see cref="Holder"/> because a property read is not observable and a method call is.
        /// </summary>
        public class Counter
        {
            public int DateReads;
            public int SpanReads;

            public DateTime Date()
            {
                DateReads++;
                return new DateTime(2001, 1, 1);
            }

            public string Span()
            {
                SpanReads++;
                return "02:00:00";
            }

            public string NoSpan()
            {
                SpanReads++;
                return null;
            }

            public int? MaybeDayCount()
            {
                SpanReads++;
                return 3;
            }

            public int? NoDayCount()
            {
                SpanReads++;
                return null;
            }
        }
    }
}

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
        /// A DateTime plus a TimeSpan - the most natural spelling of the operation - has no compiled
        /// form at all, and never had one.
        /// </summary>
        /// <remarks>
        /// Not a regression and not related to the dead DateTime + DateTime branch: OpADD emits for
        /// DateTime + string (parsed as a TimeSpan) and DateTime + a number of days, and simply has no
        /// branch for the TimeSpan itself, though DateTimeAddTimeSpanMethodInfo - which those two
        /// branches call - is sitting right there. So it refuses, the interpreter adds it, and the
        /// answer is right; it is a missing compiled form rather than a defect. Recorded here because
        /// the shape looks like it ought to be the *first* thing that compiles.
        /// </remarks>
        [Test]
        public void ADateTimePlusATimeSpanIsInterpretedForWantOfACompiledForm()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>("When + Span", EvaluationMode.MustCompile));

            Assert.AreEqual(
                new DateTime(2001, 1, 3),
                Expression.Parse("When + Span").GetValue<Holder>(new Holder()));
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
    }
}

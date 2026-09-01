using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class ConcatRoot
    {
        public string NullName { get; set; }
        public string Text { get; set; } = "b";
        public int? NoNumber { get; set; }
        public int? SomeNumber { get; set; } = 7;
        public int Number { get; set; } = 5;
        public bool Flag { get; set; } = true;
        public object NullObject { get; set; }
        public object SomeText { get; set; } = "o";
        public DateTime When { get; set; } = new DateTime(2020, 1, 1);
    }

    /// <summary>
    /// <c>+</c> concatenates only when at least one operand is an actual string at run time. Otherwise
    /// a null propagates, the same way it does everywhere else in arithmetic.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>The rule exists because only one backend has the information.</b> With a null on the left the
    /// interpreter holds a bare null reference and cannot know the declared type was <c>string</c> - it
    /// answers the same for a null <c>string</c> and a null <c>object</c>. The compiled path can see it.
    /// So "make both concatenate" was never available, and agreement had to be reached the other way.
    /// </p>
    /// <p>
    /// Before this, <c>NullName + Number</c> was <c>"5"</c> compiled and <c>null</c> interpreted;
    /// <c>NullName + NullName</c> was <c>""</c> compiled and an <c>ArgumentException</c> interpreted;
    /// and <c>NoNumber + NoNumber</c> threw interpreted while answering null compiled. 80 rows of
    /// <c>EvaluationNeverDivergesTests</c>.
    /// </p>
    /// <p>
    /// <b>Null rather than <c>""</c>, and that is forced rather than chosen.</b> Two nulls are two
    /// nulls to the interpreter whether they were strings or ints, so one answer has to serve both -
    /// and <c>""</c> is absurd for two ints. Two deviations from C# follow, both confined to nulls:
    /// <c>(string)null + 5</c> is <c>"5"</c> in C# and null here, and
    /// <c>(string)null + (string)null</c> is <c>""</c> in C# and null here.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class NullConcatenationTests : BaseCompiledTests
    {
        /// <summary>
        /// A real string on either side concatenates, and a null beside it reads as empty - which is
        /// C#'s behaviour and was already agreed before this change. Nearly every concatenation is here.
        /// </summary>
        [Test]
        public void ARealStringOnEitherSideStillConcatenates()
        {
            var root = new ConcatRoot();

            TestCompiledVsInterpreted<ConcatRoot, object>("NullName + Text", root).ResultEqualsTo("b");
            TestCompiledVsInterpreted<ConcatRoot, object>("Text + NullName", root).ResultEqualsTo("b");
            TestCompiledVsInterpreted<ConcatRoot, object>("NullName + 'x'", root).ResultEqualsTo("x");
            TestCompiledVsInterpreted<ConcatRoot, object>("'x' + NullName", root).ResultEqualsTo("x");
            TestCompiledVsInterpreted<ConcatRoot, object>("Text + Number", root).ResultEqualsTo("b5");
            TestCompiledVsInterpreted<ConcatRoot, object>("Number + Text", root).ResultEqualsTo("5b");
            TestCompiledVsInterpreted<ConcatRoot, object>("Text + Text", root).ResultEqualsTo("bb");
        }

        /// <summary>
        /// With no actual string anywhere, there is nothing to concatenate to, so the null propagates.
        /// This is the half that used to answer <c>"5"</c> compiled against null interpreted.
        /// </summary>
        [Test]
        public void WithNoRealStringTheNullPropagates()
        {
            var root = new ConcatRoot();

            Assert.IsNull(CompileGetter<ConcatRoot, object>("NullName + Number").GetValue(root));
            Assert.IsNull(InterpretGetter<ConcatRoot, object>("NullName + Number").GetValue(root));

            Assert.IsNull(CompileGetter<ConcatRoot, object>("Number + NullName").GetValue(root));
            Assert.IsNull(InterpretGetter<ConcatRoot, object>("Number + NullName").GetValue(root));

            Assert.IsNull(CompileGetter<ConcatRoot, object>("NullName + Flag").GetValue(root));
            Assert.IsNull(InterpretGetter<ConcatRoot, object>("NullName + Flag").GetValue(root));

            Assert.IsNull(CompileGetter<ConcatRoot, object>("NullName + NullName").GetValue(root));
            Assert.IsNull(InterpretGetter<ConcatRoot, object>("NullName + NullName").GetValue(root));
        }

        /// <summary>
        /// An <c>object</c> operand is asked at run time, because it might be holding a string - which
        /// is the case the rule would get wrong if it were decided statically.
        /// </summary>
        [Test]
        public void AnObjectOperandIsAskedAtRunTime()
        {
            var root = new ConcatRoot();

            // it holds a string, so there is something to concatenate to
            TestCompiledVsInterpreted<ConcatRoot, object>("NullName + SomeText", root).ResultEqualsTo("o");

            // it holds nothing, so there is not
            Assert.IsNull(CompileGetter<ConcatRoot, object>("NullName + NullObject").GetValue(root));
            Assert.IsNull(InterpretGetter<ConcatRoot, object>("NullName + NullObject").GetValue(root));
        }

        /// <summary>
        /// Nothing combined with nothing is nothing, across every arithmetic operator. The interpreter
        /// used to fall off the end of each of these and report that the two could not be combined,
        /// which contradicted its own rule one branch earlier - a null beside a number already
        /// propagated.
        /// </summary>
        [Test]
        public void NothingCombinedWithNothingIsNothingInEveryOperator()
        {
            var root = new ConcatRoot();

            foreach (var expression in new[]
                {
                    "NoNumber + NoNumber", "NoNumber - NoNumber", "NoNumber * NoNumber",
                    "NoNumber / NoNumber", "NoNumber % NoNumber", "NoNumber ^ NoNumber"
                })
            {
                Assert.IsNull(CompileGetter<ConcatRoot, object>(expression).GetValue(root), expression);
                Assert.IsNull(InterpretGetter<ConcatRoot, object>(expression).GetValue(root), expression);
            }
        }

        /// <summary>
        /// One operand missing propagates as it always did, and two present operands are untouched.
        /// </summary>
        [Test]
        public void OneMissingOperandPropagatesAndTwoPresentOnesDoNot()
        {
            var root = new ConcatRoot();

            Assert.IsNull(CompileGetter<ConcatRoot, object>("NoNumber + Number").GetValue(root));
            Assert.IsNull(InterpretGetter<ConcatRoot, object>("NoNumber + Number").GetValue(root));

            TestCompiledVsInterpreted<ConcatRoot, object>("SomeNumber + Number", root).ResultEqualsTo(12);
            TestCompiledVsInterpreted<ConcatRoot, object>("SomeNumber - Number", root).ResultEqualsTo(2);
        }

        /// <summary>
        /// <c>DateTime + string</c> parses the string as a span, and a null span propagates rather than
        /// reaching <c>TimeSpan.Parse</c>, which answers a null with <c>ArgumentNullException</c>. The
        /// compiled result becomes <c>DateTime?</c> to carry the nothing, which boxes to a
        /// <c>DateTime</c> or to null and so cannot be told apart from the interpreter's answer.
        /// </summary>
        [Test]
        public void ADateTimePlusANullSpanPropagates()
        {
            var root = new ConcatRoot();

            Assert.IsNull(CompileGetter<ConcatRoot, object>("When + NullName").GetValue(root));
            Assert.IsNull(InterpretGetter<ConcatRoot, object>("When + NullName").GetValue(root));

            TestCompiledVsInterpreted<ConcatRoot, object>("When + '1.00:00:00'", root)
                .ResultEqualsTo(new DateTime(2020, 1, 2));
        }

        /// <summary>
        /// The two deviations from C#, recorded rather than hidden. Both are confined to nulls, and both
        /// are forced by the interpreter not being able to see what the compiled path sees.
        /// </summary>
        [Test]
        public void TheTwoDeviationsFromCSharpAreRecorded()
        {
            var root = new ConcatRoot();
            string nothing = null;

            // C# concatenates a null string with a number
            Assert.AreEqual("5", nothing + 5);
            Assert.IsNull(CompileGetter<ConcatRoot, object>("NullName + Number").GetValue(root));

            // and two null strings give the empty string
            Assert.AreEqual("", nothing + nothing);
            Assert.IsNull(CompileGetter<ConcatRoot, object>("NullName + NullName").GetValue(root));
        }
    }
}

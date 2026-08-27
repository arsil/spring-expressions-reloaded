using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// '+' with a string on one side concatenates, whichever side that is.
    /// </summary>
    /// <remarks>
    /// The compiled path calls <c>String.Concat(object, object)</c>, which wants <i>both</i> arguments
    /// as object. It used to box only the right operand, so a value type on the left was handed to an
    /// object parameter unboxed and the emitter threw <c>ArgumentException</c> - not a
    /// <c>CompileErrorException</c>, so the weakly typed path could not fall back either and the whole
    /// expression was a hard failure. <c>'Ana' + 45</c> worked and <c>45 + 'Ana'</c> did not.
    /// <p>
    /// It survived because every concatenation in both suites happens to put a string - or an
    /// object-typed <c>#variable</c>, which is a reference type and so needs no boxing - on the left.
    /// Nothing here is a numeric-promotion question: <c>BinaryNumericOperatorHelper.TryCreate</c> claims
    /// every number-meets-number pair before this branch is reached.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class StringConcatenationAgreementTests : BaseCompiledTests
    {
        public class Holder
        {
            public string Name { get; set; } = "Ana";
            public int Number { get; set; } = 45;
            public decimal Amount { get; set; } = 45.5m;
            public bool Flag { get; set; } = true;
            public char Letter { get; set; } = 'x';
            public int? NullableNumber { get; set; } = 7;
            public int? NoNumber { get; set; }
            public object Anything { get; set; } = 45;
        }

        [Test]
        public void AStringOnTheLeftConcatenates()
        {
            TestCompiledVsInterpreted<Holder, object>("Name + Number", new Holder())
                .ResultEqualsTo("Ana45");
        }

        [Test]
        public void AStringOnTheRightConcatenatesToo()
        {
            TestCompiledVsInterpreted<Holder, object>("Number + Name", new Holder())
                .ResultEqualsTo("45Ana");
        }

        /// <summary>
        /// Every value type on the left needs boxing, not only int - the guard was on the operand
        /// position, never on the type.
        /// </summary>
        [Test]
        public void EveryValueTypeConcatenatesFromEitherSide()
        {
            var holder = new Holder();

            TestCompiledVsInterpreted<Holder, object>("Amount + Name", holder);
            TestCompiledVsInterpreted<Holder, object>("Name + Amount", holder);

            TestCompiledVsInterpreted<Holder, object>("Flag + Name", holder);
            TestCompiledVsInterpreted<Holder, object>("Name + Flag", holder);

            TestCompiledVsInterpreted<Holder, object>("Letter + Name", holder);
            TestCompiledVsInterpreted<Holder, object>("Name + Letter", holder);

            TestCompiledVsInterpreted<Holder, object>("NullableNumber + Name", holder);
            TestCompiledVsInterpreted<Holder, object>("Name + NullableNumber", holder);
        }

        /// <summary>
        /// A nullable holding nothing concatenates as an empty string, both ways and on both backends.
        /// </summary>
        [Test]
        public void ANullableHoldingNothingConcatenatesAsNothing()
        {
            TestCompiledVsInterpreted<Holder, object>("NoNumber + Name", new Holder())
                .ResultEqualsTo("Ana");

            TestCompiledVsInterpreted<Holder, object>("Name + NoNumber", new Holder())
                .ResultEqualsTo("Ana");
        }

        /// <summary>
        /// An object-typed operand is a reference type, so it never needed boxing - this is the shape
        /// that always worked and hid the defect.
        /// </summary>
        [Test]
        public void AnObjectTypedOperandConcatenatesFromEitherSide()
        {
            var holder = new Holder();

            TestCompiledVsInterpreted<Holder, object>("Anything + Name", holder).ResultEqualsTo("45Ana");
            TestCompiledVsInterpreted<Holder, object>("Name + Anything", holder).ResultEqualsTo("Ana45");
        }

        /// <summary>
        /// Chained, the first concatenation makes the left operand a string, so only the leftmost link
        /// exercises the boxing at all.
        /// </summary>
        [Test]
        public void ConcatenationChains()
        {
            TestCompiledVsInterpreted<Holder, object>("Number + Name + Number", new Holder())
                .ResultEqualsTo("45Ana45");

            TestCompiledVsInterpreted<Holder, object>("Number + Number + Name", new Holder())
                .ResultEqualsTo("90Ana");
        }

        /// <summary>
        /// Two numbers are claimed by numeric promotion long before the concatenation branch, so adding
        /// them still adds them.
        /// </summary>
        [Test]
        public void TwoNumbersStillAdd()
        {
            TestCompiledVsInterpreted<Holder, object>("Number + Number", new Holder())
                .ResultEqualsTo(90);
        }
    }
}

using NUnit.Framework;

using System;
using System.Collections.Generic;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A caller's own count-like struct: numeric by its implicit conversion to int, and nothing else.
    /// </summary>
    public struct CountLike
    {
        private readonly int _value;

        public CountLike(int value) { _value = value; }

        public static implicit operator int(CountLike value) { return value._value; }

        public override string ToString() { return "CountLike(" + _value + ")"; }
    }

    /// <summary>
    /// Convertible to int and to double at once, so the rank has to choose between them.
    /// </summary>
    public struct WideLike
    {
        private readonly int _value;

        public WideLike(int value) { _value = value; }

        public static implicit operator int(WideLike value) { return value._value; }
        public static implicit operator double(WideLike value) { return value._value; }
    }

    /// <summary>
    /// Convertible to char alone.
    /// </summary>
    public struct LetterLike
    {
        private readonly char _value;

        public LetterLike(char value) { _value = value; }

        public static implicit operator char(LetterLike value) { return value._value; }
    }

    /// <summary>
    /// Convertible to int, and declaring an addition of its own. Both apply, so the ordering rule
    /// decides which one runs.
    /// </summary>
    public struct CountWithOwnPlus
    {
        private readonly int _value;

        public CountWithOwnPlus(int value) { _value = value; }

        public static implicit operator int(CountWithOwnPlus value) { return value._value; }

        public static CountWithOwnPlus operator +(CountWithOwnPlus left, int right)
        {
            return new CountWithOwnPlus(left._value + right);
        }

        public override string ToString() { return "CountWithOwnPlus(" + _value + ")"; }
    }

    public class CustomIntegralHolder
    {
        public CountLike Seven { get { return new CountLike(7); } }
        public CountLike Three { get { return new CountLike(3); } }
        public WideLike WideSeven { get { return new WideLike(7); } }
        public LetterLike Initial { get { return new LetterLike('A'); } }
        public CountWithOwnPlus OwnPlusSeven { get { return new CountWithOwnPlus(7); } }

        public char Letter { get { return 'A'; } }

        public int TakesInt(int value) { return value; }

        public List<CountLike> Counts { get; }
            = new List<CountLike> { new CountLike(4), new CountLike(1), new CountLike(2) };
    }

    /// <summary>
    /// A type with an implicit conversion to an integral type is a number, on both backends: the
    /// operand converts through its own operator and the ordinary promotion rules take over from
    /// there, exactly as a type converting to decimal has always done.
    /// </summary>
    /// <remarks>
    /// <b>The semantics come from the target, not from us.</b> Converting to <c>int</c> brings integer
    /// division and integer overflow with it, so <c>Seven / 2</c> is <c>3</c> - which is also C#'s
    /// answer for the same struct.
    /// <p>
    /// Every expression here refused on both backends before, so nothing in this fixture changes an
    /// answer anyone could have been getting; it only turns refusals into results.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class CustomIntegralTypesTests : BaseCompiledTests
    {
        [Test]
        public void ACustomIntegralAddsSubtractsAndMultiplies()
        {
            var holder = new CustomIntegralHolder();

            var sum = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Seven + 1", holder).Result;
            Assert.AreEqual(typeof(int), sum.GetType());
            Assert.AreEqual(8, sum);

            TestCompiledVsInterpreted<CustomIntegralHolder, object>("1 + Seven", holder)
                .ResultEqualsTo(8);
            TestCompiledVsInterpreted<CustomIntegralHolder, object>("Seven - Three", holder)
                .ResultEqualsTo(4);
            TestCompiledVsInterpreted<CustomIntegralHolder, object>("Seven * Three", holder)
                .ResultEqualsTo(21);
        }

        /// <summary>
        /// Division and modulus are where the choice of target is visible: an int divides as an int.
        /// C# answers 3 for the same struct, which is the point - the conversion is applied and then
        /// nothing special happens.
        /// </summary>
        [Test]
        public void ACustomIntegralDividesAsAnInteger()
        {
            var holder = new CustomIntegralHolder();

            var quotient = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Seven / 2", holder)
                .Result;
            Assert.AreEqual(typeof(int), quotient.GetType());
            Assert.AreEqual(3, quotient);

            TestCompiledVsInterpreted<CustomIntegralHolder, object>("Seven % 2", holder)
                .ResultEqualsTo(1);

            // And it widens like an int when the other operand is a real one.
            var widened = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Seven / 2.0", holder)
                .Result;
            Assert.AreEqual(typeof(double), widened.GetType());
            Assert.AreEqual(3.5d, widened);
        }

        /// <summary>
        /// Power is double-only by design - Math.Pow is all the BCL offers - so an integral-valued
        /// custom type converts through its operator and then to double, like any other operand.
        /// </summary>
        [Test]
        public void ACustomIntegralRaisesToAPowerAsADouble()
        {
            var holder = new CustomIntegralHolder();

            var result = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Seven ^ 2", holder)
                .Result;

            Assert.AreEqual(typeof(double), result.GetType());
            Assert.AreEqual(49d, result);
        }

        [Test]
        public void ACustomIntegralComparesAndTestsForEquality()
        {
            var holder = new CustomIntegralHolder();

            TestCompiledVsInterpreted<CustomIntegralHolder, bool>("Seven > 3", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomIntegralHolder, bool>("Three < Seven", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomIntegralHolder, bool>("Seven >= Seven", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomIntegralHolder, bool>("Seven == 7", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomIntegralHolder, bool>("Seven != 3", holder)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CustomIntegralHolder, bool>("Three between {1, 5}", holder)
                .ResultEqualsTo(true);
        }

        [Test]
        public void ACustomIntegralNegatesAndTakesAUnaryPlus()
        {
            var holder = new CustomIntegralHolder();

            var negated = TestCompiledVsInterpreted<CustomIntegralHolder, object>("-Seven", holder)
                .Result;
            Assert.AreEqual(typeof(int), negated.GetType());
            Assert.AreEqual(-7, negated);

            TestCompiledVsInterpreted<CustomIntegralHolder, object>("+Seven", holder)
                .ResultEqualsTo(7);
        }

        /// <summary>
        /// The aggregators follow, and <c>sum()</c> folds with <c>+</c>: an int-converting collection
        /// sums to an <c>Int32</c> where a decimal-converting one sums to a <c>Decimal</c>. The
        /// average is a quotient, so it accumulates and answers in double, as it does for ints.
        /// </summary>
        [Test]
        public void AggregatorsWorkOverACollectionOfCustomIntegrals()
        {
            var holder = new CustomIntegralHolder();

            var total = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Counts.sum()", holder)
                .Result;
            Assert.AreEqual(typeof(int), total.GetType());
            Assert.AreEqual(7, total);

            var mean = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Counts.average()", holder)
                .Result;
            Assert.AreEqual(typeof(double), mean.GetType());
            Assert.AreEqual(7d / 3d, mean);

            // min() and max() hand back the winning item itself, so the caller keeps their own type.
            var smallest = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Counts.min()", holder)
                .Result;
            Assert.AreEqual(typeof(CountLike), smallest.GetType());
            Assert.AreEqual("CountLike(1)", smallest.ToString());

            var largest = TestCompiledVsInterpreted<CustomIntegralHolder, object>("Counts.max()", holder)
                .Result;
            Assert.AreEqual(typeof(CountLike), largest.GetType());
            Assert.AreEqual("CountLike(4)", largest.ToString());
        }

        /// <summary>
        /// Sorting asks the same question arithmetic does, which is why the ordering predicate reads
        /// the numeric lookup and not the real-only one: a type that can be added can be ordered.
        /// </summary>
        [Test]
        public void ACollectionOfCustomIntegralsSorts()
        {
            var holder = new CustomIntegralHolder();

            var sorted = (System.Collections.IList)Expression
                .ParseGetter<CustomIntegralHolder, object>("Counts.sort()", EvaluationMode.MustCompile)
                .GetValue(holder);

            Assert.AreEqual("CountLike(1)", sorted[0].ToString());
            Assert.AreEqual("CountLike(2)", sorted[1].ToString());
            Assert.AreEqual("CountLike(4)", sorted[2].ToString());

            var interpreted = (System.Collections.IList)Expression
                .ParseGetter<CustomIntegralHolder, object>("Counts.sort()", EvaluationMode.MustInterpret)
                .GetValue(holder);

            Assert.AreEqual("CountLike(1)", interpreted[0].ToString());
            Assert.AreEqual("CountLike(2)", interpreted[1].ToString());
            Assert.AreEqual("CountLike(4)", interpreted[2].ToString());
        }

        /// <summary>
        /// The type's own operator is found before any conversion is considered - C#'s order, and this
        /// engine's standing rule. Without it a type that both converts to a number and declares
        /// <c>operator +</c> would erase itself, answering an <c>int</c> instead of its own type.
        /// </summary>
        [Test]
        public void ATypesOwnOperatorStillWinsOverItsConversion()
        {
            var holder = new CustomIntegralHolder();

            var result = TestCompiledVsInterpreted<CustomIntegralHolder, object>("OwnPlusSeven + 1", holder)
                .Result;

            Assert.AreEqual(typeof(CountWithOwnPlus), result.GetType());
            Assert.AreEqual("CountWithOwnPlus(8)", result.ToString());
        }

        /// <summary>
        /// A type offering both an integral and a real target normalizes to the real, so this divides
        /// as a double.
        /// </summary>
        /// <remarks>
        /// <b>A deliberate deviation from C#, recorded rather than fixed.</b> C# answers <c>3</c> here:
        /// it builds a candidate list and lets the pair of operands pick <c>int/int</c>, where this
        /// engine normalizes each operand on its own before the other is consulted. Matching C# would
        /// mean porting its betterness rules over user-defined conversions into both backends - the
        /// chapter of the specification <c>UserDefinedOperatorUtils</c> declines - and would change an
        /// answer that works today. Ranking the reals highest is also what keeps the integral targets
        /// purely additive.
        /// </remarks>
        [Test]
        public void ARealTargetOutranksAnIntegralOne()
        {
            var holder = new CustomIntegralHolder();

            var quotient = TestCompiledVsInterpreted<CustomIntegralHolder, object>("WideSeven / 2", holder)
                .Result;

            Assert.AreEqual(typeof(double), quotient.GetType());
            Assert.AreEqual(3.5d, quotient);
        }

        /// <summary>
        /// A char is not a number in this language, so a type whose only conversion reaches one is not
        /// a number either. The two are asserted together: whichever way that question is ever ruled,
        /// they have to move as a pair.
        /// </summary>
        [Test]
        public void AConversionToCharIsNotAConversionToANumber()
        {
            var holder = new CustomIntegralHolder();

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<CustomIntegralHolder, object>(
                    "Initial + 1", EvaluationMode.MustCompile));

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<CustomIntegralHolder, object>(
                    "Initial + 1", EvaluationMode.MustInterpret).GetValue(holder));

            // The built-in char it converts to behaves identically - that is the whole reason.
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<CustomIntegralHolder, object>(
                    "Letter + 1", EvaluationMode.MustCompile));

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<CustomIntegralHolder, object>(
                    "Letter + 1", EvaluationMode.MustInterpret).GetValue(holder));
        }

        /// <summary>
        /// Being a number must not make a type <i>real</i>. The real-only predicate exists to answer
        /// one question - would converting into an integral parameter round in the interpreter and
        /// truncate compiled? - and a type converting to <c>int</c> loses nothing on that trip. Ask
        /// the wrong predicate and this ordinary call stops compiling.
        /// </summary>
        [Test]
        public void PassingACustomIntegralToAnIntParameterStillCompiles()
        {
            var holder = new CustomIntegralHolder();

            var result = TestCompiledVsInterpreted<CustomIntegralHolder, object>("TakesInt(Seven)", holder)
                .Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(7, result);
        }
    }
}

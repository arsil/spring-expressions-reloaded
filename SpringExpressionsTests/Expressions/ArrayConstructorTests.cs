using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// `new T[] {…}` builds an array of <b>T</b>, whatever is between the braces.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The compiled path used to emit the initializer as a whole and call <c>ToArray()</c> on the
    /// <c>List&lt;T&gt;</c> it built, so the result type was whatever the *items* unified to and the
    /// declared element type was never read: <c>new long[] {1,2}</c> produced an <c>int[]</c>, and
    /// <c>new string[] {1}</c> produced an <c>int[]</c> as well - a silently wrong type, where the
    /// interpreter threw. It builds the array item by item now.
    /// </p>
    /// <p>
    /// The conversions allowed are C#'s for an array initializer, taken from the table this engine
    /// already rules on - <c>TypeCheckingUtils.IsCSharpImplicitNumericConversion</c>, the same one the
    /// overload-resolution tier uses. Array initializers gain no rule of their own.
    /// </p>
    /// <p>
    /// The interpreter widening is a <b>deliberate change to inherited behaviour</b>: it block-copied
    /// with <c>Array.Copy</c>, which unboxes each element and demands an exact type match, so
    /// <c>new long[] {1,2}</c> threw <c>InvalidCastException</c> over boxed ints where C# widens them.
    /// Both backends widen alike now.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class ArrayConstructorTests : BaseCompiledTests
    {
        public class Root
        {
            public int Number { get; set; } = 7;
            public long Big { get; set; } = 7L;
            public string Text { get; set; } = "a";
        }

        [Test]
        public void AnExactItemTypeBuildsThatArray()
        {
            AssertBuilds<int[]>("new int[] {1, 2}");
            AssertBuilds<long[]>("new long[] {1L, 2L}");
            AssertBuilds<string[]>("new string[] {'a', 'b'}");
        }

        /// <summary>
        /// The rows the fix is named for: the declared type wins, and the items widen to reach it.
        /// </summary>
        [Test]
        public void ItemsWidenToTheDeclaredElementType()
        {
            AssertBuilds<long[]>("new long[] {1, 2}");
            AssertBuilds<double[]>("new double[] {1, 2}");
            AssertBuilds<decimal[]>("new decimal[] {1, 2}");
            AssertBuilds<long[]>("new long[] {Number}");

            var widened = (long[])Expression.Parse("new long[] {1, 2}").GetValue<Root>(new Root());
            CollectionAssert.AreEqual(new[] { 1L, 2L }, widened);
        }

        /// <summary>
        /// A boxing conversion is a conversion too, and this row also closes a divergence: a single
        /// item used to make the compiled path answer <c>Int32[]</c> where the interpreter answered
        /// <c>Object[]</c>. Two mixed items happened to agree, by unifying to object.
        /// </summary>
        [Test]
        public void ItemsBoxToAnObjectArray()
        {
            AssertBuilds<object[]>("new object[] {1}");
            AssertBuilds<object[]>("new object[] {1, 'a'}");
        }

        /// <summary>
        /// What C# refuses, both backends refuse - where the compiled path used to invent an array of
        /// the wrong type and the interpreter threw.
        /// </summary>
        [Test]
        public void AnItemThatDoesNotFitIsRefusedOnBothBackends()
        {
            AssertRefused("new int[] {1L, 2L}", "narrowing");
            AssertRefused("new int[] {1.5}", "no implicit conversion from double to int");
            AssertRefused("new string[] {1}", "not a conversion at all");
            AssertRefused("new int[] {'a'}", "a string is not an int");
        }

        [Test]
        public void ANullItemIsAllowedOnlyWhereANullCanLive()
        {
            AssertBuilds<string[]>("new string[] {null, 'a'}");
            AssertBuilds<object[]>("new object[] {null}");

            AssertRefused("new int[] {null}", "a null cannot be stored in an int array");
        }

        /// <summary>
        /// The sized form was always right, because with no initializer there was nothing to unify and
        /// the declared type had to be used. Kept so the fix is visibly about the initializer form.
        /// </summary>
        [Test]
        public void TheSizedFormIsUnaffected()
        {
            AssertBuilds<long[]>("new long[2]");
            AssertBuilds<int[]>("new int[0]");

            var sized = (long[])Expression.Parse("new long[2]").GetValue<Root>(new Root());
            Assert.AreEqual(2, sized.Length);
        }

        /// <summary>
        /// Both backends build the same runtime type, which is the invariant that was broken.
        /// </summary>
        private static void AssertBuilds<TExpected>(string expression)
        {
            var root = new Root();

            var compiled = Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustCompile)
                .GetValue(root);
            var interpreted = Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustInterpret)
                .GetValue(root);

            Assert.AreEqual(typeof(TExpected), compiled.GetType(), expression + " compiled");
            Assert.AreEqual(typeof(TExpected), interpreted.GetType(), expression + " interpreted");
        }

        private static void AssertRefused(string expression, string why)
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustCompile),
                expression + " - " + why);

            Assert.Throws<InvalidCastException>(
                () => Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustInterpret)
                    .GetValue(new Root()),
                expression + " - " + why + " (interpreted)");
        }
    }
}

using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class ThreeKindsOfNothing
    {
        public int? EmptyNullable { get; set; }
        public int? FullNullable { get; set; } = 7;
        public string NullString { get; set; }
        public string Text { get; set; } = "b";
        public int Plain { get; set; } = 7;

        public double Nan { get { return double.NaN; } }
        public double One { get { return 1.0; } }

        public List<object> WithNulls { get { return new List<object> { 3, null, 1 }; } }
    }

    /// <summary>
    /// Nothing sorts before everything, and this engine has three ways to hold nothing - a null literal,
    /// a null reference, and a nullable value type with no value. All three order identically, on both
    /// backends.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Two of the three already did. The compiled path treated a <b>nullable</b> the way C# does -
    /// every comparison against it false - while the interpreter sorted it first like the other two, so
    /// <c>EmptyNullable &lt; Plain</c> was <c>False</c> compiled and <c>True</c> interpreted. That was
    /// 116 rows of <c>EvaluationNeverDivergesTests</c> and the whole of open-issues item 17.
    /// </p>
    /// <p>
    /// <b>This deviates from C# deliberately</b>, and the frozen suite is why: upstream pins
    /// <c>null &lt; 'xyz'</c> as <c>True</c> under its own "// Null" heading, so the sorting answer is
    /// inherited behaviour for a null literal. Following C# for nullables would have left one kind of
    /// nothing ordered differently from the other two - which is exactly the state this fixed.
    /// </p>
    /// <p>
    /// <b>Null and NaN are deliberately different, and that is the subtle part.</b> .NET keeps a sorting
    /// rule and an operator rule for both, and this engine took the operator rule for NaN
    /// (<c>NaN &lt; 1</c> is false) and the sorting rule for null. The two are told apart by the frozen
    /// suite: it pins the sorting answer for null and says nothing about NaN, so for null the sorting
    /// answer is inherited and for NaN it never was. Asserted side by side below so the distinction
    /// cannot be "tidied up" by someone who notices only one half.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class NullOrderingTests : BaseCompiledTests
    {
        [Test]
        public void AllThreeKindsOfNothingSortBeforeAValue()
        {
            var root = new ThreeKindsOfNothing();

            // a nullable holding nothing - the kind that used to differ
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable < Plain", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable <= Plain", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable > Plain", root)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable >= Plain", root)
                .ResultEqualsTo(false);

            // a null reference
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("NullString < Text", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("NullString > Text", root)
                .ResultEqualsTo(false);

            // a null literal
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("null < Plain", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("null > Plain", root)
                .ResultEqualsTo(false);
        }

        /// <summary>
        /// The mirror image: a value sorts above nothing. Which side holds the nothing decides, so the
        /// rule needs three outcomes rather than two - and that is why the compiled helper takes the
        /// sort order (-1, +1, 0) rather than a pair of booleans.
        /// </summary>
        [Test]
        public void AValueSortsAboveNothing()
        {
            var root = new ThreeKindsOfNothing();

            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Plain > EmptyNullable", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Plain >= EmptyNullable", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Plain < EmptyNullable", root)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Plain <= EmptyNullable", root)
                .ResultEqualsTo(false);

            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Text > NullString", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Plain > null", root)
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// Two nothings sort equal, so <c>&lt;=</c> and <c>&gt;=</c> hold and the strict ones do not.
        /// Nobody had to state this: it falls out of the sort order being zero.
        /// </summary>
        [Test]
        public void TwoNothingsSortEqual()
        {
            var root = new ThreeKindsOfNothing();

            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable <= EmptyNullable", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable >= EmptyNullable", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable < EmptyNullable", root)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable > EmptyNullable", root)
                .ResultEqualsTo(false);
        }

        /// <summary>
        /// Comparisons between two values are untouched - the rule is about nothing, not about nullables.
        /// </summary>
        [Test]
        public void TwoValuesCompareAsTheyAlwaysDid()
        {
            var root = new ThreeKindsOfNothing();

            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("FullNullable < Plain", root)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("FullNullable <= Plain", root)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("FullNullable >= Plain", root)
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// NaN is not nothing, and it keeps the opposite rule: every ordering comparison involving it is
        /// false, which is .NET's *operator* answer where null gets .NET's *sorting* answer.
        /// Do not reconcile these two - the difference is deliberate and the frozen suite is what
        /// decides it, pinning the sorting answer for null and saying nothing about NaN.
        /// </summary>
        [Test]
        public void NaNKeepsTheOppositeRuleAndThatIsDeliberate()
        {
            var root = new ThreeKindsOfNothing();

            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Nan < One", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Nan > One", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("Nan <= Nan", root).ResultEqualsTo(false);

            // nothing, by contrast, is ordered
            TestCompiledVsInterpreted<ThreeKindsOfNothing, object>("EmptyNullable < One", root)
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// The sorting operations are untouched and still place nulls first, matching
        /// <c>Enumerable.OrderBy</c>. That half was already right; this ruling only brought the
        /// operators into line with it for a nullable.
        /// </summary>
        [Test]
        public void SortStillPlacesNullsFirst()
        {
            var root = new ThreeKindsOfNothing();

            var sorted = (List<object>)CompileGetter<ThreeKindsOfNothing, object>("WithNulls.sort()")
                .GetValue(root);
            Assert.AreEqual(new List<object> { null, 1, 3 }, sorted);

            var interpreted = (List<object>)InterpretGetter<ThreeKindsOfNothing, object>("WithNulls.sort()")
                .GetValue(root);
            Assert.AreEqual(new List<object> { null, 1, 3 }, interpreted);
        }

        /// <summary>
        /// Arithmetic keeps its own theory: nothing in, nothing out. Ordering treats nothing as a value
        /// and arithmetic treats it as absence, which is not an inconsistency - C# does the same, and so
        /// does SQL for its own reasons. Asserted here so the two theories are visible together.
        /// </summary>
        [Test]
        public void ArithmeticStillPropagatesNothingRatherThanOrderingIt()
        {
            var root = new ThreeKindsOfNothing();

            Assert.IsNull(CompileGetter<ThreeKindsOfNothing, object>("EmptyNullable + Plain").GetValue(root));
            Assert.IsNull(InterpretGetter<ThreeKindsOfNothing, object>("EmptyNullable + Plain").GetValue(root));

            Assert.IsNull(CompileGetter<ThreeKindsOfNothing, object>("EmptyNullable - Plain").GetValue(root));
            Assert.IsNull(InterpretGetter<ThreeKindsOfNothing, object>("EmptyNullable - Plain").GetValue(root));
        }
    }
}

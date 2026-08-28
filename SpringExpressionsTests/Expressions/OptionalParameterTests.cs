using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public enum OptionalColour { Red, Green }

    public class OptionalParameterCases
    {
        public string Label { get { return "L"; } }

        public string One(int a, int b = 7) { return "one:" + a + "," + b; }
        public string Three(int a, int b = 2, int c = 3) { return "three:" + a + "," + b + "," + c; }
        public string Ref(string a, string b = "x") { return "ref:" + a + "," + (b ?? "null"); }
        public string NullDefault(string a = null) { return "nulldefault:" + (a ?? "null"); }
        public string Colour(OptionalColour c = OptionalColour.Green) { return "colour:" + c; }
        public string Reference(string a, int b = 3) { return "reference:" + a + "," + b; }

        public string Dec(decimal a = 1.5m)
        {
            return "dec:" + a.ToString(CultureInfo.InvariantCulture);
        }

        public string Dt(DateTime d = default(DateTime))
        {
            return "dt:" + d.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        public string NInt(int? a = 5)
        {
            return "nint:" + (a.HasValue ? a.Value.ToString(CultureInfo.InvariantCulture) : "null");
        }

        // [Optional] with no declared value at all: DefaultValue reports Missing.Value, and C#
        // substitutes default(T) there by rules of its own for COM interop.
        public string Marked([Optional] int a) { return "marked:" + a; }

        public string Mix(int a, int b = 2, params int[] xs) { return "mix:" + a + "," + b + ":" + xs.Length; }

        public string Pick(int a) { return "pick:one"; }
        public string Pick(int a, int b = 7) { return "pick:two"; }

        public string Tie(int a, int b = 1) { return "tie:int"; }
        public string Tie(int a, string b = "x") { return "tie:string"; }

        public static string StaticOne(int a, int b = 7) { return "static:" + a + "," + b; }
    }

    public class OptionalParameterConstructorCases
    {
        public string Tag;

        public OptionalParameterConstructorCases(int a, int b = 7) { Tag = "ctor:" + a + "," + b; }
    }

    /// <summary>
    /// A parameter with a declared default may be left out of the call, and the default is supplied
    /// by the caller side on both backends. This worked on neither before: the interpreter resolved
    /// the method by name and then handed the invoker an argument list one short ("Invalid number of
    /// arguments passed into method"), and the compiled path did not admit the method as a candidate
    /// at all.
    /// </summary>
    /// <remarks>
    /// The metadata is less uniform than it looks and every default kind below was measured, not
    /// assumed - see ArgumentBindingUtils.TryGetOmittedArgumentValue. In particular a decimal default
    /// does not set ParameterAttributes.HasDefault, and a DateTime default sets it while reporting a
    /// null value that means default(T) rather than a null reference.
    /// </remarks>
    [TestFixture]
    public class OptionalParameterTests : BaseCompiledTests
    {
        [Test]
        public void AnOmittedTrailingParameterTakesItsDeclaredDefault()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "One(1)", new OptionalParameterCases())
                .ResultEqualsTo("one:1,7");
        }

        [Test]
        public void SupplyingEveryArgumentIsUnaffected()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "One(1, 2)", new OptionalParameterCases())
                .ResultEqualsTo("one:1,2");
        }

        /// <summary>
        /// Only trailing parameters that declare a default may be left out; a required one may not.
        /// </summary>
        [Test]
        public void TooFewArgumentsForTheRequiredParametersDoNotBind()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<OptionalParameterCases, string>(
                    "One()", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<OptionalParameterCases, string>(
                "One()", EvaluationMode.MustInterpret);

            Assert.Throws<ArgumentException>(() => interpreted.GetValue(new OptionalParameterCases()));
        }

        [Test]
        public void SeveralTrailingParametersAreFilledFromTheRight()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Three(1)", new OptionalParameterCases())
                .ResultEqualsTo("three:1,2,3");

            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Three(1, 5)", new OptionalParameterCases())
                .ResultEqualsTo("three:1,5,3");

            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Three(1, 5, 6)", new OptionalParameterCases())
                .ResultEqualsTo("three:1,5,6");
        }

        [Test]
        public void AReferenceTypedDefaultIsSupplied()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Ref('a')", new OptionalParameterCases())
                .ResultEqualsTo("ref:a,x");
        }

        [Test]
        public void ANullDefaultIsSupplied()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "NullDefault()", new OptionalParameterCases())
                .ResultEqualsTo("nulldefault:null");
        }

        /// <summary>
        /// A decimal default lives in a DecimalConstantAttribute rather than a metadata constant, so
        /// the parameter carries Optional without HasDefault. Reading HasDefault - the obvious test -
        /// would refuse this one.
        /// </summary>
        [Test]
        public void ADecimalDefaultIsSupplied()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Dec()", new OptionalParameterCases())
                .ResultEqualsTo("dec:1.5");
        }

        /// <summary>
        /// 'DateTime d = default(DateTime)' does carry HasDefault, and its DefaultValue is null -
        /// meaning default(T), not a null reference. Passing that null straight through would be an
        /// error rather than a value.
        /// </summary>
        [Test]
        public void ADefaultOfAValueTypeWithNoConstantIsSupplied()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Dt()", new OptionalParameterCases())
                .ResultEqualsTo("dt:0");
        }

        [Test]
        public void AnEnumDefaultIsSupplied()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Colour()", new OptionalParameterCases())
                .ResultEqualsTo("colour:Green");
        }

        /// <summary>
        /// The declared value of a Nullable&lt;T&gt; parameter arrives as a bare T.
        /// </summary>
        [Test]
        public void ANullableDefaultIsSupplied()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "NInt()", new OptionalParameterCases())
                .ResultEqualsTo("nint:5");
        }

        /// <summary>
        /// [Optional] with no declared value is refused on both backends. C# substitutes default(T)
        /// there by COM-interop rules of its own, and this engine has no reason to guess at them; the
        /// parameter reports Missing.Value, which is not a default it can supply.
        /// </summary>
        [Test]
        public void AnOptionalAttributeWithNoDeclaredValueIsNotOmittable()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<OptionalParameterCases, string>(
                    "Marked()", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<OptionalParameterCases, string>(
                "Marked()", EvaluationMode.MustInterpret);

            Assert.Throws<ArgumentException>(() => interpreted.GetValue(new OptionalParameterCases()));
        }

        /// <summary>
        /// Optional parameters and a trailing params array fill together, and positionally: the
        /// arguments cover the fixed parameters from the left, the leftover ones become the array,
        /// and any gap in between takes its default.
        /// </summary>
        [Test]
        public void OptionalParametersAndAParamsArrayFillTogether()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Mix(1)", new OptionalParameterCases())
                .ResultEqualsTo("mix:1,2:0");

            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Mix(1, 5)", new OptionalParameterCases())
                .ResultEqualsTo("mix:1,5:0");

            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Mix(1, 5, 9, 9)", new OptionalParameterCases())
                .ResultEqualsTo("mix:1,5:2");
        }

        /// <summary>
        /// An overload that takes the arguments as written beats one that has to fill a default, so
        /// admitting the second as a candidate cannot change a pick that already resolved. This call
        /// answered 'pick:one' before optional parameters were understood at all, and still does.
        /// </summary>
        [Test]
        public void AnOverloadThatOmitsNothingWins()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Pick(1)", new OptionalParameterCases())
                .ResultEqualsTo("pick:one");

            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Pick(1, 2)", new OptionalParameterCases())
                .ResultEqualsTo("pick:two");
        }

        /// <summary>
        /// Two overloads that both fill a default and declare the same number of parameters: C# has
        /// nothing left to prefer either by, and neither does this engine. The compiled path refuses
        /// it and the interpreter reports the ambiguity at evaluation, which is where that resolver
        /// has always reported ties.
        /// </summary>
        [Test]
        public void TwoOverloadsThatBothFillADefaultAreAmbiguous()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<OptionalParameterCases, string>(
                    "Tie(1)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<OptionalParameterCases, string>(
                "Tie(1)", EvaluationMode.MustInterpret);

            Assert.Throws<AmbiguousMatchException>(
                () => interpreted.GetValue(new OptionalParameterCases()));
        }

        /// <summary>
        /// A reference-typed argument alongside an omitted optional keeps its compiled form. The
        /// overload gate refuses a shape whose candidates differ in arity, so this only holds while
        /// there is nothing else of that name to choose from - which is the ordinary case.
        /// </summary>
        [Test]
        public void AReferenceTypedArgumentBesideAnOmittedOptionalStillCompiles()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Reference(Label)", new OptionalParameterCases())
                .ResultEqualsTo("reference:L,3");

            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "Reference(Label, 9)", new OptionalParameterCases())
                .ResultEqualsTo("reference:L,9");
        }

        [Test]
        public void AStaticMethodFillsDefaultsToo()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "T(SpringExpressionsTests.Expressions.OptionalParameterCases, SpringExpressionsTests).StaticOne(1)",
                new OptionalParameterCases())
                .ResultEqualsTo("static:1,7");
        }

        [Test]
        public void AConstructorFillsDefaultsToo()
        {
            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "new SpringExpressionsTests.Expressions.OptionalParameterConstructorCases(1).Tag",
                new OptionalParameterCases())
                .ResultEqualsTo("ctor:1,7");

            TestCompiledVsInterpreted<OptionalParameterCases, string>(
                "new SpringExpressionsTests.Expressions.OptionalParameterConstructorCases(1, 2).Tag",
                new OptionalParameterCases())
                .ResultEqualsTo("ctor:1,2");
        }
    }
}

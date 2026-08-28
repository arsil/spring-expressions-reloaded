using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class ParamArrayCases
    {
        public int[] Numbers { get { return new[] { 7, 8 }; } }
        public string Label { get { return "label"; } }
        public string NoLabel { get { return null; } }

        public string Ints(params int[] xs)
        {
            if (xs == null)
                return "int[]:null";

            var text = "int[" + xs.Length + "]:";
            for (var i = 0; i < xs.Length; i++)
                text += (i > 0 ? "," : "") + xs[i];
            return text;
        }

        public string Longs(params long[] xs)
        {
            var text = "long[" + xs.Length + "]:";
            for (var i = 0; i < xs.Length; i++)
                text += (i > 0 ? "," : "") + xs[i];
            return text;
        }

        public string Decimals(params decimal[] xs)
        {
            var text = "decimal[" + xs.Length + "]:";
            for (var i = 0; i < xs.Length; i++)
                text += (i > 0 ? "," : "") + xs[i];
            return text;
        }

        public string Shorts(params short[] xs)
        {
            return "short[" + xs.Length + "]";
        }

        public string NullableInts(params int?[] xs)
        {
            var text = "int?[" + xs.Length + "]:";
            for (var i = 0; i < xs.Length; i++)
                text += (i > 0 ? "," : "") + (xs[i].HasValue ? xs[i].Value.ToString() : "null");
            return text;
        }

        public string Objects(params object[] xs)
        {
            if (xs == null)
                return "object[]:null";

            var text = "object[" + xs.Length + "]:";
            for (var i = 0; i < xs.Length; i++)
                text += (i > 0 ? "," : "") + (xs[i] == null ? "null" : xs[i].ToString());
            return text;
        }

        public string Tagged(string tag, params int[] xs)
        {
            return "tagged:" + tag + ":" + xs.Length;
        }

        public string Pick(int a) { return "pick:int:" + a; }
        public string Pick(params int[] xs) { return "pick:params:" + xs.Length; }

        // A long parameter does not accept an int argument by assignability, so the only candidate
        // that binds Only(1) is the expanded params one - the shape that proves an expanded match is
        // reachable when a same-arity normal-form candidate exists but does not apply.
        public string Only(long a) { return "only:long:" + a; }
        public string Only(params int[] xs) { return "only:params:" + xs.Length; }

        public static string StaticInts(params int[] xs) { return "static:" + xs.Length; }
    }

    public class ParamArrayConstructorCases
    {
        public string Tag;

        public ParamArrayConstructorCases(params int[] xs)
        {
            Tag = xs == null ? "ctor:null" : "ctor:" + xs.Length;
        }
    }

    /// <summary>
    /// A <c>params</c> array is an array construction with the brackets left out, so it binds by C#'s
    /// two forms - the normal one first, the expanded one after - and its elements convert by the rule
    /// <c>new T[] {...}</c> already uses. Both backends run that rule, which is what these pins are
    /// for: every row asserts the compiled and interpreted answers agree, or that the compiled path
    /// refuses and the interpreter serves the call.
    /// </summary>
    /// <remarks>
    /// Before this, only the several-candidates path could emit a params call at all: a single
    /// candidate was handed the arguments untouched and refused one step later on the argument count.
    /// The interpreter expanded unconditionally, which broke three shapes outright - an actual array
    /// handed to a params parameter was packed inside a second one, a call with too few arguments ran
    /// off the end of the argument list, and elements converted by the CLR's primitive widening rather
    /// than C#'s, so a decimal element type rejected int arguments.
    /// </remarks>
    [TestFixture]
    public class ParamArrayTests : BaseCompiledTests
    {
        [Test]
        public void AnEmptyExpansionBuildsAnEmptyArray()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Ints()", new ParamArrayCases())
                .ResultEqualsTo("int[0]:");
        }

        [Test]
        public void OneArgumentIsBuiltIntoTheArray()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Ints(1)", new ParamArrayCases())
                .ResultEqualsTo("int[1]:1");
        }

        [Test]
        public void SeveralArgumentsAreBuiltIntoTheArray()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Ints(1, 2, 3)", new ParamArrayCases())
                .ResultEqualsTo("int[3]:1,2,3");
        }

        [Test]
        public void ParametersBeforeTheParamsArrayKeepTheirOwnArguments()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Tagged('a')", new ParamArrayCases())
                .ResultEqualsTo("tagged:a:0");

            TestCompiledVsInterpreted<ParamArrayCases, string>("Tagged('a', 1, 2)", new ParamArrayCases())
                .ResultEqualsTo("tagged:a:2");
        }

        /// <summary>
        /// The normal form: one argument per parameter, the last already an array the parameter
        /// accepts, so nothing is packed. The interpreter used to expand unconditionally and try to
        /// store the caller's array inside a fresh one-element array of ints - InvalidCastException,
        /// on an expression the compiled path was answering correctly.
        /// </summary>
        [Test]
        public void AnArrayIsHandedThroughWhole()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Ints(Numbers)", new ParamArrayCases())
                .ResultEqualsTo("int[2]:7,8");
        }

        [Test]
        public void AnArrayLiteralIsHandedThroughWhole()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "Ints(new int[] {1, 2})", new ParamArrayCases())
                .ResultEqualsTo("int[2]:1,2");
        }

        [Test]
        public void ElementsWidenToTheDeclaredElementType()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Longs(1, 2)", new ParamArrayCases())
                .ResultEqualsTo("long[2]:1,2");
        }

        /// <summary>
        /// int to decimal is one of C#'s implicit numeric conversions and is in the table
        /// 'new T[] {...}' uses, but not in the CLR's primitive widening that Array.SetValue applies -
        /// so this threw InvalidCastException interpreted while the compiled path refused it.
        /// </summary>
        [Test]
        public void ElementsWidenToDecimalToo()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Decimals(1, 2)", new ParamArrayCases())
                .ResultEqualsTo("decimal[2]:1,2");
        }

        /// <summary>
        /// int to short is a narrowing conversion, which C# refuses in an array initializer and this
        /// engine refuses here for the same reason. Both backends say no.
        /// </summary>
        [Test]
        public void ANarrowingElementDoesNotBind()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ParamArrayCases, string>(
                    "Shorts(1, 2)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<ParamArrayCases, string>(
                "Shorts(1, 2)", EvaluationMode.MustInterpret);

            Assert.Throws<ArgumentException>(() => interpreted.GetValue(new ParamArrayCases()));
        }

        [Test]
        public void ANullableElementTypeTakesValuesAndNulls()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "NullableInts(1, null)", new ParamArrayCases())
                .ResultEqualsTo("int?[2]:1,null");
        }

        [Test]
        public void AnObjectElementTypeTakesMixedItemsAndNull()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "Objects(1, 'a', null)", new ParamArrayCases())
                .ResultEqualsTo("object[3]:1,a,null");
        }

        /// <summary>
        /// A bare null in the params slot is the array itself, not an element of it - C#'s reading,
        /// and the one the interpreter arrives at from the value.
        /// </summary>
        [Test]
        public void ANullArgumentIsTheArrayItselfNotAnElement()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Ints(null)", new ParamArrayCases())
                .ResultEqualsTo("int[]:null");
        }

        /// <summary>
        /// A candidate applicable in normal form beats one that had to expand, whatever the
        /// conversions involved. Without that rule the pick depended on the order the two overloads
        /// came out of reflection - and the compiled candidate list, which is built through a
        /// dictionary, does not enumerate in the interpreter's order.
        /// </summary>
        [Test]
        public void AnApplicableNormalFormBeatsAnExpandedOne()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Pick(1)", new ParamArrayCases())
                .ResultEqualsTo("pick:int:1");

            TestCompiledVsInterpreted<ParamArrayCases, string>("Pick(1, 2)", new ParamArrayCases())
                .ResultEqualsTo("pick:params:2");
        }

        /// <summary>
        /// The empty expansion used to resolve only when the method name was unambiguous enough to
        /// skip the candidate scan: the scan demanded at least as many arguments as parameters, so
        /// this same call answered for a method with one overload and reported "does not exist" for
        /// one with two.
        /// </summary>
        [Test]
        public void AnEmptyExpansionResolvesEvenWhenTheNameIsOverloaded()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Pick()", new ParamArrayCases())
                .ResultEqualsTo("pick:params:0");
        }

        [Test]
        public void AnExpandedMatchIsTakenWhenTheSameArityCandidateDoesNotApply()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>("Only(1)", new ParamArrayCases())
                .ResultEqualsTo("only:params:1");
        }

        /// <summary>
        /// Too few arguments to fill the parameters that come before the array. The interpreter used
        /// to walk off the end of the argument list here with IndexOutOfRangeException.
        /// </summary>
        [Test]
        public void TooFewArgumentsForTheFixedParametersDoNotBind()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ParamArrayCases, string>(
                    "Tagged()", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<ParamArrayCases, string>(
                "Tagged()", EvaluationMode.MustInterpret);

            Assert.Throws<ArgumentException>(() => interpreted.GetValue(new ParamArrayCases()));
        }

        [Test]
        public void AnElementTheArrayCannotHoldDoesNotBind()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ParamArrayCases, string>(
                    "Ints('a')", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<ParamArrayCases, string>(
                "Ints('a')", EvaluationMode.MustInterpret);

            Assert.Throws<ArgumentException>(() => interpreted.GetValue(new ParamArrayCases()));
        }

        [Test]
        public void AStaticMethodExpandsToo()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "T(SpringExpressionsTests.Expressions.ParamArrayCases, SpringExpressionsTests).StaticInts(1, 2)",
                new ParamArrayCases())
                .ResultEqualsTo("static:2");
        }

        [Test]
        public void AConstructorExpandsItsTrailingArguments()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "new SpringExpressionsTests.Expressions.ParamArrayConstructorCases(1, 2, 3).Tag",
                new ParamArrayCases())
                .ResultEqualsTo("ctor:3");
        }

        [Test]
        public void AConstructorTakesAnEmptyExpansion()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "new SpringExpressionsTests.Expressions.ParamArrayConstructorCases().Tag",
                new ParamArrayCases())
                .ResultEqualsTo("ctor:0");
        }

        [Test]
        public void AConstructorTakesAnArrayWhole()
        {
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "new SpringExpressionsTests.Expressions.ParamArrayConstructorCases(Numbers).Tag",
                new ParamArrayCases())
                .ResultEqualsTo("ctor:2");
        }

        /// <summary>
        /// The one shape with no compiled form. With exactly one argument per parameter and that
        /// argument a reference type the parameter's array type does not accept, the two forms are
        /// told apart by whether the value is null at runtime - null is the array, anything else is
        /// its single element - and static types cannot answer that. So the compiled path refuses and
        /// the interpreter, which is looking at the value, serves the call.
        /// <p>
        /// Do not "fix" one side of this by guessing a form: both readings are reachable, as the two
        /// halves below show. A cast to the array type picks the normal form explicitly, and any
        /// second argument settles it the other way.
        /// </p>
        /// </summary>
        [Test]
        public void ASingleReferenceTypedArgumentIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ParamArrayCases, string>(
                    "Objects(Label)", EvaluationMode.MustCompile));

            IExpression weak = Expression.Parse("Objects(Label)");
            Assert.AreEqual("object[1]:label", weak.GetValue(new ParamArrayCases()));

            IExpression weakNull = Expression.Parse("Objects(NoLabel)");
            Assert.AreEqual("object[]:null", weakNull.GetValue(new ParamArrayCases()));

            // A second argument leaves nothing to decide, so the shape compiles again.
            TestCompiledVsInterpreted<ParamArrayCases, string>(
                "Objects(Label, 1)", new ParamArrayCases())
                .ResultEqualsTo("object[2]:label,1");
        }
    }
}

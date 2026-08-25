using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class OverloadedCallCases
    {
        public string Foo(string stringArg) { return stringArg; }
        public int Foo(int intArg) { return intArg; }
    }

    public class AmbiguousOverloadCases
    {
        public string Pick(string s, IFormatProvider p) { return "s+fp"; }
        public string Pick(string s, string t) { return "s+s"; }

        public string Both(object a, string b) { return "o+s"; }
        public string Both(string a, object b) { return "s+o"; }
    }

    public class AmbiguousStaticCases
    {
        public static string SBoth(object a, string b) { return "o+s"; }
        public static string SBoth(string a, object b) { return "s+o"; }
    }

    /// <summary>
    /// A caller's own real-valued struct: not float, double or decimal by name, but implicitly
    /// convertible to decimal - so converting it into an integral parameter must be refused exactly
    /// like a decimal would be.
    /// </summary>
    public struct DecimalLike
    {
        private readonly decimal _value;

        public DecimalLike(decimal value) { _value = value; }

        public static implicit operator decimal(DecimalLike value) { return value._value; }
    }

    public class ConvertingCalls
    {
        public string Echo(int n) { return "int:" + n; }
        public string EchoLong(long n) { return "long:" + n; }
        public string EchoDecimal(decimal d) { return "dec:" + d; }
        public DecimalLike Amount { get { return new DecimalLike(45.5m); } }
    }

    /// <summary>
    /// Pins the failure signal for method-call shapes the compiler cannot emit. Each shape is refused
    /// with a <see cref="CompileErrorException"/> - never an ArgumentException or a
    /// NullReferenceException out of the emitter - because that exception is the only one the weakly
    /// typed path's fallback catches; anything else turns an expression the interpreter evaluates
    /// quite happily into a hard failure. The value assertions prove each expression is meaningful,
    /// so the refusal cannot be mistaken for a mistyped member name.
    /// </summary>
    [TestFixture]
    public class MethodCallFallbackTests
    {
        /// <summary>
        /// The compiled path resolves argument nodes against the method's own context - here the type
        /// name 'long' - instead of #this, so ToString() binds to an instance method with no instance
        /// to call it on. The interpreter resolves argument nodes against #this and gets it right.
        /// </summary>
        [Test]
        public void ArgumentNodeAgainstTypeNameContextIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<int, long>(
                    "long.Parse(ToString())", EvaluationMode.MustCompile));

            IExpression weak = Expression.Parse("long.Parse(ToString())");

            Assert.AreEqual((long)100, weak.GetValue(100));
        }

        /// <summary>
        /// A variable is typed object at compile time, so with two Foo overloads the choice depends
        /// on the runtime value - the overload gate refuses the shape. The interpreter picks the
        /// overload from the variable's runtime value, per call.
        /// </summary>
        [Test]
        public void OverloadUndecidableFromVariableTypeIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<OverloadedCallCases, object>(
                    "Foo(#var1)", EvaluationMode.MustCompile));

            IExpression weak = Expression.Parse("Foo(#var1)");
            var context = new OverloadedCallCases();
            var variables = new Dictionary<string, object>();

            variables["var1"] = "myString";
            Assert.AreEqual("myString", weak.GetValue(context, variables));

            variables["var1"] = 12;
            Assert.AreEqual(12, weak.GetValue(context, variables));
        }

        /// <summary>
        /// A real-to-integral argument conversion rounds in the interpreter and would truncate
        /// compiled - Echo(45.5) answered 45 compiled and 46 interpreted, a silent per-backend
        /// disagreement on the weakly typed path - so it is the one conversion class the compiled path
        /// refuses. The interpreter's binder, which converts with Convert.ChangeType and rounds, serves
        /// the call on both paths.
        /// </summary>
        [Test]
        public void NarrowingArgumentConversionIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ConvertingCalls, object>(
                    "Echo(45.5)", EvaluationMode.MustCompile));

            IExpression weak = Expression.Parse("Echo(45.5)");

            Assert.AreEqual("int:46", weak.GetValue(new ConvertingCalls()));
        }

        /// <summary>
        /// Real-valuedness is detected by conversion, not by type name: a user struct implicitly
        /// convertible to decimal is refused against an integral parameter exactly like a decimal,
        /// while against a decimal parameter its own implicit operator keeps the call compiled.
        /// </summary>
        [Test]
        public void UserRealTypesAreDetectedByConversionNotByName()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<ConvertingCalls, object>(
                    "Echo(Amount)", EvaluationMode.MustCompile));

            var compiled = Expression.ParseGetter<ConvertingCalls, object>(
                    "EchoDecimal(Amount)", EvaluationMode.MustCompile)
                .GetValue(new ConvertingCalls());

            Assert.AreEqual("dec:" + 45.5m, compiled);
        }

        /// <summary>
        /// A widening argument conversion - int to long - agrees between the backends, so it keeps
        /// its compiled form and both answer alike.
        /// </summary>
        [Test]
        public void WideningArgumentConversionStaysCompiled()
        {
            var context = new ConvertingCalls();

            var compiled = Expression.ParseGetter<ConvertingCalls, object>(
                    "EchoLong(45)", EvaluationMode.MustCompile)
                .GetValue(context);
            Assert.AreEqual("long:45", compiled);

            IExpression weak = Expression.Parse("EchoLong(45)");
            Assert.AreEqual("long:45", weak.GetValue(context));
        }

        /// <summary>
        /// A null literal against a single-candidate method is not a refusal: with one candidate
        /// there is no choice the interpreter could make differently, so the null constant is
        /// retyped to the parameter type and the call compiles. All three paths answer alike. The
        /// refusal that used to live here belonged to the era when resolution typed the null as
        /// object and missed the method; with several candidates a null literal still refuses (see
        /// AmbiguousNullArgumentOverloadIsRefusedAndEvaluatesLikeTheInterpreter).
        /// </summary>
        [Test]
        public void NullArgumentAgainstInterfaceParameterCompilesAndAgrees()
        {
            var compiled = Expression.ParseGetter<decimal, string>(
                    "ToString('dummy', null)", EvaluationMode.MustCompile)
                .GetValue(0m);
            Assert.AreEqual("dummy", compiled);

            var interpreted = Expression.ParseGetter<decimal, string>(
                    "ToString('dummy', null)", EvaluationMode.MustInterpret)
                .GetValue(0m);
            Assert.AreEqual("dummy", interpreted);

            IExpression weak = Expression.Parse("ToString('dummy', null)");
            Assert.AreEqual("dummy", weak.GetValue(0m));
        }

        /// <summary>
        /// A null literal matches both reference-typed second parameters, so the compiled candidate
        /// scan ties. The tie must surface as CompileErrorException - it used to escape as
        /// AmbiguousMatchException out of the emitter, which the fallback cannot catch, turning a
        /// construction into a hard failure. After the refusal the weak path behaves exactly like the
        /// interpreter, whose own resolver reports the same ambiguity at evaluation time, as upstream
        /// always did.
        /// </summary>
        [Test]
        public void AmbiguousNullArgumentOverloadIsRefusedAndEvaluatesLikeTheInterpreter()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<AmbiguousOverloadCases, object>(
                    "Pick('a', null)", EvaluationMode.MustCompile));

            IExpression weak = Expression.Parse("Pick('a', null)");
            Assert.Throws<AmbiguousMatchException>(
                () => weak.GetValue(new AmbiguousOverloadCases()));

            var interpreted = Expression.ParseGetter<AmbiguousOverloadCases, object>(
                "Pick('a', null)", EvaluationMode.MustInterpret);
            Assert.Throws<AmbiguousMatchException>(
                () => interpreted.GetValue(new AmbiguousOverloadCases()));
        }

        /// <summary>
        /// Two strings satisfy Both(object, string) and Both(string, object) equally, so the
        /// candidate scan ties while the tree is being built - a compile-time event that must
        /// surface as CompileErrorException, or the fallback never runs. (It used to escape as
        /// AmbiguousMatchException, first from the DefaultBinder and then from the scan.) The
        /// interpreter reports its own tie at evaluation.
        /// </summary>
        [Test]
        public void AmbiguousExactTypeResolutionIsRefusedAndEvaluatesLikeTheInterpreter()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<AmbiguousOverloadCases, object>(
                    "Both('a', 'b')", EvaluationMode.MustCompile));

            IExpression weak = Expression.Parse("Both('a', 'b')");
            Assert.Throws<AmbiguousMatchException>(
                () => weak.GetValue(new AmbiguousOverloadCases()));

            var interpreted = Expression.ParseGetter<AmbiguousOverloadCases, object>(
                "Both('a', 'b')", EvaluationMode.MustInterpret);
            Assert.Throws<AmbiguousMatchException>(
                () => interpreted.GetValue(new AmbiguousOverloadCases()));
        }

        /// <summary>
        /// The same tie reached through a type-name context: the static-method probe against the
        /// named type is a third exact-type GetMethod site, and its DefaultBinder ambiguity is a
        /// compile-time event like the others.
        /// </summary>
        [Test]
        public void AmbiguousStaticMethodOnTypeNameContextIsRefusedAndEvaluatesLikeTheInterpreter()
        {
            const string expr =
                "T(SpringExpressionsTests.Expressions.AmbiguousStaticCases, SpringExpressionsTests).SBoth('a', 'b')";

            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<object, object>(
                    expr, EvaluationMode.MustCompile));

            IExpression weak = Expression.Parse(expr);
            Assert.Throws<AmbiguousMatchException>(
                () => weak.GetValue(new object()));
        }
    }
}


using System.Collections.Generic;

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
                    "long.Parse(ToString())", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

            IExpression weak = Expression.Parse("long.Parse(ToString())");

            Assert.AreEqual((long)100, weak.GetValue(100));
        }

        /// <summary>
        /// A variable is typed object at compile time, so neither Foo(string) nor Foo(int) is
        /// assignable from it and overload resolution finds no method. The interpreter picks the
        /// overload from the variable's runtime value, per call.
        /// </summary>
        [Test]
        public void OverloadUndecidableFromVariableTypeIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<OverloadedCallCases, object>(
                    "Foo(#var1)", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

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
                    "Echo(45.5)", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

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
                    "Echo(Amount)", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

            var compiled = Expression.ParseGetter<ConvertingCalls, object>(
                    "EchoDecimal(Amount)", CompileOptions.CompileOnParse | CompileOptions.MustCompile)
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
                    "EchoLong(45)", CompileOptions.CompileOnParse | CompileOptions.MustCompile)
                .GetValue(context);
            Assert.AreEqual("long:45", compiled);

            IExpression weak = Expression.Parse("EchoLong(45)");
            Assert.AreEqual("long:45", weak.GetValue(context));
        }

        /// <summary>
        /// A null literal is typed object at compile time, and overload matching hands it to
        /// decimal.ToString(string, IFormatProvider) without retyping it, which the expression tree
        /// rejects. The interpreter passes the null through reflection, which accepts it.
        /// </summary>
        [Test]
        public void NullArgumentAgainstInterfaceParameterIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<decimal, string>(
                    "ToString('dummy', null)", CompileOptions.CompileOnParse | CompileOptions.MustCompile));

            IExpression weak = Expression.Parse("ToString('dummy', null)");

            Assert.AreEqual("dummy", weak.GetValue(0m));
        }
    }
}

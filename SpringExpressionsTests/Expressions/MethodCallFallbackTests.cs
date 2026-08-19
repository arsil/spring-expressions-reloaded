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

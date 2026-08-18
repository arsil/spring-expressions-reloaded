using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A single expression instance may be evaluated many times, and concurrently, against different
    /// roots and different variables dictionaries. Every evaluation must see the root and the
    /// variables handed to <i>it</i> - not the ones another evaluation supplied.
    /// </summary>
    /// <remarks>
    /// These tests pin the absence of per-evaluation state on the expression instance. An
    /// <c>EvaluationContext</c> is mutable, so caching one on the instance and overwriting it per call
    /// made concurrent evaluations of a shared expression read each other's variables - silently,
    /// with no exception. <c>#variable</c> is the only expression shape that touches the variables
    /// dictionary at all, which is why these tests are written in terms of one.
    /// </remarks>
    [TestFixture]
    public class EvaluationContextSharingTests : BaseCompiledTests
    {
        private static Dictionary<string, object> VariablesWithX(object value)
            => new Dictionary<string, object> { { "x", value } };

        [Test]
        public void CompiledGetterReadsTheVariablesOfEachCall()
        {
            var getter = CompileGetter<object, object>("#x");

            Assert.AreEqual(1, getter.GetValue(null, VariablesWithX(1)));
            Assert.AreEqual(2, getter.GetValue(null, VariablesWithX(2)));
        }

        [Test]
        public void InterpretedGetterReadsTheVariablesOfEachCall()
        {
            var getter = InterpretGetter<object, object>("#x");

            Assert.AreEqual(1, getter.GetValue(null, VariablesWithX(1)));
            Assert.AreEqual(2, getter.GetValue(null, VariablesWithX(2)));
        }

        /// <summary>
        /// The weakly typed path used to build its evaluation context only while compiling, which
        /// happens once - so every later evaluation kept reading the dictionary that the very first
        /// evaluation had supplied.
        /// </summary>
        [Test]
        public void WeaklyTypedExpressionReadsTheVariablesOfEachCall()
        {
            IExpression expression = Expression.Parse("#x");

            Assert.AreEqual(1, expression.GetValue<object>(null, VariablesWithX(1)));
            Assert.AreEqual(2, expression.GetValue<object>(null, VariablesWithX(2)));
        }

        /// <remarks>
        /// The assigned type is <c>object</c> rather than a value type on purpose: assigning a value
        /// type to a #variable does not compile at all yet, because the emitted call to SetVariable
        /// passes the new value straight into an <c>object</c> parameter without boxing it. That is a
        /// separate defect from the one this fixture covers.
        /// </remarks>
        [Test]
        public void CompiledSetterWritesToTheVariablesOfEachCall()
        {
            var setter = CompileSetter<object, object>("#x");

            var first = new Dictionary<string, object>();
            var second = new Dictionary<string, object>();

            setter.SetValue(null, 1, first);
            setter.SetValue(null, 2, second);

            Assert.AreEqual(1, first["x"]);
            Assert.AreEqual(2, second["x"]);
        }

        /// <remarks>
        /// Interpreted, because assigning to a #variable has no working compiled form yet: the emitted
        /// SetVariable call neither boxes a value-type argument nor satisfies the void-shape check in
        /// <c>Compiler.CompileExecuteWithVoidReturnType</c>, which accepts only void and Assign and so
        /// rejects the Call that an assignment to a variable produces. Both are separate defects from
        /// the one this fixture covers; the interpreted path exercises the same per-call context.
        /// </remarks>
        [Test]
        public void InterpretedVoidExpressionWritesToTheVariablesOfEachCall()
        {
            var voidExpression = Expression.ParseVoidExpression(
                "#x = 5", CompileOptions.MustUseInterpreter);

            var first = new Dictionary<string, object>();
            var second = new Dictionary<string, object>();

            voidExpression.Execute(first);
            voidExpression.Execute(second);

            Assert.AreEqual(5, first["x"]);
            Assert.AreEqual(5, second["x"]);
        }

        [Test]
        public void CompiledGetterIsThreadSafeWhenEachThreadHasItsOwnVariables()
        {
            AssertEachThreadGetsItsOwnValue(CompileGetter<object, object>("#x"));
        }

        [Test]
        public void InterpretedGetterIsThreadSafeWhenEachThreadHasItsOwnVariables()
        {
            AssertEachThreadGetsItsOwnValue(InterpretGetter<object, object>("#x"));
        }

        /// <summary>
        /// Evaluates one shared getter for "#x" on several threads at once, each thread passing its
        /// own variables dictionary holding its own thread index, and requires every evaluation to
        /// return that thread's index back.
        /// </summary>
        private static void AssertEachThreadGetsItsOwnValue(IGetterExpression<object, object> getter)
        {
            const int threadCount = 4;
            const int iterations = 50000;

            var wrongResults = new int[threadCount];
            var thrown = new Exception[threadCount];
            var threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int threadIndex = i;

                threads[i] = new Thread(() =>
                    {
                        var variables = VariablesWithX(threadIndex);
                        try
                        {
                            for (int iteration = 0; iteration < iterations; iteration++)
                            {
                                if (!threadIndex.Equals(getter.GetValue(null, variables)))
                                    wrongResults[threadIndex]++;
                            }
                        }
                        catch (Exception e)
                        {
                            thrown[threadIndex] = e;
                        }
                    });
            }

            foreach (var thread in threads)
                thread.Start();

            foreach (var thread in threads)
                thread.Join();

            for (int i = 0; i < threadCount; i++)
                Assert.IsNull(thrown[i], "thread {0} threw {1}", i, thrown[i]);

            Assert.AreEqual(
                0,
                wrongResults.Sum(),
                "evaluations that returned another thread's value, out of {0}",
                threadCount * iterations);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Two constructors chosen by argument type, for
    /// <c>EvaluationContextSharingTests.AConstructorIsResolvedForTheArgumentsOfEachEvaluation</c>.
    /// Top-level because the grammar cannot spell a nested type's name in <c>new</c>.
    /// </summary>
    public class ResolutionKeyThing
    {
        public ResolutionKeyThing(int n) { Picked = "int:" + n; }

        public ResolutionKeyThing(string s) { Picked = "string:" + s; }

        public string Picked { get; private set; }
    }

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

        public class StaleCacheInner
        {
            public object Echo(object o) { return o; }
        }

        public class StaleCacheRoot
        {
            public StaleCacheInner Inner { get; set; }

            public string Name { get; set; } = "Ana";
        }

        /// <summary>
        /// A method call on a <b>null</b> receiver must fail whether or not this same expression object
        /// was evaluated against a present receiver first.
        /// </summary>
        /// <remarks>
        /// <c>MethodNode</c> caches its resolved method on the node, and the null branch used to leave
        /// that cache alone - so the invoke went ahead with a null target, and a method that never
        /// touches <c>this</c> simply succeeded. Measured: one expression object answered <c>"Ana"</c>
        /// for a null <c>Inner</c> after a first evaluation against a present one, where evaluating the
        /// same shape fresh threw. **The answer depended on evaluation history**, which is this
        /// fixture's subject in a place nothing had looked - not the variables dictionary but the
        /// resolved-method cache.
        /// <p>
        /// Found by the corpus rows added for gap nine (`_Docs/open-issues.md` item 34), and invisible
        /// to a probe that builds a fresh expression per root - which is exactly what my first probe
        /// did, and why it reported agreement.
        /// </p>
        /// <p>
        /// The exception types differ between the backends and that is not the point here: what must
        /// not happen is one evaluation answering because an earlier one warmed a cache.
        /// </p>
        /// </remarks>
        [Test]
        public void AMethodCallOnANullReceiverFailsRegardlessOfWhatWasEvaluatedBefore()
        {
            var present = new StaleCacheRoot { Inner = new StaleCacheInner() };
            var absent = new StaleCacheRoot();

            foreach (var mode in new[] { EvaluationMode.MustInterpret, EvaluationMode.MustCompile })
            {
                var reused = Expression.ParseGetter<StaleCacheRoot, object>(
                    "Inner.Echo(Name)", mode);

                Assert.AreEqual("Ana", reused.GetValue(present), mode + ": the present receiver");

                Assert.Catch(
                    () => reused.GetValue(absent),
                    mode + ": a null receiver must fail even after a successful evaluation");

                // And the other way round, so neither order is privileged.
                var reversed = Expression.ParseGetter<StaleCacheRoot, object>(
                    "Inner.Echo(Name)", mode);

                Assert.Catch(() => reversed.GetValue(absent), mode + ": null first");
                Assert.AreEqual("Ana", reversed.GetValue(present), mode + ": then present");
            }
        }

        /// <summary>
        /// A cached resolution must be keyed on everything the resolution depended on - here the
        /// argument types that chose the constructor.
        /// </summary>
        /// <remarks>
        /// <c>ConstructorNode</c> re-resolved only when its field was null, so one expression object
        /// picked a constructor on its first evaluation and kept it: <c>#x = 5</c> then
        /// <c>#x = 'hi'</c> reused the <c>int</c> constructor and threw <c>InvalidCastException</c>,
        /// where a freshly parsed expression answered <c>string:hi</c>.
        /// <p>
        /// <b>Identically on both backends</b>, which is why no sweep could have found it: same value,
        /// same runtime type, same read counts. The only reference that exposes this class is a
        /// freshly-parsed expression, which is `_Docs/open-issues.md` item 35.
        /// </p>
        /// </remarks>
        [Test]
        public void AConstructorIsResolvedForTheArgumentsOfEachEvaluation()
        {
            foreach (var mode in new[] { EvaluationMode.MustInterpret, EvaluationMode.CompileOrInterpret })
            {
                var reused = Expression.ParseGetter<object, object>(
                    "new SpringExpressionsTests.Expressions.ResolutionKeyThing(#x).Picked", mode);

                Assert.AreEqual("int:5", reused.GetValue(null, VariablesWithX(5)), mode.ToString());

                Assert.AreEqual(
                    "string:hi",
                    reused.GetValue(null, VariablesWithX("hi")),
                    mode + ": the second evaluation must resolve for its own arguments");

                // And back again, so the cache is not merely replaced once.
                Assert.AreEqual("int:7", reused.GetValue(null, VariablesWithX(7)), mode.ToString());
            }
        }

        /// <summary>
        /// The same rule for an indexer, keyed on the container's runtime type and the index types.
        /// </summary>
        /// <remarks>
        /// <c>IndexerNode</c> kept the first indexer it resolved, so <c>Item[0]</c> over two types that
        /// each declare <c>this[int]</c> threw <c>InvalidPropertyException</c> on the second - again
        /// identically on both backends.
        /// </remarks>
        [Test]
        public void AnIndexerIsResolvedForTheContainerOfEachEvaluation()
        {
            var one = new IndexerKeyRoot { Item = new IndexerKeyBoxOne() };
            var two = new IndexerKeyRoot { Item = new IndexerKeyBoxTwo() };

            foreach (var mode in new[] { EvaluationMode.MustInterpret, EvaluationMode.CompileOrInterpret })
            {
                var reused = Expression.ParseGetter<IndexerKeyRoot, object>("Item[0]", mode);

                Assert.AreEqual("one:0", reused.GetValue(one), mode.ToString());

                Assert.AreEqual(
                    "two:0",
                    reused.GetValue(two),
                    mode + ": the second evaluation must resolve for its own container");

                Assert.AreEqual("one:0", reused.GetValue(one), mode.ToString());
            }
        }

        public class IndexerKeyBoxOne
        {
            public string this[int i] { get { return "one:" + i; } }
        }

        public class IndexerKeyBoxTwo
        {
            public string this[int i] { get { return "two:" + i; } }
        }

        public class IndexerKeyRoot
        {
            public object Item { get; set; }
        }

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
                "#x = 5", EvaluationMode.MustInterpret);

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


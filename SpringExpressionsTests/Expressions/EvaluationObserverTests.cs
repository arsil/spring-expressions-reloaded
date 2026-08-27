using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Being told what a weakly typed expression became, per combination of declared types.
    /// </summary>
    /// <remarks>
    /// The strongly typed path answers a query, because its decision is made inside the caller's own
    /// constructor call. A weakly typed expression has no single answer to give: it decides per declared
    /// context type, on first use with that type, inside code the asker did not write and possibly on
    /// another thread. So it pushes - an observer handed to <c>Parse</c>, told once per decision, never
    /// per evaluation.
    /// </remarks>
    [TestFixture]
    public class EvaluationObserverTests : BaseCompiledTests
    {
        public class Holder
        {
            public string Name { get; set; }
            public int Number { get; set; }
        }

        /// <summary>
        /// The decision is reported, not the evaluation: an expression used a million times against one
        /// declared type is reported once.
        /// </summary>
        [Test]
        public void AReadThatCompilesIsReportedOnceHoweverOftenItIsEvaluated()
        {
            var decisions = new List<EvaluationDecision>();
            var expression = Expression.Parse("Name", EvaluationMode.CompileOrInterpret, decisions.Add);

            var holder = new Holder { Name = "Ana" };

            Assert.AreEqual("Ana", expression.GetValue<Holder>(holder));
            Assert.AreEqual("Ana", expression.GetValue<Holder>(holder));
            Assert.AreEqual("Ana", expression.GetValue<Holder>(holder));

            Assert.AreEqual(1, decisions.Count);
            Assert.AreEqual(typeof(Holder), decisions[0].ContextType);
            Assert.IsNull(decisions[0].ValueType, "a read is compiled against the context type alone");
            Assert.AreEqual(EvaluationOperation.Get, decisions[0].Operation);
            Assert.AreEqual(EvaluationKind.Compiled, decisions[0].Kind);
            Assert.IsNull(decisions[0].Reason);
            Assert.IsNull(decisions[0].RefusalMessage);
        }

        /// <summary>
        /// One expression, two declared types, two decisions - and they differ, which is the whole
        /// reason a single query could not have answered for the expression as a whole.
        /// </summary>
        [Test]
        public void ASecondDeclaredTypeIsASecondDecision()
        {
            var decisions = new List<EvaluationDecision>();
            var expression = Expression.Parse("Name", EvaluationMode.CompileOrInterpret, decisions.Add);

            var holder = new Holder { Name = "Ana" };

            Assert.AreEqual("Ana", expression.GetValue<Holder>(holder));
            Assert.AreEqual("Ana", expression.GetValue<object>(holder));

            Assert.AreEqual(2, decisions.Count);

            Assert.AreEqual(typeof(Holder), decisions[0].ContextType);
            Assert.AreEqual(EvaluationKind.Compiled, decisions[0].Kind);

            Assert.AreEqual(typeof(object), decisions[1].ContextType);
            Assert.AreEqual(EvaluationKind.Interpreted, decisions[1].Kind);
            Assert.AreEqual(InterpretationReason.CompilationRefused, decisions[1].Reason);
            StringAssert.Contains("PropertyOrFieldNode", decisions[1].RefusalMessage,
                "the refusal names the node - that is the part a caller cannot work out alone");
            StringAssert.Contains("Name", decisions[1].RefusalMessage);
        }

        /// <summary>
        /// It fires even when nothing was attempted. A caller who asked to be told the outcome is told
        /// the outcome, and the uniform rule beats a special case nobody could predict from the
        /// signature.
        /// </summary>
        [Test]
        public void RequestedInterpretationIsStillADecisionAndIsStillReported()
        {
            var decisions = new List<EvaluationDecision>();
            var expression = Expression.Parse("Name", EvaluationMode.MustInterpret, decisions.Add);

            Assert.AreEqual("Ana", expression.GetValue<Holder>(new Holder { Name = "Ana" }));

            Assert.AreEqual(1, decisions.Count);
            Assert.AreEqual(EvaluationKind.Interpreted, decisions[0].Kind);
            Assert.AreEqual(InterpretationReason.Requested, decisions[0].Reason);
            Assert.IsNull(decisions[0].RefusalMessage, "nothing was refused - the caller chose this");
        }

        /// <summary>
        /// Writes decide separately from reads against the same declared context type, which is why the
        /// payload names the operation.
        /// </summary>
        [Test]
        public void ReadingAndWritingAgainstOneContextTypeAreTwoDecisions()
        {
            var decisions = new List<EvaluationDecision>();
            var expression = Expression.Parse("Number", EvaluationMode.CompileOrInterpret, decisions.Add);

            var holder = new Holder();

            expression.SetValue(holder, 45);
            Assert.AreEqual(45, expression.GetValue<Holder>(holder));

            Assert.AreEqual(2, decisions.Count);

            Assert.AreEqual(EvaluationOperation.Set, decisions[0].Operation);
            Assert.AreEqual(typeof(Holder), decisions[0].ContextType);
            Assert.AreEqual(typeof(int), decisions[0].ValueType);
            Assert.AreEqual(EvaluationKind.Compiled, decisions[0].Kind);

            Assert.AreEqual(EvaluationOperation.Get, decisions[1].Operation);
            Assert.IsNull(decisions[1].ValueType);
        }

        /// <summary>
        /// The row the payload's <c>ValueType</c> exists for: two writes against one context type, whose
        /// only difference is the type of the value, and which decide differently.
        /// </summary>
        /// <remarks>
        /// A string into an int member has no compiled form - assignment is not conversion - so it
        /// refuses and the interpreter converts, exactly as it always did. Naming only the context type
        /// would report these two as one repeated fact about <c>Holder</c>/<c>Set</c> and leave a reader
        /// unable to tell which call produced which answer.
        /// </remarks>
        [Test]
        public void TwoValueTypesAgainstOneContextTypeAreTwoDecisions()
        {
            var decisions = new List<EvaluationDecision>();
            var expression = Expression.Parse("Number", EvaluationMode.CompileOrInterpret, decisions.Add);

            var holder = new Holder();

            expression.SetValue(holder, 45);
            Assert.AreEqual(45, holder.Number);

            expression.SetValue(holder, "46");
            Assert.AreEqual(46, holder.Number, "the interpreter converts on assignment, as it always did");

            Assert.AreEqual(2, decisions.Count);

            Assert.AreEqual(typeof(int), decisions[0].ValueType);
            Assert.AreEqual(EvaluationKind.Compiled, decisions[0].Kind);

            Assert.AreEqual(typeof(string), decisions[1].ValueType);
            Assert.AreEqual(typeof(Holder), decisions[1].ContextType, "same context type, different value type");
            Assert.AreEqual(EvaluationKind.Interpreted, decisions[1].Kind);
            Assert.AreEqual(InterpretationReason.CompilationRefused, decisions[1].Reason);
            Assert.IsNotNull(decisions[1].RefusalMessage);
        }

        /// <summary>
        /// A broken observer runs inside an unrelated caller's evaluation, so it must not surface there.
        /// </summary>
        [Test]
        public void AnObserverThatThrowsChangesNothing()
        {
            var calls = 0;
            var expression = Expression.Parse(
                "Name",
                EvaluationMode.CompileOrInterpret,
                _ =>
                {
                    calls++;
                    throw new InvalidOperationException("a broken logger");
                });

            var holder = new Holder { Name = "Ana" };

            Assert.AreEqual("Ana", expression.GetValue<Holder>(holder));
            Assert.AreEqual("Ana", expression.GetValue<Holder>(holder),
                "the throw must not have prevented the decision from being recorded");

            Assert.AreEqual(1, calls, "the evaluator was still added to the map, so nothing decided twice");
        }

        /// <summary>
        /// Many threads, one expression, two declared types: exactly two notifications.
        /// </summary>
        /// <remarks>
        /// This is what <c>TryAdd</c>-won exists for, and the test has teeth: put the notification back
        /// inside a <c>GetOrAdd</c> factory and this reports <b>eight</b> decisions rather than two, on
        /// every run - compiling takes long enough that all eight threads, released together, miss and
        /// build. <c>GetOrAdd</c> may run its factory more than once under contention and keep one
        /// result, which is harmless when the loser is a discarded evaluator and not harmless when it is
        /// a duplicate notification about a decision that happened once.
        /// </remarks>
        [Test]
        public void ConcurrentFirstUsesNotifyExactlyOncePerDeclaredType()
        {
            const int threadCount = 8;
            const int iterations = 20000;

            var decisions = new ConcurrentQueue<EvaluationDecision>();
            var expression = Expression.Parse("Name", EvaluationMode.CompileOrInterpret, decisions.Enqueue);

            var holder = new Holder { Name = "Ana" };
            var start = new ManualResetEvent(false);
            var thrown = new Exception[threadCount];
            var threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int threadIndex = i;

                threads[i] = new Thread(() =>
                    {
                        start.WaitOne();
                        try
                        {
                            for (int iteration = 0; iteration < iterations; iteration++)
                            {
                                if (threadIndex % 2 == 0)
                                    expression.GetValue<Holder>(holder);
                                else
                                    expression.GetValue<object>(holder);
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

            start.Set();

            foreach (var thread in threads)
                thread.Join();

            for (int i = 0; i < threadCount; i++)
                Assert.IsNull(thrown[i], "thread {0} threw {1}", i, thrown[i]);

            var reported = decisions.ToList();

            Assert.AreEqual(2, reported.Count, "one decision per declared type, however many threads raced");
            CollectionAssert.AreEquivalent(
                new[] { typeof(Holder), typeof(object) },
                reported.Select(d => d.ContextType).ToList());
        }

        /// <summary>
        /// An expression parsed without an observer behaves exactly as it did before there was one.
        /// </summary>
        [Test]
        public void NoObserverIsTheOrdinaryCase()
        {
            var expression = Expression.Parse("Name");

            Assert.AreEqual("Ana", expression.GetValue<Holder>(new Holder { Name = "Ana" }));
            Assert.AreEqual("Ana", expression.GetValue<object>(new Holder { Name = "Ana" }));
        }
    }
}

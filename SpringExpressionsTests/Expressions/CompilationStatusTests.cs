using System;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Asking a strongly typed expression what it became.
    /// </summary>
    /// <remarks>
    /// The author left the question in the code - "serio? jak się mamy dowiedzieć, czy jest
    /// kompilowalne" - above the empty <c>IStronglyTypedExpression</c> marker that turned out to be the
    /// hook for answering it. One method serves getters, setters and void expressions, because the answer
    /// is decided when the expression is created: ParseGetter has already compiled or refused by the time
    /// it returns, so a query is complete and can never say "not yet".
    /// <p>
    /// The weakly typed path has no counterpart on purpose. It decides per declared context type, on
    /// first use with that type, so there is no moment at which a query could be complete - it reports
    /// through an observer instead, pinned by <c>EvaluationObserverTests</c>.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class CompilationStatusTests : BaseCompiledTests
    {
        public class Holder
        {
            public string Name { get; set; }
            public int Number { get; set; }
        }

        [Test]
        public void ACompiledExpressionReportsCompiled()
        {
            var status = Expression.GetCompilationStatus(
                Expression.ParseGetter<Holder, string>("Name", EvaluationMode.MustCompile));

            Assert.AreEqual(EvaluationKind.Compiled, status.Kind);
            Assert.IsNull(status.Reason);
            Assert.IsNull(status.RefusalMessage);
        }

        [Test]
        public void AnExpressionTheCallerAskedToInterpretSaysSo()
        {
            var status = Expression.GetCompilationStatus(
                Expression.ParseGetter<Holder, string>("Name", EvaluationMode.MustInterpret));

            Assert.AreEqual(EvaluationKind.Interpreted, status.Kind);
            Assert.AreEqual(InterpretationReason.Requested, status.Reason);
            Assert.IsNull(status.RefusalMessage, "nothing was refused - the caller chose this");
        }

        /// <summary>
        /// The row the API exists for: interpreted because the shape has no compiled form, with the node
        /// that refused named in the message.
        /// </summary>
        [Test]
        public void ARefusedShapeSaysWhichNodeRefused()
        {
            // a lambda has no compiled form at all
            var status = Expression.GetCompilationStatus(
                Expression.ParseGetter<Holder, object>("{|n| $n + 1}"));

            Assert.AreEqual(EvaluationKind.Interpreted, status.Kind);
            Assert.AreEqual(InterpretationReason.CompilationRefused, status.Reason);
            Assert.IsNotNull(status.RefusalMessage);
            StringAssert.Contains("LambdaExpressionNode", status.RefusalMessage,
                "the message is the reason this API is worth having - it must name the node");
        }

        /// <summary>
        /// Setters and void expressions each carry their own Status implementation, so each is asked once.
        /// </summary>
        [Test]
        public void SettersAndVoidExpressionsAnswerTheSameWay()
        {
            Assert.AreEqual(
                EvaluationKind.Compiled,
                Expression.GetCompilationStatus(
                    Expression.ParseSetter<Holder, string>("Name", EvaluationMode.MustCompile)).Kind);

            Assert.AreEqual(
                EvaluationKind.Interpreted,
                Expression.GetCompilationStatus(
                    Expression.ParseSetter<Holder, string>("Name", EvaluationMode.MustInterpret)).Kind);

            Assert.AreEqual(
                EvaluationKind.Compiled,
                Expression.GetCompilationStatus(
                    Expression.ParseVoidExpression<Holder>("Number = 45", EvaluationMode.MustCompile)).Kind);

            Assert.AreEqual(
                EvaluationKind.Interpreted,
                Expression.GetCompilationStatus(
                    Expression.ParseVoidExpression<Holder>("Number = 45", EvaluationMode.MustInterpret)).Kind);
        }

        /// <summary>
        /// The marker interface is public and empty, so anyone may implement it. Nothing is known about
        /// such an instance, and saying so is the honest answer.
        /// </summary>
        [Test]
        public void AForeignImplementationIsRefused()
        {
            Assert.Throws<ArgumentException>(
                () => Expression.GetCompilationStatus(new NotOurExpression()));
        }

        private sealed class NotOurExpression : IGetterExpression<Holder, string>
        {
            public string GetValue(Holder context, System.Collections.Generic.IDictionary<string, object> variables = null)
                => null;
        }
    }
}

using NUnit.Framework;

using System.Collections.Generic;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class BodyScopeHolder
    {
        public List<string> Words { get; } = new List<string> { "a", "b" };
        public List<int> Ints { get; } = new List<int> { 1, 2, 3 };

        public string Sep { get { return "-"; } }
        public int Two { get { return 2; } }
    }

    /// <summary>
    /// A projection or selection body can reach what surrounds it: <c>#root</c> and the caller's
    /// <c>#variables</c>.
    /// </summary>
    /// <remarks>
    /// The body used to be compiled to a delegate on its own and handed into the emitted tree as a
    /// constant, and **a separately compiled lambda has no enclosing scope at all** - so anything
    /// outside the body emitted a reference to a parameter of the outer lambda and came out as
    /// <c>"variable 'context' … referenced from scope '', but it is not defined"</c>. Absorbed, and
    /// reported to the caller as an *internal compiler error* about their own ordinary expression.
    /// Nesting the lambda in the tree lets the outer compilation build the closure, which is what a
    /// nested lambda in an expression tree does anyway.
    /// <p>
    /// <b>It was a defect <c>CompilationNeverLeaksTests</c> is built to catch and could not</b>, for
    /// want of a row: every body the corpus generated was written in terms of <c>#this</c> alone.
    /// <c>source.!{#root.Number}</c> and <c>source.?{#root.Flag}</c> are in it now.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class BodyScopeTests : BaseCompiledTests
    {
        [Test]
        public void AProjectionBodyCanReachTheRoot()
        {
            var holder = new BodyScopeHolder();

            TestCompiledVsInterpreted<BodyScopeHolder, object>("Words.!{#root.Sep}", holder);
            TestCompiledVsInterpreted<BodyScopeHolder, object>("Words.!{#this + #root.Sep}", holder);

            // and a whole expression, not just a member read
            TestCompiledVsInterpreted<BodyScopeHolder, object>("Ints.!{#root.Ints.count()}", holder);
        }

        [Test]
        public void ASelectionBodyCanReachTheRoot()
        {
            var holder = new BodyScopeHolder();

            TestCompiledVsInterpreted<BodyScopeHolder, object>("Ints.?{#this > #root.Two}", holder);
            TestCompiledVsInterpreted<BodyScopeHolder, object>("Ints.^{#this > #root.Two}", holder);
            TestCompiledVsInterpreted<BodyScopeHolder, object>("Ints.${#this > #root.Two}", holder);
        }

        /// <summary>
        /// The caller's variables reach a body too - the same parameter of the outer lambda, and the
        /// same failure before this.
        /// </summary>
        [Test]
        public void ABodyCanReachTheCallersVariables()
        {
            var holder = new BodyScopeHolder();
            var variables = new Dictionary<string, object> { { "v", "V" } };

            var compiled = Expression
                .ParseGetter<BodyScopeHolder, object>("Words.!{#this + #v}", EvaluationMode.MustCompile)
                .GetValue(holder, variables);

            var interpreted = Expression
                .ParseGetter<BodyScopeHolder, object>("Words.!{#this + #v}", EvaluationMode.MustInterpret)
                .GetValue(holder, variables);

            CollectionAssert.AreEqual(new object[] { "aV", "bV" }, (System.Collections.IEnumerable)compiled);
            CollectionAssert.AreEqual(new object[] { "aV", "bV" }, (System.Collections.IEnumerable)interpreted);
        }

        /// <summary>
        /// A body shares the enclosing scope's <c>$locals</c> too, which followed from the same
        /// change: nesting the lambda is what put an outer block variable in scope.
        /// </summary>
        /// <remarks>
        /// What is left inside a body is the ordinary object-typed-local story - a member of whatever
        /// a local holds needs a cast, here exactly as anywhere else. See
        /// <c>LocalVariableStorageTests</c> for the accumulate-into-a-builder shape and for the
        /// uncast refusal beside it.
        /// </remarks>
        [Test]
        public void ABodyCanReachTheEnclosingLocals()
        {
            var holder = new BodyScopeHolder();

            TestCompiledVsInterpreted<BodyScopeHolder, object>("Ints.!{$x = #this}", holder);

            // Counted rather than compared as a list: an expression list costs its result the root
            // reshaping, which predates this and is unrelated to locals - _Docs/open-issues.md
            // item 47.
            TestCompiledVsInterpreted<BodyScopeHolder, object>(
                "($n = 2; Words.!{#this + $n}.count())", holder)
                .ResultEqualsTo(2);
        }

        /// <summary>
        /// A lambda argument is unaffected: it keeps its receiver as its context and is still handed
        /// to the processor that invokes it.
        /// </summary>
        [Test]
        public void ALambdaArgumentIsUnchanged()
        {
            TestCompiledVsInterpreted<BodyScopeHolder, object>(
                "Ints.orderBy({|a,b| $a - $b})", new BodyScopeHolder());
        }
    }
}

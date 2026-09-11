using NUnit.Framework;

using System.Collections.Generic;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class ExpressionListResultHolder
    {
        public List<string> Words { get; } = new List<string> { "a", "b" };
        public List<int> Ints { get; } = new List<int> { 3, 1, 2 };

        /// <summary>A collection the caller owns - it must come back as itself, not as a copy.</summary>
        public List<int> Owned { get; } = new List<int> { 9, 8 };
    }

    /// <summary>
    /// An expression list answers what its last element answers, including whether that value is a
    /// collection the engine built.
    /// </summary>
    /// <remarks>
    /// The compiled path builds a <c>List&lt;string&gt;</c> where it knows the item type and the
    /// interpreter builds a <c>List&lt;object&gt;</c>, so <c>Compiler</c> reshapes the result to
    /// <c>List&lt;object&gt;</c> at the end - but only for a collection the engine **built**, since a
    /// collection merely **read** is the caller's own object and copying it would lose its identity.
    /// It tells them apart by a registry keyed on the emitted expression, and
    /// <c>ProjectionNode</c> registers its own call.
    /// <p>
    /// <b>Wrapping the projection changed which node is at the root.</b> In
    /// <c>(1; Words.!{#this})</c> the root is the block, which nobody registered, so the reshaping
    /// was skipped and a <c>List&lt;string&gt;</c> escaped. The list propagates the registration from
    /// its last element now.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class ExpressionListResultTests : BaseCompiledTests
    {
        [Test]
        public void AWrappedProjectionKeepsTheShapeOfAnUnwrappedOne()
        {
            var holder = new ExpressionListResultHolder();

            TestCompiledVsInterpreted<ExpressionListResultHolder, object>("Words.!{#this}", holder);
            TestCompiledVsInterpreted<ExpressionListResultHolder, object>("(1; Words.!{#this})", holder);
            TestCompiledVsInterpreted<ExpressionListResultHolder, object>(
                "(1; 2; Words.!{#this})", holder);
            TestCompiledVsInterpreted<ExpressionListResultHolder, object>(
                "($n = 2; Words.!{#this})", holder);
        }

        [Test]
        public void EveryKindOfCollectionTheEngineBuildsPropagates()
        {
            var holder = new ExpressionListResultHolder();

            TestCompiledVsInterpreted<ExpressionListResultHolder, object>(
                "(1; Words.?{#this != null})", holder);
            TestCompiledVsInterpreted<ExpressionListResultHolder, object>("(1; {1,2})", holder);
            TestCompiledVsInterpreted<ExpressionListResultHolder, object>("(1; #{'k' : 1})", holder);
            TestCompiledVsInterpreted<ExpressionListResultHolder, object>("(1; Ints.sort())", holder);
        }

        /// <summary>
        /// A collection the caller owns is not the engine's to reshape, wrapped or not: it keeps its
        /// own type and the very instance.
        /// </summary>
        /// <remarks>
        /// This is the assertion that matters, because the cheap version of the fix - "a list whose
        /// last element is a collection is a constructed collection" - would pass every test above
        /// and quietly hand back a copy here. The test is on the last element having been
        /// <i>registered</i>, not on it being a collection.
        /// </remarks>
        [Test]
        public void AReadCollectionIsNeitherReshapedNorCopied()
        {
            var holder = new ExpressionListResultHolder();

            TestCompiledVsInterpreted<ExpressionListResultHolder, object>("(1; Owned)", holder);

            var compiled = Expression
                .ParseGetter<ExpressionListResultHolder, object>("(1; Owned)", EvaluationMode.MustCompile)
                .GetValue(holder);

            Assert.AreSame(holder.Owned, compiled, "a read collection must come back as itself");

            var interpreted = Expression
                .ParseGetter<ExpressionListResultHolder, object>("(1; Owned)", EvaluationMode.MustInterpret)
                .GetValue(holder);

            Assert.AreSame(holder.Owned, interpreted);
        }

        /// <summary>
        /// Parking the collection in a local agrees too, by a different mechanism: the reshaping
        /// happens on the way into the slot rather than at the root.
        /// </summary>
        /// <remarks>
        /// The list's last element here is a local <i>read</i>, not the registered projection call,
        /// so the propagation above cannot see it - and a slot is object-typed, so reshaping the read
        /// would be a no-op anyway. <c>LocalVariableNode</c> normalizes a registered collection as it
        /// is stored instead, which is exactly what the interpreter does. A collection merely read is
        /// not registered and is stored untouched.
        /// </remarks>
        [Test]
        public void ParkingACollectionInALocalAgreesToo()
        {
            var holder = new ExpressionListResultHolder();

            TestCompiledVsInterpreted<ExpressionListResultHolder, object>(
                "($xs = Words.!{#this}; $xs)", holder);

            TestCompiledVsInterpreted<ExpressionListResultHolder, object>(
                "($xs = Ints.!{#this}; $xs)", holder);

            // a collection the caller owns, parked and handed back: still their own instance
            var owned = Expression
                .ParseGetter<ExpressionListResultHolder, object>(
                    "($xs = Owned; $xs)", EvaluationMode.MustCompile)
                .GetValue(holder);

            Assert.AreSame(holder.Owned, owned);

            // and one reassigned from built to read keeps the read one, untouched
            var reassigned = Expression
                .ParseGetter<ExpressionListResultHolder, object>(
                    "($xs = Words.!{#this}; $xs = Owned; $xs)", EvaluationMode.MustCompile)
                .GetValue(holder);

            Assert.AreSame(holder.Owned, reassigned);
        }
    }
}

using NUnit.Framework;

using System.Collections.Generic;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class IndexContextInner
    {
        public int[] Numbers { get; } = new[] { 10, 20, 30 };
    }

    public class IndexContextHolder
    {
        public int[] Numbers { get; } = new[] { 10, 20, 30 };
        public List<int> Ints { get; } = new List<int> { 10, 20, 30 };
        public Dictionary<string, int> Map { get; } = new Dictionary<string, int> { { "a", 1 } };

        public IndexContextInner Inner { get; } = new IndexContextInner();

        public int Zero { get { return 0; } }
        public string Key { get { return "a"; } }
        public int One() { return 1; }

        // Names the containers declare as well, which is what turns a refusal into a wrong pick.
        public int Count { get { return 0; } }
        public int Length { get { return 0; } }

        public int this[int i] { get { return 100 + i; } }
    }

    /// <summary>
    /// An index resolves against <c>#this</c>, not against the container being indexed.
    /// </summary>
    /// <remarks>
    /// The interpreter always did - <c>NodeWithArguments.ResolveArgumentInternal</c> evaluates every
    /// argument against <c>evalContext.ThisContext</c> - while <c>IndexerNode</c> emitted index
    /// expressions against the container. <b>The same defect <c>MethodNode</c> had, and wider</b>: a
    /// method's receiver <i>is</i> <c>#this</c> for any chain starting at the root, so only a nested
    /// receiver showed it there. An indexer's context is the container and never <c>#this</c>, so
    /// every non-literal index was affected - <c>Numbers[Zero]</c> at the top of an expression
    /// already broke.
    /// <p>
    /// Mostly it refused, which the fallback covers. It became a <b>wrong pick</b> wherever the
    /// container declares the same name as the root, and for collections that is <c>Count</c>,
    /// <c>Length</c>, <c>Keys</c>, <c>Capacity</c> and friends.
    /// </p>
    /// <p>
    /// <b>Neither sweep could see the refusals</b> - a refused shape has no compiled answer to
    /// compare - so only the wrong-pick rows were visible, and the corpus indexed exclusively with
    /// literals. <c>Index</c> and <c>Length</c> are in the corpus root now and every source is
    /// indexed by both.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class IndexArgumentContextTests : BaseCompiledTests
    {
        [Test]
        public void AnIndexReadingAMemberOfTheRootCompiles()
        {
            var holder = new IndexContextHolder();

            TestCompiledVsInterpreted<IndexContextHolder, object>("Numbers[Zero]", holder)
                .ResultEqualsTo(10);
            TestCompiledVsInterpreted<IndexContextHolder, object>("Ints[Zero]", holder)
                .ResultEqualsTo(10);
            TestCompiledVsInterpreted<IndexContextHolder, object>("Map[Key]", holder)
                .ResultEqualsTo(1);
        }

        [Test]
        public void AnIndexCallingAMethodOfTheRootCompiles()
        {
            TestCompiledVsInterpreted<IndexContextHolder, object>(
                    "Numbers[One()]", new IndexContextHolder())
                .ResultEqualsTo(20);
        }

        /// <summary>
        /// The container is not the context even when it is reached through another object, so the
        /// index still names a member of the root.
        /// </summary>
        [Test]
        public void AnIndexOnANestedContainerStillResolvesAgainstTheRoot()
        {
            TestCompiledVsInterpreted<IndexContextHolder, object>(
                    "Inner.Numbers[Zero]", new IndexContextHolder())
                .ResultEqualsTo(10);
        }

        /// <summary>
        /// The rows that were a wrong pick rather than a refusal: the root and the container both
        /// declare the name, so binding against the container silently used the other member.
        /// </summary>
        /// <remarks>
        /// <c>Ints[Count]</c> answered the root's <c>Count</c> (0, so element 10) interpreted and
        /// took <c>List&lt;int&gt;.Count</c> (3, out of range) compiled; <c>Numbers[Length]</c> the
        /// same through <c>int[].Length</c>. With a longer collection these are wrong <i>values</i>
        /// rather than exceptions.
        /// </remarks>
        [Test]
        public void AnIndexNamingAMemberTheContainerAlsoDeclaresPicksTheRootsOne()
        {
            var holder = new IndexContextHolder();

            TestCompiledVsInterpreted<IndexContextHolder, object>("Ints[Count]", holder)
                .ResultEqualsTo(10);
            TestCompiledVsInterpreted<IndexContextHolder, object>("Numbers[Length]", holder)
                .ResultEqualsTo(10);
        }

        /// <summary>
        /// Indexing the root itself is the one shape that always agreed, since there the container
        /// and <c>#this</c> are the same object. Kept so the fix is known not to have moved it.
        /// </summary>
        [Test]
        public void IndexingTheRootItselfIsUnchanged()
        {
            TestCompiledVsInterpreted<IndexContextHolder, object>("[Zero]", new IndexContextHolder())
                .ResultEqualsTo(100);
        }
    }
}

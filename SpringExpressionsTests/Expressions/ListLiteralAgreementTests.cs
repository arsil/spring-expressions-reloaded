using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Holds one stable instance, so a test can tell a list that was handed through from one that was copied.
    /// </summary>
    public class TypedListHolder
    {
        public List<int> Ints { get; } = new List<int> { 1, 2, 3 };
    }

    /// <summary>
    /// Whether the two backends agree on the runtime type of a list literal.
    /// </summary>
    /// <remarks>
    /// The same question as SetOperatorAgreementTests, one node earlier.
    /// <see cref="BaseCompiledTests.TestCompiledVsInterpreted{TResult}"/> compares the runtime type as well
    /// as the value: the compiled path gives a literal whose items share a type that item type, while the
    /// interpreter sees boxed values and can only build a list of object. The literal used to come back as
    /// an ArrayList from the interpreter and a List&lt;T&gt; from the compiler - two different classes, not
    /// merely two item types.
    /// </remarks>
    [TestFixture]
    public class ListLiteralAgreementTests : BaseCompiledTests
    {
        [Test]
        public void LiteralOfIntegers()
        {
            TestCompiledVsInterpreted<object>("{1,2,3}");
        }

        [Test]
        public void LiteralOfStrings()
        {
            TestCompiledVsInterpreted<object>("{'a','b'}");
        }

        /// <summary>
        /// No common item type, so the compiled path builds a plain list of object - already what the
        /// interpreter builds, so nothing is reprojected.
        /// </summary>
        [Test]
        public void LiteralOfMixedItemTypes()
        {
            TestCompiledVsInterpreted<object>("{1,'a'}");
        }

        /// <summary>
        /// Reading a list is not building one: the value is the caller's own object and must arrive
        /// unchanged, item type and reference identity intact.
        /// </summary>
        [Test]
        public void ReadingATypedListReturnsThatVeryInstance()
        {
            var holder = new TypedListHolder();

            var compiled = CompileGetter<TypedListHolder, object>("Ints").GetValue(holder);
            var interpreted = InterpretGetter<TypedListHolder, object>("Ints").GetValue(holder);

            Assert.AreSame(holder.Ints, compiled, "compiled path returned a copy");
            Assert.AreSame(holder.Ints, interpreted, "interpreted path returned a copy");
        }

        /// <summary>
        /// Asking for the item type the literal's items share gets exactly a List&lt;T&gt; - never the
        /// internal type the engine uses to mark a list it built.
        /// </summary>
        /// <remarks>
        /// Asserting the exact runtime type is the point: IsInstanceOf would pass on the marker, since it
        /// derives from List&lt;T&gt;. The same holds for a requested IList&lt;T&gt; or IEnumerable&lt;T&gt;,
        /// which the marker would satisfy too - hence a marked list is always copied on the way out.
        /// </remarks>
        [Test]
        public void TheInternalMarkerTypeNeverReachesTheCaller()
        {
            Assert.AreEqual(typeof(List<int>), CompileGetter<List<int>>("{1,2,3}").GetValue().GetType());
            Assert.AreEqual(typeof(List<int>), CompileGetter<IList<int>>("{1,2,3}").GetValue().GetType());
            Assert.AreEqual(typeof(List<int>), CompileGetter<IEnumerable<int>>("{1,2,3}").GetValue().GetType());
            Assert.AreEqual(typeof(List<object>), CompileGetter<object>("{1,2,3}").GetValue().GetType());
        }

        /// <summary>
        /// A literal is a list, so duplicates and order survive - the reprojection must not deduplicate the
        /// way the set one does.
        /// </summary>
        [Test]
        public void ReprojectionKeepsOrderAndDuplicates()
        {
            var result = (IList<object>)CompileGetter<object>("{3,1,3,2}").GetValue();

            Assert.AreEqual(new object[] { 3, 1, 3, 2 }, result);
        }
    }
}

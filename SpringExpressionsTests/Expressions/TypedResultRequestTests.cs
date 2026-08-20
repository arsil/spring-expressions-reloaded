using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Holds stable instances: identity assertions need the same collection back on every read.
    /// </summary>
    public class TypedRequestHolder
    {
        public List<int> Ints { get; } = new List<int> { 3, 1, 2 };
        public List<string> Names { get; } = new List<string> { "Ola", "Ala" };
    }

    /// <summary>
    /// A strongly typed request for the item type works on the interpreted path too, not only the
    /// compiled one.
    /// </summary>
    /// <remarks>
    /// The interpreter builds List&lt;object&gt; / HashSet&lt;object&gt; for every collection the
    /// engine constructs, and the interpreted getter used to be a bare (TResult) cast - so
    /// MustUseInterpreter with TResult = List&lt;int&gt; threw InvalidCastException where the compiled
    /// path satisfied the same request through ToTypedList. The interpreted getter now reprojects
    /// those two shapes - and only those - to a narrower requested type; everything else still goes
    /// through the plain cast, which is what keeps a read collection's reference identity.
    /// </remarks>
    [TestFixture]
    public class TypedResultRequestTests : BaseCompiledTests
    {
        [Test]
        public void BothBackendsSatisfyATypedListRequestOverAProcessor()
        {
            var holder = new TypedRequestHolder();

            TestCompiledVsInterpreted<TypedRequestHolder, List<int>>("Ints.reverse()", holder)
                .ResultEqualsTo(new List<int> { 2, 1, 3 });
        }

        [Test]
        public void InterpretedTypedRequestsGetAFreshTypedList()
        {
            var holder = new TypedRequestHolder();

            var asList = InterpretGetter<TypedRequestHolder, List<int>>("Ints.reverse()").GetValue(holder);
            Assert.AreEqual(typeof(List<int>), asList.GetType());
            Assert.AreEqual(new[] { 2, 1, 3 }, asList);

            var asInterface = InterpretGetter<TypedRequestHolder, IList<int>>("Ints.reverse()").GetValue(holder);
            Assert.AreEqual(typeof(List<int>), asInterface.GetType());

            var asEnumerable = InterpretGetter<TypedRequestHolder, IEnumerable<int>>("Ints.sort()").GetValue(holder);
            Assert.AreEqual(typeof(List<int>), asEnumerable.GetType());
            Assert.AreEqual(new[] { 1, 2, 3 }, asEnumerable);
        }

        /// <summary>
        /// Reading a collection is not building one: the cast satisfies the request first, so the
        /// caller gets their own instance back from both backends, never a copy.
        /// </summary>
        [Test]
        public void ReadCollectionKeepsItsIdentityUnderATypedRequest()
        {
            var holder = new TypedRequestHolder();

            Assert.AreSame(holder.Ints,
                InterpretGetter<TypedRequestHolder, List<int>>("Ints").GetValue(holder));
            Assert.AreSame(holder.Ints,
                CompileGetter<TypedRequestHolder, List<int>>("Ints").GetValue(holder));
        }

        [Test]
        public void SetOperatorSatisfiesATypedSetRequestFromBothBackends()
        {
            TestCompiledVsInterpreted<HashSet<int>>("{1,2} + {3}")
                .ResultEqualsTo(new HashSet<int> { 1, 2, 3 });

            var asInterface = InterpretGetter<ISet<int>>("{1,2} + {3}").GetValue();
            Assert.AreEqual(typeof(HashSet<int>), asInterface.GetType());
        }

        /// <summary>
        /// The list reprojection keeps order and duplicates - it is a copy, not a set.
        /// </summary>
        [Test]
        public void ListLiteralSatisfiesATypedListRequestFromBothBackends()
        {
            TestCompiledVsInterpreted<List<int>>("{3,1,3,2}")
                .ResultEqualsTo(new List<int> { 3, 1, 3, 2 });
        }

        /// <summary>
        /// The request narrows only where object was asked for nothing: an object request still gets
        /// the List&lt;object&gt; the interpreter built, uncopied.
        /// </summary>
        [Test]
        public void ObjectRequestsStayObjectTyped()
        {
            var holder = new TypedRequestHolder();

            var interpreted = InterpretGetter<TypedRequestHolder, object>("Ints.reverse()").GetValue(holder);

            Assert.AreEqual(typeof(List<object>), interpreted.GetType());
        }

        /// <summary>
        /// Items that cannot become the requested type still fail loudly; the reprojection converts
        /// shapes, never values.
        /// </summary>
        [Test]
        public void UnconvertibleItemsStillThrow()
        {
            var holder = new TypedRequestHolder();
            var getter = InterpretGetter<TypedRequestHolder, List<int>>("Names.reverse()");

            Assert.Throws<System.InvalidCastException>(() => getter.GetValue(holder));
        }
    }
}

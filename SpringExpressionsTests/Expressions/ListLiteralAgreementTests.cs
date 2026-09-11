using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

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
            Assert.AreEqual(typeof(List<int>), CompileGetter<object>("{1,2,3}").GetValue().GetType());
        }

        /// <summary>
        /// A literal keeps the item type its items share, on both backends, even when nothing narrower
        /// was asked for - so an integer literal is a List&lt;int&gt; and not a List&lt;object&gt;.
        /// </summary>
        /// <remarks>
        /// The interpreter reaches that item type from the items' runtime types and the compiled path
        /// from their static ones, and the compiled path declines the literal whenever those two could
        /// differ - see AnElementWhoseRuntimeTypeCouldBeNarrowerIsNotCompiled. This is the one place in
        /// the engine where a collection it built is not object-typed, and it is possible here because
        /// a literal's items are written down: it always has at least one, and each one's static type
        /// is right there.
        /// </remarks>
        [Test]
        public void ALiteralKeepsItsItemTypeOnBothBackends()
        {
            Assert.AreEqual(typeof(List<int>),
                CompileGetter<object>("{1,2,3}").GetValue().GetType());
            Assert.AreEqual(typeof(List<int>),
                InterpretGetter<object>("{1,2,3}").GetValue().GetType());

            Assert.AreEqual(typeof(List<string>),
                CompileGetter<object>("{'a','b'}").GetValue().GetType());
            Assert.AreEqual(typeof(List<string>),
                InterpretGetter<object>("{'a','b'}").GetValue().GetType());
        }

        /// <summary>
        /// Items with no common type leave the literal object-typed, on both backends.
        /// </summary>
        [Test]
        public void AMixedLiteralIsObjectTypedOnBothBackends()
        {
            Assert.AreEqual(typeof(List<object>),
                CompileGetter<object>("{1,'a'}").GetValue().GetType());
            Assert.AreEqual(typeof(List<object>),
                InterpretGetter<object>("{1,'a'}").GetValue().GetType());
        }

        /// <summary>
        /// A literal is a list, so duplicates and order survive.
        /// </summary>
        [Test]
        public void ReprojectionKeepsOrderAndDuplicates()
        {
            var result = (IList<int>)CompileGetter<object>("{3,1,3,2}").GetValue();

            Assert.AreEqual(new List<int> { 3, 1, 3, 2 }, result);
        }

        /// <summary>
        /// A typed request is satisfied by both backends - the compiled path keeps its List&lt;T&gt;, the
        /// interpreted one reprojects its List&lt;object&gt; - and both land on exactly a List&lt;T&gt;.
        /// </summary>
        [Test]
        public void TypedRequestsAgreeOnAnIntegerLiteral()
        {
            var result = TestCompiledVsInterpreted<List<int>>("{1,2,3}").Result;

            Assert.AreEqual(typeof(List<int>), result.GetType());
            Assert.AreEqual(new List<int> { 1, 2, 3 }, result);

            Assert.AreEqual(typeof(List<int>),
                TestCompiledVsInterpreted<IList<int>>("{1,2,3}").Result.GetType());
            Assert.AreEqual(typeof(List<int>),
                TestCompiledVsInterpreted<IEnumerable<int>>("{1,2,3}").Result.GetType());
        }

        [Test]
        public void TypedRequestsAgreeOnAStringLiteral()
        {
            var result = TestCompiledVsInterpreted<List<string>>("{'a','b'}").Result;

            Assert.AreEqual(typeof(List<string>), result.GetType());
            Assert.AreEqual(new List<string> { "a", "b" }, result);
        }

        /// <summary>
        /// Mixed item types have no common T, so object items are all that can be asked for - and both
        /// backends satisfy that request with the same List&lt;object&gt;, order intact.
        /// </summary>
        [Test]
        public void TypedRequestOnAMixedLiteralTakesObjectItems()
        {
            var result = TestCompiledVsInterpreted<List<object>>("{1,'a'}").Result;

            Assert.AreEqual(typeof(List<object>), result.GetType());
            Assert.AreEqual(new List<object> { 1, "a" }, result);
        }

        // ---------- where the two backends could not agree, the literal is not compiled ----------

        /// <summary>
        /// An element whose declared type the runtime can narrow has no compiled form, because the
        /// interpreter would infer a different item type from the value.
        /// </summary>
        /// <remarks>
        /// <c>Anything</c> is declared <c>object</c> and holds an <c>int</c>: the compiled path would
        /// unify to <c>object</c> and the interpreter to <c>int</c>. Rather than pick one, the compiled
        /// path stands aside so that only the interpreter runs and there is nothing to disagree with -
        /// the same shape as the overload gate, and as item 21's ruling on comparisons.
        ///
        /// The interpreter's answer is asserted beside the refusal so this cannot pass because the
        /// expression is meaningless.
        /// </remarks>
        [Test]
        public void AnElementWhoseRuntimeTypeCouldBeNarrowerIsNotCompiled()
        {
            var holder = new NarrowableElementHolder();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<NarrowableElementHolder, object>("{Anything, Anything}"));

            Assert.AreEqual(typeof(List<int>),
                InterpretGetter<NarrowableElementHolder, object>("{Anything, Anything}")
                    .GetValue(holder).GetType());
        }

        /// <summary>
        /// A nullable element likewise: boxing a nullable yields the underlying type, never a
        /// Nullable&lt;T&gt;, so the compiled path's Nullable&lt;int&gt; and the interpreter's int could
        /// never agree.
        /// </summary>
        [Test]
        public void ANullableElementIsNotCompiled()
        {
            var holder = new NarrowableElementHolder();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<NarrowableElementHolder, object>("{MaybeNumber, MaybeNumber}"));

            Assert.AreEqual(typeof(List<int>),
                InterpretGetter<NarrowableElementHolder, object>("{MaybeNumber, MaybeNumber}")
                    .GetValue(holder).GetType());
        }

        /// <summary>
        /// And a base-declared element holding a derived value - the plainest form of the same thing.
        /// </summary>
        [Test]
        public void ABaseDeclaredElementIsNotCompiled()
        {
            var holder = new NarrowableElementHolder();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<NarrowableElementHolder, object>("{AsBase, AsBase}"));

            Assert.AreEqual(typeof(List<DerivedElement>),
                InterpretGetter<NarrowableElementHolder, object>("{AsBase, AsBase}")
                    .GetValue(holder).GetType());
        }

        /// <summary>
        /// A sealed reference type cannot be narrowed, so a literal of strings compiles - which is what
        /// stops the rule above from being "decline every reference type".
        /// </summary>
        [Test]
        public void ASealedElementTypeStillCompiles()
        {
            var holder = new NarrowableElementHolder();

            Assert.AreEqual(typeof(List<string>),
                CompileGetter<NarrowableElementHolder, object>("{Label, Label}")
                    .GetValue(holder).GetType());
            Assert.AreEqual(typeof(List<string>),
                InterpretGetter<NarrowableElementHolder, object>("{Label, Label}")
                    .GetValue(holder).GetType());
        }

        /// <summary>
        /// A null element is not asked about: it contributes no runtime type, so both backends take the
        /// item type from the other elements and reach the same answer.
        /// </summary>
        [Test]
        public void ANullElementDoesNotStopCompilation()
        {
            var holder = new NarrowableElementHolder();

            Assert.AreEqual(typeof(List<string>),
                CompileGetter<NarrowableElementHolder, object>("{Label, null}")
                    .GetValue(holder).GetType());
            Assert.AreEqual(typeof(List<string>),
                InterpretGetter<NarrowableElementHolder, object>("{Label, null}")
                    .GetValue(holder).GetType());
        }

        /// <summary>
        /// A null beside a value type has nowhere to live, so the literal falls to object on both.
        /// </summary>
        [Test]
        public void ANullBesideAValueTypeFallsToObjectOnBothBackends()
        {
            Assert.AreEqual(typeof(List<object>),
                CompileGetter<object>("{1, null}").GetValue().GetType());
            Assert.AreEqual(typeof(List<object>),
                InterpretGetter<object>("{1, null}").GetValue().GetType());
        }

        /// <summary>
        /// The cost of the rule, stated rather than hidden: a collection is a non-sealed reference type,
        /// so a literal holding one has no compiled form. The interpreter serves it.
        /// </summary>
        [Test]
        public void ALiteralHoldingACollectionIsNotCompiled()
        {
            var holder = new TypedListHolder();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<TypedListHolder, object>("{Ints}"));

            var interpreted = (IList)InterpretGetter<TypedListHolder, object>("{Ints}").GetValue(holder);

            Assert.AreSame(holder.Ints, interpreted[0]);
        }
    }

    /// <summary>
    /// Elements whose declared type is wider than the value they hold, and one that is not.
    /// </summary>
    public class NarrowableElementHolder
    {
        public object Anything { get; set; } = 45;
        public int? MaybeNumber { get; set; } = 5;
        public BaseElement AsBase { get; set; } = new DerivedElement();
        public string Label { get; set; } = "x";
    }

    public class BaseElement { }

    public class DerivedElement : BaseElement { }
}

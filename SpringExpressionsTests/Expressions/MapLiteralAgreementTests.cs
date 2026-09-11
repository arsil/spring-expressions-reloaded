using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Holds one stable instance, so a test can tell a dictionary that was handed through from one that
    /// was copied.
    /// </summary>
    public class TypedMapHolder
    {
        public Dictionary<string, int> Map { get; } = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
    }

    /// <summary>
    /// Whether the two backends agree on the runtime type of a map literal.
    /// </summary>
    /// <remarks>
    /// The same question as ListLiteralAgreementTests, for the third collection kind. The literal used to
    /// have three shapes: Hashtable from the interpreter, Dictionary&lt;K,V&gt; from the compiled path for
    /// uniformly typed entries, and Hashtable again from its mixed-entry branch. Now the interpreter and
    /// the mixed branch build Dictionary&lt;object, object&gt;, the uniform branch keeps its entry types,
    /// and the compiled root is reprojected to match at the boundary.
    /// </remarks>
    [TestFixture]
    public class MapLiteralAgreementTests : BaseCompiledTests
    {
        [Test]
        public void LiteralOfUniformEntries()
        {
            var result = TestCompiledVsInterpreted<object>("#{'a' : 1, 'b' : 2}").Result;

            // Uniform entries keep both component types on both backends since the map-literal rule.
            Assert.AreEqual(typeof(Dictionary<string, int>), result.GetType());
            Assert.AreEqual(new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }, result);
        }

        /// <summary>
        /// Mixed entry types leave the compiled path no common KeyValuePair type, so it builds a
        /// dictionary of object directly - already what the interpreter builds.
        /// </summary>
        [Test]
        public void LiteralOfMixedEntries()
        {
            var result = TestCompiledVsInterpreted<object>("#{'a' : 1, 2 : 'b'}").Result;

            Assert.AreEqual(typeof(Dictionary<object, object>), result.GetType());
            Assert.AreEqual(new Dictionary<object, object> { { "a", 1 }, { 2, "b" } }, result);
        }

        /// <summary>
        /// Keys and values unify independently: uniform keys survive mixed values, so the compiled tree
        /// keeps Dictionary&lt;int, object&gt; - which an object root still reshapes to the dictionary of
        /// object the interpreter builds, and a typed request receives from both backends.
        /// </summary>
        [Test]
        public void LiteralOfUniformKeysAndMixedValues()
        {
            var result = TestCompiledVsInterpreted<object>("#{1 : 'a', 2 : 5}").Result;

            // The keys are uniform and survive; the values are not and fall to object. Each component
            // is unified on its own, so a mismatch in one does not collapse the other.
            Assert.AreEqual(typeof(Dictionary<int, object>), result.GetType());
            Assert.AreEqual(new Dictionary<int, object> { { 1, "a" }, { 2, 5 } }, result);

            var typed = TestCompiledVsInterpreted<Dictionary<int, object>>("#{1 : 'a', 2 : 5}").Result;

            Assert.AreEqual(typeof(Dictionary<int, object>), typed.GetType());
            Assert.AreEqual(new Dictionary<int, object> { { 1, "a" }, { 2, 5 } }, typed);
        }

        /// <summary>
        /// The mirror case: uniform values survive mixed keys.
        /// </summary>
        [Test]
        public void LiteralOfMixedKeysAndUniformValues()
        {
            var result = TestCompiledVsInterpreted<object>("#{1 : 'a', 'x' : 'b'}").Result;

            Assert.AreEqual(typeof(Dictionary<object, string>), result.GetType());
            Assert.AreEqual(new Dictionary<object, string> { { 1, "a" }, { "x", "b" } }, result);

            var typed = TestCompiledVsInterpreted<Dictionary<object, string>>("#{1 : 'a', 'x' : 'b'}").Result;

            Assert.AreEqual(typeof(Dictionary<object, string>), typed.GetType());
            Assert.AreEqual(new Dictionary<object, string> { { 1, "a" }, { "x", "b" } }, typed);
        }

        /// <summary>
        /// A typed request is satisfied by both backends - the compiled path keeps its
        /// Dictionary&lt;K,V&gt;, the interpreted one reprojects its Dictionary&lt;object, object&gt; -
        /// and both land on exactly a Dictionary&lt;K,V&gt;.
        /// </summary>
        [Test]
        public void TypedRequestsAgreeOnAUniformLiteral()
        {
            var result = TestCompiledVsInterpreted<Dictionary<string, int>>("#{'a' : 1, 'b' : 2}").Result;

            Assert.AreEqual(typeof(Dictionary<string, int>), result.GetType());
            Assert.AreEqual(new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }, result);

            Assert.AreEqual(typeof(Dictionary<string, int>),
                TestCompiledVsInterpreted<IDictionary<string, int>>("#{'a' : 1, 'b' : 2}").Result.GetType());
        }

        /// <summary>
        /// Reading a dictionary is not building one: the value is the caller's own object and must
        /// arrive unchanged, entry types and reference identity intact - under an object request and
        /// under a typed one.
        /// </summary>
        [Test]
        public void ReadingATypedDictionaryReturnsThatVeryInstance()
        {
            var holder = new TypedMapHolder();

            Assert.AreSame(holder.Map,
                CompileGetter<TypedMapHolder, object>("Map").GetValue(holder), "compiled path returned a copy");
            Assert.AreSame(holder.Map,
                InterpretGetter<TypedMapHolder, object>("Map").GetValue(holder), "interpreted path returned a copy");

            Assert.AreSame(holder.Map,
                CompileGetter<TypedMapHolder, Dictionary<string, int>>("Map").GetValue(holder));
            Assert.AreSame(holder.Map,
                InterpretGetter<TypedMapHolder, Dictionary<string, int>>("Map").GetValue(holder));
        }

        // ---------- the literal rule, asked of each component ----------

        /// <summary>
        /// A map literal keeps its entry types on both backends when nothing narrower was requested -
        /// the list literal's rule, applied to keys and values independently.
        /// </summary>
        [Test]
        public void AMapLiteralKeepsItsEntryTypesOnBothBackends()
        {
            Assert.AreEqual(typeof(Dictionary<string, int>),
                CompileGetter<object>("#{'a' : 1, 'b' : 2}").GetValue().GetType());
            Assert.AreEqual(typeof(Dictionary<string, int>),
                InterpretGetter<object>("#{'a' : 1, 'b' : 2}").GetValue().GetType());
        }

        /// <summary>
        /// A component whose declared type the runtime can narrow has no compiled form, because the
        /// interpreter would infer a different type for it.
        /// </summary>
        /// <remarks>
        /// <c>Anything</c> is declared <c>object</c> and holds an <c>int</c>. The interpreter's answer
        /// is asserted beside the refusal, so this cannot pass because the expression is meaningless.
        /// </remarks>
        [Test]
        public void AValueWhoseRuntimeTypeCouldBeNarrowerIsNotCompiled()
        {
            var holder = new NarrowableEntryHolder();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<NarrowableEntryHolder, object>("#{1 : Anything}"));

            Assert.AreEqual(typeof(Dictionary<int, int>),
                InterpretGetter<NarrowableEntryHolder, object>("#{1 : Anything}")
                    .GetValue(holder).GetType());
        }

        /// <summary>
        /// And the same on the key side, which is the half a whole-pair rule would have missed.
        /// </summary>
        [Test]
        public void AKeyWhoseRuntimeTypeCouldBeNarrowerIsNotCompiled()
        {
            var holder = new NarrowableEntryHolder();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<NarrowableEntryHolder, object>("#{Anything : 1}"));

            Assert.AreEqual(typeof(Dictionary<int, int>),
                InterpretGetter<NarrowableEntryHolder, object>("#{Anything : 1}")
                    .GetValue(holder).GetType());
        }

        /// <summary>
        /// A null component is declined too, and deliberately - unlike the list literal, where a null
        /// element is exempted.
        /// </summary>
        /// <remarks>
        /// DO NOT "FIX" THIS BY EXEMPTING object. By the time an entry is a KeyValuePair&lt;object, T&gt;
        /// the difference between "a null was written here" and "this component is genuinely
        /// object-typed" is gone, so exempting object would let <c>#{Anything : 1}</c> through - where
        /// the compiled path says object and the interpreter says int. The cost is this one shape,
        /// which both backends would in fact have agreed on, and the interpreter serves it.
        /// </remarks>
        [Test]
        public void ANullComponentIsDeclinedEvenThoughTheBackendsWouldAgree()
        {
            Assert.Throws<CompileErrorException>(() => CompileGetter<object>("#{1 : null}"));

            Assert.AreEqual(typeof(Dictionary<object, object>),
                InterpretGetter<object>("#{1 : null}").GetValue().GetType());
        }

        /// <summary>
        /// The cost, stated rather than hidden: a collection is a non-sealed reference type, so a map
        /// literal holding one has no compiled form. The interpreter serves it.
        /// </summary>
        [Test]
        public void AMapLiteralHoldingACollectionIsNotCompiled()
        {
            Assert.Throws<CompileErrorException>(() => CompileGetter<object>("#{1 : {1,2,3}}"));

            var interpreted = (IDictionary)InterpretGetter<object>("#{1 : {1,2,3}}").GetValue();

            Assert.AreEqual(typeof(List<int>), interpreted[1].GetType());
        }
    }

    /// <summary>
    /// A component whose declared type is wider than the value it holds.
    /// </summary>
    public class NarrowableEntryHolder
    {
        public object Anything { get; set; } = 45;
    }
}

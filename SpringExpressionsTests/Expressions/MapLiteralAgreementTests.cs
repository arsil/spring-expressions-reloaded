using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

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

            Assert.AreEqual(typeof(Dictionary<object, object>), result.GetType());
            Assert.AreEqual(new Dictionary<object, object> { { "a", 1 }, { "b", 2 } }, result);
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

            Assert.AreEqual(typeof(Dictionary<object, object>), result.GetType());
            Assert.AreEqual(new Dictionary<object, object> { { 1, "a" }, { 2, 5 } }, result);

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

            Assert.AreEqual(typeof(Dictionary<object, object>), result.GetType());
            Assert.AreEqual(new Dictionary<object, object> { { 1, "a" }, { "x", "b" } }, result);

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
    }
}

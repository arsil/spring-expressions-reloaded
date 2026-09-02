using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A key a dictionary does not hold reads as nothing, not as an exception.
    /// </summary>
    /// <remarks>
    /// <p>
    /// This was the last row of the evaluation sweep: <c>Map['a']</c> on a dictionary without that key
    /// threw <see cref="KeyNotFoundException"/> compiled and answered null interpreted.
    /// </p>
    /// <p>
    /// <b>The interpreter was not deciding anything.</b> <c>IndexerNode.Get</c> dispatches on
    /// <c>context is IDictionary</c> - the non-generic interface, which a
    /// <c>Dictionary&lt;K, V&gt;</c> also implements - and that indexer is <c>object this[object]</c>,
    /// which returns null for a missing key where <c>IDictionary&lt;K, V&gt;</c>'s throws. So it read
    /// every dictionary through the pre-generics interface and got <c>Hashtable</c> behaviour for free.
    /// The same mechanism as the collection-processor split.
    /// </p>
    /// <p>
    /// The compiled path emits <c>TryGetValue</c> into a <c>V?</c> now. What that costs was measured
    /// rather than assumed, and it is one thing: see
    /// <c>ANonNullableTypedRequestIsTheWholeCost</c>. Open-issues item 22.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class MissingDictionaryKeyTests : BaseCompiledTests
    {
        public class Root
        {
            public Dictionary<string, int> Ints { get; set; } = new Dictionary<string, int> { { "a", 45 } };
            public Dictionary<string, string> Strings { get; set; } = new Dictionary<string, string> { { "a", "x" } };
            public Dictionary<string, int?> Nullables { get; set; } = new Dictionary<string, int?> { { "a", 45 } };
            public Dictionary<int, string> IntKeyed { get; set; } = new Dictionary<int, string> { { 1, "x" } };
            public IDictionary<string, int> Declared { get; set; } = new Dictionary<string, int> { { "a", 45 } };
            public SortedDictionary<string, int> Sorted { get; set; } = new SortedDictionary<string, int> { { "a", 45 } };
            public Hashtable Legacy { get; set; } = new Hashtable { { "a", 45 } };
            public Dictionary<string, int> Empty { get; set; } = new Dictionary<string, int>();

            public int Calls;
            public Dictionary<string, int> Counted() { Calls++; return Ints; }
            public string DoSomething(int n) { return "int:" + n; }
        }

        static void Both(string expression, object expected)
        {
            var root = new Root();

            Assert.AreEqual(expected,
                CompileGetter<Root, object>(expression).GetValue(root), "compiled: " + expression);
            Assert.AreEqual(expected,
                InterpretGetter<Root, object>(expression).GetValue(root), "interpreted: " + expression);
        }

        static void BothNull(string expression)
        {
            var root = new Root();

            Assert.IsNull(CompileGetter<Root, object>(expression).GetValue(root), "compiled: " + expression);
            Assert.IsNull(InterpretGetter<Root, object>(expression).GetValue(root), "interpreted: " + expression);
        }

        [Test]
        public void AMissingKeyReadsAsNothing()
        {
            BothNull("Ints['zz']");
            BothNull("Strings['zz']");
            BothNull("Nullables['zz']");
            BothNull("IntKeyed[9]");
            BothNull("Declared['zz']");
            BothNull("Sorted['zz']");
            BothNull("Empty['a']");
        }

        [Test]
        public void APresentKeyStillReadsItsValueAtItsOwnType()
        {
            Both("Ints['a']", 45);
            Both("Strings['a']", "x");
            Both("Nullables['a']", 45);
            Both("IntKeyed[1]", "x");
            Both("Declared['a']", 45);
            Both("Sorted['a']", 45);

            var root = new Root();
            Assert.AreEqual(typeof(int),
                CompileGetter<Root, object>("Ints['a']").GetValue(root).GetType(),
                "a present key boxes to the plain value type, so nothing downstream can tell");
        }

        /// <summary>
        /// A <see cref="Hashtable"/> keeps the accessor path and needs nothing - its own indexer already
        /// answers null - which is why it never appeared as a divergence.
        /// </summary>
        [Test]
        public void ANonGenericHashtableIsUnmoved()
        {
            Both("Legacy['a']", 45);
            BothNull("Legacy['zz']");
        }

        /// <summary>
        /// The missing key propagates through arithmetic, because the read is a nullable and nullable
        /// arithmetic is the engine's standing rule - nothing in, nothing out.
        /// </summary>
        [Test]
        public void AMissingKeyPropagatesThroughArithmetic()
        {
            Both("Ints['a'] + 1", 46);
            BothNull("Ints['zz'] + 1");

            Both("Ints['a'] == 45", true);
            Both("Ints['zz'] == 45", false);
            Both("Ints['a'] > 40", true);
            Both("Ints['a'].ToString()", "45");
        }

        /// <summary>
        /// An <c>int?</c> argument binds to an <c>int</c> parameter, so a call over a dictionary read
        /// keeps its compiled form - this engine is more permissive than C# here, which needs the cast.
        /// A missing key then throws on both backends, which it did before as well.
        /// </summary>
        [Test]
        public void ADictionaryReadStillBindsToAMethodArgument()
        {
            var root = new Root();

            Both("DoSomething(Ints['a'])", "int:45");

            Assert.Catch<Exception>(
                () => CompileGetter<Root, object>("DoSomething(Ints['zz'])").GetValue(root));
            Assert.Catch<Exception>(
                () => InterpretGetter<Root, object>("DoSomething(Ints['zz'])").GetValue(root));
        }

        /// <summary>
        /// The whole cost of the change, and it is one shape: a non-nullable typed request over the read
        /// loses its compiled form, because the nullable-request ruling refuses to deliver a nullable
        /// body as a non-nullable request.
        /// </summary>
        /// <remarks>
        /// It keeps answering - the fallback interprets it - so only a <c>MustCompile</c> caller is
        /// stopped, and the escapes are that ruling's own: ask for <c>int?</c>, or write the cast.
        /// Everything else measured kept its compiled form: arithmetic, comparison, equality, member
        /// access and method arguments.
        /// </remarks>
        [Test]
        public void ANonNullableTypedRequestIsTheWholeCost()
        {
            var root = new Root();

            Assert.Catch<CompileErrorException>(
                () => Expression.ParseGetter<Root, int>("Ints['a']", EvaluationMode.MustCompile));

            // still answers, through the interpreter
            Assert.AreEqual(45, Expression.ParseGetter<Root, int>("Ints['a']").GetValue(root));

            // and both escapes compile
            Assert.AreEqual(45,
                Expression.ParseGetter<Root, int?>("Ints['a']", EvaluationMode.MustCompile).GetValue(root));
            Assert.AreEqual(45,
                Expression.ParseGetter<Root, int>("Ints['a'] as int", EvaluationMode.MustCompile).GetValue(root));
        }

        [Test]
        public void TheDictionaryIsEvaluatedOnce()
        {
            var root = new Root();

            CompileGetter<Root, object>("Counted()['a']").GetValue(root);
            Assert.AreEqual(1, root.Calls, "compiled");
        }

        /// <summary>
        /// The setter is untouched - writing a key is a different question, and a missing key is not an
        /// error there in the first place.
        /// </summary>
        [Test]
        public void TheSetterIsUnmoved()
        {
            var root = new Root();

            Expression.ParseSetter<Root, int>("Ints['a']", EvaluationMode.MustCompile).SetValue(root, 99);
            Assert.AreEqual(99, root.Ints["a"]);

            Expression.ParseSetter<Root, int>("Ints['new']", EvaluationMode.MustCompile).SetValue(root, 7);
            Assert.AreEqual(7, root.Ints["new"]);
        }

        /// <summary>
        /// A null index keeps the legacy exact-match quirk rather than being handed to
        /// <c>TryGetValue</c>, which throws <see cref="ArgumentNullException"/> for a
        /// <c>Dictionary</c>. Both backends throw, as they did before.
        /// </summary>
        [Test]
        public void ANullIndexIsUnmoved()
        {
            var root = new Root();

            Assert.Catch<Exception>(() => CompileGetter<Root, object>("Ints[null]").GetValue(root));
            Assert.Catch<Exception>(() => InterpretGetter<Root, object>("Ints[null]").GetValue(root));
        }

        /// <summary>
        /// The pure-language spelling, needing no root object at all.
        /// </summary>
        [Test]
        public void TheLanguageCanSayItWithoutAnyObject()
        {
            Assert.IsNull(ExpressionEvaluator.GetValue(null, "#{'a':1}['b']"));
            Assert.AreEqual(1, ExpressionEvaluator.GetValue(null, "#{'a':1}['a']"));
        }
    }
}

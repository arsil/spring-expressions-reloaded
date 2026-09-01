using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class StringSourceHolder
    {
        public string Text { get; set; } = "cab";
        public string Repeated { get; set; } = "banana";
        public List<string> Names { get; set; } = new List<string> { "b", "a" };
    }

    /// <summary>
    /// A string reaching a collection operation is its characters, on both backends.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>This engine has always half-said so.</b> Upstream Spring.NET's <c>ProjectionNode</c> asks for
    /// <c>IEnumerable</c>, which a string is, so <c>Text.!{…}</c> has enumerated characters since before
    /// the fork - while <c>ICollectionProcessor.Process</c> takes <c>ICollection</c>, which a string is
    /// not, so <c>Text.sort()</c> was refused. Two interface names typed in two files, not a decision.
    /// The compiled path then accepted a string for every processor with an open-generic implementation,
    /// and that mismatch was the divergence.
    /// </p>
    /// <p>
    /// <b>C# reads a string the same way</b>: <c>"cab".Min()</c> is <c>'a'</c>, <c>"cab".Distinct()</c>
    /// enumerates characters, because <c>string</c> is an <c>IEnumerable&lt;char&gt;</c>. Only
    /// <c>Sum()</c> is missing there, for want of a <c>char</c> overload.
    /// </p>
    /// <p>
    /// <b>Refusing everywhere was the alternative and was measured before being dropped.</b> Nothing in
    /// either suite uses a string as a collection source, so it would have cost no working expression -
    /// but it meant deviating from C# and from upstream at the same time, and changing both backends
    /// rather than one. It is a fair reading and stays on the record: someone who finds
    /// <c>Notes.count()</c> answering a character count surprising is not wrong, and <c>Notes.Length</c>
    /// is what they wanted.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class StringAsCollectionTests : BaseCompiledTests
    {
        [Test]
        public void TheProcessorsSeeTheCharacters()
        {
            var root = new StringSourceHolder();

            TestCompiledVsInterpreted<StringSourceHolder, object>("Text.count()", root).ResultEqualsTo(3);

            var sorted = (List<object>)CompileGetter<StringSourceHolder, object>("Text.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { 'a', 'b', 'c' }, sorted);

            var sortedInterpreted =
                (List<object>)InterpretGetter<StringSourceHolder, object>("Text.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { 'a', 'b', 'c' }, sortedInterpreted);

            var reversed = (List<object>)CompileGetter<StringSourceHolder, object>("Text.reverse()").GetValue(root);
            Assert.AreEqual(new List<object> { 'b', 'a', 'c' }, reversed);

            var distinct =
                (List<object>)CompileGetter<StringSourceHolder, object>("Repeated.distinct()").GetValue(root);
            Assert.AreEqual(new List<object> { 'b', 'a', 'n' }, distinct);
        }

        /// <summary>
        /// <c>min()</c> and <c>max()</c> answer characters too. The compiled processors have no
        /// <c>char</c> entry in their type dictionaries, so they refuse and the interpreter serves the
        /// expression - which is agreement, since only one backend ever runs.
        /// </summary>
        [Test]
        public void MinAndMaxAnswerCharactersThroughTheInterpreter()
        {
            var root = new StringSourceHolder();

            Assert.AreEqual('a', Expression.ParseGetter<StringSourceHolder, object>("Text.min()").GetValue(root));
            Assert.AreEqual('c', Expression.ParseGetter<StringSourceHolder, object>("Text.max()").GetValue(root));

            // and C# says the same
            Assert.AreEqual('a', "cab".Min());
            Assert.AreEqual('c', "cab".Max());
        }

        /// <summary>
        /// Projections and selections already did this, on both backends and since before the fork. They
        /// are asserted here beside the processors so the two halves cannot drift apart again.
        /// </summary>
        [Test]
        public void ProjectionsAndSelectionsAlreadySawTheCharacters()
        {
            var root = new StringSourceHolder();

            var projected = (List<object>)CompileGetter<StringSourceHolder, object>("Text.!{#this}").GetValue(root);
            Assert.AreEqual(new List<object> { 'c', 'a', 'b' }, projected);

            var projectedInterpreted =
                (List<object>)InterpretGetter<StringSourceHolder, object>("Text.!{#this}").GetValue(root);
            Assert.AreEqual(new List<object> { 'c', 'a', 'b' }, projectedInterpreted);

            TestCompiledVsInterpreted<StringSourceHolder, object>("Text.^{#this != null}", root)
                .ResultEqualsTo('c');
        }

        /// <summary>
        /// A real collection of strings is untouched - the change is about a string as the source, not
        /// about strings as items.
        /// </summary>
        [Test]
        public void ACollectionOfStringsIsUnaffected()
        {
            var root = new StringSourceHolder();

            TestCompiledVsInterpreted<StringSourceHolder, object>("Names.count()", root).ResultEqualsTo(2);

            var sorted = (List<object>)CompileGetter<StringSourceHolder, object>("Names.sort()").GetValue(root);
            Assert.AreEqual(new List<object> { "a", "b" }, sorted);
        }

        /// <summary>
        /// And the string's own members still win where they exist - a caller wanting the length writes
        /// <c>Length</c>, which is the answer to the surprise this ruling accepts.
        /// </summary>
        [Test]
        public void TheStringsOwnMembersStillWork()
        {
            var root = new StringSourceHolder();

            TestCompiledVsInterpreted<StringSourceHolder, object>("Text.Length", root).ResultEqualsTo(3);
            TestCompiledVsInterpreted<StringSourceHolder, object>("Text.ToUpper()", root).ResultEqualsTo("CAB");
        }
    }
}

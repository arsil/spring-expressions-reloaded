using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// What the interpreter's setter accepts for a collection-shaped property, by the <i>kind</i> of
    /// the target rather than its concrete type.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The conversion lives in inherited code (<c>TypeConversionUtils.ConvertValueIfNecessary</c>) and
    /// builds a new collection of the target's kind, converting each element on the way. Two things
    /// were wrong with it, and <b>both became reachable only because this fork changed what the engine
    /// hands out</b> - its collection operators return a BCL <c>HashSet&lt;T&gt;</c> where upstream
    /// returned the vendored <c>HybridSet</c>, and its list literals a <c>List&lt;object&gt;</c> where
    /// upstream returned an <c>ArrayList</c>.
    /// </p>
    /// <p>
    /// <b>A BCL set is not a non-generic <c>ICollection</c></b>, which every branch tested for. So a
    /// set arriving at the converter matched nothing: into an array property it fell through to the
    /// single-value case and produced a one-element array holding the set's <c>ToString()</c>,
    /// <i>silently</i>; into a list property it threw. Same blind spot as
    /// <c>ICollectionProcessor.Process</c>, one layer down.
    /// </p>
    /// <p>
    /// <b>And nothing could be assigned to a BCL set property at all</b> - the converter knew only the
    /// vendored set, which is all upstream had; <c>ISet&lt;T&gt;</c> postdates it.
    /// </p>
    /// <p>
    /// The compiled setter declines every conversion here and falls back, which is why these are
    /// interpreted-only. That gap is <c>_Docs/open-issues.md</c> item 2's last row.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class CollectionAssignmentTests
    {
        public class Root
        {
            public string[] ToArray { get; set; }

            public List<string> ToList { get; set; }

            public HashSet<string> ToSet { get; set; }

            public ISet<string> ToSetInterface { get; set; }

            public SpringCollections.Generic.ISet<string> ToVendoredSet { get; set; }

            public HashSet<int> ToIntSet { get; set; }

            public int[] ToIntArray { get; set; }

            public List<string> AList { get { return new List<string> { "a", "b" }; } }

            public string[] AnArray { get { return new[] { "a", "b" }; } }

            public ArrayList AnArrayList { get { return new ArrayList { "a", "b" }; } }

            public HashSet<string> ASet { get { return new HashSet<string> { "a", "b" }; } }

            public IEnumerable<string> ALazySequence
            {
                get { return new List<string> { "a", "b" }.ConvertAll(x => x); }
            }

            public string CommaDelimited { get { return "a,b"; } }
        }

        private static object Assign(string expression)
        {
            var root = new Root();

            Expression.ParseVoidExpression<Root>(
                expression, EvaluationMode.MustInterpret,
                SandboxPolicy.DangerouslyAllowEverything).Execute(root);

            var name = expression.Substring(0, expression.IndexOf(' '));

            return typeof(Root).GetProperty(name).GetValue(root, null);
        }

        private static List<string> Items(object collection)
        {
            var items = new List<string>();

            foreach (var item in (IEnumerable)collection)
                items.Add(item == null ? "null" : item.ToString());

            items.Sort();   // a set promises no order, and only the membership is under test here
            return items;
        }

        [Test]
        public void AnyCollectionKindConvertsIntoAnArrayProperty()
        {
            foreach (var source in new[] { "{'a','b'}", "AList", "AnArray", "AnArrayList", "ASet", "ALazySequence" })
            {
                var value = Assign("ToArray = " + source);

                Assert.IsInstanceOf<string[]>(value, source);
                CollectionAssert.AreEqual(new[] { "a", "b" }, Items(value), source);
            }
        }

        [Test]
        public void AnyCollectionKindConvertsIntoAListProperty()
        {
            foreach (var source in new[] { "{'a','b'}", "AList", "AnArray", "AnArrayList", "ASet", "ALazySequence" })
            {
                var value = Assign("ToList = " + source);

                Assert.IsInstanceOf<List<string>>(value, source);
                CollectionAssert.AreEqual(new[] { "a", "b" }, Items(value), source);
            }
        }

        [Test]
        public void AnyCollectionKindConvertsIntoASetProperty()
        {
            foreach (var target in new[] { "ToSet", "ToSetInterface" })
            {
                foreach (var source in new[] { "{'a','b'}", "AList", "AnArray", "AnArrayList", "ASet", "ALazySequence" })
                {
                    var value = Assign(target + " = " + source);

                    Assert.IsInstanceOf<HashSet<string>>(value, target + " = " + source);
                    CollectionAssert.AreEqual(new[] { "a", "b" }, Items(value), target + " = " + source);
                }
            }
        }

        [Test]
        public void TheVendoredSetKeepsItsOwnBranchAndItsOwnType()
        {
            // Untouched by the BCL branch added beside it: a vendored-set property still gets a
            // vendored HashedSet, which is what upstream did and what any consumer of that interface
            // expects.
            var value = Assign("ToVendoredSet = {'a','b'}");

            Assert.IsInstanceOf<SpringCollections.Generic.ISet<string>>(value);
            Assert.AreEqual("HashedSet`1", value.GetType().Name);
            CollectionAssert.AreEqual(new[] { "a", "b" }, Items(value));
        }

        [Test]
        public void ElementsAreConvertedOnTheWayIntoAnArrayOrASet()
        {
            // The conversion is per element, not a cast of the collection - which is also why an
            // emitted ToArray() could never reproduce this, and why the compiled setter declines.
            CollectionAssert.AreEqual(new[] { 1, 2 }, Items(Assign("ToIntArray = {'1','2'}")).ConvertAll(int.Parse));
            CollectionAssert.AreEqual(new[] { 1, 2 }, Items(Assign("ToIntSet = {'1','2'}")).ConvertAll(int.Parse));
        }

        [Test]
        public void AStringIsNotTakenApartAsASequenceOfCharacters()
        {
            // A string is enumerable, so widening the collection test had to exclude it: the array
            // branch has its own handling for a string - a comma-delimited list - and that has to keep
            // winning. Without the exclusion this would be ['a', ',', 'b'].
            var value = Assign("ToArray = CommaDelimited");

            CollectionAssert.AreEqual(new[] { "a", "b" }, Items(value));
        }

        [Test]
        public void ASetSourceIntoAnArrayIsConvertedRatherThanWrapped()
        {
            // The row that was silently wrong: a BCL set is not a non-generic ICollection, so it
            // missed every branch and landed in the single-value case - a one-element array holding
            // the set's ToString(). No exception, just a wrong answer.
            var value = (string[])Assign("ToArray = ASet");

            Assert.AreEqual(2, value.Length, "a set must be converted element by element, not wrapped");
            CollectionAssert.AreEqual(new[] { "a", "b" }, Items(value));
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// The two backends must reach the same outcome for the same expression over the same data - for
    /// thousands of expressions and three sets of data nobody wrote a test for.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <c>CompilationNeverLeaksTests</c> guards what escapes <i>compilation</i>. This is the layer below
    /// it, and it exists because two defects escaped at <i>evaluation</i> in one sitting, both silent,
    /// both data-dependent, and neither findable by anything else in the suite:
    /// </p>
    /// <list type="bullet">
    /// <item><description>
    /// <c>min()</c> over an empty <c>List&lt;int&gt;</c> threw <c>"Sequence contains no elements"</c>
    /// compiled and answered null interpreted. Compilation <i>succeeded</i> - emitting the call is
    /// valid, and emptiness is not knowable then - so the fallback, which catches
    /// <c>CompileErrorException</c> while building the delegate, was long finished. The exception went
    /// straight to the caller, and only when the data happened to be empty.
    /// </description></item>
    /// <item><description>
    /// <c>average()</c> over a <c>List&lt;uint&gt;</c> threw <c>InvalidCastException</c> compiled -
    /// <c>Cast&lt;long&gt;</c> unboxes, and a boxed <c>uint</c> is not a <c>long</c> - while the
    /// interpreter answered.
    /// </description></item>
    /// </list>
    /// <p>
    /// <b>The invariant is deliberately not "the same exception".</b> Several exception-type differences
    /// are documented and ruled - a non-nullable request failing as <c>InvalidOperationException</c>
    /// compiled against <c>NullReferenceException</c> interpreted, for one. What must not happen is
    /// <b>one backend answering while the other throws</b>, or both answering different values. Those
    /// are the shapes a caller cannot defend against, since which backend runs is not their choice.
    /// </p>
    /// <p>
    /// <b>Data variety is the point.</b> The corpus is shared with the compile-time sweep, but that one
    /// never evaluates, so a single root would do. Here the same expressions run against three roots:
    /// as-constructed, nulls-and-empties, and zeros-and-NaN. The first defect above needs an empty
    /// collection to appear at all.
    /// </p>
    /// <p>
    /// The ledger below is the part to keep current, and it is <b>counted</b> on purpose: a regression
    /// that inflates an existing class rather than adding a new one would otherwise slip past. The NaN
    /// ruling did exactly that - it took one class from 4 to 27 - and the count is what showed it.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class EvaluationNeverDivergesTests
    {
        /// <summary>
        /// The divergences this sweep finds, as <c>"COUNTx SURFACE :: OUTCOME"</c>. <b>Empty: the two
        /// backends agree on every row of the corpus.</b>
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>It started at 1,441 and reached zero.</b> Keeping it empty is the point of the fixture -
        /// a new row means either the backends disagree somewhere new, or a rule that was settled has
        /// come undone, and the list below is what was settled so that a reader knows which. Nothing
        /// here is a permanent exception: there are none left.
        /// </p>
        /// <p>
        /// The one thing an empty ledger does <b>not</b> mean is that the backends agree everywhere. It
        /// means they agree on the shapes this corpus generates, and every group below turned out wider
        /// than the ledger showed once somebody measured the surface by hand rather than reading the
        /// row count. <b>Prefer widening the corpus to reasoning about whether a shape is covered</b> -
        /// a source costs 16 generated expressions per root.
        /// </p>
        /// <p>What was settled, with the causes behind it:</p>
        /// <list type="bullet">
        /// <item><description>
        /// <b>All six comparison operators are done and have no rows here at all</b> - open-issues
        /// items 21 and 17, and this sweep is what found and then measured every one of them.
        /// <c>==</c> and <c>!=</c> went from 540 rows per operator to zero (item 21: the compiled path
        /// may only emit a comparison when the static types tell it which comparison to make, and
        /// otherwise refuses so the interpreter can answer from the runtime values); the four relational
        /// operators went from 116 rows to zero (item 17: a nullable holding nothing sorts before every
        /// value, like the other two kinds of nothing). Do not reintroduce a comparison fallback that
        /// always answers - the last one returned <c>false</c> for pairs it could not compare, and
        /// depended on string interning for the rest.
        /// </description></item>
        /// <item><description>
        /// <b>Arithmetic is done too and has no rows here</b> - <c>+ - * / % ^</c> with a null operand.
        /// The rule: <c>+</c> concatenates only when at least one operand is an actual string at run
        /// time, and otherwise a null propagates. It had to be null rather than <c>""</c> because the
        /// interpreter cannot tell two null strings from two null ints - only the compiled path can see
        /// that - so the answer that serves both is the one the rest of arithmetic already uses.
        /// A real string on either side still concatenates, which is nearly every use.
        /// </description></item>
        /// <item><description>
        /// <b>A null collection source is done and has no rows here</b> - it had 5, and 25 once the
        /// surface was measured rather than read off the ledger. A null collection has nothing in it, so
        /// it answers what the empty-collection ruling says "there is no answer" looks like: null, or
        /// <c>0</c> for <c>count()</c>. The interpreter said so already for the six processors that
        /// return a collection and the frozen suite pins it
        /// (<c>Assert.IsNull(GetValue(null, "sort()"))</c>); <c>min()</c>, <c>max()</c> and
        /// <c>average()</c> were missing the guard on both backends and crashed. <c>sum()</c> is the
        /// carve-out and still throws on both: its result type is the item type, so there is no null to
        /// return - see <c>MethodNode.NullSourceAnswer</c>.
        /// </description></item>
        /// <item><description>
        /// <b><c>sum()</c> used to be 19 of the 27 rows and is gone</b> - item 24, ruled as
        /// <i>sum() is a fold of '+'</i>. Worth knowing what those 19 were, because they are the shape
        /// of what widening a corpus reveals: 11 over a non-empty source (the <c>0d</c> accumulator),
        /// 6 over an empty one (nothing to seed from, so the item type had to be read from the source),
        /// and 2 an <c>OverflowException</c> class of their own, where
        /// <c>Enumerable.Sum(IEnumerable&lt;int&gt;)</c> is checked and a <c>double</c> accumulator
        /// cannot overflow. That last pair needed <i>data at the edge</i> to show at all - it appeared
        /// only when a <c>List&lt;int&gt;</c> holding <c>{int.MaxValue, 1}</c> joined the roots.
        /// </description></item>
        /// <item><description>
        /// <b><c>is</c> had 2 rows and is gone</b> - and it was never the nullable question the ledger
        /// made it look like. The compiled <c>is</c> emitted a <i>compile-time constant</i> from the
        /// static type, so it never looked at the value: 8 of 20 measured shapes diverged, including an
        /// <c>object</c> holding an int, a base-typed variable holding a derived instance, and a null
        /// string reported as <i>being</i> a string. It emits <c>LExpression.TypeIs</c> now, which C#
        /// compiles <c>is</c> to and which matches the interpreter on every row. The corpus generates
        /// one <c>is</c> shape, over an operand kind that happened to be a nullable, which is the whole
        /// reason it read as two nullable rows.
        /// </description></item>
        /// <item><description>
        /// <b>An indexer on a missing key was the last row</b> - <c>Map['a']</c>,
        /// <see cref="System.Collections.Generic.KeyNotFoundException"/> compiled against null
        /// interpreted. The interpreter was not deciding anything: <c>IndexerNode.Get</c> dispatches on
        /// <c>context is IDictionary</c>, the non-generic interface, whose <c>object this[object]</c>
        /// returns null for a missing key. The compiled path emits <c>TryGetValue</c> into a <c>V?</c>
        /// now - see <c>IndexerNode.TryCreateGenericDictionaryRead</c> for what that costs, which is one
        /// thing and was measured.
        /// </description></item>
        /// </list>
        /// </remarks>
        private static readonly string[] KnownDivergences = new string[0];

        [Test]
        public void TheBackendsAgreeOrTheDivergenceIsOneOfTheKnownOnes()
        {
            var roots = Roots();
            var found = new SortedDictionary<string, int>();
            var samples = new Dictionary<string, string>();
            var compared = 0;

            foreach (var expression in CompilationNeverLeaksTests.Corpus())
            {
                IGetterExpression<CompilationNeverLeaksTests.Root, object> compiled, interpreted;

                try
                {
                    compiled = Expression.ParseGetter<CompilationNeverLeaksTests.Root, object>(
                        expression, EvaluationMode.MustCompile);
                }
                catch (Exception)
                {
                    // no compiled form, so there is nothing to compare - an ordinary refusal, and the
                    // compile-time sweep's business rather than this one's
                    continue;
                }

                try
                {
                    interpreted = Expression.ParseGetter<CompilationNeverLeaksTests.Root, object>(
                        expression, EvaluationMode.MustInterpret);
                }
                catch (Exception)
                {
                    continue;
                }

                for (var v = 0; v < roots.Count; v++)
                {
                    compared++;

                    var outcome = Compare(compiled, interpreted, roots[v].Root,
                        out var compiledSide, out var interpretedSide);

                    if (outcome == null)
                        continue;

                    var key = SurfaceOf(expression) + "  ::  " + outcome;
                    found[key] = found.TryGetValue(key, out var n) ? n + 1 : 1;

                    if (!samples.ContainsKey(key))
                        samples[key] = expression + "   [" + roots[v].Name + "]"
                            + "   compiled=" + compiledSide + "   interpreted=" + interpretedSide;
                }
            }

            Assert.Greater(compared, 5000, "the sweep should be large enough to be worth running");

            AssertDivergencesAreTheKnownOnes(found, samples);
        }

        /// <summary>
        /// Null when the two agree; otherwise the kind of disagreement. Note what is <i>not</i> compared:
        /// two failures, whatever their exception types. Several of those differences are ruled.
        /// </summary>
        private static string Compare(
            IGetterExpression<CompilationNeverLeaksTests.Root, object> compiled,
            IGetterExpression<CompilationNeverLeaksTests.Root, object> interpreted,
            CompilationNeverLeaksTests.Root root,
            out string compiledSide,
            out string interpretedSide)
        {
            object compiledValue = null, interpretedValue = null;
            string compiledFailure = null, interpretedFailure = null;

            try { compiledValue = compiled.GetValue(root); }
            catch (Exception e) { compiledFailure = e.GetType().Name; }

            try { interpretedValue = interpreted.GetValue(root); }
            catch (Exception e) { interpretedFailure = e.GetType().Name; }

            compiledSide = compiledFailure ?? Render(compiledValue);
            interpretedSide = interpretedFailure ?? Render(interpretedValue);

            if (compiledFailure == null && interpretedFailure != null)
                return "compiled answered / interpreted threw " + interpretedFailure;

            if (compiledFailure != null && interpretedFailure == null)
                return "compiled threw " + compiledFailure + " / interpreted answered";

            if (compiledFailure != null)
                return null;   // both failed; the exception types may legitimately differ

            return compiledSide == interpretedSide
                ? null
                : "both answered, values differ";
        }

        private static void AssertDivergencesAreTheKnownOnes(
            SortedDictionary<string, int> found, Dictionary<string, string> samples)
        {
            var current = found.Select(kv => kv.Value + "x " + kv.Key).ToList();

            var unexpected = current.Where(k => !KnownDivergences.Contains(k)).ToList();
            var goneOrChanged = KnownDivergences.Where(k => !current.Contains(k)).ToList();

            var report = new StringBuilder();
            report.AppendLine("current table - paste over KnownDivergences to re-baseline:");
            foreach (var kv in found)
            {
                report.AppendLine("            \"" + kv.Value + "x " + kv.Key + "\",");
                report.AppendLine("                // e.g. " + samples[kv.Key]);
            }

            Assert.IsEmpty(
                unexpected,
                "a divergence appeared that is not in the ledger. Either the backends disagree somewhere "
                    + "new, or an existing class grew - the counts are there to catch the second:"
                    + Environment.NewLine + string.Join(Environment.NewLine, unexpected)
                    + Environment.NewLine + Environment.NewLine + report);

            Assert.IsEmpty(
                goneOrChanged,
                "these ledger rows no longer match - remove them if the divergence is fixed, or update "
                    + "the count:" + Environment.NewLine + string.Join(Environment.NewLine, goneOrChanged)
                    + Environment.NewLine + Environment.NewLine + report);
        }

        /// <summary>
        /// Three sets of data over the same properties. The first defect this fixture was written for
        /// needed an empty collection; the third set exists because NaN and zero reach code the other
        /// two do not.
        /// </summary>
        private static List<NamedRoot> Roots()
        {
            return new List<NamedRoot>
            {
                new NamedRoot("as constructed", new CompilationNeverLeaksTests.Root()),

                new NamedRoot("nulls and empties", new CompilationNeverLeaksTests.Root
                {
                    Name = null,
                    NullableNumber = null,
                    Anything = null,
                    Inner = null,
                    Ints = new List<int>(),
                    Names = new List<string>(),
                    Array = new int[0],
                    Old = new ArrayList(),
                    OldMap = new Hashtable(),
                    Map = new Dictionary<string, int>(),
                    Set = new HashSet<int>(),
                    Sequence = new int[0].Select(x => x),
                    Huge = new List<int>(),
                    Amounts = new List<decimal>(),
                    Reals = new List<float>()
                }),

                new NamedRoot("zeros and NaN", new CompilationNeverLeaksTests.Root
                {
                    Number = 0,
                    Big = 0L,
                    Real = double.NaN,
                    Amount = 0m,
                    Letter = '\0',
                    Ints = new List<int> { 0 },
                    Names = new List<string> { null },
                    Array = new[] { 0 },
                    Old = new ArrayList { null },
                    Anything = "text"
                })
            };
        }

        private class NamedRoot
        {
            public NamedRoot(string name, CompilationNeverLeaksTests.Root root)
            {
                Name = name;
                Root = root;
            }

            public readonly string Name;
            public readonly CompilationNeverLeaksTests.Root Root;
        }

        /// <summary>
        /// The runtime type is part of the answer, not only the value: <c>sum()</c> returning
        /// <c>Int32</c> against <c>Double</c> is a divergence even though the numbers are equal.
        /// </summary>
        private static string Render(object value)
        {
            if (value == null)
                return "null";

            if (value is string text)
                return "String:'" + text + "'";

            if (value is IEnumerable items)
            {
                var parts = new List<string>();
                foreach (var item in items)
                    parts.Add(item == null ? "null" : item.ToString());

                return value.GetType().Name + "[" + string.Join(",", parts) + "]";
            }

            return value.GetType().Name + ":" + value;
        }

        private static readonly string[] Operators =
        {
            "==", "!=", "<=", ">=", "and", "or", "xor", "between", "in", "is", "??",
            "+", "-", "*", "/", "%", "^", "<", ">"
        };

        /// <summary>
        /// Which surface the expression exercises, so the ledger groups by cause rather than listing
        /// hundreds of expressions. Longer operators are matched first, or <c>&lt;=</c> would report as
        /// <c>&lt;</c>.
        /// </summary>
        private static string SurfaceOf(string expression)
        {
            foreach (var op in Operators)
                if (expression.Contains(" " + op + " "))
                    return "binary " + op;

            if (expression.StartsWith("-")) return "unary -";
            if (expression.StartsWith("!")) return "unary !";
            if (expression.Contains(" ? ")) return "ternary";
            if (expression.Contains(".!{")) return "projection";
            if (expression.Contains(".?{")) return "selection";
            if (expression.Contains(".^{")) return "first-match";
            if (expression.Contains(".${")) return "last-match";
            if (expression.Contains(" as ") || expression.Contains("as<")) return "cast";
            if (expression.StartsWith("{") || expression.StartsWith("#{")) return "literal";
            if (expression.Contains(" = ")) return "assign";
            if (expression.Contains("[")) return "indexer";
            if (expression.Contains("()")) return "call";

            return "other";
        }
    }
}

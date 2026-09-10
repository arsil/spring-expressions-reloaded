using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// The fourth invariant: <b>an expression object must answer the same as a freshly parsed one,
    /// whatever it was evaluated against before.</b>
    /// </summary>
    /// <remarks>
    /// <p>
    /// The other three sweeps compare the engine against <i>itself</i> - compiled against interpreted,
    /// and operand read counts between the two - so none of them can see a defect that behaves the
    /// same way on both backends. This one has an external reference that costs nothing: <b>a
    /// freshly parsed expression</b>. That is what makes this class findable at all.
    /// </p>
    /// <p>
    /// <b>Three members existed when it was written, and all three were found by hand</b>
    /// (<c>_Docs/open-issues.md</c> item 35). Nodes cache resolved members on the instance, and a
    /// cache keyed on less than the resolution depended on answers for the wrong data:
    /// </p>
    /// <list type="bullet">
    /// <item><c>MethodNode</c> served a <b>null</b> receiver from the method a previous evaluation had
    /// resolved, so <c>Inner.Echo(Name)</c> answered <c>"Ana"</c> where a fresh expression threw.</item>
    /// <item><c>ConstructorNode</c> kept the first constructor it picked, so <c>new Thing(#x)</c> with
    /// an <c>int</c> and then a <c>string</c> threw <c>InvalidCastException</c>.</item>
    /// <item><c>IndexerNode</c> kept the first indexer, so <c>Item[0]</c> over two types that each
    /// declare <c>this[int]</c> threw on the second.</item>
    /// </list>
    /// <p>
    /// Only the first was ever caught by a sweep, and <b>only by luck</b>: its stale cache made the
    /// interpreter answer while the compiled path threw, so it surfaced as a backend disagreement. The
    /// other two fail identically on both backends - same exception, same runtime type, same read
    /// counts - and <c>EvaluationNeverDivergesTests</c> passes them by construction. Verified, not
    /// assumed: with those two defects reintroduced, that sweep stays green and this one goes red.
    /// </p>
    /// <p>
    /// <b>Both orders are swept</b>, because a cache that is merely replaced once would pass a
    /// one-directional check. And every root pair is covered implicitly: the reference for a root does
    /// not depend on what came before it, so one reference per root serves every sequence.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class ReusedExpressionsNeverDrift
    {
        /// <summary>
        /// Empty, and keeping it empty is the whole job. A row here means a reused expression answers
        /// differently from a fresh one somewhere - which is always a defect, never a ruling: nothing
        /// about an expression's meaning depends on what it was used for previously.
        /// </summary>
        private static readonly string[] KnownDrifts = { };

        [Test]
        public void AReusedExpressionAnswersLikeAFreshOne()
        {
            var roots = Roots();
            var found = new Dictionary<string, int>();
            var samples = new Dictionary<string, string>();
            var compared = 0;

            foreach (var expression in CompilationNeverLeaksTests.Corpus())
            {
                foreach (var mode in new[] { EvaluationMode.MustCompile, EvaluationMode.MustInterpret })
                {
                    // One reference per root, each from an expression that has never been used. The
                    // reference does not depend on what preceded it - that is the invariant - so these
                    // serve every sequence below.
                    var reference = new string[roots.Count];
                    var parsed = true;

                    for (var i = 0; i < roots.Count && parsed; i++)
                    {
                        var fresh = TryParse(expression, mode);

                        if (fresh == null)
                            parsed = false;
                        else
                            reference[i] = Outcome(fresh, roots[i].Root);
                    }

                    if (!parsed)
                        continue;   // no form in this mode - the compile sweep's business, not this one's

                    var forward = Enumerable.Range(0, roots.Count).ToArray();
                    var backward = forward.Reverse().ToArray();

                    foreach (var order in new[] { forward, backward })
                    {
                        var reused = TryParse(expression, mode);

                        if (reused == null)
                            continue;

                        foreach (var i in order)
                        {
                            compared++;

                            var actual = Outcome(reused, roots[i].Root);

                            if (actual == reference[i])
                                continue;

                            var key = SurfaceOf(expression) + "  ::  fresh " + reference[i]
                                      + " / reused " + actual;

                            found[key] = found.TryGetValue(key, out var n) ? n + 1 : 1;

                            if (!samples.ContainsKey(key))
                            {
                                samples[key] = expression + "   [" + roots[i].Name + ", "
                                               + mode + ", "
                                               + (order == forward ? "forward" : "backward") + "]";
                            }
                        }
                    }
                }
            }

            Assert.Greater(compared, 5000, "the sweep should be large enough to be worth running");

            AssertDriftsAreTheKnownOnes(found, samples);
        }

        private static IGetterExpression<CompilationNeverLeaksTests.Root, object> TryParse(
            string expression, EvaluationMode mode)
        {
            try
            {
                return Expression.ParseGetter<CompilationNeverLeaksTests.Root, object>(expression, mode);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Outcome(
            IGetterExpression<CompilationNeverLeaksTests.Root, object> expression,
            CompilationNeverLeaksTests.Root root)
        {
            try
            {
                return Render(expression.GetValue(root));
            }
            catch (Exception e)
            {
                // The exception *type* is part of the answer here, unlike in the compiled-versus-
                // interpreted sweep where the two backends may legitimately differ. One expression
                // object has no such licence against itself.
                return "!" + e.GetType().Name;
            }
        }

        private static void AssertDriftsAreTheKnownOnes(
            Dictionary<string, int> found, Dictionary<string, string> samples)
        {
            var rows = found
                .Select(pair => pair.Value + "x " + pair.Key)
                .OrderBy(row => row, StringComparer.Ordinal)
                .ToList();

            var unexpected = rows.Where(row => !KnownDrifts.Contains(row)).ToList();
            var goneOrChanged = KnownDrifts.Where(row => !rows.Contains(row)).ToList();

            var report = string.Join(
                Environment.NewLine,
                rows.Select(row =>
                {
                    var key = row.Substring(row.IndexOf("x ", StringComparison.Ordinal) + 2);
                    return row + Environment.NewLine + "      e.g. " + samples[key];
                }));

            Assert.IsEmpty(
                unexpected,
                "a reused expression answered differently from a fresh one. Nothing about an "
                + "expression's meaning may depend on what it was evaluated against before - so this "
                + "is a defect, and the cache behind it is keyed on less than its resolution depended "
                + "on:" + Environment.NewLine + report);

            Assert.IsEmpty(
                goneOrChanged,
                "these ledger rows no longer match - remove them if the drift is fixed:"
                + Environment.NewLine + string.Join(Environment.NewLine, goneOrChanged));
        }

        /// <summary>
        /// The same three data sets the evaluation sweep uses, and for the same reason - but here what
        /// matters is that a value's <b>runtime type</b> changes between them. <c>Anything</c> is
        /// <c>45</c>, then <c>null</c>, then <c>"text"</c>, which is what makes a resolution chosen
        /// from an operand's type go stale.
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
                    Reals = new List<float>(),
                    Counts = new List<CompilationNeverLeaksTests.Tally>()
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
                    Anything = "text",
                    Counted = new CompilationNeverLeaksTests.Tally(0),
                    Counts = new List<CompilationNeverLeaksTests.Tally>
                    {
                        new CompilationNeverLeaksTests.Tally(0)
                    }
                })
            };
        }

        private static string SurfaceOf(string expression)
        {
            if (expression.Contains("new SpringExpressionsTests")) return "constructor";
            if (expression.Contains("Probe[")) return "indexer";
            if (expression.Contains("Inner.Echo(")) return "call on a non-this receiver";
            if (expression.Contains("[")) return "indexer";
            if (expression.Contains("()")) return "call";

            return "other";
        }

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
    }
}

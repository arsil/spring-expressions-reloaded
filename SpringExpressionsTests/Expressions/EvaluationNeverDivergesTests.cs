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
        /// The divergences this sweep finds, as <c>"COUNTx SURFACE :: OUTCOME"</c>. Every row is a
        /// standing invitation to rule; none is an accident nobody has looked at.
        /// </summary>
        /// <remarks>
        /// <p>Grouped, with the causes behind them:</p>
        /// <list type="bullet">
        /// <item><description>
        /// <b>The comparison operators are done</b> - open-issues item 21, four stages, and this sweep
        /// is what found and then measured every one of them. <c>==</c> and <c>!=</c> have no rows here
        /// at all now, from 540 per operator; the relational operators keep only their
        /// "values differ" rows, which are item 17's nullable question and a different problem. The
        /// ruling: the compiled path may only emit a comparison when the static types tell it which
        /// comparison to make, and otherwise refuses so the interpreter - which sees the runtime values
        /// - can answer. Do not reintroduce a fallback that always answers; the last one returned
        /// <c>false</c> for pairs it could not compare and depended on string interning for the rest.
        /// </description></item>
        /// <item><description>
        /// <b>Nullable arithmetic where both operands hold nothing</b> - <c>NoNumber + NoNumber</c> is
        /// null compiled (LINQ's lifted operator) and <c>ArgumentException</c> interpreted. Present in
        /// the as-constructed data, so it needs no exotic input.
        /// </description></item>
        /// <item><description>
        /// <b>A null reference in a relational operator</b> - <c>Name &lt; Number</c> with a null
        /// <c>Name</c> is <c>NullReferenceException</c> compiled (the emitted
        /// <c>IComparable.CompareTo</c> call) and <c>True</c> interpreted (the inherited
        /// null-sorts-first rule). Adjacent to open-issues item 17 and blocked on the same ruling.
        /// </description></item>
        /// <item><description>
        /// <b>Item 17's null half itself</b> - <c>NullableNumber &lt; Number</c> is <c>False</c> compiled
        /// and <c>True</c> interpreted. Known, open, and the only entry here already written up.
        /// </description></item>
        /// <item><description>
        /// <b>A null or absent operand of <c>+</c></b> - a null <c>Name</c> concatenates to <c>""</c>
        /// compiled and throws or answers null interpreted.
        /// </description></item>
        /// <item><description>
        /// <b>A null source reaching a processor or indexer</b> - <c>Name.sort()</c>,
        /// <c>Name.count()</c>, and <c>Map['a']</c> on a map without that key, where the compiled path
        /// throws and the interpreter answers null.
        /// </description></item>
        /// <item><description>
        /// <b>Two already documented</b>: <c>sum()</c> over ints is <c>Int32</c> compiled and
        /// <c>Double</c> interpreted, and a member call on a nullable holding nothing is <c>""</c>
        /// compiled against a throw interpreted.
        /// </description></item>
        /// </list>
        /// </remarks>
        private static readonly string[] KnownDivergences =
        {
            "6x binary -  ::  compiled answered / interpreted threw ArgumentException",
            "6x binary *  ::  compiled answered / interpreted threw ArgumentException",
            "6x binary /  ::  compiled answered / interpreted threw ArgumentException",
            "6x binary %  ::  compiled answered / interpreted threw ArgumentException",
            "6x binary ^  ::  compiled answered / interpreted threw ArgumentException",
            "12x binary +  ::  both answered, values differ",
            "38x binary +  ::  compiled answered / interpreted threw ArgumentException",
            "6x binary and  ::  both answered, values differ",
            "1x binary and  ::  compiled answered / interpreted threw ArgumentException",
            "2x binary is  ::  both answered, values differ",
            "6x binary or  ::  both answered, values differ",
            "1x binary or  ::  compiled answered / interpreted threw ArgumentException",
            "6x binary xor  ::  both answered, values differ",
            "1x binary xor  ::  compiled answered / interpreted threw ArgumentException",
            "6x call  ::  both answered, values differ",
            "13x call  ::  compiled answered / interpreted threw ArgumentException",
            "1x call  ::  compiled answered / interpreted threw NullReferenceException",
            "4x call  ::  compiled threw ArgumentNullException / interpreted answered",
            "1x call  ::  compiled threw NullReferenceException / interpreted answered",
            "1x indexer  ::  compiled threw KeyNotFoundException / interpreted answered",
            "3x literal  ::  both answered, values differ"
        };

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
                    Map = new Dictionary<string, int>()
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

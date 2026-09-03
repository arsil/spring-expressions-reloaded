using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// The two backends must read each operand the same number of times - the third invariant, and the
    /// one nothing guarded.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <c>CompilationNeverLeaksTests</c> asks what escapes compilation. <c>EvaluationNeverDivergesTests</c>
    /// asks whether the backends produce the same value and runtime type. <b>An operand evaluated twice
    /// passes both of those</b>: compilation succeeds, and the answer is identical - only the side
    /// effects happen twice.
    /// </p>
    /// <p>
    /// Three instances of exactly that defect have been found, and <b>every one of them by hand</b>:
    /// </p>
    /// <list type="bullet">
    /// <item><description>
    /// <c>OpAND</c> and <c>OpOR</c> ended with
    /// <c>Convert.ToBoolean(l) &amp;&amp; Convert.ToBoolean(GetRightValue(...))</c> <i>after</i> a branch
    /// above had already evaluated the right operand, so <c>0 or SideEffect()</c> ran the side effect
    /// twice. Still live in upstream Spring.NET.
    /// </description></item>
    /// <item><description>
    /// <c>OpADD</c>'s <c>DateTime + string</c> branch was a bare conditional over the operand
    /// <i>expressions</i>, so the right operand was emitted twice and the left one only inside the true
    /// branch: <c>Date() + Span()</c> called <c>Span()</c> twice, and <c>Date() + NoSpan()</c> never
    /// called <c>Date()</c> at all.
    /// </description></item>
    /// </list>
    /// <p>
    /// <b>Why this needs a corpus of its own rather than a row in the shared one.</b> Every operand here
    /// has to be observable, and every value in
    /// <c>CompilationNeverLeaksTests.Corpus</c> is a property read - which is not. Making them methods
    /// there would rewrite the corpus both existing sweeps run on, and their ledgers with it; the
    /// operand kinds are mirrored instead, one counting method per kind.
    /// </p>
    /// <p>
    /// <b>The invariant is agreement, not "exactly once".</b> Reading an operand no times is often
    /// correct - <c>and</c> and <c>or</c> short-circuit, and a conditional evaluates one branch - and
    /// which operator short-circuits is a decided part of this language. What must not happen is the two
    /// backends disagreeing, because which one runs is not the caller's choice.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class OperandReadsNeverDivergeTests
    {
        public enum Shade { Light, Dark }

        /// <summary>
        /// A root whose every operand is a method that records being called. One counter per name, so a
        /// disagreement can say which operand was read how many times rather than only that something
        /// differed.
        /// </summary>
        /// <remarks>
        /// The return values are fixed and deliberately dull - this fixture is not measuring answers.
        /// What matters is the <i>type</i> of each, because the compiled path branches on static types
        /// and the branches are where a doubled read lives.
        /// </remarks>
        public class Counting
        {
            public readonly SortedDictionary<string, int> Reads = new SortedDictionary<string, int>();

            public string Text() { return Record("Text", "lit"); }
            public string NoText() { return Record("NoText", (string)null); }
            public int Num() { return Record("Num", 45); }

            /// <summary>A second <c>int</c>, so a conditional's two branches can be told apart.</summary>
            public int Other() { return Record("Other", 9); }

            public long Big() { return Record("Big", 45L); }
            public double Real() { return Record("Real", 4.5); }
            public decimal Amount() { return Record("Amount", 45.5m); }
            public bool On() { return Record("On", true); }
            public bool Off() { return Record("Off", false); }
            public char Letter() { return Record("Letter", 'x'); }
            public Shade Colour() { return Record("Colour", Shade.Light); }
            public int? Maybe() { return Record("Maybe", (int?)7); }
            public int? Nothing() { return Record("Nothing", (int?)null); }
            public DateTime When() { return Record("When", new DateTime(2001, 1, 1)); }
            public TimeSpan Span() { return Record("Span", TimeSpan.FromHours(2)); }
            public string SpanText() { return Record("SpanText", "02:00:00"); }
            public string NoSpanText() { return Record("NoSpanText", (string)null); }
            public object Anything() { return Record("Anything", (object)45); }
            public List<int> Ints() { return Record("Ints", new List<int> { 3, 1, 2 }); }
            public List<int> NoInts() { return Record("NoInts", (List<int>)null); }
            public Dictionary<string, int> Map()
            {
                return Record("Map", new Dictionary<string, int> { { "a", 1 } });
            }

            private T Record<T>(string name, T value)
            {
                Reads[name] = Reads.TryGetValue(name, out var n) ? n + 1 : 1;
                return value;
            }
        }

        /// <summary>
        /// The read-count disagreements this sweep finds, as <c>"COUNTx SURFACE :: OUTCOME"</c>.
        /// <b>241 rows in 13 classes when it was written; 25 in 7 now, and every one of the 25 is a
        /// question rather than a defect.</b>
        /// </summary>
        /// <remarks>
        /// <p>
        /// Empty is the goal, for the reason its two sibling ledgers are: a row means the backends
        /// disagree about how many times a caller's own code runs, which no caller can defend against
        /// because which backend serves them is not their choice.
        /// </p>
        /// <p>
        /// <b>The counts are identical on netcoreapp2.1, net472 and net8.0</b>, which was checked before
        /// writing them down: <c>TimeSpan</c> declares different operators per framework, so a counted
        /// ledger over an operand of that type could have been TFM-dependent. It is not.
        /// </p>
        /// <p>
        /// <b>Four causes are fixed, 216 rows, and all four were one mistake.</b> An emitted operand is
        /// an <i>expression</i>, and writing it into two places in the tree evaluates it twice. Each
        /// site now hoists into block variables through
        /// <c>SpringExpressions.Expressions.Compiling.OperandLocals</c>, assigning left before right,
        /// which is the order the interpreter reads them:
        /// </p>
        /// <list type="bullet">
        /// <item><description>
        /// <b>A string operand of <c>+</c> was read twice</b> - 124 rows, the largest class.
        /// <c>AtLeastOneIsARealString</c> tests the operand and then <c>Concat</c> uses the same
        /// expression again, so <c>Text() + Text()</c> read one operand three times.
        /// </description></item>
        /// <item><description>
        /// <b>A nullable operand of <c>&lt; &lt;= &gt; &gt;=</c> was read twice</b> - 21 rows each, 84
        /// in all, from <c>HasValue</c> and <c>Value</c> both mentioning it. Two more faults went with
        /// it at that site: the operands were evaluated <i>right before left</i>, because the
        /// conditional tests the nullable one and the other sits inside a branch, and
        /// <c>Num() &lt; Nothing()</c> never evaluated <c>Num()</c> at all.
        /// </description></item>
        /// <item><description>
        /// <b><c>between</c> read both operands twice</b> - 7 rows. It is two comparisons over the same
        /// operands, so needing them again is structural rather than accidental.
        /// </description></item>
        /// <item><description>
        /// <b>A nullable receiver was read twice</b> - 1 row, <c>Maybe().ToString()</c>.
        /// <c>GuardWithHasValue</c> takes a builder rather than a finished member access now, so the
        /// hoisting can wrap both halves.
        /// </description></item>
        /// </list>
        /// <p><b>The two that remain are rulings, and neither is a wrong answer:</b></p>
        /// <list type="bullet">
        /// <item><description>
        /// <b>Lifted arithmetic skips the right operand compiled when the left is empty</b> - 12 rows
        /// over <c>+ - * / % ^</c>. LINQ's lifted operators short-circuit; the interpreter reads both
        /// operands and then decides. Whether nullable arithmetic <i>should</i> short-circuit is a
        /// language question, not a defect at a site.
        /// </description></item>
        /// <item><description>
        /// <b><c>??</c> reads its right operand interpreted when the left is a non-nullable value
        /// type</b> - 13 rows, <c>Num() ?? Num()</c>. The compiled path can see that an <c>int</c> is
        /// never null and drops the right operand; the interpreter evaluates it and discards the result.
        /// <b>Neither backend short-circuits <c>??</c> for a reference type</b> - <c>Text() ?? Text()</c>
        /// reads twice on both - so the question is "does <c>??</c> short-circuit at all", which C#
        /// answers yes, and these 13 rows are only the slice where the compiled path can prove it.
        /// </description></item>
        /// </list>
        /// </remarks>
        private static readonly string[] KnownReadDifferences =
        {
            "1x binary -  ::  read counts differ",
                // e.g. Nothing() - Amount()   compiled=Nothing=1   interpreted=Amount=1 Nothing=1
            "13x binary ??  ::  read counts differ",
                // e.g. Num() ?? Num()   compiled=Num=1   interpreted=Num=2
            "1x binary *  ::  read counts differ",
                // e.g. Nothing() * Amount()   compiled=Nothing=1   interpreted=Amount=1 Nothing=1
            "1x binary /  ::  read counts differ",
                // e.g. Nothing() / Amount()   compiled=Nothing=1   interpreted=Amount=1 Nothing=1
            "1x binary %  ::  read counts differ",
                // e.g. Nothing() % Amount()   compiled=Nothing=1   interpreted=Amount=1 Nothing=1
            "7x binary ^  ::  read counts differ",
                // e.g. Nothing() ^ Num()   compiled=Nothing=1   interpreted=Nothing=1 Num=1
            "1x binary +  ::  read counts differ",
                // e.g. Nothing() + Amount()   compiled=Nothing=1   interpreted=Amount=1 Nothing=1
        };

        [Test]
        public void TheBackendsReadEachOperandTheSameNumberOfTimes()
        {
            var found = new SortedDictionary<string, int>();
            var samples = new Dictionary<string, string>();
            var compared = 0;

            foreach (var expression in Corpus())
            {
                IGetterExpression<Counting, object> compiled, interpreted;

                try
                {
                    compiled = Expression.ParseGetter<Counting, object>(
                        expression, EvaluationMode.MustCompile);
                }
                catch (Exception)
                {
                    // no compiled form, so there are no two sets of reads to compare
                    continue;
                }

                try
                {
                    interpreted = Expression.ParseGetter<Counting, object>(
                        expression, EvaluationMode.MustInterpret);
                }
                catch (Exception)
                {
                    continue;
                }

                compared++;

                var compiledRoot = new Counting();
                var interpretedRoot = new Counting();

                var compiledThrew = Evaluate(compiled, compiledRoot);
                var interpretedThrew = Evaluate(interpreted, interpretedRoot);

                // A failure stops evaluation part-way, so the reads before it carry no information
                // about the shape. Which exception each backend raises is EvaluationNeverDivergesTests'
                // subject, not this one's.
                if (compiledThrew || interpretedThrew)
                    continue;

                var compiledReads = Render(compiledRoot);
                var interpretedReads = Render(interpretedRoot);

                if (compiledReads == interpretedReads)
                    continue;

                var key = SurfaceOf(expression) + "  ::  read counts differ";
                found[key] = found.TryGetValue(key, out var n) ? n + 1 : 1;

                if (!samples.ContainsKey(key))
                    samples[key] = expression
                        + "   compiled=" + compiledReads + "   interpreted=" + interpretedReads;
            }

            Assert.Greater(
                compared, 500,
                "the sweep should be large enough to be worth running - most of the corpus has no "
                    + "compiled form, so this counts only the shapes where there is something to compare");

            AssertDifferencesAreTheKnownOnes(found, samples);
        }

        /// <summary>
        /// Reading an operand twice is the defect this fixture exists for, and a doubled read of a
        /// method that only returns a value is invisible to every other test in the suite. This is the
        /// worked example, kept separate from the sweep so that a regression names the shape.
        /// </summary>
        [Test]
        public void ADateAndASpanStringReadEachOperandOnce()
        {
            var root = new Counting();

            Expression.ParseGetter<Counting, object>("When() + SpanText()", EvaluationMode.MustCompile)
                .GetValue(root);

            Assert.AreEqual(1, root.Reads["When"], "the left operand");
            Assert.AreEqual(1, root.Reads["SpanText"], "the right operand, which used to be read twice");
        }

        /// <summary>
        /// And the left operand is read even when the right turns out to be absent - it used to sit
        /// inside the conditional's true branch alone, so a null span skipped it entirely.
        /// </summary>
        [Test]
        public void ADateIsReadEvenWhenTheSpanStringIsNull()
        {
            var root = new Counting();

            Expression.ParseGetter<Counting, object>("When() + NoSpanText()", EvaluationMode.MustCompile)
                .GetValue(root);

            Assert.AreEqual(1, root.Reads["When"], "the left operand must still be evaluated");
            Assert.AreEqual(1, root.Reads["NoSpanText"], "the right operand");
        }

        /// <summary>
        /// Short-circuiting is a decided behaviour and must be preserved rather than flattened by this
        /// invariant: the two backends agree that the right operand is not read at all.
        /// </summary>
        [Test]
        public void BothBackendsShortCircuitTheLogicalOperators()
        {
            var compiled = new Counting();
            Expression.ParseGetter<Counting, object>("Off() and On()", EvaluationMode.MustCompile)
                .GetValue(compiled);

            var interpreted = new Counting();
            Expression.ParseGetter<Counting, object>("Off() and On()", EvaluationMode.MustInterpret)
                .GetValue(interpreted);

            Assert.AreEqual(1, compiled.Reads["Off"]);
            Assert.IsFalse(compiled.Reads.ContainsKey("On"), "'false and X' must not read X");
            Assert.AreEqual(1, interpreted.Reads["Off"]);
            Assert.IsFalse(interpreted.Reads.ContainsKey("On"), "and the interpreter agrees");

            var compiledOr = new Counting();
            Expression.ParseGetter<Counting, object>("On() or Off()", EvaluationMode.MustCompile)
                .GetValue(compiledOr);

            var interpretedOr = new Counting();
            Expression.ParseGetter<Counting, object>("On() or Off()", EvaluationMode.MustInterpret)
                .GetValue(interpretedOr);

            Assert.AreEqual(1, compiledOr.Reads["On"]);
            Assert.IsFalse(compiledOr.Reads.ContainsKey("Off"), "'true or X' must not read X");
            Assert.AreEqual(1, interpretedOr.Reads["On"]);
            Assert.IsFalse(interpretedOr.Reads.ContainsKey("Off"), "and the interpreter agrees");
        }

        /// <summary>
        /// A conditional reads its test and one branch, never both branches, on either backend.
        /// </summary>
        /// <remarks>
        /// Both branches must be the same type, which is why this uses two <c>int</c> operands rather
        /// than an <c>int</c> and a <c>long</c>: a conditional whose branches disagree has no compiled
        /// form at all, by the ruling <c>TernaryBranchTypeTests</c> pins. A first draft of this test
        /// used <c>On() ? Num() : Big()</c> and failed on the refusal rather than on any read count.
        /// </remarks>
        [Test]
        public void AConditionalReadsOneBranch()
        {
            foreach (var mode in new[] { EvaluationMode.MustCompile, EvaluationMode.MustInterpret })
            {
                var root = new Counting();

                Expression.ParseGetter<Counting, object>("On() ? Num() : Other()", mode).GetValue(root);

                Assert.AreEqual(1, root.Reads["On"], mode.ToString());
                Assert.AreEqual(1, root.Reads["Num"], mode.ToString());
                Assert.IsFalse(root.Reads.ContainsKey("Other"), "the untaken branch, " + mode);
            }
        }

        /// <summary>
        /// The four causes the sweep found and that are fixed, each as the shape that showed it. Kept
        /// separate from the sweep so that a regression names the site rather than only a count.
        /// </summary>
        [Test]
        public void TheFixedCausesReadEachOperandOnce()
        {
            AssertReadsOnce("Text() + Text()", "Text", 2);
            AssertReadsOnce("Text() + Num()", "Text", 1);
            AssertReadsOnce("Num() < Maybe()", "Maybe", 1);
            AssertReadsOnce("Maybe() >= Num()", "Maybe", 1);
            AssertReadsOnce("Num() between {1, 10}", "Num", 1);
            AssertReadsOnce("Maybe().ToString()", "Maybe", 1);
        }

        /// <summary>
        /// A comparison reads both operands, left first, even when the nullable one holds nothing and
        /// the answer is already decided by it.
        /// </summary>
        /// <remarks>
        /// The compiled tree tested the nullable operand and put the other inside a branch, so the two
        /// were evaluated right-before-left and the branch's operand was skipped entirely when the test
        /// failed. <c>Num() &lt; Nothing()</c> never read <c>Num()</c>. Both are the interpreter's
        /// behaviour now, which reads left then right whatever the comparison does with them.
        /// </remarks>
        [Test]
        public void AComparisonReadsBothOperandsEvenWhenANullableDecidesTheAnswer()
        {
            foreach (var mode in new[] { EvaluationMode.MustCompile, EvaluationMode.MustInterpret })
            {
                var root = new Counting();

                Expression.ParseGetter<Counting, object>("Num() < Nothing()", mode).GetValue(root);

                Assert.AreEqual(1, root.Reads["Num"], "the left operand, " + mode);
                Assert.AreEqual(1, root.Reads["Nothing"], "the right operand, " + mode);
            }
        }

        private static void AssertReadsOnce(string expression, string operand, int expected)
        {
            var compiled = new Counting();
            Expression.ParseGetter<Counting, object>(expression, EvaluationMode.MustCompile)
                .GetValue(compiled);

            var interpreted = new Counting();
            Expression.ParseGetter<Counting, object>(expression, EvaluationMode.MustInterpret)
                .GetValue(interpreted);

            Assert.AreEqual(
                expected, compiled.Reads[operand],
                expression + " - compiled reads of " + operand);

            Assert.AreEqual(
                interpreted.Reads[operand], compiled.Reads[operand],
                expression + " - the backends must agree on the reads of " + operand);
        }

        private static bool Evaluate(IGetterExpression<Counting, object> expression, Counting root)
        {
            try
            {
                expression.GetValue(root);
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>The reads as a comparable string, so a difference is one string comparison.</summary>
        private static string Render(Counting root)
        {
            return string.Join(" ", root.Reads.Select(kv => kv.Key + "=" + kv.Value).ToArray());
        }

        private static readonly string[] Operators =
        {
            "==", "!=", "<=", ">=", "and", "or", "xor", "between", "in", "??",
            "+", "-", "*", "/", "%", "^", "<", ">"
        };

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
            if (expression.Contains(" as ")) return "cast";
            if (expression.StartsWith("{") || expression.StartsWith("#{")) return "literal";
            if (expression.Contains("[")) return "indexer";

            return "call";
        }

        /// <summary>
        /// The shared corpus' shape with every operand replaced by a method that counts its own reads.
        /// </summary>
        /// <remarks>
        /// The operand kinds mirror <c>CompilationNeverLeaksTests.Corpus</c>'s, plus three the shared one
        /// has no reason to carry: a <c>false</c> boolean, because short-circuiting is directional and
        /// only <c>Off() and X</c> exercises one side of it; and a TimeSpan-shaped string with a null
        /// twin, because the branch that was found broken keys on <c>DateTime + string</c> and its null
        /// case is where the left operand went unread.
        /// </remarks>
        private static IEnumerable<string> Corpus()
        {
            var values = new[]
            {
                "Text()", "NoText()", "Num()", "Other()", "Big()", "Real()", "Amount()", "On()",
                "Off()", "Letter()", "Colour()", "Maybe()", "Nothing()", "When()", "Span()",
                "SpanText()", "NoSpanText()", "Anything()", "Ints()", "Map()"
            };

            var operators = new[]
            {
                "+", "-", "*", "/", "%", "^", "==", "!=", "<", ">", "<=", ">=", "and", "or", "xor"
            };

            foreach (var op in operators)
                foreach (var left in values)
                    foreach (var right in values)
                        yield return left + " " + op + " " + right;

            foreach (var value in values)
            {
                yield return "-" + value;
                yield return "!" + value;
                yield return value + " ? Num() : Big()";
                yield return "On() ? " + value + " : Num()";
                yield return "Off() ? Num() : " + value;
                yield return value + " ?? Num()";
                yield return value + " between {1, 10}";
                yield return value + " in {1, 2}";
                yield return value + ".ToString()";
                yield return "{" + value + ", " + value + "}";
                yield return "#{'k' : " + value + "}";
                yield return "new int[] {" + value + "}";
                yield return value + " as string";
            }

            var sources = new[] { "Ints()", "NoInts()", "Map()", "Text()" };
            var processors = new[]
            {
                "sort()", "distinct()", "reverse()", "nonNull()", "sum()",
                "average()", "min()", "max()", "count()", "convert(decimal)",
                "orderBy({|a,b| $a - $b})"
            };

            foreach (var source in sources)
            {
                foreach (var processor in processors)
                    yield return source + "." + processor;

                yield return source + ".!{#this}";
                yield return source + ".?{#this != null}";
                yield return source + ".^{#this != null}";
                yield return source + ".${#this != null}";
                yield return source + "[0]";
                yield return source + "['a']";
            }
        }

        private static void AssertDifferencesAreTheKnownOnes(
            SortedDictionary<string, int> found, Dictionary<string, string> samples)
        {
            var current = found.Select(kv => kv.Value + "x " + kv.Key).ToList();

            var unexpected = current.Where(k => !KnownReadDifferences.Contains(k)).ToList();
            var goneOrChanged = KnownReadDifferences.Where(k => !current.Contains(k)).ToList();

            var report = new StringBuilder();
            report.AppendLine("current table - paste over KnownReadDifferences to re-baseline:");
            foreach (var kv in found)
            {
                report.AppendLine("            \"" + kv.Value + "x " + kv.Key + "\",");
                report.AppendLine("                // e.g. " + samples[kv.Key]);
            }

            Assert.IsEmpty(
                unexpected,
                "the backends disagree about how many times an operand is read. A caller cannot defend "
                    + "against that, because which backend serves them is not their choice:"
                    + Environment.NewLine + string.Join(Environment.NewLine, unexpected)
                    + Environment.NewLine + Environment.NewLine + report);

            Assert.IsEmpty(
                goneOrChanged,
                "these ledger rows no longer match - remove them if fixed, or update the count:"
                    + Environment.NewLine + string.Join(Environment.NewLine, goneOrChanged)
                    + Environment.NewLine + Environment.NewLine + report);
        }
    }
}

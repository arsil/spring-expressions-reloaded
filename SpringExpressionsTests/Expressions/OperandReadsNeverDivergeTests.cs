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
    /// <p>
    /// <b>A row where one backend fails and the other answers is reported here too</b>, even though
    /// that is a value divergence and <c>EvaluationNeverDivergesTests</c> would normally own it. Its
    /// corpus has no operand that <i>can</i> fail - every value in it is a property read - so this is
    /// the only fixture where such a row can appear at all. It matters because that is the worst form
    /// of the defect this sweep exists for: before the lifted-arithmetic fix,
    /// <c>Nothing() + Boom()</c> answered <c>null</c> compiled, because the compiled path never ran
    /// <c>Boom()</c>, while the interpreter ran it and threw. <b>A swallowed exception.</b> This sweep
    /// used to skip every throwing row and so could not see it either; both blind spots are closed.
    /// Where <i>both</i> backends fail, the reads before the failure are still compared - each ran the
    /// operands it needed and then stopped, so a difference means they needed different ones.
    /// </p>
    /// <p>
    /// <b>Corpus width is the thing to keep growing here</b>, and it is the lesson all three sweeps
    /// keep relearning: every gap any of them has left had something real in it. The first version of
    /// this one had four collection sources, no empty source, no set, no bare sequence, no decimal
    /// collection, no assignments and no failing operand. All of those were found by hand rather than
    /// by the test, which is exactly the situation the test is meant to remove.
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

            /// <summary>Assignment targets, so the corpus can generate writes as well as reads.</summary>
            public object Target { get; set; }
            public int Number { get; set; }
            public string Name { get; set; }

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

            // Collection shapes the first version of this corpus had no example of, added for the same
            // reason the other two sweeps grew theirs: every gap either of them left had something real
            // in it. An empty source takes a different branch in six processors; a HashSet is not the
            // non-generic ICollection; a bare IEnumerable cannot answer count() without being walked;
            // and a decimal collection accumulates in a different family.
            public List<int> Empty() { return Record("Empty", new List<int>()); }
            public HashSet<int> Set() { return Record("Set", new HashSet<int> { 3, 1, 2 }); }
            public IEnumerable<int> Sequence()
            {
                return Record("Sequence", new[] { 3, 1, 2 }.Select(x => x));
            }
            public List<decimal> Amounts()
            {
                return Record("Amounts", new List<decimal> { 3m, 1m, 2m });
            }

            /// <summary>
            /// An operand that fails rather than answering.
            /// </summary>
            /// <remarks>
            /// The sweep used to skip every row where either backend threw, so the worst form of the
            /// operand-reuse defect was invisible to it: before the arithmetic fix,
            /// <c>Nothing() + Boom()</c> answered <c>null</c> compiled - the compiled path never ran
            /// <c>Boom()</c> - while the interpreter ran it and threw. **A swallowed exception**, not
            /// merely a doubled side effect, and neither this sweep nor the evaluation sweep could see
            /// it: this one skipped the row and that one has no throwing operand in its corpus.
            /// </remarks>
            public int Boom()
            {
                Record("Boom", 0);
                throw new InvalidOperationException("this operand fails");
            }

            private T Record<T>(string name, T value)
            {
                Reads[name] = Reads.TryGetValue(name, out var n) ? n + 1 : 1;
                return value;
            }
        }

        /// <summary>
        /// The read-count disagreements this sweep finds, as <c>"COUNTx SURFACE :: OUTCOME"</c>.
        /// <b>Empty: the backends read every operand of every corpus expression the same number of
        /// times. It started at 241 rows in 13 classes.</b>
        /// </summary>
        /// <remarks>
        /// <p>
        /// Keeping it empty is the point, for the reason its two sibling ledgers are: a row means the
        /// backends disagree about how many times a caller's own code runs, which no caller can defend
        /// against because which backend serves them is not their choice.
        /// </p>
        /// <p>
        /// <b>The counts were identical on netcoreapp2.1, net472 and net8.0</b> throughout, checked
        /// before any of them was written down: <c>TimeSpan</c> declares different operators per
        /// framework, so a counted ledger over an operand of that type could have been TFM-dependent.
        /// </p>
        /// <p>
        /// <b>216 of the 241 were one mistake at four sites.</b> An emitted operand is an
        /// <i>expression</i>, so writing it into two places in the tree evaluates it twice, and writing
        /// it into one branch of a conditional evaluates it not at all when the other branch is taken.
        /// <c>SpringExpressions.Expressions.Compiling.OperandLocals</c> is the one implementation:
        /// a block variable per operand, assigned left before right, which is the order <c>Get</c>
        /// reads them.
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
        /// it at that site: the operands were evaluated <i>right before left</i>, and
        /// <c>Num() &lt; Nothing()</c> never evaluated <c>Num()</c> at all.
        /// </description></item>
        /// <item><description>
        /// <b><c>between</c> read both operands twice</b> - 7 rows, structural rather than accidental:
        /// it is two comparisons over the same operands.
        /// </description></item>
        /// <item><description>
        /// <b>A nullable receiver was read twice</b> - 1 row, <c>Maybe().ToString()</c>.
        /// <c>GuardWithHasValue</c> takes a builder rather than a finished member access now, so the
        /// hoisting wraps both halves.
        /// </description></item>
        /// </list>
        /// <p>
        /// <b>The last 25 were filed as two open rulings and were neither open nor rulings.</b>
        /// Measuring the same operations in real C# - the engine's two backends and C# side by side -
        /// showed both had a known-correct answer, and that one of them was broken on <i>both</i>
        /// backends in opposite directions:
        /// </p>
        /// <list type="bullet">
        /// <item><description>
        /// <b>Lifted arithmetic did not evaluate the right operand when the left held nothing</b> - 12
        /// rows over <c>+ - * / % ^</c>, and compiled only. C# evaluates both operands and then applies
        /// the lifted operator, our interpreter did too, and nothing in this language says <c>-</c>
        /// conditionally evaluates an operand. <c>BinaryNumericOperatorHelper.TryCreate</c> hoists a
        /// nullable pair before building the operator, which leaves its null-in-null-out semantics
        /// untouched.
        /// </description></item>
        /// <item><description>
        /// <b><c>??</c> was wrong on both sides</b> - 13 rows. The compiled path read its <i>left</i>
        /// operand twice (the same reuse mistake as the four above), and the interpreter evaluated its
        /// <i>right</i> operand whether it needed it or not, so <c>Name ?? Expensive()</c> called
        /// <c>Expensive()</c> with a name in hand. C# skips it; the compiled path always had, since its
        /// right operand sits in the false branch of a conditional. <c>??</c> is inherited, so the
        /// frozen suite was the authority on whether eager evaluation was decided behaviour - it pins
        /// six rows, every one asserting a value and none with a side-effecting operand, so the cost of
        /// short-circuiting was measured at zero.
        /// </description></item>
        /// </list>
        /// </remarks>
        private static readonly string[] KnownReadDifferences = new string[0];

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

                var compiledReads = Render(compiledRoot);
                var interpretedReads = Render(interpretedRoot);

                string outcome = null;

                if (compiledThrew != interpretedThrew)
                {
                    // One backend failed and the other answered. That is a value divergence rather than
                    // a read-count one, and EvaluationNeverDivergesTests would normally own it - but its
                    // corpus has no operand that can fail, so this fixture is the only place such a row
                    // can appear. Before the lifted-arithmetic fix 'Nothing() + Boom()' was exactly
                    // this: the compiled path never ran Boom() and answered null, while the interpreter
                    // ran it and threw. A swallowed exception, and both sweeps were blind to it - this
                    // one skipped the row outright, which is the gap this branch closes.
                    outcome = compiledThrew
                        ? "compiled threw / interpreted answered"
                        : "compiled answered / interpreted threw";
                }
                else if (compiledReads != interpretedReads)
                {
                    // Reads before a failure are still comparable when *both* backends failed: each ran
                    // the operands it needed and then stopped, so a difference means they needed
                    // different ones. Which exception either raises is not this fixture's subject.
                    outcome = compiledThrew
                        ? "read counts differ, both threw"
                        : "read counts differ";
                }

                if (outcome == null)
                    continue;

                var key = SurfaceOf(expression) + "  ::  " + outcome;
                found[key] = found.TryGetValue(key, out var n) ? n + 1 : 1;

                if (!samples.ContainsKey(key))
                    samples[key] = expression
                        + "   compiled=" + compiledReads + (compiledThrew ? " (threw)" : "")
                        + "   interpreted=" + interpretedReads + (interpretedThrew ? " (threw)" : "");
            }

            Assert.Greater(
                compared, 1400,
                "the sweep should be large enough to be worth running - most of the corpus has no "
                    + "compiled form, so this counts only the shapes where there is something to "
                    + "compare. A floor rather than the exact count, because the count is TFM-dependent: "
                    + "1,524 on netcoreapp2.1 and net8.0 against 1,520 on net472, where TimeSpan "
                    + "declares no multiplication or division operators and four rows therefore have no "
                    + "compiled form. The floor is what catches the corpus silently shrinking.");

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

        /// <summary>
        /// Arithmetic evaluates both operands even when the left one holds nothing and the answer is
        /// already null. LINQ's lifted operators do not, which is the whole of what was wrong.
        /// </summary>
        /// <remarks>
        /// C# was measured on the same operations while this was decided: it evaluates both operands and
        /// only then applies the lifted operator, and our interpreter already did. So there was no
        /// ruling to make - three witnesses agreed and only the compiled path dissented.
        /// </remarks>
        [Test]
        public void ArithmeticEvaluatesBothOperandsEvenWhenTheLeftHoldsNothing()
        {
            foreach (var expression in new[]
            {
                "Nothing() + Amount()", "Nothing() - Amount()", "Nothing() * Amount()",
                "Nothing() / Amount()", "Nothing() % Amount()", "Nothing() ^ Num()"
            })
            {
                var compiled = new Counting();
                var value = Expression
                    .ParseGetter<Counting, object>(expression, EvaluationMode.MustCompile)
                    .GetValue(compiled);

                var interpreted = new Counting();
                Expression.ParseGetter<Counting, object>(expression, EvaluationMode.MustInterpret)
                    .GetValue(interpreted);

                Assert.IsNull(value, expression + " - nothing in, nothing out, unchanged");
                Assert.AreEqual(1, compiled.Reads["Nothing"], expression + " - the left operand");
                Assert.AreEqual(
                    interpreted.Reads.Count, compiled.Reads.Count,
                    expression + " - both operands must run on both backends");
            }
        }

        /// <summary>
        /// <c>??</c> does not evaluate its right operand when the left one has a value, and reads the
        /// left operand once. It was wrong on both backends, in opposite directions.
        /// </summary>
        /// <remarks>
        /// The compiled path read the left operand twice - it tests it and then returns it - while the
        /// interpreter read both operands and then chose, so <c>Name ?? Expensive()</c> called
        /// <c>Expensive()</c> with a name in hand. C# skips it, and the frozen suite's six inherited
        /// <c>??</c> rows all assert values over side-effect-free operands, so nothing pinned the eager
        /// behaviour.
        /// </remarks>
        [Test]
        public void TheDefaultOperatorReadsItsLeftOperandOnceAndSkipsTheRightWhenItCan()
        {
            foreach (var mode in new[] { EvaluationMode.MustCompile, EvaluationMode.MustInterpret })
            {
                // Both operands are strings: '??' requires one operand type to convert to the other,
                // and 'Text() ?? Num()' is refused for that reason on both backends - a pre-existing
                // and separate rule, which a first draft of this test tripped over.
                var present = new Counting();
                Assert.AreEqual(
                    "lit",
                    Expression.ParseGetter<Counting, object>("Text() ?? SpanText()", mode)
                        .GetValue(present),
                    mode.ToString());

                Assert.AreEqual(1, present.Reads["Text"], "the left operand, once, " + mode);
                Assert.IsFalse(
                    present.Reads.ContainsKey("SpanText"),
                    "the right operand must not be evaluated when the left has a value, " + mode);

                var absent = new Counting();
                Assert.AreEqual(
                    "02:00:00",
                    Expression.ParseGetter<Counting, object>("NoText() ?? SpanText()", mode)
                        .GetValue(absent),
                    mode.ToString());

                Assert.AreEqual(1, absent.Reads["NoText"], "the left operand, once, " + mode);
                Assert.AreEqual(1, absent.Reads["SpanText"], "and the right one, once, " + mode);

                var nullable = new Counting();
                Assert.AreEqual(
                    7,
                    Expression.ParseGetter<Counting, object>("Maybe() ?? Num()", mode).GetValue(nullable),
                    mode.ToString());

                Assert.AreEqual(1, nullable.Reads["Maybe"], "a nullable left operand, once, " + mode);
                Assert.IsFalse(
                    nullable.Reads.ContainsKey("Num"),
                    "and it has a value, so the right operand is skipped, " + mode);
            }
        }

        /// <summary>
        /// A failing operand fails on both backends. This is the shape that made the
        /// lifted-arithmetic defect worse than a doubled side effect, and the shape no sweep could see.
        /// </summary>
        /// <remarks>
        /// <c>Nothing() + Boom()</c> answered <c>null</c> compiled - LINQ's lifted operator saw the
        /// left operand held nothing and never ran the right one - while the interpreter ran it and
        /// threw. <b>The compiled path swallowed the caller's exception and invented an answer.</b>
        /// <c>CompilationNeverLeaksTests</c> could not see it (compilation succeeded),
        /// <c>EvaluationNeverDivergesTests</c> could not (no operand in its corpus can fail), and this
        /// sweep skipped every throwing row. All three gaps are closed.
        /// </remarks>
        [Test]
        public void AFailingOperandFailsOnBothBackends()
        {
            foreach (var expression in new[]
            {
                "Nothing() + Boom()", "Nothing() - Boom()", "Nothing() * Boom()",
                "Num() + Boom()", "Boom() + Num()", "Nothing() < Boom()", "Num() between {Boom(), 10}"
            })
            {
                foreach (var mode in new[] { EvaluationMode.MustCompile, EvaluationMode.MustInterpret })
                {
                    IGetterExpression<Counting, object> built;

                    try
                    {
                        built = Expression.ParseGetter<Counting, object>(expression, mode);
                    }
                    catch (Exception)
                    {
                        // no compiled form for this shape - nothing to assert about evaluation
                        continue;
                    }

                    var root = new Counting();

                    Assert.Throws<InvalidOperationException>(
                        () => built.GetValue(root),
                        expression + " must not swallow the operand's failure, " + mode);

                    Assert.AreEqual(
                        1, root.Reads["Boom"],
                        expression + " - the failing operand ran exactly once, " + mode);
                }
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
                "SpanText()", "NoSpanText()", "Anything()", "Ints()", "Map()", "Boom()"
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

                // Writes, which the first version of this corpus generated none of. A setter is emitted
                // by a different path from a getter and has two operands of its own - the target and
                // the value - so it can reuse either.
                yield return "Target = " + value;
                yield return "Number = " + value;
                yield return "Name = " + value;
            }

            var sources = new[]
            {
                "Ints()", "NoInts()", "Map()", "Text()",
                "Empty()", "Set()", "Sequence()", "Amounts()"
            };
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Compilation either succeeds or refuses. Nothing else escapes it - for thousands of expressions
    /// nobody wrote a test for.
    /// </summary>
    /// <remarks>
    /// <p>
    /// An emitter that reports failure as anything but <see cref="CompileErrorException"/> escapes the
    /// weakly typed path's fallback and turns a shape the interpreter handles into a hard failure, in
    /// every mode including the default one. Sixteen such escapes were found and fixed one at a time,
    /// each by a test that happened to cover the shape; the ones that had no test - <c>45 + 'Ana'</c>,
    /// <c>DateTime + DateTime</c> - survived for years. This fixture exists because incidental coverage
    /// is not a guarantee.
    /// </p>
    /// <p>
    /// It generates the cross-product rather than listing expressions, so it covers combinations nobody
    /// would think to write. Most produce nonsense; that is the point - nonsense must be *refused*, not
    /// crash the emitter.
    /// </p>
    /// <p>
    /// See <c>_Docs/compilation-error-reporting.md</c>. The known-defect ledger below is the part to
    /// keep current: when a defect is fixed, its row is removed and this test says so.
    /// </p>
    /// </remarks>
    /// <summary>
    /// Overloaded constructors and an overloaded indexer, so the corpus can express a resolution
    /// chosen from a <i>value</i> - corpus gap ten.
    /// </summary>
    /// <remarks>
    /// <b>Both were resolved once and cached with no key</b> (`_Docs/open-issues.md` item 35), which
    /// the corpus could not reach: it constructed only arrays and indexed only with the constant
    /// <c>[0]</c> and <c>['a']</c>, so no constructor or indexer was ever chosen from an operand whose
    /// type varies. <c>Anything</c> is the operand that makes these rows bite - it holds <c>45</c>,
    /// then <c>null</c>, then <c>"text"</c> across the three roots, and the sweeps reuse one expression
    /// object across all three.
    /// <p>
    /// Top-level because the grammar cannot spell a nested type's name in <c>new</c>.
    /// </p>
    /// </remarks>
    public class ResolutionProbe
    {
        public ResolutionProbe(int n) { Picked = "int:" + n; }

        public ResolutionProbe(string s) { Picked = "string:" + s; }

        public string Picked { get; private set; }

        public string this[int i] { get { return "byInt:" + i; } }

        public string this[string s] { get { return "byString:" + s; } }
    }

    [TestFixture]
    public class CompilationNeverLeaksTests
    {
        public enum Colour { Red, Green }

        /// <summary>
        /// A receiver that is <b>not</b> <c>#this</c>, with a method to call on it - corpus gap nine.
        /// </summary>
        /// <remarks>See the class-level note; kept beside the corpus it belongs to.</remarks>
        /// <remarks>
        /// Every method call the corpus generated had the root as its receiver (<c>Text(x)</c>,
        /// <c>Count(x)</c>), so the receiver and <c>#this</c> were the same object and item 30 could
        /// not appear: the compiled path resolved arguments against the receiver where the interpreter
        /// used <c>#this</c>, and <c>Inner.Echo(Name)</c> therefore answered <c>"inner"</c> compiled
        /// and <c>"Ana"</c> interpreted - both compiling, neither complaining.
        /// <p>
        /// <c>Name</c> shadowing the root's is what makes the rows discriminate, and it was already
        /// here; only <c>Echo</c> had to be added. It returns its argument unchanged, so the value
        /// coming out names the member that went in with nothing else in the way.
        /// </p>
        /// </remarks>
        public class Inner
        {
            public string Name { get; set; } = "inner";

            public object Echo(object o) { return o; }
        }

        /// <summary>
        /// A caller's own type with an implicit conversion to an <b>integral</b> type - the operand kind
        /// no corpus held, and the gap that hid a divergence from all four sweeps at once.
        /// </summary>
        /// <remarks>
        /// The engine had only ever learned about conversions to a <i>real</i> type (decimal, double,
        /// float), so <c>TakesInt(tally)</c> answered <c>int:7</c> compiled and threw
        /// <c>InvalidCastException</c> interpreted - one backend answering while the other threw, which
        /// is the exact invariant <c>EvaluationNeverDivergesTests</c> exists to guard. It could not:
        /// every custom type either sweep generated converted to a real one, so the whole
        /// integral-conversion half of the surface was unsampled and both of that item's defects were
        /// found by probing it by hand.
        /// <p>
        /// Deliberately <b>not</b> real-valued, and deliberately without operators of its own -
        /// <c>CustomRealTypesTests</c> and <c>UserDefinedOperatorTests</c> already carry those, and a
        /// type with an operator would be answered by the operator lookup before any conversion is
        /// consulted, which is a different path from the one this row is here to sample.
        /// </p>
        /// </remarks>
        public struct Tally
        {
            public Tally(int count) { Count = count; }

            public readonly int Count;

            public static implicit operator int(Tally t) { return t.Count; }

            public override string ToString() { return "Tally(" + Count + ")"; }
        }

        public class Root
        {
            public string Name { get; set; } = "Ana";
            public int Number { get; set; } = 45;
            public long Big { get; set; } = 45L;
            public double Real { get; set; } = 4.5;
            public decimal Amount { get; set; } = 45.5m;
            public bool Flag { get; set; } = true;
            public char Letter { get; set; } = 'x';
            public Colour Colour { get; set; } = Colour.Red;
            public int? NullableNumber { get; set; } = 7;
            public int? NoNumber { get; set; }
            public DateTime When { get; set; } = new DateTime(2001, 1, 1);
            public TimeSpan Span { get; set; } = TimeSpan.FromDays(1);
            public object Anything { get; set; } = 45;
            public Inner Inner { get; set; } = new Inner();
            public List<int> Ints { get; set; } = new List<int> { 3, 1, 2 };
            public List<string> Names { get; set; } = new List<string> { "b", "a" };
            public int[] Array { get; set; } = { 1, 2, 3 };
            public ArrayList Old { get; set; } = new ArrayList { 1, 2 };
            public Hashtable OldMap { get; set; } = new Hashtable { { "a", 1 } };
            public Dictionary<string, int> Map { get; set; } = new Dictionary<string, int> { { "a", 1 } };

            // Four collection shapes the corpus had no example of, each added because a divergence was
            // found by hand in exactly the gap it leaves - see _Docs/open-issues.md items 23 and 24.
            // A sweep's corpus is a sample, and an empty ledger only means "nothing diverges among the
            // shapes we generate".
            //
            // Set:      a HashSet<T> is not the non-generic ICollection, which used to make every
            //           processor throw interpreted while the compiled path answered (item 23).
            // Sequence: a bare IEnumerable<T>, the same gap and the only shape that cannot answer
            //           count() without being walked.
            // Huge:     an overflowing List<int>. Enumerable.Sum(IEnumerable<int>) is checked while the
            //           interpreter accumulates in double, so this row needs data at the edge to show.
            // Amounts:  a decimal collection, empty in the "nulls and empties" root - SumAggregator's
            //           `total ?? 0d` hard-codes double when nothing was added.
            public HashSet<int> Set { get; set; } = new HashSet<int> { 3, 1, 2 };
            public IEnumerable<int> Sequence { get; set; } = new[] { 3, 1, 2 }.Select(x => x);
            public List<int> Huge { get; set; } = new List<int> { int.MaxValue, 1 };
            public List<decimal> Amounts { get; set; } = new List<decimal> { 3m, 1m, 2m };

            // Reals:   a float collection, because float is the one item type whose average() and
            //          sum() do not widen into double, and the corpus had no example - which is why
            //          'Floats.average()' answering Single compiled and Double interpreted was found
            //          by hand rather than by this sweep.
            public List<float> Reals { get; set; } = new List<float> { 3f, 1f, 2f };

            // Counted / Counts: a custom type converting to an integral type, and a collection of one.
            // See the Tally remarks - this is the operand kind whose absence hid a divergence from all
            // four sweeps, and the seventh corpus gap to be found by hand rather than by a test.
            // SomeType: a Type-valued operand, corpus gap eight. A Type receiver is resolved against
            // the type it *represents*, so it is the one operand kind whose member lookup runs down a
            // different path entirely - and the interpreter used to bind an instance method there and
            // invoke it with the Type object as its target (InvalidCastException where the compiled
            // path answered). Found by hand while diagnosing something else, like all seven before it;
            // see _Docs/open-issues.md items 29 and 31.
            public Type SomeType { get; set; } = typeof(string);

            // Corpus gap ten: something to construct and something to index whose resolution is
            // chosen from the operand - see the ResolutionProbe remarks.
            public ResolutionProbe Probe { get; set; } = new ResolutionProbe(0);

            // An index that reads a member of the root rather than a literal - corpus gap thirteen.
            // Length is deliberately a name an array also declares, so an index bound against the
            // container instead of #this picks a different member rather than failing to find one.
            public int Index { get; set; }
            public int Length { get; set; }

            public Tally Counted { get; set; } = new Tally(7);
            public List<Tally> Counts { get; set; } = new List<Tally>
            {
                new Tally(3), new Tally(1), new Tally(2)
            };

            public string Text(string s) { return s; }
            public int Count(IEnumerable e) { return 1; }
            public void Nothing() { }
            public int this[int i] { get { return i; } set { } }
        }

        /// <summary>
        /// The defects this sweep still finds, by the node that failed and the exception it failed with.
        /// </summary>
        /// <remarks>
        /// Each is absorbed into a refusal, so no caller is broken by it - the interpreter serves the
        /// expression. They are listed here rather than silently tolerated, because a swallowed defect
        /// nobody can see is the other half of the problem this fixture guards against.
        /// <p>
        /// Grouped by node and exception type rather than by message: a message carries type names and
        /// parameter formatting that differ between target frameworks, and this fixture runs on five.
        /// </p>
        /// </remarks>
        private static readonly string[] KnownDefects =
        {
            // empty, and it is meant to stay that way. A row here is an expression whose *emitter*
            // failed - not a shape without a compiled form, which is an ordinary refusal and does not
            // appear. Adding one is admitting a defect; the test fails either way until a row matches
            // reality.
        };

        [Test]
        public void NothingButARefusalEscapesCompilation()
        {
            var leaked = new List<string>();
            var defects = new SortedDictionary<string, int>();
            var attempted = 0;

            foreach (var expression in Corpus())
            {
                attempted++;

                try
                {
                    Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustCompile);
                }
                catch (CompileErrorException e)
                {
                    // InternalCompilerErrorException is internal on purpose - our defects are not part
                    // of the public vocabulary - so it is identified by name, the same way this fixture
                    // identifies the parser's internal SyntaxErrorException below. A rename cannot make
                    // this silently pass: every row of KnownDefects would then report as fixed, and the
                    // ledger assertion fails.
                    if (e.GetType().Name != "InternalCompilerErrorException")
                        continue;   // an ordinary refusal, which is the expected outcome for most of this corpus

                    var key = (e.NodeType == null ? "?" : e.NodeType.Name)
                        + "/" + (e.InnerException == null ? "?" : e.InnerException.GetType().Name);

                    defects[key] = defects.TryGetValue(key, out var count) ? count + 1 : 1;
                }
                catch (Exception e) when (e.GetType().Name == "SyntaxErrorException")
                {
                    // the parser rejected it before compilation was reached
                }
                catch (Exception e)
                {
                    leaked.Add(expression + " => " + e.GetType().Name + ": " + e.Message);
                }
            }

            Assert.Greater(attempted, 5000, "the corpus should be large enough to be worth running");

            Assert.IsEmpty(
                leaked,
                "compilation must throw CompileErrorException or nothing at all, and these escaped:"
                    + Environment.NewLine + string.Join(Environment.NewLine, leaked.Take(20)));

            AssertDefectsAreTheKnownOnes(defects);
        }

        /// <summary>
        /// The absorbed defects must be exactly the ones recorded - no new kinds, and none left listed
        /// after being fixed.
        /// </summary>
        private static void AssertDefectsAreTheKnownOnes(SortedDictionary<string, int> defects)
        {
            var found = defects.Keys.ToList();

            var unexpected = found.Where(k => !KnownDefects.Contains(k)).ToList();
            var fixedSince = KnownDefects.Where(k => !found.Contains(k)).ToList();

            var report = new StringBuilder();
            foreach (var defect in defects)
                report.AppendLine("  " + defect.Key + " x" + defect.Value);

            Assert.IsEmpty(
                unexpected,
                "a new kind of compiler defect appeared. It is absorbed, so nothing is broken - but it "
                    + "is a defect, and it needs a row in KnownDefects or a fix:" + Environment.NewLine
                    + report);

            Assert.IsEmpty(
                fixedSince,
                "these defects no longer occur - remove them from KnownDefects: "
                    + string.Join(", ", fixedSince));
        }

        /// <summary>
        /// A refusal states a reason. Two generic messages used to stand in for one, and each meant
        /// something worse than vagueness.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <c>"node produced no expression tree"</c> is what <see cref="SpringExpressions.BaseNode"/>
        /// says when an emit method returns null - and an emit method is <c>[NotNull]</c>, so the
        /// message can only ever mean a node broke its own contract. <c>OpAND</c>, <c>OpOR</c> and
        /// <c>OpXOR</c> each returned a <c>[CanBeNull]</c> helper result straight out, which was 1,638
        /// of this corpus' 7,556 refusals stating no reason whatsoever. Nothing may reach it again.
        /// </p>
        /// <p>
        /// <c>"no compiled implementation for this node type"</c> is the base method's own message and
        /// is legitimate for a node that genuinely has none - <c>FunctionNode</c>,
        /// <c>LocalFunctionNode</c>, <c>OpLike</c> and <c>QualifiedIdentifier</c>. What it must not do
        /// is stand in for "not these operands", which is how four nodes with a working compiled
        /// implementation used to refuse: they ended their emit with
        /// <c>return base.GetExpressionTreeIfPossible(...)</c>. This corpus reaches none of the four
        /// legitimate nodes, so the message must not appear here at all - and if a future corpus row
        /// reaches one, this assertion names it rather than hiding it.
        /// </p>
        /// <p>
        /// The third assertion is about the structured field rather than the text. A refusal raised by
        /// a <c>static</c> helper through the message-only <c>CompileErrorException</c> constructor has
        /// a null <c>NodeType</c>, so nothing can group it by node - which is exactly what happened
        /// when this corpus was first censused: 918 refusals came back nodeless, all of them from
        /// <c>EqualityHelper</c> and <c>MethodNode</c>'s argument binding. The helpers are passed the
        /// node being compiled now. <c>Compiler</c>'s three entry points are legitimately nodeless -
        /// they run outside node emit - and are not reachable from here.
        /// </p>
        /// </remarks>
        [Test]
        public void EveryRefusalStatesAReason()
        {
            var contractBreaks = new List<string>();
            var standIns = new List<string>();
            var nodeless = new List<string>();

            foreach (var expression in Corpus())
            {
                try
                {
                    Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustCompile);
                }
                catch (CompileErrorException e)
                {
                    if (e.NodeType == null)
                        nodeless.Add(expression + " => " + e.Message);

                    if (e.Message.Contains("node produced no expression tree")
                        || e.Message.Contains("node produced no assignment expression tree"))
                    {
                        contractBreaks.Add(expression + " => " + e.Message);
                    }
                    else if (e.Message.Contains("no compiled implementation for this node type")
                        || e.Message.Contains("no compiled assignment implementation for this node type"))
                    {
                        standIns.Add(expression + " => " + e.Message);
                    }
                }
                catch (Exception e) when (e.GetType().Name == "SyntaxErrorException")
                {
                }
            }

            Assert.IsEmpty(
                nodeless,
                "a refusal reached the caller with a null NodeType, so nothing can group it by node. "
                    + "That happens when a static helper uses the message-only CompileErrorException "
                    + "constructor instead of being passed the node being compiled - it was 918 of "
                    + "this corpus' refusals before EqualityHelper, MethodNode, ConstructorNode and "
                    + "MethodBaseHelpers were given one:"
                    + Environment.NewLine + string.Join(Environment.NewLine, nodeless.Take(20)));

            Assert.IsEmpty(
                contractBreaks,
                "an emit method returned null, which its [NotNull] forbids. The node has to throw "
                    + "CannotCompile itself - only it can name itself and its operands:"
                    + Environment.NewLine + string.Join(Environment.NewLine, contractBreaks.Take(20)));

            Assert.IsEmpty(
                standIns,
                "these refused through the base method's message, which claims the node has no compiled "
                    + "implementation at all. If that is true the node belongs on the list in this "
                    + "test's remarks; if it is false the node must say which operands it could not "
                    + "take:" + Environment.NewLine + string.Join(Environment.NewLine, standIns.Take(20)));
        }

        /// <summary>
        /// A name the caller got wrong is the caller's mistake, and must be reported as a refusal
        /// naming the node - never absorbed and returned as "internal compiler error … please report
        /// it", which blames the engine for a typo.
        /// </summary>
        /// <remarks>
        /// Four surfaces resolve a type name and three of them said this properly; <c>TypeNode</c> was
        /// missed when the others were converted, so <c>T(Nope)</c> alone told the caller to file a bug
        /// against us. The distinction is the same one that made six deliberate user-error throws
        /// convert to refusals: the absorber is for defects, and nothing else may reach it.
        /// </remarks>
        [Test]
        public void AnUnresolvableTypeNameIsTheCallersMistakeOnEverySurfaceThatResolvesOne()
        {
            foreach (var expression in new[]
                { "T(Nope)", "Number is T(Nope)", "new Nope()", "Number as T(Nope)", "Number as Nope" })
            {
                var refusal = Assert.Throws<CompileErrorException>(
                    () => Expression.ParseGetter<Root, object>(expression, EvaluationMode.MustCompile),
                    expression);

                Assert.AreNotEqual(
                    "InternalCompilerErrorException", refusal.GetType().Name,
                    "'" + expression + "' is a name the caller got wrong, not a defect of ours");

                StringAssert.Contains("does not resolve", refusal.Message, expression);

                Assert.Throws<TypeLoadException>(
                    () => Expression.Parse(expression).GetValue<Root>(new Root()),
                    "and the interpreter reports the unresolvable name at evaluation: " + expression);
            }
        }

        /// <summary>
        /// Every binary operator over every pair of operand kinds, then every operand kind through the
        /// unary, conditional, collection and conversion surfaces.
        /// </summary>
        /// <remarks>
        /// Shared with <c>EvaluationNeverDivergesTests</c>, which runs the same expressions against
        /// several sets of data and compares the two backends' outcomes. One corpus, two invariants:
        /// nothing escapes compilation, and the backends agree at evaluation.
        /// </remarks>
        internal static IEnumerable<string> Corpus()
        {
            var values = new[]
            {
                "Name", "Number", "Big", "Real", "Amount", "Flag", "Letter", "Colour",
                "NullableNumber", "NoNumber", "When", "Span", "Anything", "Inner", "Ints", "Array",
                "Old", "OldMap", "Map", "null", "'lit'", "45", "45.5", "true",

                // A custom type converting to an integral type - the operand kind that was missing.
                "Counted",

                // A Type-valued operand: corpus gap eight, and the seam item 29's divergence lived in.
                "SomeType"
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
                yield return value + " ? 1 : 2";
                yield return "1 ? " + value + " : 2";

                // A boolean test with one varying branch. Neither row above reaches a mismatch
                // between the branches: the first has two int branches, and the second is refused on
                // its non-boolean test before the branches are looked at - which is why the branch
                // types leaked out of LExpression.Condition for as long as they did.
                yield return "true ? " + value + " : 2";
                yield return "true ? 2 : " + value;
                yield return "true ? " + value + " : null";
                yield return value + " ?? 1";
                yield return value + " between {1, 10}";
                yield return value + " in {1, 2}";
                yield return value + " is T(System.Int32)";
                yield return value + ".ToString()";
                yield return "Text(" + value + ")";
                yield return "Count(" + value + ")";

                // A receiver that is not #this - corpus gap nine, and the shape item 30's silent wrong
                // answer lived in. Both rows above call a method on the *root*, so receiver and #this
                // are one object and an argument resolved against either gives the same value.
                yield return "Inner.Echo(" + value + ")";

                // Corpus gap ten: a constructor and an indexer chosen from the operand's type, which
                // is what item 35's two cache defects needed. Nothing here constructed a fixture type
                // or indexed with anything but a constant before.
                yield return "new SpringExpressionsTests.Expressions.ResolutionProbe("
                             + value + ").Picked";
                yield return "Probe[" + value + "]";
                yield return "{" + value + ", " + value + "}";
                yield return "#{'k' : " + value + "}";
                yield return "new int[] {" + value + "}";
                yield return value + " as string";
                yield return "as<object>(" + value + ")";
                yield return "Anything = " + value;
            }

            var sources = new[]
            {
                "Ints", "Names", "Array", "Old", "OldMap", "Map", "{1,2}", "Name",
                "Set", "Sequence", "Huge", "Amounts", "Reals",

                // A collection whose item type converts to an integral type: sort() reaches CompareTo,
                // distinct() reaches Equals and sum() a numeric conversion, none of them named by the
                // expression, and none of them previously sampled for such an item type.
                "Counts"
            };
            var processors = new[]
            {
                "sort()", "distinct()", "reverse()", "nonNull()", "sum()",
                "average()", "min()", "max()", "count()", "convert(decimal)",

                // orderBy was in neither sweep at all, which is how an absorbed compiler defect in it
                // survived: a comparer lambda whose subtraction does not yield an int handed a
                // Func<T,T,decimal> to a Func<T,T,int> parameter, and the ledger that exists to catch
                // exactly that never saw the expression.
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

                // Corpus gap fourteen: a body that mentions something outside itself. Every body
                // above is written in terms of #this alone, so nothing sampled what a body can
                // reach - and the answer was nothing at all, because the body was compiled to a
                // delegate separately and handed in as a constant. '#root' and every '#variable'
                // came out as an absorbed internal compiler error, which is precisely what this
                // fixture exists to catch and could not, for want of a row.
                yield return source + ".!{#root.Number}";
                yield return source + ".?{#root.Flag}";

                // A $local reaching into a body, and one written from inside it. Both were refused
                // outright until the body lambda was nested, so neither shape had ever been swept;
                // now they are ordinary compilations and the only thing standing between a local and
                // a member of whatever it holds is the cast an object-typed local always needed.
                yield return "($n = 1; " + source + ".!{#this})";
                yield return "($n = 1; " + source + ".?{#root.Flag})";
                yield return source + ".!{$x = #this}";

                // A collection the engine built, wrapped in an expression list, and one merely read
                // wrapped the same way. The wrapper used to cost the built one its root reshaping -
                // it came back typed compiled and object-typed interpreted - while the read one must
                // keep its own type and its identity. Both directions matter, and '; ' appeared in
                // no corpus at all before this.
                yield return "(1; " + source + ".!{#this})";
                yield return "(1; " + source + ")";
                yield return source + "[0]";
                yield return source + "['a']";

                // Corpus gap thirteen: an index that is not a constant. Every indexing row above
                // writes a literal, so nothing sampled the context an index resolves against - and
                // it was the container rather than #this, which is item 30's defect one node over.
                // 'Index' is declared on the root and 'Length' on both the root and an array, so the
                // second row is the one that binds a *different member* per backend rather than
                // merely refusing on one of them. A refusal is invisible to the evaluation sweep,
                // since there is no compiled answer to compare.
                yield return source + "[Index]";
                yield return source + "[Length]";
            }

            yield return "date('2001-01-01')";
            yield return "date('2001-01-01', 'yyyy')";
            yield return "date(Number)";
            yield return "T(System.Int32)";

            // names that do not resolve, on every surface that resolves one. A caller's typo must be
            // refused, never absorbed as a defect of ours - TypeNode was missed when its siblings were
            // converted, and this corpus did not catch it because these rows were not in it.
            yield return "T(Nope)";
            yield return "Number is T(Nope)";
            yield return "new Nope()";
            yield return "Number as T(Nope)";
            yield return "Number as Nope";
            yield return "Ints.convert(Nope)";
            yield return "new System.Text.StringBuilder()";
            yield return "new System.Text.StringBuilder(Number)";
            yield return "new System.Text.StringBuilder(Name, Number)";
        }
    }
}

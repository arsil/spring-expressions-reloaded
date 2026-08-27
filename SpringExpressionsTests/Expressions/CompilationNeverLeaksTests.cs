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
    [TestFixture]
    public class CompilationNeverLeaksTests
    {
        public enum Colour { Red, Green }

        public class Inner
        {
            public string Name { get; set; } = "inner";
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
            // the ternary emits Condition without checking that its test is boolean; the interpreter
            // coerces by truthiness. Needs a ruling, not a patch - see the design document
            "TernaryNode/ArgumentException",

            // '!' emits Not without checking the operand is boolean or integral
            "OpNOT/InvalidOperationException",

            // the collection-union path indexes something empty when an operand is an array
            "OpADD/IndexOutOfRangeException",

            // DateTimeAddDateTimeMethodInfo is null - DateTime.Add(DateTime) does not exist in the BCL,
            // so the reflective lookup returned null at type-initialisation and the branch guarded by
            // 'left is DateTime && right is DateTime' has been dead-and-crashing since it was written
            "OpADD/ArgumentNullException"
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
        /// Every binary operator over every pair of operand kinds, then every operand kind through the
        /// unary, conditional, collection and conversion surfaces.
        /// </summary>
        private static IEnumerable<string> Corpus()
        {
            var values = new[]
            {
                "Name", "Number", "Big", "Real", "Amount", "Flag", "Letter", "Colour",
                "NullableNumber", "NoNumber", "When", "Span", "Anything", "Inner", "Ints", "Array",
                "Old", "OldMap", "Map", "null", "'lit'", "45", "45.5", "true"
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
                yield return value + " ?? 1";
                yield return value + " between {1, 10}";
                yield return value + " in {1, 2}";
                yield return value + " is T(System.Int32)";
                yield return value + ".ToString()";
                yield return "Text(" + value + ")";
                yield return "Count(" + value + ")";
                yield return "{" + value + ", " + value + "}";
                yield return "#{'k' : " + value + "}";
                yield return "new int[] {" + value + "}";
                yield return value + " as string";
                yield return "as<object>(" + value + ")";
                yield return "Anything = " + value;
            }

            var sources = new[] { "Ints", "Names", "Array", "Old", "OldMap", "Map", "{1,2}", "Name" };
            var processors = new[]
            {
                "sort()", "distinct()", "reverse()", "nonNull()", "sum()",
                "average()", "min()", "max()", "count()", "convert(decimal)"
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

            yield return "date('2001-01-01')";
            yield return "date('2001-01-01', 'yyyy')";
            yield return "date(Number)";
            yield return "T(System.Int32)";
            yield return "new System.Text.StringBuilder()";
            yield return "new System.Text.StringBuilder(Number)";
            yield return "new System.Text.StringBuilder(Name, Number)";
        }
    }
}

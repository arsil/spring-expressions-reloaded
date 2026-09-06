using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// The sandbox's corpus: expressions that must be refused, and — once the catalog exists —
    /// expressions that must keep working.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>The negative list is the catalog's specification.</b> It is written before the catalog and
    /// not derived from it: "these expressions must be refused" is what the whole feature exists to
    /// deliver, and a catalog is then written until every row here refuses and nothing in the positive
    /// list does. Deriving the list from the catalog instead would only prove the catalog agrees with
    /// itself.
    /// </p>
    /// <p>
    /// It is the fourth sweep in this repo, and it differs from the other three in what it compares
    /// against. <c>CompilationNeverLeaksTests</c>, <c>EvaluationNeverDivergesTests</c> and
    /// <c>OperandReadsNeverDivergeTests</c> all compare the engine against itself; this one compares it
    /// against a stated intention. That makes the list itself the artefact worth reviewing - a row
    /// nobody thought of is a hole, exactly as a corpus gap was in the other three.
    /// </p>
    /// <p>
    /// <b>Both backends, every row.</b> Which one serves a caller is not their choice, so a refusal on
    /// one and an answer on the other would be the worst possible outcome - and it is the shape three
    /// separate defects took while the gates were being built.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class SandboxCorpusTests
    {
        /// <summary>
        /// Everything an expression must not be able to do. Grouped by what it would reach.
        /// </summary>
        /// <remarks>
        /// Written against a <see cref="SandboxPolicy.Restricted"/> whose catalog is still empty, so
        /// today most rows refuse for the trivial reason that nothing is catalogued. That is fine and
        /// deliberate: the list exists so that it keeps refusing once the catalog is written and most
        /// of these types have neighbours that are permitted. It is the regression guard for a catalog
        /// that does not exist yet.
        /// </remarks>
        /// <summary>
        /// The assembly System.Environment lives in, which is not the same on every target:
        /// System.Private.CoreLib on .NET, mscorlib on .NET Framework. Computed rather than written
        /// down, because a hard-coded qualifier passed on three TFMs and failed on the other two.
        /// </summary>
        private static readonly string CoreLib = typeof(Environment).Assembly.GetName().Name;

        private static readonly string[] MustBeRefused =
        {
            // --- process, filesystem, network, registry: effects, never catalogued (§5.3 rule 1)
            "new System.Diagnostics.Process()",
            "T(System.Diagnostics.Process)",
            "T(System.Diagnostics.Process).GetCurrentProcess()",
            "T(System.IO.File).Delete('nonexistent.txt')",
            "T(System.IO.Directory).GetCurrentDirectory()",
            "T(System.IO.Path).GetTempPath()",
            "new System.IO.FileStream('x', T(System.IO.FileMode).Open)",
            "T(System.Net.Http.HttpClient, System.Net.Http)",

            // --- the loader: the reason a forbid-list cannot work at all (§1.1)
            "T(System.Reflection.Assembly).Load('System.Net.Http')",
            "T(System.AppDomain).CurrentDomain",
            "T(System.Activator).CreateInstance(T(System.Object))",

            // --- reflection: the escape that needs no type name (§1.2)
            "'abc'.GetType().Assembly",
            "'abc'.GetType().Module",
            "'abc'.GetType().GetMethods()",
            "T(System.Int32).Assembly",
            "T(System.Int32).Assembly.FullName",

            // --- process-wide state: the effect nobody looks for (§5.3)
            "T(System.Globalization.CultureInfo).CreateSpecificCulture('de-DE')",
            // Assembly-qualified deliberately - see NoteOnTypeRegistryPoisoning below.
            "T(System.Environment, " + CoreLib + ").MachineName",

            // --- threads and interop
            "T(System.Threading.Thread).CurrentThread",
            "T(System.Runtime.InteropServices.Marshal, System.Runtime.InteropServices)",
            "T(System.GC).Collect()",

            // --- a forbidden type smuggled inside a generic argument (§4.1)
            "T(System.Collections.Generic.List<System.Diagnostics.Process>)",
            "new System.Diagnostics.Process[1]",
        };

        /// <summary>
        /// Rows that must be refused and must <b>never be evaluated</b>, sandbox on or off.
        /// </summary>
        /// <remarks>
        /// <c>Environment.Exit</c> and <c>FailFast</c> end the process, and a test suite that runs
        /// them turns a broken gate into a host crash instead of a red test - which is exactly what
        /// happened when this corpus was first written and every row was executed to prove it would
        /// otherwise work. <c>SetEnvironmentVariable</c> is not lethal but mutates the process for
        /// every test that follows.
        /// <p>
        /// So these are checked at <i>compile</i> only. That still exercises the type gate, which is
        /// where they would be stopped; it does not exercise the interpreter, whose resolution happens
        /// at evaluation. The non-lethal rows above carry that half.
        /// </p>
        /// </remarks>
        private static readonly string[] MustBeRefusedAndNeverRun =
        {
            "T(System.Environment, " + CoreLib + ").Exit(0)",
            "T(System.Environment, " + CoreLib + ").FailFast('x')",
            "T(System.Environment, " + CoreLib + ").SetEnvironmentVariable('x', 'y')",
        };

        [Test, TestCaseSource(nameof(MustBeRefused))]
        public void RefusedWhenCompiled(string expression)
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    expression, EvaluationMode.MustCompile, SandboxPolicy.Restricted),
                "compiled: " + expression);
        }

        [Test, TestCaseSource(nameof(MustBeRefused))]
        public void RefusedWhenInterpreted(string expression)
        {
            var interpreted = Expression.ParseGetter<object, object>(
                expression, EvaluationMode.MustInterpret, SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(
                () => interpreted.GetValue(null), "interpreted: " + expression);
        }

        [Test, TestCaseSource(nameof(MustBeRefusedAndNeverRun))]
        public void RefusedAtCompileAndNeverRun(string expression)
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    expression, EvaluationMode.MustCompile, SandboxPolicy.Restricted),
                expression);
        }

        [Test]
        public void EveryRefusalIsASandboxDenialAndNotSomeOtherFailure()
        {
            // A row that refuses for the wrong reason - a syntax error, a missing method, an
            // unresolvable name - is not testing anything, and would go on "passing" after the
            // catalog made the type reachable. So the corpus is only meaningful if each row would
            // otherwise work: with the sandbox off, every one of these must do something.
            var offences = new StringBuilder();

            var all = new List<string>(MustBeRefused);
            all.AddRange(MustBeRefusedAndNeverRun);

            foreach (var expression in all)
            {
                try
                {
                    // Compiled, never evaluated - see MustBeRefusedAndNeverRun for why this must not
                    // run anything. Building the delegate is enough: it resolves the type and the
                    // member, which is precisely what the gates intercept.
                    Expression.ParseGetter<object, object>(
                        expression,
                        EvaluationMode.MustCompile,
                        SandboxPolicy.DangerouslyAllowEverything);
                }
                catch (SandboxViolationException)
                {
                    offences.AppendLine("denied with the sandbox OFF: " + expression);
                }
                catch (SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException)
                {
                    // Resolved fine, simply has no compiled form. Not what this test is looking for.
                }
                catch (Exception e)
                {
                    offences.AppendLine(
                        expression + "  ->  " + e.GetType().Name + ": " + Shorten(e.Message));
                }
            }

            Assert.AreEqual(
                string.Empty,
                offences.ToString(),
                "every negative row must succeed with the sandbox off, or it is not the sandbox that "
                + "refuses it");
        }

        /// <summary>
        /// Why the Environment rows are spelled assembly-qualified: <c>ExpressionEvaluatorTests
        /// .TestTypeNodeIllegalType</c> calls
        /// <c>TypeRegistry.RegisterType("System.Environment", typeof(int))</c>, process-globally and
        /// permanently, and §3.1 rules that a registration <i>overrides</i> the catalog. So after that
        /// test has run - in whatever order NUnit chooses - the bare name resolves to <c>int</c> and
        /// these rows stop being denied.
        /// </summary>
        /// <remarks>
        /// <b>That is not a defect in the corpus, it is the ruling working, and it is worth seeing
        /// once.</b> A registration is the application deliberately rebinding a name, and the sandbox
        /// stands aside for it exactly as it stands aside for the engineer's own model (§2). The sharp
        /// edge is that <c>TypeRegistry</c> is a static mutable dictionary with process lifetime, so
        /// "deliberately" can mean "in an unrelated test, an hour ago" - which is the tension
        /// <c>_Docs/type-sandboxing.md</c> §3.1 records and does not resolve.
        /// <p>
        /// The assembly-qualified spelling is a different registry key, so it misses the alias and
        /// reaches the real type. The rows still test what they mean to test.
        /// </p>
        /// </remarks>
        [Test]
        public void ARegistrationOverridesTheCatalog()
        {
            SpringCore.TypeResolution.TypeRegistry.RegisterType("SandboxCorpusAlias", typeof(int));

            Assert.AreEqual(
                typeof(int),
                Expression.ParseGetter<object, object>(
                    "T(SandboxCorpusAlias)",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted).GetValue(null),
                "a registered name resolves unrestricted, whatever the catalog says - §3.1");
        }

        // ------------------------------------------------------------------------------------------
        // The positive half. A plausible domain object, and the expressions somebody would actually
        // write against it - property paths, dates, money, collections, formatting.
        // ------------------------------------------------------------------------------------------

        public class Address
        {
            public string City { get; set; } = "Kraków";
            public string Country { get; set; } = "PL";
        }

        public class Customer
        {
            public string Name { get; set; } = "Ana Kowalska";
            public Address Address { get; set; } = new Address();
        }

        public class OrderLine
        {
            public string Sku { get; set; } = "A-1";
            public int Quantity { get; set; } = 2;
            public decimal Amount { get; set; } = 19.99m;
        }

        public enum OrderStatus { Draft, Shipped, Cancelled }

        public class DomainOrder
        {
            public Customer Customer { get; set; } = new Customer();
            public DateTime ShippedOn { get; set; } = new DateTime(2024, 3, 17);
            public DateTime? CancelledOn { get; set; }
            public decimal Total { get; set; } = 129.95m;
            public OrderStatus Status { get; set; } = OrderStatus.Shipped;
            public string[] Tags { get; set; } = { "priority", "fragile" };
            public List<OrderLine> Lines { get; set; } = new List<OrderLine>
            {
                new OrderLine(),
                new OrderLine { Sku = "B-2", Quantity = 1, Amount = 90m }
            };
            public Dictionary<string, object> Meta { get; set; } =
                new Dictionary<string, object> { { "channel", "web" } };
        }

        /// <summary>
        /// Ordinary expressions that must keep working under the built-in catalog.
        /// </summary>
        /// <remarks>
        /// <b>This list is the specification for the catalog's nameable half</b>, the way
        /// <see cref="MustBeRefused"/> is for its forbidden half. It was written first and the catalog
        /// was then filled in until every row passed - not the other way round, which would only have
        /// proved the catalog agrees with itself.
        /// <p>
        /// Most rows need no catalog entry at all, and that is §5.2 working: <c>Customer</c>,
        /// <c>Address</c>, <c>DateTime</c>-as-a-property-type, <c>string</c>, <c>decimal</c> and
        /// <c>List&lt;&gt;</c> are all *arrived at*, so they are trusted. The rows that forced an entry
        /// are the ones that **name** a type - <c>new DateTime(…)</c>, <c>T(System.Math)</c>,
        /// <c>as int</c> - which is exactly the distinction the ruling draws.
        /// </p>
        /// </remarks>
        private static readonly string[] MustKeepWorking =
        {
            // --- property paths: the commonest thing anyone writes, and no entry is needed for any of it
            "Customer.Name",
            "Customer.Address.City",
            "Customer.Address.Country",
            "Total",
            "Status",

            // --- strings
            "Customer.Name.Length",
            "Customer.Name.ToUpper()",
            "Customer.Name + ' <' + Customer.Address.City + '>'",

            // --- dates, reached
            "ShippedOn.Year",
            "ShippedOn.Month",
            "ShippedOn.DayOfWeek",
            "ShippedOn.AddDays(7)",
            "ShippedOn.Ticks",

            // --- dates and numbers, formatted - the reason the culture types are catalogued
            "ShippedOn.ToString('yyyy-MM-dd')",
            "Total.ToString('0.00')",
            "Total.ToString('C', T(System.Globalization.CultureInfo).InvariantCulture)",
            "T(System.Globalization.CultureInfo).InvariantCulture.Name",

            // --- named types: the half that does need entries
            "new System.DateTime(2020, 1, 2)",
            "T(System.Math).Sqrt(9)",
            "T(System.Math).Max(2, 3)",
            "T(System.Convert).ToInt32('42')",
            "T(System.Guid).NewGuid().ToString().Length",
            "new System.String[] {'a', 'b'}",

            // --- arithmetic, comparison, ternary
            "Total * 2",
            "Total > 100",
            "Status == 'Shipped'",
            "ShippedOn.Year > 2000 ? 'recent' : 'old'",
            "CancelledOn == null",

            // --- collections: literals, aggregation, projection, selection
            "{1, 2, 3}.count()",
            "{1, 2, 3}.sum()",
            "Lines.count()",
            "Lines.!{ Sku }",
            "Lines.?{ Amount > 50 }.count()",
            "Lines.^{ Quantity == 1 }.Sku",
            "Lines.!{ Amount }.sum()",
            "Tags.Length",

            // --- indexing, which has no gate of its own (§4.2)
            "Tags[0]",
            "Meta['channel']",
            "Lines[0].Sku",

            // --- casts, which name a type
            "Total as System.Double",
            "'42' as System.String",
        };

        [Test, TestCaseSource(nameof(MustKeepWorking))]
        public void KeptWorkingWhenCompiled(string expression)
        {
            Assert.DoesNotThrow(
                () => Expression.ParseGetter<DomainOrder, object>(
                        expression, EvaluationMode.CompileOrInterpret, SandboxPolicy.Restricted)
                    .GetValue(new DomainOrder()),
                "compiled: " + expression);
        }

        [Test, TestCaseSource(nameof(MustKeepWorking))]
        public void KeptWorkingWhenInterpreted(string expression)
        {
            Assert.DoesNotThrow(
                () => Expression.ParseGetter<DomainOrder, object>(
                        expression, EvaluationMode.MustInterpret, SandboxPolicy.Restricted)
                    .GetValue(new DomainOrder()),
                "interpreted: " + expression);
        }

        [Test]
        public void TheCatalogTrustsNothingItHasNotBeenToldAbout()
        {
            // §5.3's closure rule, as DescribeImplicitTrust reports it since §5.2 changed what a gap
            // means: not "the caller gets an inert object" but "you are trusting this and nobody said
            // so". The list is expected to be non-empty - DateTime.Date hands back a DateTime and
            // nobody minds - so this is a visibility test, not an emptiness one. What it is for is
            // noticing the day a permitted member starts handing back something with a settable
            // static on it, which is how CultureInfo was found by hand (§5.3).
            var gaps = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted).DescribeImplicitTrust();

            // Nothing a permitted member hands back may come from a namespace the catalog exists to
            // keep out. This is the assertion that would have caught a real hole.
            foreach (var gap in gaps)
            {
                Assert.IsFalse(
                    gap.Contains("System.Reflection.")
                    || gap.Contains("System.Diagnostics.")
                    || gap.Contains("System.IO.")
                    || gap.Contains("System.Net.")
                    || gap.Contains("System.Threading.")
                    || gap.Contains("System.Runtime.InteropServices."),
                    "a permitted member hands back something from a namespace the catalog forbids: " + gap);
            }

            // And a counted ledger, the way the three other sweeps carry one - but a *ceiling* rather
            // than an exact number, because the count is TFM-dependent and an exact one would be a
            // cross-framework trap of the kind CLAUDE.md already records for TimeSpan's operators.
            // Measured: net472 28, netcoreapp2.1 29, net8.0 46, net10.0 50. The newer frameworks are
            // higher because generic math added Math.Abs/Clamp/Max/Min over IntPtr and the DivRem and
            // SinCos tuple overloads, and String gained GetPinnableReference.
            //
            // A ceiling is the security-relevant direction: it fails when the catalog starts trusting
            // *more* than it did, which is what a careless AllowAllMembersOf would do. Read the list
            // when it fails; do not just raise the number.
            //
            // What is in there today is dominated by the harmless: arrays of catalogued types, enums
            // (TypeCode, DayOfWeek, CalendarWeekRule), collection plumbing (Enumerator, KeyCollection,
            // IEqualityComparer<>), and ValueTuples from the generic-math overloads. Four are worth a
            // second look and none is exploitable, recorded because the next reader will wonder:
            //   IntPtr / UIntPtr  - Math.Abs/Clamp/Max/Min, .NET 7+ generic math
            //   System.Char&      - String.GetPinnableReference, a managed reference
            //   CompareInfo       - culture plumbing, read-only in practice
            //   TextInfo          - the same, and settable only on a culture nobody can install
            Assert.LessOrEqual(
                Distinct(gaps),
                50,
                "the catalog implicitly trusts more types than it did. Read them:"
                + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        // ------------------------------------------------------------------------------------------
        // The collection processors, which reach members reflectively on a collection's item type and
        // so are a member route neither gate sees. §9 listed this as the last unexamined path.
        // ------------------------------------------------------------------------------------------

        public class ForbiddenItem : IComparable
        {
            public int Rank { get; set; }
            public string Secret() => "reached";
            public int CompareTo(object other) => Rank.CompareTo(((ForbiddenItem)other).Rank);
            public override bool Equals(object o) => o is ForbiddenItem f && f.Rank == Rank;
            public override int GetHashCode() => Rank;
        }

        public class ForbiddenItemHolder
        {
            public List<ForbiddenItem> Items { get; set; } = new List<ForbiddenItem>
            {
                new ForbiddenItem { Rank = 2 }, new ForbiddenItem { Rank = 1 }
            };
        }

        private static SandboxPolicy WithoutTheItemType()
        {
            return SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted).Forbid<ForbiddenItem>().Build();
        }

        /// <summary>
        /// Naming a member of a forbidden item type is refused, by every route an expression has.
        /// </summary>
        [TestCase("Items[0].Rank")]
        [TestCase("Items[0].Secret()")]
        [TestCase("Items.!{ Rank }")]
        [TestCase("Items.?{ Rank > 1 }")]
        public void AForbiddenItemTypeIsOutOfReachWhereverAMemberIsNamed(string expression)
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<ForbiddenItemHolder, object>(
                    expression, EvaluationMode.MustCompile, WithoutTheItemType()),
                "compiled: " + expression);

            var interpreted = Expression.ParseGetter<ForbiddenItemHolder, object>(
                expression, EvaluationMode.MustInterpret, WithoutTheItemType());

            Assert.Throws<SandboxViolationException>(
                () => interpreted.GetValue(new ForbiddenItemHolder()), "interpreted: " + expression);
        }

        /// <summary>
        /// A collection processor refuses a forbidden item type, on both backends.
        /// </summary>
        /// <remarks>
        /// <b>Neither gate sees this route, which is why it needed one of its own</b>: <c>sort()</c>
        /// reaches the item type's <c>CompareTo</c>, <c>distinct()</c> its <c>Equals</c> and
        /// <c>GetHashCode</c>, <c>sum()</c> its implicit numeric conversion - none of which the
        /// expression names, and none of which goes through <c>PropertyOrFieldNode</c> or
        /// <c>MethodNode</c>'s member resolution. Measured first, then ruled.
        /// <p>
        /// <b>Ruled: an explicit refusal beats an implicit exposure.</b> <c>Forbid&lt;T&gt;()</c> is
        /// not "nobody mentioned this type" - §5.2 answers that with trust, and must, since an
        /// engineer's own model classes are uncatalogued and their collections have to keep sorting.
        /// It is the engineer going out of their way to say no. Where they have <i>also</i> exposed a
        /// <c>List&lt;T&gt;</c> of it they have contradicted themselves, and the more deliberate of
        /// the two statements wins.
        /// </p>
        /// <p>
        /// <b>All ten processors, including the three that touch nothing.</b> <c>count()</c>,
        /// <c>reverse()</c> and <c>nonNull()</c> only move items about and could safely have been left
        /// alone - but "count() works and sort() does not" is a distinction nobody can predict without
        /// reading <c>MethodNode</c>. A rule that fits in the head beats a technically minimal one.
        /// </p>
        /// <p>
        /// The cost, stated so it is not rediscovered as a bug: you can no longer count a collection
        /// of a forbidden item type. If the type is forbidden, counting them is a strange thing to
        /// want.
        /// </p>
        /// </remarks>
        [TestCase("Items.count()")]
        [TestCase("Items.sort()")]
        [TestCase("Items.distinct()")]
        [TestCase("Items.reverse()")]
        [TestCase("Items.nonNull()")]
        [TestCase("Items.orderBy({|a,b| 0})")]
        public void ACollectionProcessorRefusesAForbiddenItemTypeOnBothBackends(string expression)
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<ForbiddenItemHolder, object>(
                        expression, EvaluationMode.MustCompile, WithoutTheItemType())
                    .GetValue(new ForbiddenItemHolder()),
                "compiled: " + expression);

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<ForbiddenItemHolder, object>(
                        expression, EvaluationMode.MustInterpret, WithoutTheItemType())
                    .GetValue(new ForbiddenItemHolder()),
                "interpreted: " + expression);
        }

        [Test]
        public void MinAndMaxRefuseAForbiddenItemTypeToo()
        {
            // min() and max() used to hand back a ForbiddenItem that nothing could then use - §9's
            // "a forbidden type can still be produced" question in miniature. Under the ruling they
            // do not get that far: the aggregator is a collection processor like any other, so it
            // refuses on the item type before there is a value to hand back.
            foreach (var expression in new[] { "Items.min()", "Items.max()" })
            {
                Assert.Throws<SandboxViolationException>(
                    () => Expression.ParseGetter<ForbiddenItemHolder, object>(
                            expression, EvaluationMode.MustCompile, WithoutTheItemType())
                        .GetValue(new ForbiddenItemHolder()),
                    "compiled: " + expression);

                Assert.Throws<SandboxViolationException>(
                    () => Expression.ParseGetter<ForbiddenItemHolder, object>(
                            expression, EvaluationMode.MustInterpret, WithoutTheItemType())
                        .GetValue(new ForbiddenItemHolder()),
                    "interpreted: " + expression);
            }
        }

        // ------------------------------------------------------------------------------------------
        // Smuggling: put a forbidden value in a collection, take it out again, and use it. The gate
        // asks about the *receiver's type* wherever a member is named, so the container it travelled
        // in makes no difference - but that is a claim worth measuring rather than asserting.
        // ------------------------------------------------------------------------------------------

        public class SmugglingHolder
        {
            public object[] Mixed { get; set; } = { "abc".GetType(), 1 };
            public List<object> Types { get; set; } = new List<object> { typeof(string) };
        }

        /// <summary>
        /// A <see cref="Type"/> hidden in a collection is still a <see cref="Type"/> when it comes out.
        /// </summary>
        /// <remarks>
        /// The question this answers: can an expression build a collection holding something the
        /// catalog curates, pull it out through <c>min()</c>, an indexer or a projection - none of
        /// which names the member - and then reach a member the catalog refuses?
        /// <p>
        /// <b>No, and by construction rather than by luck.</b> Every one of these ends in a member
        /// access, and the member gate asks about the receiver's type at that point. How the receiver
        /// got there was never part of the question - which is the same reason indexing needs no gate
        /// of its own (§4.2) and the same sentence §2 states: holding a value grants nothing, using it
        /// is what is governed.
        /// </p>
        /// <p>
        /// <b>Asserted in the default mode, deliberately.</b> Under <c>MustCompile</c> half of these
        /// refuse a step earlier with an ordinary <c>CompileErrorException</c>, because <c>min()</c>
        /// and friends are object-typed and the member is not statically findable. That is not the
        /// sandbox and it would be a false pass: what matters is where a real caller ends up, and
        /// <c>CompileOrInterpret</c> falls back to the interpreter, which denies.
        /// </p>
        /// </remarks>
        [TestCase("'abc'.GetType().Assembly")]
        [TestCase("{ 'abc'.GetType() }[0].Assembly")]
        [TestCase("{ 'abc'.GetType() }.min().Assembly")]
        [TestCase("{ 'abc'.GetType() }.max().Assembly")]
        [TestCase("{ 'abc'.GetType() }.sort()[0].Assembly")]
        [TestCase("{ 'abc'.GetType() }.distinct()[0].Assembly")]
        [TestCase("{ 'abc'.GetType() }.reverse()[0].Assembly")]
        [TestCase("{ 'abc'.GetType() }.nonNull()[0].Assembly")]
        [TestCase("{ 'abc'.GetType() }.^{ true }.Assembly")]
        [TestCase("{ 'abc'.GetType() }.!{ Assembly }")]
        [TestCase("Types[0].Assembly")]
        [TestCase("Types.min().Assembly")]
        [TestCase("Types.max().Assembly")]
        [TestCase("Mixed.nonNull()[0].Assembly")]
        public void ACuratedTypeCannotBeSmuggledThroughACollection(string expression)
        {
            foreach (var mode in new[] { EvaluationMode.CompileOrInterpret, EvaluationMode.MustInterpret })
            {
                var denial = Assert.Throws<SandboxViolationException>(
                    () => Expression.ParseGetter<SmugglingHolder, object>(
                            expression, mode, SandboxPolicy.Restricted)
                        .GetValue(new SmugglingHolder()),
                    mode + ": " + expression);

                Assert.AreEqual("Assembly", denial.DeniedMember, expression);
            }
        }

        [Test]
        public void TheSmugglingRouteItselfWorks()
        {
            // The control the rows above need. Without it they would pass just as well if collections
            // of Type were impossible to build, or if every member on one were unreachable for some
            // unrelated reason - and the corpus would be asserting nothing.
            Assert.AreEqual(
                "System.String",
                Expression.ParseGetter<SmugglingHolder, object>(
                        "{ 'abc'.GetType() }[0].FullName",
                        EvaluationMode.CompileOrInterpret,
                        SandboxPolicy.Restricted)
                    .GetValue(new SmugglingHolder()));
        }

        /// <summary>How many distinct types the report names, which is the number worth watching.</summary>
        private static int Distinct(IList<string> gaps)
        {
            var types = new HashSet<string>(StringComparer.Ordinal);

            foreach (var gap in gaps)
            {
                const string marker = " hands back ";
                var at = gap.IndexOf(marker, StringComparison.Ordinal);

                if (at >= 0)
                    types.Add(gap.Substring(at + marker.Length));
            }

            return types.Count;
        }

        private static string Shorten(string message)
        {
            if (message == null)
                return "(null)";

            var firstLine = message.Split('\n')[0].Trim();

            return firstLine.Length <= 110 ? firstLine : firstLine.Substring(0, 110) + "…";
        }
    }
}

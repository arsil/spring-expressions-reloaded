using System;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A member may be permitted for reading, for writing, or for both — and the catalog says which.
    /// </summary>
    /// <remarks>
    /// The axis exists because refusing a member outright to stop its setter is collateral damage, and
    /// the catalog had three instances of it: <c>CultureInfo.CurrentCulture</c>,
    /// <c>Environment.CurrentDirectory</c> and <c>Environment.ExitCode</c> are all readers an ordinary
    /// application wants, each sitting beside a writer that changes unrelated code.
    /// <p>
    /// <b>Where the check happens differs by backend, and the tests below run both.</b> The compiled
    /// path has a separate emit method per direction, so it asks for the one it is. The interpreter
    /// builds one memoised accessor serving both <c>Get</c> and <c>Set</c>, so it answers both
    /// directions when the accessor is built and keeps two booleans — the lookup stays once per node,
    /// only the decision moves to the point of use.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class SandboxAccessDirectionTests
    {
        public class Model
        {
            public string Readable { get; set; } = "r";
            public string Writable { get; set; } = "w";
            public string Both { get; set; } = "b";
            public string Neither { get; set; } = "n";

            public string Danger() { return "did the dangerous thing"; }
        }

        [Test]
        public void AMethodIsGovernedByTheMethodVerbsAndNotTheDirectionalOnes()
        {
            // Why the catalog keys on the member's kind at all. The first cut of this axis keyed on
            // the name alone and mapped invoking onto *reading*, on the reasoning that a method has
            // one mode of use. Measured, that made two of the four directional verbs meaningless on
            // methods, in opposite and equally misleading directions - and the second of those was
            // dangerous, because it read as a refusal and refused nothing.
            var byMethodVerb = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowMethod<Model>(nameof(Model.Danger))
                .Build();

            Assert.AreEqual(
                "did the dangerous thing",
                Expression.ParseGetter<Model, object>(
                    "Danger()", EvaluationMode.MustCompile, byMethodVerb).GetValue(new Model()));

            // The directional verbs say PropertyOrField in their names, and that is what they mean:
            // naming a method here permits nothing, because there is no property of that name.
            var byDirectionalVerb = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowPropertyOrFieldRead<Model>(nameof(Model.Danger))
                .Build();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Model, object>(
                    "Danger()", EvaluationMode.MustCompile, byDirectionalVerb));
        }

        [Test]
        public void ExceptMethodIsWhatRefusesAMethodOnAWholeAllowedType()
        {
            // The sharpest edge in the name-keyed version, and the reason ExceptMethod exists:
            // AllowAllMembersOf<T>().ExceptWrite("Danger") refused nothing at all, because a method has
            // no write to refuse - a policy line that read as a refusal and was a no-op.
            var refused = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowAllMembersOf<Model>()
                .ExceptMethod<Model>(nameof(Model.Danger))
                .Build();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Model, object>(
                    "Danger()", EvaluationMode.MustCompile, refused));

            // and the properties of the same type are untouched by it
            Assert.AreEqual(
                "r",
                Expression.ParseGetter<Model, object>(
                    "Readable", EvaluationMode.MustCompile, refused).GetValue(new Model()));
        }

        [Test]
        public void ADirectionalRefusalAimedAtAMethodStillRefusesNothing()
        {
            // The limit the vocabulary makes visible but cannot remove: this compiles, because member
            // names are strings, and it refuses nothing - a method has no write. What changed is that
            // the line now reads wrong where it is typed: it says PropertyOrField about something that
            // is a method, and ExceptMethod is sitting next to it.
            //
            // Closing it entirely would need Build() to reject a directional verb aimed at a method.
            // Recorded rather than done: a well-named API where the mistake is legible is the ruling,
            // and validation is a separate decision.
            var ineffective = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowAllMembersOf<Model>()
                .ExceptPropertyOrFieldWrite<Model>(nameof(Model.Danger))
                .Build();

            Assert.AreEqual(
                "did the dangerous thing",
                Expression.ParseGetter<Model, object>(
                    "Danger()", EvaluationMode.MustCompile, ineffective).GetValue(new Model()),
                "a directional refusal cannot refuse a method - use ExceptMethod");
        }

        [Test]
        public void OneNameMayBePermittedForOneKindAndNotTheOther()
        {
            // What keying on the kind buys beyond making the verbs honest: the two kinds are separate
            // entries, so a name is permitted for whichever kind was named and no other.
            var propertyOnly = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowPropertyOrField<Model>(nameof(Model.Readable))
                .Build();

            Assert.AreEqual(
                "r",
                Expression.ParseGetter<Model, object>(
                    "Readable", EvaluationMode.MustCompile, propertyOnly).GetValue(new Model()));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Model, object>(
                    "Danger()", EvaluationMode.MustCompile, propertyOnly));

            // and the undirected Allow still means both kinds, which is what keeps every existing
            // catalog row and every existing consumer policy meaning what it did.
            var eitherKind = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<Model>(nameof(Model.Readable), nameof(Model.Danger))
                .Build();

            Assert.AreEqual(
                "r",
                Expression.ParseGetter<Model, object>(
                    "Readable", EvaluationMode.MustCompile, eitherKind).GetValue(new Model()));

            Assert.AreEqual(
                "did the dangerous thing",
                Expression.ParseGetter<Model, object>(
                    "Danger()", EvaluationMode.MustCompile, eitherKind).GetValue(new Model()));
        }

        private static SandboxPolicy Policy()
        {
            return SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowPropertyOrFieldRead<Model>(nameof(Model.Readable))
                .AllowPropertyOrFieldWrite<Model>(nameof(Model.Writable))
                .Allow<Model>(nameof(Model.Both))
                .Build();
        }

        [Test]
        public void AReadOnlyMemberReadsAndDoesNotWrite()
        {
            AssertReads("Readable", "r");
            AssertWriteDenied("Readable");
        }

        [Test]
        public void AWriteOnlyMemberWritesAndDoesNotRead()
        {
            AssertWrites("Writable");
            AssertReadDenied("Writable");
        }

        [Test]
        public void BothMeansBoth()
        {
            AssertReads("Both", "b");
            AssertWrites("Both");
        }

        [Test]
        public void AMemberPermittedInNeitherDirectionIsDeniedOutright()
        {
            AssertReadDenied("Neither");
            AssertWriteDenied("Neither");
        }

        [Test]
        public void AWriteOnlyMemberIsDeniedWhenTheExpressionAlsoReadsIt()
        {
            // Write-only is coherent rather than decorative: this is denied on its read half, and the
            // denial names the read even though the expression is an assignment.
            var policy = Policy();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Model, object>(
                    "Writable + 'x'", EvaluationMode.MustCompile, policy));
        }

        [Test]
        public void TheFlagsAreUnionedAcrossBuilderCallsRatherThanReplaced()
        {
            // Two calls describing one member must not have an order-dependent result.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowPropertyOrFieldWrite<Model>(nameof(Model.Readable))
                .AllowPropertyOrFieldRead<Model>(nameof(Model.Readable))
                .Build();

            Assert.AreEqual(
                "r",
                Expression.ParseGetter<Model, object>(
                    "Readable", EvaluationMode.MustCompile, policy).GetValue(new Model()));

            Expression.ParseSetter<Model, string>("Readable", EvaluationMode.MustCompile, policy)
                .SetValue(new Model(), "x");
        }

        [Test]
        public void ExceptWriteKeepsAWholeAllowedTypeReadable()
        {
            // The verb the catalog actually needed, and the shape CultureInfo now uses.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowAllMembersOf<Model>()
                .ExceptPropertyOrFieldWrite<Model>(nameof(Model.Readable))
                .Build();

            Assert.AreEqual(
                "r",
                Expression.ParseGetter<Model, object>(
                    "Readable", EvaluationMode.MustCompile, policy).GetValue(new Model()));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseSetter<Model, string>(
                    "Readable", EvaluationMode.MustCompile, policy));

            // and everything else on the type is untouched
            Expression.ParseSetter<Model, string>("Both", EvaluationMode.MustCompile, policy)
                .SetValue(new Model(), "x");
        }

        [Test]
        public void ExceptReadKeepsAWholeAllowedTypeWritable()
        {
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowAllMembersOf<Model>()
                .ExceptPropertyOrFieldRead<Model>(nameof(Model.Writable))
                .Build();

            Expression.ParseSetter<Model, string>("Writable", EvaluationMode.MustCompile, policy)
                .SetValue(new Model(), "x");

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Model, object>(
                    "Writable", EvaluationMode.MustCompile, policy));
        }

        [Test]
        public void ADirectionalRejectionOnABaseTypeIsNotUndoneByADerivedEntry()
        {
            // The rejection union OR-s directions, which is what makes this hold: a derived entry
            // mentioning only reading must not lift the base type's refusal to write.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .AllowAllMembersOf<Model>()
                .ExceptPropertyOrFieldWrite<Model>(nameof(Model.Readable))
                .Allow<Derived>(nameof(Model.Readable))
                .Build();

            Assert.AreEqual(
                "r",
                Expression.ParseGetter<Derived, object>(
                    "Readable", EvaluationMode.MustCompile, policy).GetValue(new Derived()));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseSetter<Derived, string>(
                    "Readable", EvaluationMode.MustCompile, policy));
        }

        public class Derived : Model
        {
        }

        [Test]
        public void ANameThatIsATypeRatherThanAMemberIsNotGovernedByTheAxis()
        {
            // How the first cut of this was found to be wrong: PropertyOrFieldNode also resolves *type
            // names*, and a member lookup that finds nothing must leave both directions permissive
            // rather than record a refusal - the node carries on and reaches a type accessor.
            // Recording false there denied 'Ints.convert(decimal)', 'Foo.FooType' and
            // 'Society.Society' across seventeen tests in both suites.
            Assert.AreEqual(
                new[] { 1m, 2m },
                Expression.Parse("Values.convert(decimal)")
                    .GetValue<Holder>(new Holder()) as System.Collections.IEnumerable);
        }

        public class Holder
        {
            public System.Collections.Generic.List<int> Values { get; set; }
                = new System.Collections.Generic.List<int> { 1, 2 };
        }

        [Test]
        public void EnvironmentIsCuratedRatherThanForbidden()
        {
            // It shipped forbidden outright, which denied NewLine along with Exit - §5.3 uses this very
            // type as its worked example of "permit readers, refuse effects" and §6.3 listed the set.
            // Neither suite noticed, because nothing used it and the one test that names the type
            // registers it to typeof(int) first.
            //
            // The allow-list is 11 names where a reject-list would be 23, which corrects §6.1's guess
            // that this type wanted the reject direction - and the allow direction is safer here for a
            // second reason: the framework keeps adding to Environment, so a reject-list would silently
            // admit whatever the next version brings.
            // Assembly-qualified, and computed rather than written out - §8.8's TypeRegistry poisoning.
            // ExpressionEvaluatorTests.TestTypeNodeIllegalType registers "System.Environment" to
            // typeof(int), process-globally and permanently, so the plain name resolves to int for
            // every test that parses after it and this fixture passed alone and failed in the full
            // run. Computed because Environment lives in mscorlib on net40/net472 and
            // System.Private.CoreLib elsewhere.
            var env = "T(" + typeof(Environment).AssemblyQualifiedName + ")";

            Assert.AreEqual(
                Environment.NewLine,
                Expression.Parse(env + ".NewLine").GetValue<object>(null));

            Assert.AreEqual(
                Environment.ProcessorCount,
                Expression.Parse(env + ".ProcessorCount").GetValue<object>(null));

            Assert.IsNotNull(
                Expression.Parse(env + ".OSVersion.Platform").GetValue<object>(null),
                "OSVersion needs System.OperatingSystem catalogued too - §5.3's closure rule");

            // The effects, and the disclosure. Identity is refused deliberately, which deviates from
            // §6.3's set: it names MachineName as allowed, and the server's name ends up in reports
            // and error messages a script can render. Ruled 2026-09-07 to refuse it along with
            // UserName and UserDomainName.
            foreach (var denied in new[]
                     {
                         "Exit(0)", "SetEnvironmentVariable('ZZ', 'x')", "FailFast('x')",
                         "GetEnvironmentVariable('PATH')", "CommandLine", "StackTrace",
                         "MachineName", "UserName", "UserDomainName", "CurrentDirectory"
                     })
            {
                Assert.Throws<SandboxViolationException>(
                    () => Expression.Parse(env + "." + denied).GetValue<object>(null),
                    denied);
            }
        }

        [Test]
        public void TheCultureIsReadableAndNotInstallable()
        {
            // What the axis bought in the built-in catalog. CurrentCulture used to be refused outright
            // to stop the setter, and §5.3's measurement is why the setter must stay refused: an
            // expression changed the process culture and every subsequent ToString('C') changed with
            // it. Reading it is the ordinary thing an expression wants.
            Assert.AreEqual(
                System.Globalization.CultureInfo.CurrentCulture.Name,
                Expression.Parse("T(System.Globalization.CultureInfo).CurrentCulture.Name")
                    .GetValue<object>(null));

            var setter = Expression.ParseSetter<object, System.Globalization.CultureInfo>(
                "T(System.Globalization.CultureInfo).CurrentCulture");

            Assert.Throws<SandboxViolationException>(
                () => setter.SetValue(null, System.Globalization.CultureInfo.InvariantCulture));

            // The rest of the culture ruling is unchanged: the thread defaults and the two members
            // that hand back a *writable* culture stay refused in both directions (§8.8).
            foreach (var denied in new[]
                     {
                         "DefaultThreadCurrentCulture", "CreateSpecificCulture('fr-FR')",
                         "GetCultures(0)", "ClearCachedData()"
                     })
            {
                Assert.Throws<SandboxViolationException>(
                    () => Expression.Parse("T(System.Globalization.CultureInfo)." + denied)
                        .GetValue<object>(null),
                    denied);
            }
        }

        private static void AssertReads(string member, string expected)
        {
            foreach (var mode in new[] { EvaluationMode.MustCompile, EvaluationMode.MustInterpret })
            {
                Assert.AreEqual(
                    expected,
                    Expression.ParseGetter<Model, object>(member, mode, Policy()).GetValue(new Model()),
                    member + " " + mode);
            }
        }

        private static void AssertWrites(string member)
        {
            foreach (var mode in new[] { EvaluationMode.MustCompile, EvaluationMode.MustInterpret })
            {
                var model = new Model();
                Expression.ParseSetter<Model, string>(member, mode, Policy()).SetValue(model, "set");

                Assert.AreEqual(
                    "set",
                    typeof(Model).GetProperty(member).GetValue(model, null),
                    member + " " + mode);
            }
        }

        private static void AssertReadDenied(string member)
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Model, object>(
                    member, EvaluationMode.MustCompile, Policy()),
                member + " compiled read");

            var interpreted = Expression.ParseGetter<Model, object>(
                member, EvaluationMode.MustInterpret, Policy());

            Assert.Throws<SandboxViolationException>(
                () => interpreted.GetValue(new Model()), member + " interpreted read");
        }

        private static void AssertWriteDenied(string member)
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseSetter<Model, string>(
                    member, EvaluationMode.MustCompile, Policy()),
                member + " compiled write");

            var interpreted = Expression.ParseSetter<Model, string>(
                member, EvaluationMode.MustInterpret, Policy());

            Assert.Throws<SandboxViolationException>(
                () => interpreted.SetValue(new Model(), "x"), member + " interpreted write");
        }
    }
}

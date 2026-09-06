using System;

using NUnit.Framework;

using SpringCore.TypeResolution;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Stage 2 of the sandbox: the type gate. Every type name an expression resolves goes through
    /// <c>TypeResolutionUtils.ResolveTypeForExpression</c>, on both backends.
    /// </summary>
    /// <remarks>
    /// The catalog was a stub while this fixture was written and is curated now (stage 4), so the
    /// denied names here are types the catalog really refuses - <c>System.Diagnostics.Process</c>
    /// above all - rather than "everything, because the list is empty".
    /// <p>
    /// Every test names its policy explicitly. The process default is still
    /// <see cref="SandboxPolicy.DangerouslyAllowEverything"/> until stage 5, which is what keeps both
    /// suites at baseline while the gates are built.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class SandboxTypeGateTests
    {
        [Test]
        public void ATypeNameOutsideTheCatalogIsDeniedOnBothBackends()
        {
            var compiled = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "T(System.Diagnostics.Process)",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            Assert.AreEqual(typeof(System.Diagnostics.Process), compiled.DeniedType);
            Assert.IsNull(compiled.DeniedMember);

            var interpreted = Expression.ParseGetter<object, object>(
                "T(System.Diagnostics.Process)",
                EvaluationMode.MustInterpret,
                SandboxPolicy.Restricted);

            var thrown = Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(null));

            Assert.AreEqual(typeof(System.Diagnostics.Process), thrown.DeniedType);
            Assert.AreEqual(compiled.Message, thrown.Message);
        }

        [Test]
        public void TheWeakPathDoesNotFallBackToTheInterpreter()
        {
            // The rule the whole exception type exists for. A CompileErrorException here would be
            // caught by WeaklyTypedExpression's fallback and turned into "interpret it instead", so
            // the caller would get a working expression and no error at all. See
            // _Docs/type-sandboxing.md §3.3.
            var expression = Expression.Parse(
                "T(System.Diagnostics.Process)",
                EvaluationMode.CompileOrInterpret,
                null,
                SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(() => expression.GetValue<object>(null));
        }

        [Test]
        public void ADenialSurvivesTheInternalCompilerErrorAbsorber()
        {
            // Compiler's entry points absorb anything that is not already a CompileErrorException,
            // so without an explicit exclusion a denial would arrive as an
            // InternalCompilerErrorException - which derives from CompileErrorException, which the
            // weak fallback catches. NUnit's Throws demands the exact type, so this fails the moment
            // that exclusion is removed.
            var thrown = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "new System.Diagnostics.Process()",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            Assert.IsFalse(thrown.Message.Contains("internal compiler error"));
            Assert.IsFalse(thrown.Message.Contains("please report it"));
        }

        [Test]
        public void ConstructingADeniedTypeIsDeniedOnBothBackends()
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "new System.Diagnostics.Process()",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            var interpreted = Expression.ParseGetter<object, object>(
                "new System.Diagnostics.Process()",
                EvaluationMode.MustInterpret,
                SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(null));
        }

        [Test]
        public void ACastTargetIsGated()
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "'abc' as T(System.Diagnostics.Process)",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            var interpreted = Expression.ParseGetter<object, object>(
                "'abc' as T(System.Diagnostics.Process)",
                EvaluationMode.MustInterpret,
                SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(null));
        }

        [Test]
        public void AnArrayConstructorsElementTypeIsGated()
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "new System.Diagnostics.Process[2]",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            var interpreted = Expression.ParseGetter<object, object>(
                "new System.Diagnostics.Process[2]",
                EvaluationMode.MustInterpret,
                SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(null));
        }

        [Test]
        public void AnAttributeTypeIsGatedLikeAnyOtherName()
        {
            // AttributeNode retries the name with "Attribute" appended when the first resolution
            // fails. Both attempts go through the gate - an ungated retry would be a way of naming a
            // type the first attempt was refused. Ruled at stage 2; §8.4 listed this node's verdict
            // as something to decide before the gate landed.
            //
            // This used to read @[System.Serializable], which the catalog now permits - the frozen
            // suite names it and it is the only framework attribute either suite does. DebuggerDisplay
            // is uncatalogued and exercises the same retry: neither "System.Diagnostics.DebuggerDisplay"
            // nor "System.Diagnostics.DebuggerDisplayAttribute" may be named.
            var interpreted = Expression.ParseGetter<object, object>(
                "@[System.Diagnostics.DebuggerDisplay]",
                EvaluationMode.MustInterpret,
                SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(null));
        }

        [Test]
        public void AnArrayOfAPermittedTypeIsNameable()
        {
            // The verdict test used to run before the decomposition, and an array type is never in the
            // catalog, so every array was Unknown and therefore denied - string[] refused while string
            // was allowed whole. An array is a construction over an element type, not a thing the
            // catalog rules on, so it is asked about its element and never about itself.
            Assert.AreEqual(
                typeof(DateTime[]),
                Expression.ParseGetter<object, object>(
                    "T(System.DateTime[])", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(null));

            Assert.AreEqual(
                typeof(int[,]),
                Expression.ParseGetter<object, object>(
                    "T(System.Int32[,])", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(null));

            Assert.AreEqual(
                new[] { "a", "b" },
                Expression.ParseGetter<object, string[]>(
                    "new System.String[] {'a','b'}", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(null));

            Assert.IsNull(
                Expression.ParseGetter<object, object>(
                    "null as string[]", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(null));
        }

        [Test]
        public void AnArrayOfAForbiddenTypeIsStillDenied()
        {
            // The other half of the same rule, and the reason the message is now honest: an array can
            // be denied for exactly one reason, so naming its element type always names the culprit.
            // Before the fix T(System.DateTime[], mscorlib) reported that System.DateTime was not
            // permitted - a type this very policy allows whole.
            var thrown = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "T(System.Diagnostics.Process[][])",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            Assert.AreEqual(typeof(System.Diagnostics.Process), thrown.DeniedType);
        }

        [Test]
        public void AnEnumIsNameableWhoeverDeclaredIt()
        {
            // An enum is data: its own named constants, plus what System.Enum gives every one of them.
            // Nothing on it reaches anything, so they are nameable as a class rather than catalogued
            // one at a time - which would have covered the framework's and missed the consumer's.
            Assert.AreEqual(
                typeof(DayOfWeek),
                Expression.ParseGetter<object, object>(
                    "T(System.DayOfWeek)", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(null));

            Assert.AreEqual(
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                Expression.ParseGetter<object, object>(
                    "T(System.Text.RegularExpressions.RegexOptions).IgnoreCase",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted).GetValue(null));

            TypeRegistry.RegisterType("SandboxGateProbeEnum", typeof(GateProbeEnum));

            Assert.AreEqual(
                GateProbeEnum.Second,
                Expression.ParseGetter<object, object>(
                    "T(SandboxGateProbeEnum).Second",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted).GetValue(null));
        }

        [Test]
        public void EveryCataloguedTypeIsNameableOnEveryFramework()
        {
            // A catalog entry is added with typeof(X) and matched against the Type an expression
            // resolves by name - and those are not always the same object. Measured on netcoreapp2.1:
            // typeof(Hashtable) is "…, System.Runtime.Extensions, 4.2.1.0" while resolving the name
            // gives "…, System.Private.CoreLib, 4.0.0.0", and Equals between them is false. So
            // T(System.Collections.Hashtable) was denied on that one framework and worked on the other
            // four - a boundary that moves with the runtime, which is the class of bug this fork
            // exists to remove. TryGetEntry falls back to the full name for exactly this.
            //
            // Sweeping the catalog rather than pinning Hashtable, because the next duplicated identity
            // will be some other type on some other framework, and a single row would not see it.
            foreach (var name in new[]
                     {
                         "System.Boolean", "System.Char", "System.String", "System.Int32",
                         "System.Int64", "System.Double", "System.Decimal", "System.DateTime",
                         "System.TimeSpan", "System.DateTimeOffset", "System.Guid", "System.Math",
                         "System.Convert", "System.Version", "System.Array", "System.Uri",
                         "System.Type", "System.Object", "System.Text.StringBuilder",
                         "System.Globalization.CultureInfo", "System.Collections.ArrayList",
                         "System.Collections.Hashtable", "System.Collections.IEnumerable",
                         "System.Collections.ICollection", "System.Collections.IList",
                         "System.Collections.IDictionary", "System.SerializableAttribute"
                     })
            {
                Assert.DoesNotThrow(
                    () => Expression.ParseGetter<object, object>(
                        "T(" + name + ")", EvaluationMode.MustCompile, SandboxPolicy.Restricted),
                    name);
            }

            // The forbidden entries are matched the same way, so they must still refuse.
            foreach (var name in new[] { "System.Reflection.Assembly", "System.AppDomain" })
            {
                Assert.Throws<SandboxViolationException>(
                    () => Expression.ParseGetter<object, object>(
                        "T(" + name + ")", EvaluationMode.MustCompile, SandboxPolicy.Restricted),
                    name);
            }
        }

        [Test]
        public void AForbiddenEnumIsStillForbidden()
        {
            // The blanket rule sits after the two forbidding checks on purpose, so an explicit refusal
            // still wins - the same ordering §8.8 ruled for collection processors.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Forbid<DayOfWeek>()
                .Build();

            var thrown = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "T(System.DayOfWeek)", EvaluationMode.MustCompile, policy));

            Assert.AreEqual(typeof(DayOfWeek), thrown.DeniedType);
        }

        [Test]
        public void ANullableAndTheCollectionInterfacesAreNameable()
        {
            // A collection named by its interface was denied while the same collection named by its
            // class was allowed, because only the concrete types had entries.
            Assert.AreEqual(
                typeof(int?),
                Expression.ParseGetter<object, object>(
                    "T(System.Nullable<System.Int32>)",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted).GetValue(null));

            Assert.AreEqual(
                typeof(System.Collections.Generic.IList<int>),
                Expression.ParseGetter<object, object>(
                    "T(System.Collections.Generic.IList<System.Int32>)",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted).GetValue(null));

            Assert.AreEqual(
                typeof(System.Collections.ICollection),
                Expression.ParseGetter<object, object>(
                    "T(System.Collections.ICollection)",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted).GetValue(null));
        }

        [Test]
        public void CataloguingObjectDoesNotHandEveryCataloguedTypeAnUnrestrictedVerdict()
        {
            // System.Object had to be catalogued - T(System.Object) is an ordinary expression - and it
            // is the one type that must not be allowed whole, however harmless its four members look.
            // Compute unions the entries up the ancestor chain and returns Unrestricted the moment an
            // ancestor allows everything, so an AllMembers entry on object would have handed every
            // catalogued type an unrestricted verdict: System.Type included, and with it Assembly and
            // Assembly.Load. This is that one line, written as a test.
            Assert.AreEqual(
                typeof(object),
                Expression.ParseGetter<object, object>(
                    "T(System.Object)", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(null));

            var thrown = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "'abc'.GetType().Assembly", EvaluationMode.MustCompile, SandboxPolicy.Restricted));

            Assert.AreEqual(typeof(Type), thrown.DeniedType);
            Assert.AreEqual("Assembly", thrown.DeniedMember);
        }

        private enum GateProbeEnum
        {
            First = 1,
            Second = 2
        }

        [Test]
        public void AGenericArgumentIsJudgedToo()
        {
            // The finding that changed what stage 2 had to include. GenericTypeResolver resolves the
            // generic definition and each type argument through the *ungated*
            // TypeResolutionUtils.ResolveType, so gating the entry point alone leaves the arguments
            // unexamined: List<> would be judged and Process never looked at. The verdict decomposes
            // the type instead, which is the only place the parts can be reached.
            var thrown = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "T(System.Collections.Generic.List<System.Diagnostics.Process>)",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            // The message names the part that was denied, not the composite that carried it.
            Assert.AreEqual(typeof(System.Diagnostics.Process), thrown.DeniedType);
            Assert.AreEqual(
                "The sandbox does not permit the type 'System.Diagnostics.Process'.",
                thrown.Message);
        }

        [Test]
        public void AnArrayIsJudgedByItsElementType()
        {
            var thrown = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "T(System.Diagnostics.Process[])",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            Assert.AreEqual(typeof(System.Diagnostics.Process), thrown.DeniedType);
        }

        [Test]
        public void WithTheSandboxOffEveryNameResolvesExactlyAsBefore()
        {
            var compiled = Expression.ParseGetter<object, object>(
                "T(System.Diagnostics.Process)",
                EvaluationMode.MustCompile,
                SandboxPolicy.DangerouslyAllowEverything);

            Assert.AreEqual(typeof(System.Diagnostics.Process), compiled.GetValue(null));

            var interpreted = Expression.ParseGetter<object, object>(
                "T(System.Diagnostics.Process)",
                EvaluationMode.MustInterpret,
                SandboxPolicy.DangerouslyAllowEverything);

            Assert.AreEqual(typeof(System.Diagnostics.Process), interpreted.GetValue(null));
        }

        [Test]
        public void OmittingThePolicyMeansTheProcessDefaultGoverns()
        {
            // Stage 5 inverted this test. It used to pin that an omitted policy left the gate inert,
            // which is what kept both suites at baseline while stages 2-4 were built; now an omitted
            // policy is the sandbox, which is the whole of what stage 5 changed for a caller.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>("T(System.Diagnostics.Process)"));

            // And the escape, spelled the way a reviewer greps for it.
            Assert.AreEqual(
                typeof(System.Diagnostics.Process),
                Expression.ParseGetter<object, object>(
                    "T(System.Diagnostics.Process)",
                    EvaluationMode.CompileOrInterpret,
                    SandboxPolicy.DangerouslyAllowEverything).GetValue(null));
        }

        [Test]
        public void ARegisteredTypeResolvesUnrestricted()
        {
            // §3.1: TypeRegistry is already the engineer's own allow-list, so a registered name is
            // never asked about. It is also the only mitigation available before the catalog exists -
            // an application can pre-empt a dangerous name by registering it to something harmless,
            // which is precisely what TestTypeNodeIllegalType does with System.Environment.
            TypeRegistry.RegisterType("SandboxGateProbeAlias", typeof(int));

            var value = Expression.ParseGetter<object, object>(
                "T(SandboxGateProbeAlias)",
                EvaluationMode.MustCompile,
                SandboxPolicy.Restricted).GetValue(null);

            Assert.AreEqual(typeof(int), value);
        }
    }
}

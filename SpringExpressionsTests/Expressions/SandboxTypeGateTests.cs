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
    /// The catalog is still empty (stage 4 curates it), so <see cref="SandboxPolicy.Restricted"/>
    /// denies every type that is not a <see cref="TypeRegistry"/> entry. That is what "stub" means and
    /// it is exactly what these tests want: they are about the gate, not about the catalog.
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
            var interpreted = Expression.ParseGetter<object, object>(
                "@[System.Serializable]",
                EvaluationMode.MustInterpret,
                SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(null));
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
        public void OmittingThePolicyStillMeansTodaysBehaviour()
        {
            // The process default is DangerouslyAllowEverything until stage 5 flips it, so every
            // existing caller is unaffected by the gate's arrival. This is what keeps both suites at
            // baseline through stages 2-4, and it is why they need no explicit opt-out - an earlier
            // draft of §8.1 said they would.
            Assert.AreSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.Default);

            Assert.AreEqual(
                typeof(System.Diagnostics.Process),
                Expression.ParseGetter<object, object>("T(System.Diagnostics.Process)").GetValue(null));
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

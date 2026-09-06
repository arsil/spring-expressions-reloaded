using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// The sandbox's vocabulary: the policy object, the process default, and the denial exception.
    /// The behavioural pins live in <c>SandboxTypeGateTests</c>, <c>SandboxMemberGateTests</c> and
    /// <c>SandboxCorpusTests</c>.
    /// </summary>
    /// <remarks>
    /// <b>The default is on since stage 5</b> (2026-09-06), and this suite runs under a policy of its
    /// own - see <c>SandboxDefaultForTheSuite</c> at the root of the project - so the tests below
    /// assert against <i>whatever that policy is</i> rather than naming a built-in singleton. Three of
    /// them used to assert the default was <c>DangerouslyAllowEverything</c>, which is what the flip
    /// changed.
    /// </remarks>
    [TestFixture]
    public class SandboxPolicyTests
    {
        [Test]
        public void TheProcessDefaultIsRestrictiveNow()
        {
            // Stage 5. The suite's own SetUpFixture then narrows it further to a policy naming the
            // fixture types its expressions construct, which is what a consumer writes - so the
            // assertion is "not the off switch" rather than a named singleton.
            Assert.AreNotSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.Default);

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>("new System.Diagnostics.Process()"));
        }

        [Test]
        public void TheProcessDefaultRejectsNull()
        {
            var original = SandboxPolicy.Default;

            Assert.Throws<ArgumentNullException>(() => SandboxPolicy.Default = null);
            Assert.AreSame(original, SandboxPolicy.Default);
        }

        [Test]
        public void TheProcessDefaultCanBeSwappedAndPutBack()
        {
            var original = SandboxPolicy.Default;
            try
            {
                SandboxPolicy.Default = SandboxPolicy.DangerouslyAllowEverything;
                Assert.AreSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.Default);
            }
            finally
            {
                SandboxPolicy.Default = original;
            }

            Assert.AreSame(original, SandboxPolicy.Default);
        }

        [Test]
        public void TheTwoBuiltInPoliciesAreStableSingletons()
        {
            // Each expression captures a policy instance when it is created, so identity is what makes
            // "this expression was parsed under this policy" a stable, inspectable fact.
            Assert.AreSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.DangerouslyAllowEverything);
            Assert.AreSame(SandboxPolicy.Restricted, SandboxPolicy.Restricted);
            Assert.AreNotSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.Restricted);
        }

        [Test]
        public void ASandboxViolationIsNotACompileError()
        {
            // The rule this whole exception exists for. CompileErrorException is a *routing* signal -
            // three places catch it under CompileOrInterpret and build an interpreter instead - so a
            // denial reported that way would be converted into an instruction to interpret, and the
            // caller would get a working expression back and no error. See type-sandboxing.md §3.3.
            Assert.IsFalse(
                typeof(CompileErrorException).IsAssignableFrom(typeof(SandboxViolationException)),
                "A sandbox denial must not be catchable as a compile error - the weakly typed fallback "
                + "would turn it into 'interpret this instead'.");

            Assert.AreEqual(typeof(Exception), typeof(SandboxViolationException).BaseType);
        }

        [Test]
        public void ATypeDenialNamesTheTypeAndCarriesIt()
        {
            var violation = new SandboxViolationException(typeof(System.Diagnostics.Process));

            Assert.AreEqual(
                "The sandbox does not permit the type 'System.Diagnostics.Process'.",
                violation.Message);
            Assert.AreEqual(typeof(System.Diagnostics.Process), violation.DeniedType);
            Assert.IsNull(violation.DeniedMember);
        }

        [Test]
        public void AMemberDenialNamesBothAndCarriesThem()
        {
            var violation = new SandboxViolationException(typeof(Type), "Assembly");

            Assert.AreEqual(
                "The sandbox does not permit the member 'Assembly' on type 'System.Type'.",
                violation.Message);
            Assert.AreEqual(typeof(Type), violation.DeniedType);
            Assert.AreEqual("Assembly", violation.DeniedMember);
        }

        [Test]
        public void ADenialRejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new SandboxViolationException(null));
            Assert.Throws<ArgumentNullException>(() => new SandboxViolationException(null, "Assembly"));
            Assert.Throws<ArgumentNullException>(() => new SandboxViolationException(typeof(Type), null));
        }
    }
}

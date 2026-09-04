using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Stage 1 of the sandbox: the policy object, the process default, and the denial exception. None
    /// of it is wired to anything yet - there is no type gate and no member gate - so the only thing
    /// these can assert is the shape of the vocabulary and that the default has not been switched on.
    /// The behavioural pins arrive with the gates; see <c>_Docs/type-sandboxing.md</c> §8.1.
    /// </summary>
    [TestFixture]
    public class SandboxPolicyTests
    {
        [Test]
        public void TheProcessDefaultIsPermissiveUntilTheSandboxIsSwitchedOn()
        {
            // Stage 5 flips this. Until then nothing about the library's behaviour has changed, which
            // is what lets stages 2-4 be built without either test suite going red for the wrong
            // reason.
            Assert.AreSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.Default);
        }

        [Test]
        public void TheProcessDefaultRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => SandboxPolicy.Default = null);
            Assert.AreSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.Default);
        }

        [Test]
        public void TheProcessDefaultCanBeSwappedAndPutBack()
        {
            var original = SandboxPolicy.Default;
            try
            {
                SandboxPolicy.Default = SandboxPolicy.Restricted;
                Assert.AreSame(SandboxPolicy.Restricted, SandboxPolicy.Default);
            }
            finally
            {
                SandboxPolicy.Default = original;
            }

            Assert.AreSame(SandboxPolicy.DangerouslyAllowEverything, SandboxPolicy.Default);
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

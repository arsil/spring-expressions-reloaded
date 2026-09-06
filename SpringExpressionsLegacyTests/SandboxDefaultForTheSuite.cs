using System.Reflection;

using NUnit.Framework;

using SpringExpressions;

/// <summary>
/// The policy the frozen suite runs under, and the measurement it exists to take: <b>this file is
/// the entire migration cost of the sandbox becoming the default.</b>
/// </summary>
/// <remarks>
/// <p>
/// <b>Why a new file is allowed here at all.</b> This suite is frozen and carries only the changes a
/// consumer would be forced to make when upgrading. A consumer whose expressions name their own types
/// is forced to write exactly this, so it qualifies - and it is a new file rather than an edit, so
/// not one upstream test moves. The alternative considered and rejected was a permissive default for
/// the weakly typed path, which <c>_Docs/type-sandboxing.md</c> §3.5 refuses precisely because it
/// would keep this suite green by construction and hide the change from the instrument built to see
/// it. Six lines in a setup file do not hide anything.
/// </p>
/// <p>
/// <b>Six types, and they are all <i>named</i> by an expression</b> - <c>new X(…)</c> through a
/// hand-built <c>ConstructorNode</c>, <c>T(X).Static()</c>, <c>T(X, assembly)</c>. Every other type
/// this suite touches is <i>walked to</i> and needs no entry, which is why upstream's whole
/// non-generic object graph goes on working untouched.
/// </p>
/// <p>
/// <c>AllowAllMembersOf</c> rather than <c>Allow</c>: cataloguing a type for naming also switches its
/// members from trusted to governed, so a bare <c>Allow&lt;T&gt;()</c> would name a type nothing can
/// then read. <see cref="Inventor"/> is the one that would hurt - most of this suite walks through it.
/// </p>
/// </remarks>
[SetUpFixture]
public class SandboxDefaultForTheSuite
{
    [OneTimeSetUp]
    public void ApplyThePolicyAConsumerWouldWrite()
    {
        SandboxPolicy.Default = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
            .AllowAllMembersOf<ExpressionEvaluator>()
            .AllowAllMembersOf<Inventor>()
            .AllowAllMembersOf<SingleMethodTestClass>()
            .AllowAllMembersOf<DerivedSingleMethodTestClass>()
            .AllowAllMembersOf<ConstructorNodeTests.PublicTestClass>()
            .AllowAllMembersOf(
                typeof(ConstructorNodeTests).GetNestedType(
                    "PrivateTestClass", BindingFlags.NonPublic))
            .Build();
    }
}

using System.Reflection;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressionsTests.Expressions;

/// <summary>
/// The policy this suite runs under, and the whole of what a consumer must write when the sandbox
/// became the default.
/// </summary>
/// <remarks>
/// <p>
/// <b>Outside any namespace on purpose.</b> An NUnit <see cref="SetUpFixtureAttribute"/> governs its
/// own namespace and everything below it, and this suite's fixtures live under two unrelated roots -
/// <c>SpringExpressions</c> for the inherited ones and <c>SpringExpressionsTests.Expressions</c> for
/// the new ones - so only a global one covers both.
/// </p>
/// <p>
/// <b>Type by type, not <c>AllowAssemblyOf</c>, and that is a deliberate choice about what this suite
/// demonstrates.</b> One assembly line would do the same job in two lines, but it also hands over
/// every static in the assembly - measured, see
/// <c>SandboxMemberGateTests.AllowingAnAssemblyAlsoHandsOverEveryStaticInIt</c> - which is not what a
/// careful application does. Listing the types is the migration a consumer actually performs, and
/// until now nothing exercised the per-type path at any scale. The assembly verb keeps its own pins.
/// </p>
/// <p>
/// <b><c>AllowAllMembersOf</c> rather than <c>Allow</c>, and this is the trap.</b> Cataloguing a type
/// to make it <i>nameable</i> also switches its members from trusted to governed (§5.2: an
/// uncatalogued type an expression reaches is trusted). So <c>Allow&lt;T&gt;()</c> with no member
/// names yields a type you can name and can do nothing with - measured: <c>new Thing(45).Picked</c>
/// is denied on <c>Picked</c>. That matters most for <see cref="Inventor"/>, which dozens of
/// currently-green tests walk through: a curated member list would govern it everywhere and break
/// them on whichever property was forgotten.
/// </p>
/// <p>
/// Every type below is here because an expression <b>names</b> it - <c>new X(…)</c>,
/// <c>T(X).Static()</c>, <c>x as X[]</c>, <c>x is T(X)</c>. Nothing is here for being walked to;
/// walking needs no entry at all, which is why a suite this size needs seventeen lines and not
/// seven hundred.
/// </p>
/// </remarks>
[SetUpFixture]
public class SandboxDefaultForTheSuite
{
    [OneTimeSetUp]
    public void ApplyThePolicyAConsumerWouldWrite()
    {
        SandboxPolicy.Default = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)

            // The library's own type. Nothing is self-trusting: T(SpringExpressions.ExpressionEvaluator,
            // SpringExpressions, …) is as much a named type as anything else.
            .AllowAllMembersOf<ExpressionEvaluator>()

            // Model types the expressions construct or cast to.
            .AllowAllMembersOf<Inventor>()

            // Overload-resolution fixtures - every one of these is reached by `new X(…)`.
            .AllowAllMembersOf<CtorWidening>()
            .AllowAllMembersOf<CtorTie>()
            .AllowAllMembersOf<CtorGate>()
            .AllowAllMembersOf<CtorMoney>()
            .AllowAllMembersOf<CtorHier>()
            .AllowAllMembersOf<ParamArrayConstructorCases>()
            .AllowAllMembersOf<OptionalParameterConstructorCases>()

            // Static-call fixtures - T(X, assembly).Static(…).
            .AllowAllMembersOf<ParamArrayCases>()
            .AllowAllMembersOf<OptionalParameterCases>()
            .AllowAllMembersOf<AmbiguousStaticCases>()
            .AllowAllMembersOf<SingleMethodTestClass>()
            .AllowAllMembersOf<DerivedSingleMethodTestClass>()

            // `x is T(Dog)` and `x is T(Animal)` - both operands of an `is` name a type, so the base
            // class needs its own entry. Inheritance lends *members* up the chain, never nameability.
            .AllowAllMembersOf<IsOperatorTests.Dog>()
            .AllowAllMembersOf<IsOperatorTests.Animal>()

            // ConstructorNodeTests builds its node by hand from typeof(...).FullName. PublicTestClass
            // is public; PrivateTestClass is not, so it cannot be named in C# and comes through
            // reflection - which is also a fair demonstration that the catalog keys on the Type and
            // not on anything about visibility.
            .AllowAllMembersOf<ConstructorNodeTests.PublicTestClass>()
            .AllowAllMembersOf(
                typeof(ConstructorNodeTests).GetNestedType(
                    "PrivateTestClass", BindingFlags.NonPublic))

            .Build();
    }
}

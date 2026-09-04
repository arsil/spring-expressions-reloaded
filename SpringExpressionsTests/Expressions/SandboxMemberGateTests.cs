using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Stage 3 of the sandbox: the member gate. This is the half that closes
    /// <c>'abc'.GetType().Assembly</c>, which names no type at all and so never meets the type gate.
    /// </summary>
    /// <remarks>
    /// Every test names its policy explicitly - the process default is still
    /// <see cref="SandboxPolicy.DangerouslyAllowEverything"/> until stage 5. The policies here are
    /// built with <see cref="SandboxPolicy.NewBasedOn"/>, which came forward from stage 4 because the
    /// gate is untestable without it: against the empty built-in catalog every member is denied, so
    /// the branch that actually matters would ship unexercised.
    /// </remarks>
    [TestFixture]
    public class SandboxMemberGateTests
    {
        public class Order
        {
            public string Customer { get; set; } = "Ana";
            public int Quantity { get; set; } = 3;
            public Dictionary<string, int> Totals { get; set; } = new Dictionary<string, int> { { "net", 42 } };
            public string Describe() => "an order";
            public string Secret() => "should not be reachable";
        }

        private static SandboxPolicy OrderPolicy()
        {
            return SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<Order>(nameof(Order.Customer), nameof(Order.Quantity), nameof(Order.Describe))
                .Build();
        }

        [Test]
        public void ACataloguedMemberIsReachableOnBothBackends()
        {
            Assert.AreEqual(
                "Ana",
                Expression.ParseGetter<Order, object>(
                    "Customer", EvaluationMode.MustCompile, OrderPolicy()).GetValue(new Order()));

            Assert.AreEqual(
                "Ana",
                Expression.ParseGetter<Order, object>(
                    "Customer", EvaluationMode.MustInterpret, OrderPolicy()).GetValue(new Order()));
        }

        [Test]
        public void ACataloguedMethodIsReachableOnBothBackends()
        {
            Assert.AreEqual(
                "an order",
                Expression.ParseGetter<Order, object>(
                    "Describe()", EvaluationMode.MustCompile, OrderPolicy()).GetValue(new Order()));

            Assert.AreEqual(
                "an order",
                Expression.ParseGetter<Order, object>(
                    "Describe()", EvaluationMode.MustInterpret, OrderPolicy()).GetValue(new Order()));
        }

        [Test]
        public void AnUncataloguedMemberOfACataloguedTypeIsDeniedOnBothBackends()
        {
            var compiled = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Order, object>(
                    "Totals", EvaluationMode.MustCompile, OrderPolicy()));

            Assert.AreEqual(typeof(Order), compiled.DeniedType);
            Assert.AreEqual("Totals", compiled.DeniedMember);
            Assert.AreEqual(
                "The sandbox does not permit the member 'Totals' on type "
                + "'SpringExpressionsTests.Expressions.SandboxMemberGateTests+Order'.",
                compiled.Message);

            var interpreted = Expression.ParseGetter<Order, object>(
                "Totals", EvaluationMode.MustInterpret, OrderPolicy());

            var thrown = Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(new Order()));

            Assert.AreEqual(compiled.Message, thrown.Message);
        }

        [Test]
        public void AnUncataloguedMethodIsDeniedOnBothBackends()
        {
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Order, object>(
                    "Secret()", EvaluationMode.MustCompile, OrderPolicy()));

            var interpreted = Expression.ParseGetter<Order, object>(
                "Secret()", EvaluationMode.MustInterpret, OrderPolicy());

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(new Order()));
        }

        [Test]
        public void TheReflectionEscapeIsClosed()
        {
            // §1.1's finding, and the reason the member gate exists at all: this reaches Assembly from
            // a bare string literal, naming no type, so the type gate never sees it. Denied at
            // GetType, which System.Object does not catalogue here.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<string>(nameof(string.Length))
                .Build();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "'abc'.GetType().Assembly", EvaluationMode.MustCompile, policy));

            var interpreted = Expression.ParseGetter<object, object>(
                "'abc'.GetType().Assembly", EvaluationMode.MustInterpret, policy);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(null));
        }

        [Test]
        public void PermittingGetTypeStillDoesNotPermitAssembly()
        {
            // The catalog's own worked example: System.Type is reachable with descriptive members
            // only, so the chain gets one step further and stops. Permitting a member without the
            // members of what it returns is what §5.3's closure rule is about.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<string>(nameof(string.Length))
                .Allow<object>(nameof(object.GetType))
                .Allow<Type>(nameof(Type.FullName))
                .Build();

            Assert.AreEqual(
                typeof(string).FullName,
                Expression.ParseGetter<object, object>(
                    "'abc'.GetType().FullName", EvaluationMode.MustCompile, policy).GetValue(null));

            var denied = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "'abc'.GetType().Assembly", EvaluationMode.MustCompile, policy));

            Assert.AreEqual("Assembly", denied.DeniedMember);
        }

        [Test]
        public void AnInheritedMemberIsPermittedByTheTypeThatDeclaresIt()
        {
            // Reachability comes from the type's own entry; the member list is the union up the
            // chain. So GetType is written once, on System.Object, rather than on every entry that
            // wants it - and cataloguing System.Object does not by itself make String reachable.
            var withObject = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<string>(nameof(string.Length))
                .Allow<object>(nameof(object.GetType))
                .Build();

            Assert.AreEqual(
                typeof(string),
                Expression.ParseGetter<object, object>(
                    "'abc'.GetType()", EvaluationMode.MustCompile, withObject).GetValue(null));

            var withoutObject = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<string>(nameof(string.Length))
                .Build();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "'abc'.GetType()", EvaluationMode.MustCompile, withoutObject));
        }

        [Test]
        public void CataloguingABaseTypeDoesNotMakeEveryTypeReachable()
        {
            var onlyObject = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<object>(nameof(object.GetType))
                .Build();

            var denied = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<object, object>(
                    "'abc'.Length", EvaluationMode.MustCompile, onlyObject));

            Assert.AreEqual(typeof(string), denied.DeniedType);
        }

        public class Containers
        {
            public Dictionary<string, int> Concrete { get; set; } = new Dictionary<string, int> { { "a", 1 } };
            public IDictionary<string, int> AsInterface { get; set; } = new Dictionary<string, int> { { "a", 2 } };
            public IList<int> ListAsInterface { get; set; } = new List<int> { 20 };
            public int[] Array { get; set; } = { 30 };
        }

        [Test]
        public void IndexingIsNotGatedAtAllAndBothBackendsAgree()
        {
            // The stage 3 ruling, and it took three attempts. First an "Item" member rule, which is
            // not implementable: the interpreter dispatches on the container's runtime shape - Array,
            // IList, IDictionary, string - and only its last resort resolves an Item property, while
            // the compiled path emits ArrayIndex, a TryGetValue or a get_Item call. There is no name
            // both backends could be asked about.
            //
            // Then "the container's type must be reachable", which is not implementable either, and
            // this is the row that showed it: AsInterface is declared IDictionary<,> and holds a
            // Dictionary<,>, so the compiled gate asked about the interface and the interpreter's
            // about the concrete type. Denied compiled, served interpreted - or the reverse, with the
            // interfaces catalogued instead. Same static-versus-runtime split the whole fork is about.
            //
            // So indexing has no gate: what governs it is the member that produced the container.
            // Only Containers' own members are catalogued below - no container type is - and every
            // row still answers on both backends.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<Containers>(
                    nameof(Containers.Concrete), nameof(Containers.AsInterface),
                    nameof(Containers.ListAsInterface), nameof(Containers.Array))
                .Build();

            foreach (var row in new[]
                     {
                         Tuple.Create("Concrete['a']", (object)1),
                         Tuple.Create("AsInterface['a']", (object)2),
                         Tuple.Create("ListAsInterface[0]", (object)20),
                         Tuple.Create("Array[0]", (object)30)
                     })
            {
                Assert.AreEqual(
                    row.Item2,
                    Expression.ParseGetter<Containers, object>(
                        row.Item1, EvaluationMode.MustCompile, policy).GetValue(new Containers()),
                    "compiled: " + row.Item1);

                Assert.AreEqual(
                    row.Item2,
                    Expression.ParseGetter<Containers, object>(
                        row.Item1, EvaluationMode.MustInterpret, policy).GetValue(new Containers()),
                    "interpreted: " + row.Item1);
            }
        }

        [Test]
        public void ReachingTheContainerIsWhatIsGated()
        {
            // Indexing is ungated, so the defence is one link earlier: the member that hands the
            // container over. Deny that and the indexing never happens.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<Containers>(nameof(Containers.Concrete))
                .Build();

            var denied = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Containers, object>(
                    "AsInterface['a']", EvaluationMode.MustCompile, policy));

            Assert.AreEqual("AsInterface", denied.DeniedMember);

            var interpreted = Expression.ParseGetter<Containers, object>(
                "AsInterface['a']", EvaluationMode.MustInterpret, policy);

            Assert.AreEqual(
                denied.Message,
                Assert.Throws<SandboxViolationException>(
                    () => interpreted.GetValue(new Containers())).Message);
        }

        [Test]
        public void AnArrayIsReachableWhenItsElementTypeIsAndItsMembersComeFromSystemArray()
        {
            // Nobody should have to catalogue int[], string[] and every other instantiation, so an
            // array's reachability follows its element type. Its members come from the union up the
            // chain, where System.Array's entry is the useful one - it covers every array at once.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<Containers>(nameof(Containers.Array))
                .Allow<int>()
                .Allow(typeof(Array), nameof(Array.Length))
                .Build();

            Assert.AreEqual(
                1,
                Expression.ParseGetter<Containers, object>(
                    "Array.Length", EvaluationMode.MustCompile, policy).GetValue(new Containers()));

            Assert.AreEqual(
                1,
                Expression.ParseGetter<Containers, object>(
                    "Array.Length", EvaluationMode.MustInterpret, policy).GetValue(new Containers()));
        }

        [Test]
        public void MemberNamesMatchCaseInsensitivelyBecauseTheBinderDoes()
        {
            // Upper('x'), upper('x') and UPPER('x') all resolve the same method in this engine, so a
            // case-sensitive catalog would deny a spelling the binder accepts - a gate disagreeing
            // with the thing it gates. Item 11 in _Docs/open-issues.md is whether the language should
            // be case-insensitive at all; the comparer moves with that ruling if it is ever made.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<Order>(nameof(Order.Customer))
                .Build();

            Assert.AreEqual(
                "Ana",
                Expression.ParseGetter<Order, object>(
                    "customer", EvaluationMode.MustInterpret, policy).GetValue(new Order()));
        }

        [Test]
        public void WithTheSandboxOffEveryMemberIsReachable()
        {
            Assert.AreEqual(
                "should not be reachable",
                Expression.ParseGetter<Order, object>(
                    "Secret()",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.DangerouslyAllowEverything).GetValue(new Order()));
        }

        [Test]
        public void ABuilderProducesANewPolicyAndLeavesItsBaseAlone()
        {
            var builder = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted).Allow<Order>(nameof(Order.Customer));
            var first = builder.Build();

            builder.Allow<Order>(nameof(Order.Quantity));
            var second = builder.Build();

            Assert.AreNotSame(first, second);

            // The first policy is unaffected by what the builder did afterwards - which is the whole
            // reason this is a builder producing immutable policies rather than a fluent method on
            // the policy itself.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Order, object>("Quantity", EvaluationMode.MustCompile, first));

            Assert.AreEqual(
                3,
                Expression.ParseGetter<Order, object>(
                    "Quantity", EvaluationMode.MustCompile, second).GetValue(new Order()));
        }

        [Test]
        public void TheOffSwitchCannotBeUsedAsABuilderBase()
        {
            // Adding permissions to a policy that already permits everything is meaningless, and
            // treating it as an empty catalog would silently turn "start from the sandbox being off"
            // into "start from everything denied".
            Assert.Throws<ArgumentException>(
                () => SandboxPolicy.NewBasedOn(SandboxPolicy.DangerouslyAllowEverything));
        }
    }
}

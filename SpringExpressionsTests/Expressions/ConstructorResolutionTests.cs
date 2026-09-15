using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class CtorWidening
    {
        public string Picked { get; private set; }
        public CtorWidening(long x) { Picked = "long:" + x; }
    }

    public class CtorTie
    {
        public string Picked { get; private set; }
        public CtorTie(double x) { Picked = "double:" + x; }
        public CtorTie(decimal x) { Picked = "decimal:" + x; }
    }

    public class CtorGate
    {
        public string Picked { get; private set; }
        public CtorGate(object o) { Picked = "object version"; }
        public CtorGate(string s) { Picked = "string version"; }
    }

    public class CtorMoney
    {
        public string Picked { get; private set; }
        public CtorMoney(decimal d) { Picked = "decimal:" + d; }
    }

    public class CtorHier
    {
        public string Picked { get; private set; }
        public CtorHier(object o) { Picked = "object version"; }
        public CtorHier(ResolutionDerived d) { Picked = "derived version"; }
    }

    /// <summary>
    /// Candidates a null can reach and a string cannot - the shape that told the two backends apart,
    /// since the compiled path rules out CtorNull(Uri) from the static type and the interpreter, given
    /// only a null, ranks both and lands on the more specific one.
    /// </summary>
    public class CtorNull
    {
        public string Picked { get; private set; }
        public CtorNull(object o) { Picked = "object version"; }
        public CtorNull(Uri u) { Picked = "Uri version"; }
    }

    public class CtorContext
    {
        public object Payload { get { return "payload"; } }
        public MoneyLike Amount { get { return new MoneyLike(45.5m); } }
        public ResolutionDerived DerivedHoldingSealed { get { return new ResolutionSealed(); } }

        /// <summary>A string-typed property holding null, for the declared-type rule.</summary>
        public string StringPropNull { get { return null; } }

        public string StringProp { get { return "text"; } }
    }

    /// <summary>
    /// The overload-resolution ruling applied to constructors, tier for tier - see
    /// OverloadResolutionTests for the method half. The compiled path used to resolve through the
    /// exact-type GetConstructor: the DefaultBinder's AmbiguousMatchException escaped compilation
    /// past the fallback's catch, its primitive widening succeeded where the interpreter's
    /// assignability-only scan threw ("new Thing(45)" against Thing(long) was a
    /// succeeds-versus-throws divergence), and an unresolvable type name leaked TypeLoadException at
    /// compile time. Both backends now resolve constructors from the same tiers, with the same
    /// overload gate and the same betterness tie-break.
    /// </summary>
    [TestFixture]
    public class ConstructorResolutionTests : BaseCompiledTests
    {
        /// <summary>
        /// A single candidate widens on every path: interpreted used to throw "constructor does not
        /// exist" (the legacy scan knows assignability, not widening) while compiled succeeded
        /// through the DefaultBinder - the invoker's argument converter now performs the int-to-long
        /// conversion the widening tier resolves.
        /// </summary>
        /// <summary>
        /// A null argument is matched by the type its node was declared as, so both backends rule out
        /// the same candidates and call the same constructor.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The method half of this landed on 2026-09-14 and the constructor half the day after, found
        /// by asking whether the same defect existed one node over rather than by a failing test.
        /// It did, in both of its shapes: <c>new CtorNull(StringPropNull)</c> called
        /// <c>CtorNull(object)</c> compiled and <c>CtorNull(Uri)</c> interpreted - the silent one -
        /// and a pair of incomparable candidates threw <c>AmbiguousMatchException</c> interpreted
        /// while the compiled path answered.
        /// </p>
        /// <p>
        /// <b>Indexers needed nothing</b>, and the reason is worth keeping: the interpreter's legacy
        /// tier maps a null index to <c>typeof(object)</c> and exact-matches <c>this[object]</c>
        /// (IndexerResolutionTests' quirk 2), which the compiled path replays. A quirk that has been
        /// there since upstream happens to be exactly this rule, arrived at from the other side.
        /// </p>
        /// </remarks>
        [Test]
        public void ANullArgumentPicksTheSameConstructorOnBothBackends()
        {
            TestCompiledVsInterpreted<CtorContext, string>(
                    "new SpringExpressionsTests.Expressions.CtorNull(StringPropNull).Picked",
                    new CtorContext())
                .ResultEqualsTo("object version");

            // The same constructor set with a value present, so a regression that simply stopped
            // considering the second candidate could not pass by leaving this alone.
            TestCompiledVsInterpreted<CtorContext, string>(
                    "new SpringExpressionsTests.Expressions.CtorNull(StringProp).Picked",
                    new CtorContext())
                .ResultEqualsTo("object version");
        }

        [Test]
        public void SingleCandidateWidensOnEveryPath()
        {
            TestCompiledVsInterpreted<string>(
                    "new SpringExpressionsTests.Expressions.CtorWidening(45).Picked")
                .ResultEqualsTo("long:45");
        }

        /// <summary>
        /// int against double-or-decimal constructors is C#'s CS0121 ambiguity, and it fails on every
        /// path - at parse where compilation was demanded, at evaluation from the interpreter - never
        /// the DefaultBinder's accidental double pick.
        /// </summary>
        [Test]
        public void WideningTieFailsOnEveryPathLikeCSharp()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<string>("new SpringExpressionsTests.Expressions.CtorTie(45).Picked"));

            Assert.Throws<AmbiguousMatchException>(
                () => InterpretGetter<string>("new SpringExpressionsTests.Expressions.CtorTie(45).Picked").GetValue());

            IExpression weak = Expression.Parse("new SpringExpressionsTests.Expressions.CtorTie(45).Picked");
            Assert.Throws<AmbiguousMatchException>(() => weak.GetValue());
        }

        /// <summary>
        /// The overload gate: an object-typed argument against several constructors refuses compiled,
        /// and the interpreter's runtime choice is the answer.
        /// </summary>
        [Test]
        public void ObjectTypedArgumentRefusesCompiledAndTheInterpreterDecides()
        {
            var ctx = new CtorContext();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<CtorContext, string>(
                    "new SpringExpressionsTests.Expressions.CtorGate(Payload).Picked"));

            IExpression weak = Expression.Parse(
                "new SpringExpressionsTests.Expressions.CtorGate(Payload).Picked");
            Assert.AreEqual("string version", weak.GetValue(ctx));
        }

        /// <summary>
        /// A null literal against comparable constructors resolves to the most specific one - C#'s
        /// pick - on both backends, like Show(null) on the method side.
        /// </summary>
        [Test]
        public void NullLiteralResolvesToTheMostSpecificConstructorEverywhere()
        {
            var ctx = new CtorContext();

            TestCompiledVsInterpreted<CtorContext, string>(
                    "new SpringExpressionsTests.Expressions.CtorGate(null).Picked", ctx)
                .ResultEqualsTo("string version");
        }

        /// <summary>
        /// A custom real-valued argument binds on every path: the compiled emitter runs op_Implicit
        /// through the conversion gate, and the interpreter's invoker normalizes through
        /// ToBuiltInRealIfPossible before Convert.ChangeType.
        /// </summary>
        [Test]
        public void CustomRealArgumentBindsOnEveryPath()
        {
            var ctx = new CtorContext();

            TestCompiledVsInterpreted<CtorContext, string>(
                    "new SpringExpressionsTests.Expressions.CtorMoney(Amount).Picked", ctx)
                .ResultEqualsTo("decimal:" + 45.5m);
        }

        /// <summary>
        /// A Derived-typed argument against CtorHier(object)/CtorHier(Derived) compiles to the
        /// specific constructor: a runtime Derived exact-matches it and a runtime sealed leaf ties
        /// into it by betterness, so every possible value agrees with the compiled pick.
        /// </summary>
        [Test]
        public void DerivedTypedArgumentCompilesToTheSpecificConstructor()
        {
            var ctx = new CtorContext();

            TestCompiledVsInterpreted<CtorContext, string>(
                    "new SpringExpressionsTests.Expressions.CtorHier(DerivedHoldingSealed).Picked", ctx)
                .ResultEqualsTo("derived version");
        }

        /// <summary>
        /// The interpreter's constructor tie, broken by betterness instead of crashing: a
        /// most-derived value through a variable used to throw AmbiguousMatchException at evaluation.
        /// The compiled path stays out (a variable is statically object, the gate refuses).
        /// </summary>
        [Test]
        public void InterpreterConstructorTieBreaksByBetternessInsteadOfCrashing()
        {
            var ctx = new CtorContext();
            var variables = new Dictionary<string, object> { { "v", new ResolutionSealed() } };

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<CtorContext, string>(
                    "new SpringExpressionsTests.Expressions.CtorHier(#v).Picked"));

            IExpression weak = Expression.Parse(
                "new SpringExpressionsTests.Expressions.CtorHier(#v).Picked");
            Assert.AreEqual("derived version", weak.GetValue(ctx, variables));
        }

        /// <summary>
        /// An unresolvable type name used to leak TypeLoadException out of tree building - invisible
        /// to the fallback. It refuses with CompileErrorException now, and the interpreter reports
        /// the TypeLoadException at evaluation, as upstream always did.
        /// </summary>
        [Test]
        public void UnresolvableTypeNameRefusesCompiledAndThrowsAtEvaluationInterpreted()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<object>("new No.Such.TypeAtAll(1)"));

            IExpression weak = Expression.Parse("new No.Such.TypeAtAll(1)");
            Assert.Throws<TypeLoadException>(() => weak.GetValue());
        }
    }
}

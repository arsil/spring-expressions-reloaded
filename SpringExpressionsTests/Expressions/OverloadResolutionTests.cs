using System;
using System.Reflection;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class ResolutionBase { }
    public class ResolutionDerived : ResolutionBase { }
    public sealed class ResolutionSealed : ResolutionDerived { }

    public class ResolutionCases
    {
        public string IntOrLong(int x) { return "int:" + x; }
        public string IntOrLong(long x) { return "long:" + x; }

        public string OnlyLong(long x) { return "long:" + x; }

        public string LongOrString(long x) { return "long:" + x; }
        public string LongOrString(string s) { return "string:" + s; }

        public string DblOrDec(double x) { return "double:" + x; }
        public string DblOrDec(decimal x) { return "decimal:" + x; }

        public string TakesDecimal(decimal x) { return "decimal:" + x; }

        public MoneyLike Amount { get { return new MoneyLike(45.5m); } }

        public object Payload { get { return "payload"; } }
        public string StringProp { get { return "payload"; } }
        public string StringPropNull { get { return null; } }

        public string Show(object o) { return "object version"; }
        public string Show(string s) { return "string version"; }

        public string Report(string s) { return "string overload"; }
        public string Report(Uri u) { return "uri overload"; }

        public ResolutionBase BaseHoldingDerived { get { return new ResolutionDerived(); } }
        public string Grab(ResolutionBase b) { return "base overload"; }
        public string Grab(ResolutionDerived d) { return "derived overload"; }

        public ResolutionDerived DerivedHoldingSealed { get { return new ResolutionSealed(); } }
        public ResolutionSealed SealedLeaf { get { return new ResolutionSealed(); } }
        public string Pin(object o) { return "object overload"; }
        public string Pin(ResolutionDerived d) { return "derived overload"; }
    }

    /// <summary>
    /// Pins the overload-resolution ruling: both backends resolve from the same tiers, so a call that
    /// compiles can only pick the method the interpreter would pick. Tier 1 is the legacy
    /// assignability scan - every pick it made is preserved, and its ties now break by C#'s
    /// betterness on both backends (most specific candidate wins; upstream threw
    /// AmbiguousMatchException instead, so the change only turns legacy errors into C#'s answers).
    /// Tier 2 is C#'s implicit numeric widening (custom real-valued types going through their own
    /// operator first), running only where tier 1 found nothing, with the same betterness and C#'s
    /// ambiguity refusals - double against decimal ties exactly where C# reports CS0121, and the tie
    /// fails on every path (it failed before the fork too; the compiled 'double' answer was the
    /// reflection binder's accident, decimal never being in its race). Where the overload choice
    /// genuinely depends on runtime types the compiler cannot see - an object-typed property, a
    /// variable, any static type a runtime subtype of which could reach a different candidate - the
    /// compiled path refuses and the interpreter's choice is the answer.
    /// </summary>
    [TestFixture]
    public class OverloadResolutionTests : BaseCompiledTests
    {
        [Test]
        public void ExactMatchesKeepTheirLegacyPicks()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("IntOrLong(45)", ctx)
                .ResultEqualsTo("int:45");
            TestCompiledVsInterpreted<ResolutionCases, string>("IntOrLong(45L)", ctx)
                .ResultEqualsTo("long:45");
            TestCompiledVsInterpreted<ResolutionCases, string>("DblOrDec(4.5)", ctx)
                .ResultEqualsTo("double:" + 4.5);
            TestCompiledVsInterpreted<ResolutionCases, string>("DblOrDec(4.5m)", ctx)
                .ResultEqualsTo("decimal:" + 4.5m);
        }

        [Test]
        public void SingleCandidateWidensOnEveryPath()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("OnlyLong(45)", ctx)
                .ResultEqualsTo("long:45");
        }

        /// <summary>
        /// The widening tier's headline: an int against long-or-string overloads used to throw
        /// "method does not exist" interpreted (the legacy scan knows assignability, not widening)
        /// while the compiled path resolved it - a succeeds-versus-throws divergence. Both widen to
        /// the unique numeric target now.
        /// </summary>
        [Test]
        public void UniqueWideningTargetResolvesOnEveryPath()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("LongOrString(45)", ctx)
                .ResultEqualsTo("long:45");
        }

        /// <summary>
        /// int against double-or-decimal is ambiguous in C# (CS0121: neither target converts
        /// implicitly into the other), and it failed before the fork too. It now fails on every
        /// path: at parse where compilation was demanded, at evaluation from the interpreter - never
        /// the reflection binder's accidental 'double' pick.
        /// </summary>
        [Test]
        public void WideningTieFailsOnEveryPathLikeCSharp()
        {
            var ctx = new ResolutionCases();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ResolutionCases, string>("DblOrDec(45)"));

            Assert.Throws<AmbiguousMatchException>(
                () => InterpretGetter<ResolutionCases, string>("DblOrDec(45)").GetValue(ctx));

            IExpression weak = Expression.Parse("DblOrDec(45)");
            Assert.Throws<AmbiguousMatchException>(() => weak.GetValue(ctx));
        }

        /// <summary>
        /// A custom real-valued argument binds on every path now: the compiled emitter always ran
        /// its op_Implicit, but the interpreter's invoker only knew Convert.ChangeType and died with
        /// InvalidCastException - the one site the custom-real ruling had not reached.
        /// </summary>
        [Test]
        public void CustomRealArgumentBindsOnEveryPath()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("TakesDecimal(Amount)", ctx)
                .ResultEqualsTo("decimal:" + 45.5m);
        }

        /// <summary>
        /// A custom real against double-or-decimal overloads is not a tie: the type's own operator
        /// names decimal, and decimal does not convert implicitly to double, so decimal is the
        /// unique applicable target - on both backends.
        /// </summary>
        [Test]
        public void CustomRealPicksItsOwnConversionTarget()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("DblOrDec(Amount)", ctx)
                .ResultEqualsTo("decimal:" + 45.5m);
        }

        /// <summary>
        /// The overload gate: an object-typed property against several candidates means the
        /// interpreter chooses from the runtime value, which the compiler cannot see - so the
        /// compiled path refuses, and every caller gets the interpreter's answer. Before the gate
        /// this expression answered "object version" compiled and "string version" interpreted, a
        /// silent divergence steered by how the caller's variable happened to be declared.
        /// </summary>
        [Test]
        public void ObjectTypedArgumentRefusesCompiledAndTheInterpreterDecides()
        {
            var ctx = new ResolutionCases();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ResolutionCases, string>("Show(Payload)"));

            Assert.AreEqual("string version",
                InterpretGetter<ResolutionCases, string>("Show(Payload)").GetValue(ctx));

            IExpression weak = Expression.Parse("Show(Payload)");
            Assert.AreEqual("string version", weak.GetValue(ctx));
        }

        /// <summary>
        /// The same gate for a non-sealed reference type: the declared Base could hold anything
        /// derived, and here it does - the interpreter exact-matches the runtime type's overload.
        /// </summary>
        [Test]
        public void NonSealedArgumentRefusesCompiledAndTheInterpreterDecides()
        {
            var ctx = new ResolutionCases();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ResolutionCases, string>("Grab(BaseHoldingDerived)"));

            IExpression weak = Expression.Parse("Grab(BaseHoldingDerived)");
            Assert.AreEqual("derived overload", weak.GetValue(ctx));
        }

        /// <summary>
        /// What the gate deliberately lets through: a string literal's value is fully known, and a
        /// string-typed property is sealed - its runtime type cannot differ - so both keep the call
        /// compiled and both backends exact-match the string overload.
        /// </summary>
        [Test]
        public void SealedAndLiteralArgumentsStayCompiled()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("Show('abc')", ctx)
                .ResultEqualsTo("string version");

            TestCompiledVsInterpreted<ResolutionCases, string>("Show(StringProp)", ctx)
                .ResultEqualsTo("string version");
        }

        /// <summary>
        /// A null literal against several comparable candidates resolves to the most specific one -
        /// C#'s pick - on both backends, through the same betterness tie-break. It used to be the
        /// interpreter's AmbiguousMatchException while the compiled path quietly picked the object
        /// overload; incomparable candidate sets still refuse and tie (see
        /// MethodCallFallbackTests.AmbiguousNullArgumentOverloadIsRefusedAndEvaluatesLikeTheInterpreter).
        /// </summary>
        [Test]
        public void NullLiteralResolvesToTheMostSpecificOverloadEverywhere()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("Show(null)", ctx)
                .ResultEqualsTo("string version");
        }

        /// <summary>
        /// The betterness ruling's headline: a Derived-typed argument against
        /// Pin(object)/Pin(Derived) compiles to Pin(Derived), because no runtime subtype can change
        /// the interpreter's pick any more - a runtime Derived exact-matches it, and a runtime sealed
        /// leaf ties between the two and betterness picks Pin(Derived), exactly as C# would. This
        /// shape was refused by the gate before the interpreter learned to break the tie.
        /// </summary>
        [Test]
        public void DerivedTypedArgumentCompilesToTheSpecificOverload()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("Pin(DerivedHoldingSealed)", ctx)
                .ResultEqualsTo("derived overload");

            TestCompiledVsInterpreted<ResolutionCases, string>("Pin(SealedLeaf)", ctx)
                .ResultEqualsTo("derived overload");
        }

        /// <summary>
        /// A string-typed argument holding null at runtime, against comparable candidates: the
        /// compiled path exact-matched Show(string) from the static type, and the interpreter - which
        /// sees only the null - ties between the two overloads and betterness lands it on the same
        /// Show(string). Before the betterness ruling this was a documented succeeds-versus-throws
        /// edge; for comparable sets it is simply agreement now.
        /// </summary>
        [Test]
        public void RuntimeNullAgainstComparableOverloadsAgrees()
        {
            var ctx = new ResolutionCases();

            TestCompiledVsInterpreted<ResolutionCases, string>("Show(StringPropNull)", ctx)
                .ResultEqualsTo("string version");
        }

        /// <summary>
        /// The accepted residual edge, recorded deliberately (see MethodNode.IsStaticallyDeterminate):
        /// a string-typed argument holding null at runtime, against INCOMPARABLE candidates - string
        /// and Uri, neither a better conversion target. The compiled path exact-matched Report(string)
        /// from the static type and calls it with the null; the interpreter sees only the null, which
        /// satisfies both parameters, and no betterness can break that tie - the legacy ambiguity, at
        /// evaluation. A null-only divergence, accepted because refusing every string argument against
        /// overload sets would decompile ubiquitous calls; do not "fix" one side without a ruling.
        /// </summary>
        [Test]
        public void RuntimeNullAgainstIncomparableOverloadsIsTheAcceptedEdge()
        {
            var ctx = new ResolutionCases();

            Assert.AreEqual("string overload",
                CompileGetter<ResolutionCases, string>("Report(StringPropNull)").GetValue(ctx));

            Assert.Throws<AmbiguousMatchException>(
                () => InterpretGetter<ResolutionCases, string>("Report(StringPropNull)").GetValue(ctx));

            // the shape compiles, so the weak path runs the compiled form and callers get the answer
            IExpression weak = Expression.Parse("Report(StringPropNull)");
            Assert.AreEqual("string overload", weak.GetValue(ctx));
        }

        /// <summary>
        /// The interpreter's crash fix, seen on its own: a most-derived value against
        /// Pin(object)/Pin(Derived) used to throw AmbiguousMatchException at evaluation (upstream
        /// had no betterness for inexact matches); it answers with C#'s pick now. Reached through a
        /// variable so the compiled path stays out (an object-typed argument still refuses) and the
        /// interpreter's own resolution is what answers.
        /// </summary>
        [Test]
        public void InterpreterTieBreaksByBetternessInsteadOfCrashing()
        {
            var ctx = new ResolutionCases();
            var variables = new System.Collections.Generic.Dictionary<string, object>
                { { "v", new ResolutionSealed() } };

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ResolutionCases, string>("Pin(#v)"));

            IExpression weak = Expression.Parse("Pin(#v)");
            Assert.AreEqual("derived overload", weak.GetValue(ctx, variables));
        }
    }

    public class HierarchyA { }
    public class HierarchyB : HierarchyA { }
    public sealed class HierarchyC : HierarchyB { }

    public class HierarchyCases
    {
        public object ObjectHoldingA { get { return new HierarchyA(); } }
        public object ObjectHoldingB { get { return new HierarchyB(); } }
        public object ObjectHoldingC { get { return new HierarchyC(); } }

        public HierarchyA AHoldingA { get { return new HierarchyA(); } }
        public HierarchyA AHoldingB { get { return new HierarchyB(); } }
        public HierarchyA AHoldingC { get { return new HierarchyC(); } }

        public HierarchyB BHoldingB { get { return new HierarchyB(); } }
        public HierarchyB BHoldingC { get { return new HierarchyC(); } }

        public HierarchyC CHoldingC { get { return new HierarchyC(); } }

        public string Method(object o) { return "object version"; }
        public string Method(HierarchyB b) { return "B version"; }
    }

    /// <summary>
    /// The ruling's worked example, row for row: the hierarchy object &lt;- A &lt;- B &lt;- C (C
    /// sealed) against Method(object) and Method(B). Static object or A refuses compiled - a runtime
    /// A takes the object overload where a runtime B takes the B overload, so the value genuinely
    /// decides and the interpreter does; static B or C compiles to Method(B), because no possible
    /// runtime value can land anywhere else once betterness breaks the C tie the way C# does. The C
    /// rows crashed with AmbiguousMatchException on the interpreter before the betterness ruling -
    /// upstream had no tie-break for inexact matches.
    /// </summary>
    [TestFixture]
    public class HierarchyOverloadResolutionTests : BaseCompiledTests
    {
        [Test]
        public void StaticObjectOrARefusesCompiledAndTheValueDecides()
        {
            var ctx = new HierarchyCases();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<HierarchyCases, string>("Method(ObjectHoldingA)"));
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<HierarchyCases, string>("Method(AHoldingA)"));

            Assert.AreEqual("object version", Expression.Parse("Method(ObjectHoldingA)").GetValue(ctx));
            Assert.AreEqual("B version", Expression.Parse("Method(ObjectHoldingB)").GetValue(ctx));
            Assert.AreEqual("object version", Expression.Parse("Method(AHoldingA)").GetValue(ctx));
            Assert.AreEqual("B version", Expression.Parse("Method(AHoldingB)").GetValue(ctx));
        }

        /// <summary>
        /// A runtime C through an indeterminate static type: the interpreter's tie, broken by
        /// betterness to Method(B) - these were the "interpretation crashes" rows.
        /// </summary>
        [Test]
        public void RuntimeCThroughIndeterminateStaticsAnswersInsteadOfCrashing()
        {
            var ctx = new HierarchyCases();

            Assert.AreEqual("B version", Expression.Parse("Method(ObjectHoldingC)").GetValue(ctx));
            Assert.AreEqual("B version", Expression.Parse("Method(AHoldingC)").GetValue(ctx));
        }

        /// <summary>
        /// Static B compiles to Method(B): a runtime B exact-matches it and a runtime C ties into it
        /// by betterness, so every possible value agrees with the compiled pick.
        /// </summary>
        [Test]
        public void StaticBCompilesToMethodBWhateverItHolds()
        {
            var ctx = new HierarchyCases();

            TestCompiledVsInterpreted<HierarchyCases, string>("Method(BHoldingB)", ctx)
                .ResultEqualsTo("B version");
            TestCompiledVsInterpreted<HierarchyCases, string>("Method(BHoldingC)", ctx)
                .ResultEqualsTo("B version");
        }

        /// <summary>
        /// Static C (sealed) compiles too: a C matches both candidates exactly-neither, and
        /// betterness picks Method(B) - the pick C# makes, where upstream threw.
        /// </summary>
        [Test]
        public void StaticSealedCCompilesToMethodB()
        {
            var ctx = new HierarchyCases();

            TestCompiledVsInterpreted<HierarchyCases, string>("Method(CHoldingC)", ctx)
                .ResultEqualsTo("B version");
        }

        /// <summary>
        /// The null literal row: null satisfies both reference parameters, and betterness resolves
        /// the tie to the more specific Method(B) on both backends - C#'s pick for Method(null).
        /// </summary>
        [Test]
        public void NullLiteralResolvesToMethodBEverywhere()
        {
            var ctx = new HierarchyCases();

            TestCompiledVsInterpreted<HierarchyCases, string>("Method(null)", ctx)
                .ResultEqualsTo("B version");
        }
    }
}

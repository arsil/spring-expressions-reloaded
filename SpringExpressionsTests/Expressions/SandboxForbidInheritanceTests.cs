using System;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Forbidding runs down the tree, and an entry of a type's own stops it there - plus the half a
    /// static type cannot answer, which the compiled path declines rather than guesses at.
    /// </summary>
    /// <remarks>
    /// <c>_Docs/type-sandboxing.md</c> §5.4 is the reasoning. Every test names its policy explicitly,
    /// because the suite-wide default is a different policy and these are about what a consumer writes.
    /// </remarks>
    [TestFixture]
    public class SandboxForbidInheritanceTests
    {
        /// <summary>
        /// A two-level hierarchy of the fixture's own, so the leaf-forbid test does not depend on the
        /// shipped catalog. It used to use <c>Stream</c>/<c>MemoryStream</c>, and stopped isolating
        /// anything the day <c>Restricted</c> began forbidding <c>Stream</c> itself: the access was
        /// then denied outright rather than declined, which is a different mechanism.
        /// </summary>
        public class Effectish
        {
            public int Marker { get { return 3; } }
        }

        public class DerivedEffectish : Effectish
        {
        }

        /// <summary>An uncatalogued struct, so its base chain reaches <c>System.ValueType</c>.</summary>
        public struct Tallyish
        {
            public int Rank { get { return 5; } }
        }

        public class Holder
        {
            public Effectish AsBaseEffectish { get { return new DerivedEffectish(); } }

            public Tallyish Tally { get { return new Tallyish(); } }

            private readonly MemoryStream _one = new MemoryStream(new byte[] { 1, 2, 3 });

            public MemoryStream AsMemory { get { return _one; } }

            public Stream AsStream { get { return _one; } }

            public object AsObject { get { return (Func<int, int>)(x => x * 2); } }

            public object AsPlainObject { get { return "text"; } }

            public Func<int, int> Twice { get { return x => x * 2; } }

            public string Name { get { return "holder"; } }

            public List<MemoryStream> Streams { get { return new List<MemoryStream> { _one }; } }
        }

        [Test]
        public void AForbiddenBaseTypeCoversASubclassNobodyMentioned()
        {
            // The defect this fixture exists for: Forbid<Stream>() used to deny a property declared
            // Stream and permit one declared MemoryStream, because a type with no entry of its own
            // answered Unknown - which the member gate reads as trust - before any ancestor was looked
            // at. Every forbid row was narrower than it reads; Forbid<Assembly>() missed
            // RuntimeAssembly.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted).Forbid<Stream>().Build();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsMemory.CanWrite", EvaluationMode.MustCompile, policy));

            var interpreted = Expression.ParseGetter<Holder, object>(
                "AsMemory.CanWrite", EvaluationMode.MustInterpret, policy);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(new Holder()));
        }

        [Test]
        public void AForbiddenBaseTypeCoversAnItemTypeToo()
        {
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted).Forbid<Stream>().Build();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "Streams.count()", EvaluationMode.MustCompile, policy).GetValue(new Holder()));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "Streams.count()", EvaluationMode.MustInterpret, policy).GetValue(new Holder()));
        }

        [Test]
        public void AnEntryOfATypesOwnStopsTheInheritanceThere()
        {
            // Forbid a base and write an entry back for the one subclass you want - which is the verb
            // the effect-type rows need, since Stream's subtree holds FileStream and MemoryStream
            // alike.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Forbid<Stream>()
                .Allow<MemoryStream>("CanWrite")
                .Build();

            Assert.AreEqual(
                true,
                Expression.ParseGetter<Holder, object>(
                    "AsMemory.CanWrite", EvaluationMode.MustCompile, policy).GetValue(new Holder()));

            Assert.AreEqual(
                true,
                Expression.ParseGetter<Holder, object>(
                    "AsMemory.CanWrite", EvaluationMode.MustInterpret, policy).GetValue(new Holder()));

            // The rescue is per member, as the entry says - Length was not listed.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsMemory.Length", EvaluationMode.MustCompile, policy));
        }

        [Test]
        public void TheCuratedSystemTypeEntrySurvivesMemberInfoBeingForbidden()
        {
            // This is the test that earns its keep if anyone ever simplifies the ancestor walk.
            // System.Type derives from System.Reflection.MemberInfo, which the built-in catalog
            // forbids - so a walk that fired on *any* forbidden ancestor would override the curated
            // System.Type entry, the most load-bearing row in the catalog, and refuse this expression.
            // A first cut did exactly that, and it also made System.RuntimeType - the class every
            // GetType() actually hands you - come out Denied while System.Type stayed reachable.
            Assert.AreEqual(
                "String",
                Expression.ParseGetter<Holder, object>(
                    "Name.GetType().Name",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted).GetValue(new Holder()));

            Assert.AreEqual(
                "String",
                Expression.ParseGetter<Holder, object>(
                    "Name.GetType().Name",
                    EvaluationMode.MustInterpret,
                    SandboxPolicy.Restricted).GetValue(new Holder()));
        }

        [Test]
        public void TheBuiltInDelegateRowReachesActualDelegateTypesNow()
        {
            // Forbid<Delegate>() was near-decoration before inheritance: it bit only on a property
            // declared exactly Delegate. It reaches every Func and Action now, which is a widening of
            // the *shipped* policy rather than of anyone's own Forbid, and .Target is what justifies
            // it - the closure display class, and with it the engineer's captured state.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "Twice.Target", EvaluationMode.MustCompile, SandboxPolicy.Restricted));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "Twice.Method", EvaluationMode.MustCompile, SandboxPolicy.Restricted));

            var interpreted = Expression.ParseGetter<Holder, object>(
                "Twice.Target", EvaluationMode.MustInterpret, SandboxPolicy.Restricted);

            Assert.Throws<SandboxViolationException>(() => interpreted.GetValue(new Holder()));
        }

        [Test]
        public void InvokingADelegateIsNotGatedAtAllAndDidNotMove()
        {
            // Measured while checking what the delegate widening cost: FunctionNode calls
            // new SafeMethod(callback.Method).Invoke(...) and asks the policy nothing, so a delegate
            // in the variables dictionary is invoked with no gate. Defensible under §2 - the engineer
            // put it there, exactly as they chose the root object - but it is a hole by design rather
            // than by check, and it is pinned so nobody discovers it later and reads it as a defect.
            var variables = new Dictionary<string, object>
            {
                { "f", (Func<int, int>)(x => x * 2) }
            };

            Assert.AreEqual(
                4,
                Expression.ParseGetter<Holder, object>(
                    "#f(2)", EvaluationMode.MustInterpret, SandboxPolicy.Restricted)
                    .GetValue(new Holder(), variables));
        }

        [Test]
        public void AForbiddenLeafBehindABaseDeclaredPropertyAgreesOnBothBackends()
        {
            // Inheritance closes forbidding a base; this is the mirror case, and it is what the
            // compiled path's decline is for. Forbid the leaf, declare the property as the base, and
            // the compiled gate cannot tell what the value really is - it used to permit the access
            // while the interpreter denied it, with the *compiled* path as the permissive side, which
            // is the wrong way round for a boundary.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Forbid<DerivedEffectish>()
                .Build();

            // Not a denial: the declared type does not settle the question, so there is no compiled
            // form and the interpreter decides. Reported as a CompileErrorException precisely so the
            // fallback can act on it.
            var declined = Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsBaseEffectish.Marker", EvaluationMode.MustCompile, policy));

            StringAssert.Contains("could hold a value of a type this sandbox forbids", declined.Message);

            // The separation the whole mechanism rests on: a denial is not a compile error, so it can
            // never be caught by the fallback and quietly turned into "interpret instead" - while this
            // decline is exactly that signal, deliberately.
            Assert.IsFalse(
                typeof(CompileErrorException).IsAssignableFrom(typeof(SandboxViolationException)));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsBaseEffectish.Marker", EvaluationMode.MustInterpret, policy)
                    .GetValue(new Holder()));

            // What a caller actually gets on the default path: the denial, from the backend that can
            // tell. That agreement is the whole point of declining rather than guessing.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsBaseEffectish.Marker", EvaluationMode.CompileOrInterpret, policy)
                    .GetValue(new Holder()));

            // And with nothing forbidden below it, the very same expression compiles - so the decline
            // is the ban's doing and not the shape's.
            Assert.AreEqual(
                3,
                Expression.ParseGetter<Holder, object>(
                    "AsBaseEffectish.Marker", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(new Holder()));
        }

        [Test]
        public void AnObjectDeclaredReceiverHoldingAForbiddenValueIsDeniedOnTheDefaultPath()
        {
            // The divergence the delegate widening introduced, and the reason `object` is in the
            // ambiguous set while it is excluded from the inheritance walk. Compiled binds
            // object.ToString and would have permitted it; interpreted sees a Func and denies.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsObject.ToString()", EvaluationMode.CompileOrInterpret, SandboxPolicy.Restricted)
                    .GetValue(new Holder()));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsObject.GetHashCode()", EvaluationMode.CompileOrInterpret, SandboxPolicy.Restricted)
                    .GetValue(new Holder()));
        }

        [Test]
        public void AnObjectDeclaredReceiverIsInterpretedRatherThanRefused()
        {
            // The measured price of putting `object` in the ambiguous set, and it is a price rather
            // than a breakage: the expression still answers, through the interpreter. Measured before
            // the change was taken - declining every method call on an object-declared receiver left
            // both suites fully green and took the corpus from 1,856 compiled expressions to 1,854.
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsPlainObject.ToString()", EvaluationMode.MustCompile, SandboxPolicy.Restricted));

            var expression = Expression.ParseGetter<Holder, object>(
                "AsPlainObject.ToString()", EvaluationMode.CompileOrInterpret, SandboxPolicy.Restricted);

            Assert.AreEqual(
                EvaluationKind.Interpreted, Expression.GetCompilationStatus(expression).Kind);

            Assert.AreEqual("text", expression.GetValue(new Holder()));
        }

        [Test]
        public void TheShippedCatalogForbidsTheEffectTypesAModelCanHandBack()
        {
            // Item 1a-ii. §5.2 made "not catalogued" mean *trusted when reached*, so a FileStream a
            // model handed back was usable - measured, myOrder.Log.WriteByte(65) wrote. These rows are
            // what closes the reached route; naming was already closed, since an uncatalogued type
            // cannot be named.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsStream.CanWrite", EvaluationMode.MustCompile, SandboxPolicy.Restricted));

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsStream.CanWrite", EvaluationMode.MustInterpret, SandboxPolicy.Restricted)
                    .GetValue(new Holder()));

            // Bases, not leaves - MemoryStream is covered by Stream's row, with nobody naming it.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsMemory.CanWrite", EvaluationMode.MustCompile, SandboxPolicy.Restricted));
        }

        [Test]
        public void AConsumerCanTakeOneSubtreeMemberBackFromTheShippedBan()
        {
            // The escape the default deliberately does not take for you. Stream's subtree holds
            // FileStream and NetworkStream, which should go, and MemoryStream, which is harmless -
            // so the default forbids the base and a consumer who wants the harmless one writes it.
            var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Allow<MemoryStream>("CanWrite")
                .Build();

            Assert.AreEqual(
                true,
                Expression.ParseGetter<Holder, object>(
                    "AsMemory.CanWrite", EvaluationMode.MustCompile, policy).GetValue(new Holder()));

            // Per member, and only the one named: Length was not listed.
            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "AsMemory.Length", EvaluationMode.MustCompile, policy));
        }

        [Test]
        public void AStaticOnlyEffectTypeNeedsNoRowBecauseNobodyCanReachOne()
        {
            // The rule that keeps the forbid list short, and the question to ask before adding to it:
            // System.IO.File has no instances, so a model cannot hand one back, and the only route to
            // it is naming it - which an uncatalogued type already refuses. A row would buy nothing.
            var denial = Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "T(System.IO.File).Exists('x')",
                    EvaluationMode.MustCompile,
                    SandboxPolicy.Restricted));

            StringAssert.Contains("System.IO.File", denial.Message);
        }

        [Test]
        public void ForbiddingObjectIsRefusedBecauseItCouldOnlyFailSilently()
        {
            // Measured before the guard existed: Restricted + Forbid<object>() denied nothing at all -
            // an uncatalogued model's members, Name.Length and T(System.Math).Max(1, 2) all still
            // worked - because the inheritance walk deliberately skips object's entry. A silent no-op
            // in a security API is the worst of the three options, so it is refused instead.
            var builder = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted);

            var thrown = Assert.Throws<ArgumentException>(() => builder.Forbid<object>());

            StringAssert.Contains("cannot mean anything", thrown.Message);
            StringAssert.Contains("pure allow-list", thrown.Message);

            // Exactly one special case, not a category: object is the only type the walk skips, so it
            // is the only ban that could do nothing. ValueType genuinely works - an uncatalogued
            // struct's base chain reaches that entry and is denied.
            var valueTypesForbidden = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
                .Forbid<ValueType>()
                .Build();

            Assert.Throws<SandboxViolationException>(
                () => Expression.ParseGetter<Holder, object>(
                    "Tally.Rank", EvaluationMode.MustCompile, valueTypesForbidden));

            // And it is the ban doing that, not the shape: the same expression compiles otherwise.
            Assert.AreEqual(
                5,
                Expression.ParseGetter<Holder, object>(
                    "Tally.Rank", EvaluationMode.MustCompile, SandboxPolicy.Restricted)
                    .GetValue(new Holder()));
        }

        [Test]
        public void AReceiverWithNothingForbiddenBelowItStillCompiles()
        {
            // The ambiguous set is matched against the receiver's *declared* type, never against what
            // a value inherits - so a string-declared or model-declared receiver is untouched even
            // though every value in the process derives from object.
            var expression = Expression.ParseGetter<Holder, object>(
                "Name.Length", EvaluationMode.MustCompile, SandboxPolicy.Restricted);

            Assert.AreEqual(6, expression.GetValue(new Holder()));
            Assert.AreEqual(EvaluationKind.Compiled, Expression.GetCompilationStatus(expression).Kind);
        }
    }
}

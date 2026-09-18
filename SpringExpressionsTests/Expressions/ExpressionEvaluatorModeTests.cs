using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

using SpringCore;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// <see cref="ExpressionEvaluator"/> parses on every invocation, so it interprets by default and
    /// compiles only when asked.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Compiling for a single evaluation is waste: measured, a one-shot
    /// <c>GetValue(root, "Inner.Name")</c> is <b>171 us compiled against 55 us interpreted</b>, because
    /// the delegate is built and then thrown away. The rate item 27 measured is what an expression
    /// <i>can</i> reach, and it is worth asking for when the same evaluation does a lot of work - a
    /// projection over a large collection - or when the caller wants the compiled path's
    /// declared-type binding. It is not worth paying for by default.
    /// </p>
    /// <p>
    /// <b>The lever these tests pull is member hiding</b>, which is the only thing that tells the two
    /// backends apart by their answer rather than by their speed: a compiled read binds
    /// <see cref="EvaluationModeTests.Base"/>'s <c>Label</c> because that is the declared type, while
    /// the interpreter resolves against the runtime <see cref="EvaluationModeTests.Derived"/> that
    /// hides it with <c>new</c>. A test that only asserted a value would pass whichever backend ran.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class ExpressionEvaluatorModeTests
    {
        public class Holder
        {
            public Holder Nested { get; set; }
            public string Name { get; set; } = "Ana";
            public object Slot { get; set; } = "untouched";
        }

        [Test]
        public void ReadingInterpretsByDefault()
        {
            EvaluationModeTests.Base derived = new EvaluationModeTests.Derived();

            Assert.AreEqual("derived", ExpressionEvaluator.GetValue(derived, "Label"),
                "the runtime type's member, so nothing compiled");
        }

        [Test]
        public void ReadingCompilesWhenAsked()
        {
            EvaluationModeTests.Base derived = new EvaluationModeTests.Derived();

            Assert.AreEqual(
                "base",
                ExpressionEvaluator.GetValue(derived, "Label", EvaluationMode.CompileOrInterpret),
                "the declared type's member, so the compiled path ran");
        }

        /// <summary>
        /// Asking for compilation is not enough on its own - the call site has to have a type to
        /// compile against. This is the same call one overload over, and it interprets.
        /// </summary>
        [Test]
        public void AskingForCompilationBuysNothingAtAnObjectCallSite()
        {
            object derived = new EvaluationModeTests.Derived();

            Assert.AreEqual(
                "derived",
                ExpressionEvaluator.GetValue(derived, "Label", EvaluationMode.CompileOrInterpret),
                "object declares no members, so there was nothing to bind and the interpreter served it");
        }

        [Test]
        public void WritingInterpretsByDefault()
        {
            var target = new EvaluationModeTests.Derived();

            ExpressionEvaluator.SetValue((EvaluationModeTests.Base)target, "Label", "written");

            Assert.AreEqual("written", target.Label, "the runtime type's member, so nothing compiled");
            Assert.AreEqual("base", ((EvaluationModeTests.Base)target).Label);
        }

        [Test]
        public void WritingCompilesWhenAsked()
        {
            var target = new EvaluationModeTests.Derived();

            ExpressionEvaluator.SetValue(
                (EvaluationModeTests.Base)target, "Label", EvaluationMode.CompileOrInterpret, "written");

            Assert.AreEqual("written", ((EvaluationModeTests.Base)target).Label,
                "the declared type's member, so the compiled path ran");
            Assert.AreEqual("derived", target.Label);
        }

        /// <summary>
        /// The mode is the <b>third</b> argument of a write, and moving it last would be silently
        /// wrong rather than merely different.
        /// </summary>
        /// <remarks>
        /// Measured: as a trailing optional, <c>SetValue(root, "Slot", null, EvaluationMode.X)</c>
        /// compiled with no diagnostic and wrote <b>the mode itself</b> into the property - C# binds it
        /// to <c>SetValue(root, expression, variables: null, newValue: the mode)</c>, because
        /// <c>newValue</c> is <c>object</c> and swallows anything. This test is what fails if anyone
        /// tidies the signature into a trailing optional.
        /// </remarks>
        [Test]
        public void AWriteTakesItsModeThirdSoTheValueIsNeverTheMode()
        {
            var byDefault = new Holder();
            ExpressionEvaluator.SetValue(byDefault, "Slot", null);
            Assert.IsNull(byDefault.Slot);

            var asked = new Holder();
            ExpressionEvaluator.SetValue(asked, "Slot", EvaluationMode.MustInterpret, null);
            Assert.IsNull(asked.Slot, "the mode must not reach the property");

            var valued = new Holder();
            ExpressionEvaluator.SetValue(valued, "Slot", EvaluationMode.CompileOrInterpret, "value");
            Assert.AreEqual("value", valued.Slot);
        }

        /// <summary>
        /// A null in the middle of a path raises the inherited exception on <b>both</b> backends.
        /// </summary>
        /// <remarks>
        /// The interpreter has always raised <see cref="NullValueInNestedPathException"/> here and the
        /// frozen suite pins it; the compiled path let the CLR raise a bare
        /// <c>NullReferenceException</c> instead, which made it disagree with the interpreter and with
        /// its own handling of a null <i>nullable</i> receiver at the same time. Closed by
        /// <c>NullableReceiver.GuardAgainstNullReference</c>.
        /// <p>
        /// <b>This test exists because the frozen suite stopped reaching the compiled path.</b>
        /// <c>TestPropertyGetWithNullInThePath</c> caught it only while <c>ExpressionEvaluator</c>
        /// compiled by default; now that it interprets, nothing else would notice the guard being
        /// removed.
        /// </p>
        /// </remarks>
        [Test]
        public void ANullMidPathRaisesTheSameExceptionOnBothBackends()
        {
            var root = new Holder();   // Nested is null

            Assert.Throws<NullValueInNestedPathException>(
                () => Expression.ParseGetter<Holder, object>("Nested.Name", EvaluationMode.MustCompile)
                    .GetValue(root));

            Assert.Throws<NullValueInNestedPathException>(
                () => Expression.ParseGetter<Holder, object>("Nested.Name", EvaluationMode.MustInterpret)
                    .GetValue(root));

            Assert.Throws<NullValueInNestedPathException>(
                () => ExpressionEvaluator.GetValue(root, "Nested.Name"));

            Assert.Throws<NullValueInNestedPathException>(
                () => ExpressionEvaluator.GetValue(
                    root, "Nested.Name", EvaluationMode.CompileOrInterpret));
        }

        /// <summary>
        /// The four shapes that would silently write an evaluation mode into the caller's object are
        /// compile errors.
        /// </summary>
        /// <remarks>
        /// <p>
        /// A write's value parameter is <c>object</c>, so it accepts an option meant for the engine.
        /// Measured, before the guards: <c>SetValue(root, "Slot", EvaluationMode.CompileOrInterpret)</c>
        /// and <c>SetValue(root, "Slot", variables, EvaluationMode.CompileOrInterpret)</c> both compiled
        /// clean and left <c>Slot = CompileOrInterpret</c>. They are "forgot the value" mistakes rather
        /// than misreadings, but the failure is silent and lands in the user's data.
        /// </p>
        /// <p>
        /// <b>A compile-error guard cannot be called from a test</b> - that is the point of it - so what
        /// is pinned here is that the four overloads still exist and still carry
        /// <c>[Obsolete(error: true)]</c>. Delete one and the silent write comes back.
        /// </p>
        /// <p>
        /// The getters need none: no getter has an object-typed data parameter, so an
        /// <see cref="EvaluationMode"/> has nowhere to be mistaken for anything. Both an <c>object</c>
        /// and a generic form are needed for each, because a typed root binds the generic overload.
        /// </p>
        /// </remarks>
        [Test]
        public void TheShapesThatWouldWriteAModeIntoTheTargetAreCompileErrors()
        {
            var guarded = typeof(ExpressionEvaluator)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "SetValue"
                    && m.GetParameters().Last().ParameterType == typeof(EvaluationMode))
                .ToList();

            Assert.AreEqual(4, guarded.Count,
                "one object and one generic form for each of the two shapes");

            foreach (var method in guarded)
            {
                var obsolete = (ObsoleteAttribute)Attribute.GetCustomAttribute(
                    method, typeof(ObsoleteAttribute));

                Assert.NotNull(obsolete, "a mode in the value position must not be callable");
                Assert.IsTrue(obsolete.IsError, "a warning would still let the mode be written");
            }
        }

        /// <summary>
        /// A policy passed per call governs that call, instead of the process-wide default.
        /// </summary>
        [Test]
        public void APolicyPassedPerCallGovernsThatCall()
        {
            var root = new Holder();
            const string reachesTheFileSystem = "T(System.IO.File).Exists('x')";

            Assert.Throws<SandboxViolationException>(
                () => ExpressionEvaluator.GetValue(
                    root, reachesTheFileSystem,
                    EvaluationMode.MustInterpret, SandboxPolicy.Restricted, null));

            Assert.AreEqual(
                false,
                ExpressionEvaluator.GetValue(
                    root, reachesTheFileSystem,
                    EvaluationMode.MustInterpret, SandboxPolicy.DangerouslyAllowEverything, null));
        }

        /// <summary>
        /// The policy-taking overloads have an arity no other overload uses, and that is what keeps
        /// them unambiguous.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <see cref="SandboxPolicy"/> and <c>IDictionary&lt;string, object&gt;</c> are both reference
        /// types, so a bare <c>null</c> converts to either. If they ever shared a position at the same
        /// arity, <c>GetValue(root, e, mode, null)</c> would be <c>CS0121</c> ambiguous - measured. An
        /// arity nothing else uses means there is exactly one candidate at that length, so every
        /// argument lands where it was meant to, <c>null</c> included. The price is passing
        /// <c>null</c> for <c>variables</c> when there are none.
        /// </p>
        /// <p>
        /// <b>This differs from the mode, and the difference is the type kind.</b>
        /// <see cref="EvaluationMode"/> is a struct, so <c>null</c> does not convert to it and it can
        /// share an arity with <c>variables</c> safely - which is why <c>GetValue(root, e, mode)</c>
        /// and <c>GetValue(root, e, variables)</c> coexist. A second reference-typed option cannot.
        /// </p>
        /// <p>
        /// So: adding a shorter policy overload later reintroduces the ambiguity. This test is what
        /// fails if someone does.
        /// </p>
        /// </remarks>
        [Test]
        public void ThePolicyOverloadsHaveAnArityNothingElseUses()
        {
            var byName = typeof(ExpressionEvaluator)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "GetValue" || m.Name == "SetValue")
                .ToList();

            foreach (var name in new[] { "GetValue", "SetValue" })
            {
                var overloads = byName.Where(m => m.Name == name).ToList();

                var takingAPolicy = overloads
                    .Where(m => m.GetParameters().Any(x => x.ParameterType == typeof(SandboxPolicy)))
                    .ToList();

                Assert.AreEqual(2, takingAPolicy.Count,
                    name + ": one object form and one generic form take a policy");

                foreach (var withPolicy in takingAPolicy)
                {
                    var arity = withPolicy.GetParameters().Length;

                    // The object form and the generic form share this arity and differ in their first
                    // parameter, exactly as every other pair on this class does - that is not the
                    // hazard. The hazard is an overload at the same length that takes no policy, where
                    // a null could be meant for either parameter.
                    var sharing = overloads
                        .Where(m => m.GetParameters().Length == arity)
                        .Where(m => m.GetParameters().All(x => x.ParameterType != typeof(SandboxPolicy)))
                        .ToList();

                    Assert.IsEmpty(sharing,
                        name + ": a policy overload must not share an arity with a variables overload, "
                            + "or a null argument becomes ambiguous");
                }
            }
        }

        /// <summary>
        /// Every signature this class shipped with still exists, so nothing that compiled before needs
        /// editing.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The rule this pins is that a capability arrives as a <b>new overload</b> and the mode sits
        /// <b>third</b>, never last. Both halves are about silent misreading rather than about
        /// compatibility: a trailing option is what let
        /// <c>SetValue(root, "Slot", null, EvaluationMode.X)</c> write the mode into the property.
        /// </p>
        /// <p>
        /// Keeping the original signatures also means an assembly compiled against an older build keeps
        /// running without a rebuild - C# bakes an optional parameter in at the call site, so adding one
        /// deletes the old signature and an un-rebuilt consumer dies with
        /// <c>MissingMethodException</c>, measured. That is a side benefit here rather than the reason.
        /// </p>
        /// </remarks>
        [Test]
        public void EverySignatureThisClassShippedWithStillExists()
        {
            var shipped = new[]
            {
                "GetValue(Object, String)",
                "GetValue(Object, String, IDictionary`2)",
                "SetValue(Object, String, Object)",
                "SetValue(Object, String, IDictionary`2, Object)",
            };

            var actual = typeof(ExpressionEvaluator)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "GetValue" || m.Name == "SetValue")
                .Select(m => m.Name + "("
                    + string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name).ToArray())
                    + ")")
                .ToList();

            foreach (var signature in shipped)
                Assert.That(actual, Contains.Item(signature),
                    "this signature shipped and must not become an optional parameter");
        }

        /// <summary>
        /// The guard costs nothing where nothing can be null, and must not change a working read.
        /// </summary>
        [Test]
        public void APresentPathStillReadsOnBothBackends()
        {
            var root = new Holder { Nested = new Holder { Name = "Bob" } };

            Assert.AreEqual("Bob",
                Expression.ParseGetter<Holder, object>("Nested.Name", EvaluationMode.MustCompile)
                    .GetValue(root));
            Assert.AreEqual("Bob",
                Expression.ParseGetter<Holder, object>("Nested.Name", EvaluationMode.MustInterpret)
                    .GetValue(root));
        }
    }
}

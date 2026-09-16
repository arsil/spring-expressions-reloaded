using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class LocalDeclarationCases
    {
        public int Number { get; set; } = 4;

        public int Smaller { get; set; } = 1;

        public int Bigger { get; set; } = 9;

        public string Label { get { return "ab"; } }

        public List<int> Ints { get { return new List<int> { 3, 1, 2 }; } }

        public List<object> OwnedObjects { get; set; } = new List<object> { 7, 8 };
    }

    /// <summary>
    /// <c>int $x = 5</c> - a local whose type the writer states, so both backends carry it.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>The declaration is a language feature, which is why a declared local can have a type at all.</b>
    /// The interpreter seeds its per-evaluation dictionary with the converted value and the compiled path
    /// declares a block variable of the declared type; neither is reproducing the other, so there is
    /// nothing to keep in step. Inferring the type from a first assignment was rejected for exactly the
    /// opposite reason - inference has to reproduce untyped storage, and the interpreter has no type to
    /// reproduce.
    /// </p>
    /// <p>
    /// <b>A declared local has no scope of its own.</b> A free <c>$x</c> is expression-wide on both
    /// backends already - one flat dictionary interpreted, one block variable per name hoisted to the
    /// outermost block compiled - and a declaration joins that namespace rather than adding a second
    /// rule. Block scope would have given the compiled path scoping free from LINQ while the interpreter
    /// needed a scope chain written by hand, which is the shape every divergence in this fork has had.
    /// The price is that a name cannot be shadowed, and it is paid by
    /// <see cref="ANameMayBeDeclaredOnlyOnce"/>.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class LocalDeclarationTests : BaseCompiledTests
    {
        [Test]
        public void ADeclaredLocalHoldsItsInitialiser()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 5; $x)", new LocalDeclarationCases())
                .ResultEqualsTo(5);
        }

        /// <summary>
        /// The payoff, in one row: an undeclared <c>$x</c> is object-typed, so <c>$x + Number</c> has no
        /// compiled form and needs <c>$x as int + Number</c>. A declared one is an <c>int</c> on both
        /// backends and the arithmetic is ordinary int arithmetic.
        /// </summary>
        [Test]
        public void ADeclaredLocalDoesArithmeticWithoutACast()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 5; $x + Number)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(9, result);
        }

        /// <summary>
        /// <i>A local always has a value. An undeclared one starts as null; a declared one starts as
        /// <c>default(T)</c>.</i> Deliberately not C#, which refuses to read an unassigned local - this
        /// engine has no definite-assignment analysis and does not want one.
        /// </summary>
        [Test]
        public void ADeclarationWithNoInitialiserStartsAtTheTypeSDefault()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x; $x)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(0, result);
        }

        [Test]
        public void AReferenceTypedDeclarationWithNoInitialiserStartsAtNull()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(string $s; $s)", new LocalDeclarationCases())
                .ResultEqualsTo(null);
        }

        /// <summary>
        /// This is the hole that killed inferring a local's type from its first assignment: under that
        /// rule <c>$x</c> would be typed <c>int</c> from an assignment inside a branch, so a block
        /// variable started at 0 compiled while the interpreter answered null. A declaration is executed
        /// on both backends, so the value before the branch is the same on both and the branch decides
        /// nothing about the type.
        /// </summary>
        [Test]
        public void AConditionalAssignmentDecidesNothingAboutTheType()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 5; Number > 100 ? $x = 9 : 0; $x)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(5, result);
        }

        [Test]
        public void AnInitialiserWidensIntoTheDeclaredType()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(long $x = 5; $x)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(long), result.GetType());
            Assert.AreEqual(5L, result);
        }

        [Test]
        public void ADecimalLocalDividesAsADecimal()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(decimal $d = 5; $d / 2)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(decimal), result.GetType());
            Assert.AreEqual(2.5m, result);
        }

        [Test]
        public void AStringLocalConcatenates()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(string $s = 'a'; $s + 'b')", new LocalDeclarationCases())
                .ResultEqualsTo("ab");
        }

        [Test]
        public void AnObjectTypedDeclarationIsLegalAndHoldsTheBoxedValue()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(object $o = 5; $o)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(5, result);
        }

        /// <summary>
        /// One type vocabulary with the cast: the structural name for everyday types, <c>T(...)</c> for
        /// what only the slurp can spell.
        /// </summary>
        [Test]
        public void TheTypeEscapeSpellsADeclaredTypeToo()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(T(System.Int32) $x = 5; $x)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(5, result);
        }

        /// <summary>
        /// A member reached through a declared local binds against the declared type, which is the thing
        /// an object-typed local can never do without a cast.
        /// </summary>
        [Test]
        public void AMemberOfADeclaredLocalResolvesAgainstItsDeclaredType()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(System.Collections.Generic.List<int> $xs = Ints; $xs.Count)",
                new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(3, result);
        }

        /// <summary>
        /// A collection the caller owns is assigned into the slot as the very instance - it is never
        /// registered as engine-built, so nothing reshapes or copies it.
        /// </summary>
        [Test]
        public void ACallerOwnedCollectionIsStoredAsTheVeryInstance()
        {
            var root = new LocalDeclarationCases();

            var compiled = CompileGetter<LocalDeclarationCases, object>(
                "(System.Collections.Generic.List<object> $xs = OwnedObjects; $xs)").GetValue(root);
            var interpreted = InterpretGetter<LocalDeclarationCases, object>(
                "(System.Collections.Generic.List<object> $xs = OwnedObjects; $xs)").GetValue(root);

            Assert.AreSame(root.OwnedObjects, compiled);
            Assert.AreSame(root.OwnedObjects, interpreted);
        }

        [Test]
        public void ALaterAssignmentWritesThroughTheDeclaredType()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 5; $x = 7; $x)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(7, result);
        }

        [Test]
        public void AnUndeclaredLocalBesideADeclaredOneIsUntouched()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 5; $y = $x + 1; $y)", new LocalDeclarationCases())
                .ResultEqualsTo(6);
        }

        [Test]
        public void ACastStillWorksOnADeclaredLocal()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 5; $x as long)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(long), result.GetType());
            Assert.AreEqual(5L, result);
        }

        // ---------------------------------------------------------------------------------------
        // No scope of its own
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// The ruling, stated as a test: a nested <c>(...)</c> is not a scope. This is what an
        /// undeclared <c>$x</c> already does - <c>(($x = 5; $x); $x)</c> reads 5 on both backends -
        /// and a declaration joins that rule rather than introducing block scope.
        /// </summary>
        [Test]
        public void ANestedListIsNotAScope()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "((int $x = 5; $x); $x)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(5, result);
        }

        [Test]
        public void ADeclarationIsVisibleInsideANestedList()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 5; (1; $x))", new LocalDeclarationCases())
                .ResultEqualsTo(5);
        }

        /// <summary>
        /// A projection body shares the enclosing scope, so a declared local is writable from inside one
        /// and the write is visible after it - the same as for an undeclared local since 2026-09-11.
        /// </summary>
        [Test]
        public void AProjectionBodyWritesTheEnclosingDeclaredLocal()
        {
            var result = TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(int $x = 0; Ints.!{$x = #this}; $x)", new LocalDeclarationCases()).Result;

            Assert.AreEqual(typeof(int), result.GetType());
            Assert.AreEqual(2, result);
        }

        /// <summary>
        /// A declaration inside a projection body runs once per item, and re-entering the <i>same</i>
        /// declaration is not a redeclaration - which is why the check is on which node declared the
        /// name rather than on whether the name is present.
        /// </summary>
        [Test]
        public void ADeclarationInsideAProjectionBodyRunsOncePerItem()
        {
            var result = (List<object>)TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "Ints.!{(int $x = #this; $x + 1)}", new LocalDeclarationCases()).Result;

            Assert.AreEqual(new List<object> { 4, 2, 3 }, result);
        }

        // ---------------------------------------------------------------------------------------
        // What is refused, and what the interpreter then says
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// A name may be declared once. Without this rule one expression would hold two different
        /// <c>$x</c>, which is precisely what the no-scope ruling gives up shadowing to avoid.
        /// </summary>
        [Test]
        public void ANameMayBeDeclaredOnlyOnce()
        {
            var compileError = Assert.Catch<CompileErrorException>(
                () => CompileGetter<LocalDeclarationCases, object>("(int $x = 1; int $x = 2; $x)"));
            Assert.That(compileError.Message, Does.Contain("already in use"));

            var interpreted = InterpretGetter<LocalDeclarationCases, object>("(int $x = 1; int $x = 2; $x)");
            var evaluationError = Assert.Catch<ArgumentException>(
                () => interpreted.GetValue(new LocalDeclarationCases()));
            Assert.That(evaluationError.Message, Does.Contain("declared more than once"));
        }

        /// <summary>
        /// A declaration must precede every use of its name, or one expression means two different
        /// <c>$x</c> - the untyped one, then a new typed one.
        /// </summary>
        [Test]
        public void ANameCannotBeDeclaredAfterItHasBeenUsed()
        {
            Assert.Catch<CompileErrorException>(
                () => CompileGetter<LocalDeclarationCases, object>("($x = 'five'; int $x; $x)"));

            var interpreted = InterpretGetter<LocalDeclarationCases, object>("($x = 'five'; int $x; $x)");
            var evaluationError = Assert.Catch<ArgumentException>(
                () => interpreted.GetValue(new LocalDeclarationCases()));
            Assert.That(evaluationError.Message, Does.Contain("used before it is declared"));
        }

        /// <summary>
        /// An initialiser the declared type cannot hold: the compiled path refuses and the interpreter
        /// raises the error at evaluation, which is the standing paired shape for an illegal expression
        /// both backends agree about.
        /// </summary>
        [Test]
        public void AnInitialiserTheDeclaredTypeCannotHoldIsRefusedAndThrows()
        {
            Assert.Catch<CompileErrorException>(
                () => CompileGetter<LocalDeclarationCases, object>("(System.DateTime $d = 5; $d)"));

            var interpreted = InterpretGetter<LocalDeclarationCases, object>("(System.DateTime $d = 5; $d)");
            Assert.Catch<Exception>(() => interpreted.GetValue(new LocalDeclarationCases()));
        }

        [Test]
        public void AnAssignmentTheDeclaredTypeCannotHoldIsRefusedAndThrows()
        {
            Assert.Catch<CompileErrorException>(
                () => CompileGetter<LocalDeclarationCases, object>("(int $x = 5; $x = 'no'; $x)"));

            var interpreted = InterpretGetter<LocalDeclarationCases, object>("(int $x = 5; $x = 'no'; $x)");
            Assert.Catch<Exception>(() => interpreted.GetValue(new LocalDeclarationCases()));
        }

        /// <summary>
        /// An unresolvable declared type is the caller's own mistake, so it must be a plain refusal -
        /// not an absorbed internal compiler error telling them to report a bug about their typo. The
        /// same rule <c>TypeNode</c> and <c>CastNode</c> already follow.
        /// </summary>
        [Test]
        public void AnUnresolvableDeclaredTypeIsTheCallersMistake()
        {
            var compileError = Assert.Catch<CompileErrorException>(
                () => CompileGetter<LocalDeclarationCases, object>("(NoSuchTypeAnywhere $x; $x)"));

            Assert.That(compileError.Message, Does.Contain("does not resolve"));
            Assert.That(compileError.Message, Does.Not.Contain("internal compiler error"));
        }

        /// <summary>
        /// The declared type goes through the same gate a cast's does, so a policy that cannot name a
        /// type cannot declare a local of it either.
        /// </summary>
        [Test]
        public void ADeclaredTypeIsGatedByTheSandbox()
        {
            Assert.Catch<SandboxViolationException>(
                () => CompileGetter<LocalDeclarationCases, object>(
                    "(System.Diagnostics.Process $p; $p)"));

            var interpreted = InterpretGetter<LocalDeclarationCases, object>(
                "(System.Diagnostics.Process $p; $p)");
            Assert.Catch<SandboxViolationException>(
                () => interpreted.GetValue(new LocalDeclarationCases()));
        }

        // ---------------------------------------------------------------------------------------
        // The grammar is carved out of error space
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// <c>Foo $x</c> is two operands with no operator between them and has always been a syntax
        /// error, so nothing that parsed before parses differently now. The one place a reader might
        /// worry is the generics-versus-comparison decision, which the type rule already settles the
        /// way C# does: a <c>&lt;</c> opens generic arguments only if a complete argument list parses.
        /// </summary>
        [Test]
        public void AComparisonAsAListElementIsStillAComparison()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(Smaller < Bigger; Bigger < Smaller)", new LocalDeclarationCases())
                .ResultEqualsTo(false);
        }

        [Test]
        public void AnOrdinaryExpressionListIsUnchanged()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "(1; 2)", new LocalDeclarationCases())
                .ResultEqualsTo(2);
        }

        [Test]
        public void AnUndeclaredLocalIsStillObjectTyped()
        {
            TestCompiledVsInterpreted<LocalDeclarationCases, object>(
                "($x = 5; $x)", new LocalDeclarationCases())
                .ResultEqualsTo(5);

            Assert.Catch<CompileErrorException>(
                () => CompileGetter<LocalDeclarationCases, object>("($x = 5; $x + Number)"));
        }
    }
}

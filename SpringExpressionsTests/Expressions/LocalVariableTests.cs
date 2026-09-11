using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class LocalVariableCases
    {
        public int Number { get; set; }

        public List<int> Ints { get { return new List<int> { 3, 1, 2 }; } }
    }

    /// <summary>
    /// A free <c>$local</c> - one no enclosing lambda declares as a parameter - is storage the
    /// expression owns for the length of one evaluation. It had no compiled form at all: the setter
    /// was the single node with an interpreted <c>Set</c> and no emitted one, and the getter refused
    /// as well, so <c>$x = 5</c> and <c>$x</c> alike fell back to the interpreter.
    /// </summary>
    /// <remarks>
    /// The compiled form is a block variable holding a fresh <c>Dictionary&lt;string, object&gt;</c>,
    /// declared only where something asks for one - the interpreter's per-evaluation
    /// <c>EvaluationContext.LocalVariables</c>, written as a LINQ scope. That it is a block variable
    /// rather than anything shared is what keeps a compiled expression safe to evaluate from two
    /// threads at once.
    /// </remarks>
    [TestFixture]
    public class LocalVariableTests : BaseCompiledTests
    {
        [Test]
        public void ALocalIsWrittenAndReadBack()
        {
            TestCompiledVsInterpreted<LocalVariableCases, object>(
                "($x = 5; $x)", new LocalVariableCases())
                .ResultEqualsTo(5);
        }

        [Test]
        public void AStringValuedLocalIsWrittenAndReadBack()
        {
            TestCompiledVsInterpreted<LocalVariableCases, object>(
                "($x = 'five'; $x)", new LocalVariableCases())
                .ResultEqualsTo("five");
        }

        /// <summary>
        /// An assignment is an expression and yields the value assigned, as a <c>#variable</c>
        /// assignment does. The value is boxed into the dictionary's object slot on the way; without
        /// that, <c>$x = 5</c> refused while <c>$x = 'five'</c> compiled.
        /// </summary>
        [Test]
        public void AnAssignmentYieldsTheValueAssigned()
        {
            TestCompiledVsInterpreted<LocalVariableCases, object>(
                "($x = 5)", new LocalVariableCases())
                .ResultEqualsTo(5);
        }

        /// <summary>
        /// A local nothing has assigned to reads as null rather than failing - the interpreter's
        /// answer, from a dictionary that does not hold the key.
        /// </summary>
        [Test]
        public void AnUnassignedLocalReadsAsNull()
        {
            var compiled = Expression.ParseGetter<LocalVariableCases, object>(
                "($x)", EvaluationMode.MustCompile);
            var interpreted = Expression.ParseGetter<LocalVariableCases, object>(
                "($x)", EvaluationMode.MustInterpret);

            Assert.IsNull(compiled.GetValue(new LocalVariableCases()));
            Assert.IsNull(interpreted.GetValue(new LocalVariableCases()));

            TestCompiledVsInterpreted<LocalVariableCases, object>(
                "($x == null)", new LocalVariableCases())
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// The storage lives one evaluation and no longer: a second expression sees nothing the first
        /// assigned, on either backend. The compiled form gets that from the dictionary being a block
        /// variable of the emitted delegate, created on entry.
        /// </summary>
        [Test]
        public void ALocalDoesNotOutliveTheEvaluation()
        {
            var root = new LocalVariableCases();

            var assigning = Expression.ParseGetter<LocalVariableCases, object>(
                "($x = 5; $x)", EvaluationMode.MustCompile);
            Assert.AreEqual(5, assigning.GetValue(root));

            var reading = Expression.ParseGetter<LocalVariableCases, object>(
                "($x)", EvaluationMode.MustCompile);
            Assert.IsNull(reading.GetValue(root));

            // And the same compiled delegate starts clean on every invocation.
            Assert.AreEqual(5, assigning.GetValue(root));
            Assert.IsNull(reading.GetValue(root));
        }

        [Test]
        public void ALocalHoldsWhateverTheRootSupplied()
        {
            var compiled = Expression.ParseGetter<LocalVariableCases, object>(
                "($x = Number; $x)", EvaluationMode.MustCompile);

            Assert.AreEqual(4, compiled.GetValue(new LocalVariableCases { Number = 4 }));
            Assert.AreEqual(9, compiled.GetValue(new LocalVariableCases { Number = 9 }));
        }

        /// <summary>
        /// A local is object-typed, exactly as a <c>#variable</c> is - the dictionary holds objects
        /// and nothing records what went in - so arithmetic against it has no compiled form and a cast
        /// buys one back. That is the engine's standing object-typed-operand story, not something
        /// locals introduce; do not "fix" the refusal without ruling on that.
        /// </summary>
        [Test]
        public void ALocalIsObjectTypedSoArithmeticNeedsACast()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<LocalVariableCases, object>(
                    "($x = 5; $x + Number)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<LocalVariableCases, object>(
                "($x = 5; $x + Number)", EvaluationMode.MustInterpret);

            Assert.AreEqual(9, interpreted.GetValue(new LocalVariableCases { Number = 4 }));

            TestCompiledVsInterpreted<LocalVariableCases, object>(
                "($x = 5; $x as int + Number)", new LocalVariableCases { Number = 4 })
                .ResultEqualsTo(9);
        }

        /// <summary>
        /// Inside a lambda a <c>$name</c> is the lambda's parameter, and reading one compiles.
        /// </summary>
        [Test]
        public void ALambdaParameterIsRead()
        {
            TestCompiledVsInterpreted<LocalVariableCases, object>(
                "Ints.orderBy({|a,b| $b - $a})", new LocalVariableCases())
                .ResultEqualsTo(new List<object> { 3, 2, 1 });
        }

        /// <summary>
        /// Assigning to one is refused. The interpreter writes it into the argument dictionary the
        /// call swapped in, which the compiled form has no equivalent of - its parameters are the
        /// delegate's own - so rather than assign to the ParameterExpression and hope the two stay
        /// level, the shape is left to the interpreter. Do not "fix" one side without ruling on what
        /// assigning to a lambda parameter means.
        /// </summary>
        [Test]
        public void AssigningToALambdaParameterIsRefusedButStillEvaluates()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<LocalVariableCases, object>(
                    "Ints.orderBy({|a,b| $a = $b})", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<LocalVariableCases, object>(
                "Ints.orderBy({|a,b| $a = $b})", EvaluationMode.MustInterpret);

            Assert.IsNotNull(interpreted.GetValue(new LocalVariableCases()));
        }

        /// <summary>
        /// A local declared outside a projection is readable inside its body.
        /// </summary>
        /// <remarks>
        /// This was a refusal until 2026-09-11, and the reason was real at the time: the body was
        /// compiled by its own <c>Compile()</c> call and handed into the tree as a constant delegate,
        /// so a block variable of the enclosing compilation was genuinely not in scope and emitting a
        /// reference to one produced an unbound-variable failure out of the LINQ compiler. Nesting
        /// the body lambda removed the obstacle; the scope is shared and the outer lambda's closure
        /// carries the variable.
        /// </remarks>
        [Test]
        public void ALocalIsReadableInsideAProjectionBody()
        {
            // Summed rather than compared as a list: an expression list costs its result the root
            // reshaping, so a projection wrapped in one comes back List<string> compiled and
            // List<object> interpreted - a divergence that predates this and has nothing to do with
            // locals, since '(1; Words.!{#this})' does it too. See _Docs/open-issues.md item 47.
            TestCompiledVsInterpreted<LocalVariableCases, object>(
                "($x = 7; Ints.!{ $x }.sum())", new LocalVariableCases())
                .ResultEqualsTo(21);
        }

        /// <summary>
        /// The setter entry point emits an assignment to a local now rather than refusing. Nothing
        /// outlives the call - which is what a local is - so the value is written and dropped.
        /// </summary>
        [Test]
        public void TheSetterEntryPointCompilesAnAssignmentToALocal()
        {
            var compiled = Expression.ParseSetter<LocalVariableCases, int>("$x", EvaluationMode.MustCompile);
            Assert.DoesNotThrow(() => compiled.SetValue(new LocalVariableCases(), 5));

            var interpreted = Expression.ParseSetter<LocalVariableCases, int>("$x", EvaluationMode.MustInterpret);
            Assert.DoesNotThrow(() => interpreted.SetValue(new LocalVariableCases(), 5));
        }

        /// <summary>
        /// As a void expression it compiles, and the contrast with <c>#x = 5</c> is the point: a void
        /// expression must emit a void call or an assignment node, and assigning to a local now *is*
        /// an assignment node, where assigning to a <c>#variable</c> is a call that writes into the
        /// caller's dictionary and returns the value. Storing a local in a block variable rather than
        /// a dictionary is what closed this, and it was not the point of the change.
        /// </summary>
        [Test]
        public void AVoidExpressionAssigningToALocalCompiles()
        {
            var compiled = Expression.ParseVoidExpression<LocalVariableCases>(
                "$x = 5", EvaluationMode.MustCompile);
            Assert.DoesNotThrow(() => compiled.Execute(new LocalVariableCases()));

            var interpreted = Expression.ParseVoidExpression<LocalVariableCases>(
                "$x = 5", EvaluationMode.MustInterpret);
            Assert.DoesNotThrow(() => interpreted.Execute(new LocalVariableCases()));

            // The #variable twin still emits a Call, so it still has no void form.
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseVoidExpression<LocalVariableCases>(
                    "#x = 5", EvaluationMode.MustCompile));
        }
    }
}

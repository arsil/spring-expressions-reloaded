using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Every <see cref="EvaluationMode"/> against every operation, on both paths: the matrix the
    /// compilation-options rework exists to make true.
    /// </summary>
    /// <remarks>
    /// The three modes answer one question - what happens when the compiled backend refuses a shape -
    /// and the answer has to be the same wherever it is asked. <c>MustCompile</c> reports the refusal,
    /// <c>CompileOrInterpret</c> evaluates the shape anyway, <c>MustInterpret</c> never compiles even
    /// when it could. Getters, setters and void expressions on the strongly typed path; reads and
    /// writes on the weakly typed one, which has no void operation.
    /// <p>
    /// <c>MustInterpret</c> is the mode a test can accidentally fail to exercise: asserting it on a
    /// shape that has no compiled form proves nothing, since every mode interprets that. Each
    /// <c>MustInterpret</c> row below therefore uses a shape that <i>would</i> compile, and shows the
    /// interpreter's own answer coming back - member hiding, where the compiled path binds against the
    /// declared type and the interpreter against the runtime one.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class EvaluationModeTests : BaseCompiledTests
    {
        public class Holder
        {
            public string Name { get; set; }
            public int Number { get; set; }
            public List<int> Ints { get; set; } = new List<int> { 3, 1, 2 };
        }

        public class Base
        {
            public string Label { get; set; } = "base";
        }

        public class Derived : Base
        {
            public new string Label { get; set; } = "derived";
        }

        // ----- strongly typed getters

        /// <summary>
        /// The refusal lands at construction, which is the whole reason lazy compilation was dropped:
        /// a shape the engine cannot compile is discovered by the code that created the expression,
        /// not by some later evaluation on another thread.
        /// </summary>
        [Test]
        public void AStrongGetterUnderMustCompileReportsTheRefusalAtParse()
        {
            // set difference has no compiled form at all
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<object, object>("{1,2,3} - {2}", EvaluationMode.MustCompile));
        }

        [Test]
        public void AStrongGetterUnderCompileOrInterpretEvaluatesTheSameShape()
        {
            var getter = Expression.ParseGetter<object, object>("{1,2,3} - {2}");

            var value = getter.GetValue(null);

            Assert.AreEqual(typeof(HashSet<object>), value.GetType());
            CollectionAssert.AreEquivalent(new object[] { 1, 3 }, (HashSet<object>)value);

            var status = Expression.GetCompilationStatus(getter);
            Assert.AreEqual(EvaluationKind.Interpreted, status.Kind);
            Assert.AreEqual(InterpretationReason.CompilationRefused, status.Reason);
            StringAssert.Contains("OpSUBTRACT", status.RefusalMessage);
        }

        /// <summary>
        /// A shape that <i>would</i> compile, so that "never compile" is actually under test.
        /// </summary>
        /// <remarks>
        /// The compiled path binds <c>Label</c> against the declared <c>Base</c>; the interpreter
        /// resolves it against the runtime <c>Derived</c>, which hides it with <c>new</c>. Two backends,
        /// two answers, same expression and same object - so the answer names which backend ran.
        /// </remarks>
        [Test]
        public void AStrongGetterUnderMustInterpretDoesNotCompileAShapeThatCould()
        {
            var derived = new Derived();

            Assert.AreEqual("base",
                Expression.ParseGetter<Base, string>("Label", EvaluationMode.MustCompile).GetValue(derived));

            var interpreted = Expression.ParseGetter<Base, string>("Label", EvaluationMode.MustInterpret);

            Assert.AreEqual("derived", interpreted.GetValue(derived),
                "the interpreter's own answer, so nothing was compiled");

            var status = Expression.GetCompilationStatus(interpreted);
            Assert.AreEqual(EvaluationKind.Interpreted, status.Kind);
            Assert.AreEqual(InterpretationReason.Requested, status.Reason);
            Assert.IsNull(status.RefusalMessage);
        }

        // ----- strongly typed setters

        [Test]
        public void AStrongSetterHonoursAllThreeModes()
        {
            var compiled = new Holder();
            var setter = Expression.ParseSetter<Holder, string>("Name", EvaluationMode.MustCompile);
            setter.SetValue(compiled, "Ana");

            Assert.AreEqual("Ana", compiled.Name);
            Assert.AreEqual(EvaluationKind.Compiled, Expression.GetCompilationStatus(setter).Kind);

            // a string into an int member: assignment is not conversion, so there is no compiled form
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseSetter<Holder, string>("Number", EvaluationMode.MustCompile));

            var fallenBack = new Holder();
            Expression.ParseSetter<Holder, string>("Number").SetValue(fallenBack, "45");
            Assert.AreEqual(45, fallenBack.Number, "the interpreter converts on assignment");

            var interpretedTarget = new Derived();
            var interpreted = Expression.ParseSetter<Base, string>("Label", EvaluationMode.MustInterpret);
            interpreted.SetValue(interpretedTarget, "written");

            Assert.AreEqual("written", interpretedTarget.Label, "the runtime type's member, so nothing compiled");
            Assert.AreEqual("base", ((Base)interpretedTarget).Label);
            Assert.AreEqual(
                InterpretationReason.Requested,
                Expression.GetCompilationStatus(interpreted).Reason);
        }

        // ----- strongly typed void expressions

        [Test]
        public void AStrongVoidExpressionHonoursAllThreeModes()
        {
            var compiled = new Holder();
            var voidExpression = Expression.ParseVoidExpression<Holder>("Number = 45", EvaluationMode.MustCompile);
            voidExpression.Execute(compiled);

            Assert.AreEqual(45, compiled.Number);
            Assert.AreEqual(EvaluationKind.Compiled, Expression.GetCompilationStatus(voidExpression).Kind);

            // an assignment to a #variable emits a Call returning the assigned value, which is not a
            // void shape
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseVoidExpression("#x = 'five'", EvaluationMode.MustCompile));

            var variables = new Dictionary<string, object>();
            Expression.ParseVoidExpression("#x = 'five'").Execute(variables);
            Assert.AreEqual("five", variables["x"]);

            var interpretedTarget = new Derived();
            var interpreted = Expression.ParseVoidExpression<Base>("Label = 'written'", EvaluationMode.MustInterpret);
            interpreted.Execute(interpretedTarget);

            Assert.AreEqual("written", interpretedTarget.Label, "the runtime type's member, so nothing compiled");
            Assert.AreEqual("base", ((Base)interpretedTarget).Label);
            Assert.AreEqual(
                InterpretationReason.Requested,
                Expression.GetCompilationStatus(interpreted).Reason);
        }

        // ----- weakly typed reads

        /// <summary>
        /// The two paths report a refusal in different places, and this is the difference: a weakly
        /// typed expression has nothing to decide at parse, because the context type comes from the
        /// call site.
        /// </summary>
        [Test]
        public void AWeakRefusalSurfacesAtTheFirstEvaluationNotAtParse()
        {
            var expression = Expression.Parse("{1,2,3} - {2}", EvaluationMode.MustCompile);

            Assert.Throws<CompileErrorException>(() => expression.GetValue<object>(null));
        }

        /// <summary>
        /// One expression, one mode, two declared types, two outcomes - which is also why the weak path
        /// reports through an observer rather than answering a query.
        /// </summary>
        [Test]
        public void AWeakModeIsAppliedPerDeclaredType()
        {
            var expression = Expression.Parse("Name", EvaluationMode.MustCompile);
            var holder = new Holder { Name = "Ana" };

            Assert.AreEqual("Ana", expression.GetValue<Holder>(holder));

            Assert.Throws<CompileErrorException>(() => expression.GetValue<object>(holder),
                "object declares no member of that name, so there is nothing to compile against");
        }

        [Test]
        public void AWeakReadUnderCompileOrInterpretEvaluatesEitherWay()
        {
            var value = Expression.Parse("{1,2,3} - {2}").GetValue<object>(null);

            Assert.AreEqual(typeof(HashSet<object>), value.GetType());
            CollectionAssert.AreEquivalent(new object[] { 1, 3 }, (HashSet<object>)value);

            Assert.AreEqual("Ana", Expression.Parse("Name").GetValue<object>(new Holder { Name = "Ana" }));
        }

        [Test]
        public void AWeakReadUnderMustInterpretDoesNotCompileAShapeThatCould()
        {
            var derived = new Derived();

            Assert.AreEqual("base", Expression.Parse("Label").GetValue<Base>(derived),
                "the default mode compiles this, and a compiled read binds against the declared type");

            var decisions = new List<EvaluationDecision>();

            Assert.AreEqual("derived",
                Expression.Parse("Label", EvaluationMode.MustInterpret, decisions.Add).GetValue<Base>(derived),
                "the interpreter's own answer, so nothing was compiled");

            Assert.AreEqual(1, decisions.Count);
            Assert.AreEqual(EvaluationKind.Interpreted, decisions[0].Kind);
            Assert.AreEqual(InterpretationReason.Requested, decisions[0].Reason);
        }

        // ----- weakly typed writes

        [Test]
        public void AWeakWriteHonoursAllThreeModes()
        {
            var compiled = new Holder();
            Expression.Parse("Number", EvaluationMode.MustCompile).SetValue(compiled, 45);
            Assert.AreEqual(45, compiled.Number);

            Assert.Throws<CompileErrorException>(
                () => Expression.Parse("Number", EvaluationMode.MustCompile).SetValue(new Holder(), "45"));

            var fallenBack = new Holder();
            Expression.Parse("Number").SetValue(fallenBack, "45");
            Assert.AreEqual(45, fallenBack.Number);

            var decisions = new List<EvaluationDecision>();
            var interpretedTarget = new Derived();
            Expression.Parse("Label", EvaluationMode.MustInterpret, decisions.Add)
                .SetValue<Base, string>(interpretedTarget, "written");

            Assert.AreEqual("written", interpretedTarget.Label, "the runtime type's member, so nothing compiled");
            Assert.AreEqual("base", ((Base)interpretedTarget).Label);

            Assert.AreEqual(1, decisions.Count);
            Assert.AreEqual(EvaluationOperation.Set, decisions[0].Operation);
            Assert.AreEqual(InterpretationReason.Requested, decisions[0].Reason);
        }

        // ----- the two shapes that used to escape the fallback entirely

        /// <summary>
        /// Assigning a value type to a <c>#variable</c> used to throw <c>ArgumentException</c> out of
        /// the emitter, which the fallback cannot see - so <c>CompileOrInterpret</c> was not honoured
        /// and the shape was a hard failure on every path. It compiles now.
        /// </summary>
        /// <remarks>
        /// The dictionary holds objects and LINQ inserts no boxing of its own, so an int-typed value
        /// handed to <c>SetVariable</c>'s <c>object</c> parameter made the call factory throw. That
        /// became a refusal first - which is what this test used to pin - and the value is boxed on
        /// the way in now, which removes the refusal too. A string-valued assignment compiled all
        /// along, which is why the split went unnoticed: the same assignment behaved differently
        /// depending only on whether the value happened to be a reference type.
        /// </remarks>
        [Test]
        public void AssigningAValueTypeToAVariableCompilesAndAgreesWithTheInterpreter()
        {
            var compiledVariables = new Dictionary<string, object>();
            Assert.AreEqual(5,
                Expression.ParseGetter<object, object>("#x = 5", EvaluationMode.MustCompile)
                    .GetValue(null, compiledVariables));
            Assert.AreEqual(5, compiledVariables["x"]);

            var interpretedVariables = new Dictionary<string, object>();
            Assert.AreEqual(5,
                Expression.ParseGetter<object, object>("#x = 5", EvaluationMode.MustInterpret)
                    .GetValue(null, interpretedVariables));
            Assert.AreEqual(5, interpretedVariables["x"]);

            var weakVariables = new Dictionary<string, object>();
            Assert.AreEqual(5, Expression.Parse("#x = 5").GetValue<object>(null, weakVariables));
            Assert.AreEqual(5, weakVariables["x"]);

            // the string form has a compiled setter and always did
            var stringVariables = new Dictionary<string, object>();
            Assert.AreEqual("five",
                Expression.ParseGetter<object, object>("#x = 'five'", EvaluationMode.MustCompile)
                    .GetValue(null, stringVariables));
            Assert.AreEqual("five", stringVariables["x"]);
        }

        /// <summary>
        /// The value that lands in the caller's dictionary is a boxed value of the assigned type, not
        /// something the boxing conversion reshaped: a caller reading the dictionary back sees exactly
        /// what a <c>#variable</c> assignment has always put there.
        /// </summary>
        [Test]
        public void AValueTypeAssignedToAVariableArrivesBoxedAsItself()
        {
            var compiledVariables = new Dictionary<string, object>();
            Expression.ParseGetter<object, object>("#x = 5", EvaluationMode.MustCompile)
                .GetValue(null, compiledVariables);

            var interpretedVariables = new Dictionary<string, object>();
            Expression.ParseGetter<object, object>("#x = 5", EvaluationMode.MustInterpret)
                .GetValue(null, interpretedVariables);

            Assert.AreEqual(typeof(int), compiledVariables["x"].GetType());
            Assert.AreEqual(typeof(int), interpretedVariables["x"].GetType());

            var doubleVariables = new Dictionary<string, object>();
            Expression.ParseGetter<object, object>("#x = 5.5", EvaluationMode.MustCompile)
                .GetValue(null, doubleVariables);

            Assert.AreEqual(typeof(double), doubleVariables["x"].GetType());
            Assert.AreEqual(5.5, doubleVariables["x"]);
        }

        /// <summary>
        /// A void expression whose body produces a value used to throw <c>InvalidOperationException</c>
        /// out of the void-shape check, which likewise escaped the fallback.
        /// </summary>
        [Test]
        public void AVoidExpressionThatProducesAValueIsRefusedRatherThanEscapingTheFallback()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseVoidExpression<Holder>("Ints.sort()", EvaluationMode.MustCompile));

            var holder = new Holder();
            var voidExpression = Expression.ParseVoidExpression<Holder>("Ints.sort()");

            // sort() builds a fresh list rather than sorting in place, and a void expression discards
            // whatever its body produced - so the point here is that it runs at all
            voidExpression.Execute(holder);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, holder.Ints);

            var status = Expression.GetCompilationStatus(voidExpression);
            Assert.AreEqual(InterpretationReason.CompilationRefused, status.Reason);
            StringAssert.Contains("a void expression must emit a void call or an assignment",
                status.RefusalMessage);
        }
    }
}

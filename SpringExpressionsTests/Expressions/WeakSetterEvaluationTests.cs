using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// What a weakly typed write does now that it goes through the compiler instead of always
    /// interpreting.
    /// </summary>
    /// <remarks>
    /// <c>WeaklyTypedExpression.SetValue</c> used to hand every write straight to the interpreter, on the
    /// grounds that routing it at the compiler "would refuse shapes that work today". The fallback
    /// removed that objection - under <see cref="EvaluationMode.CompileOrInterpret"/> a refusal is not a
    /// refusal - so writes now compile where they can, honour the mode, and are keyed by the declared
    /// types of *both* the context and the value.
    /// <p>
    /// The value's type is part of that key deliberately: it is what the assignment is compiled against.
    /// Without it every weak write assigned an object-typed value to a typed member, which compiles into
    /// a cast and then disagrees with the interpreter's conversion - a difference nothing would have
    /// caught, because nothing was refused.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class WeakSetterEvaluationTests : BaseCompiledTests
    {
        public class Holder
        {
            public string Name { get; set; }
            public int Number { get; set; }
            public object Anything { get; set; }
        }

        public class Base
        {
            public string Label { get; set; } = "base";
        }

        public class Derived : Base
        {
            public new string Label { get; set; } = "derived";
        }

        [Test]
        public void AWeakWriteCompilesWhenBothTypesAreKnown()
        {
            var holder = new Holder();

            Expression.Parse("Name", EvaluationMode.MustCompile).SetValue(holder, "Ana");
            Assert.AreEqual("Ana", holder.Name);

            Expression.Parse("Number", EvaluationMode.MustCompile).SetValue(holder, 45);
            Assert.AreEqual(45, holder.Number);
        }

        /// <summary>
        /// A value the member cannot hold is refused at compile time - which is the point of the value
        /// type being part of the key. The refusal is visible, so the fallback catches it and the
        /// interpreter converts, exactly as it always did.
        /// </summary>
        [Test]
        public void AValueTheMemberCannotHoldIsRefusedAndInterpretedInstead()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.Parse("Number", EvaluationMode.MustCompile).SetValue(new Holder(), "45"));

            var holder = new Holder();
            Expression.Parse("Number").SetValue(holder, "45");
            Assert.AreEqual(45, holder.Number);
        }

        /// <summary>
        /// An object-typed value against a typed member has no compiled form: only the runtime value
        /// decides whether it fits. The engine says the same about an object-typed method argument.
        /// </summary>
        /// <remarks>
        /// This is the case the value type had to be added for. Compiled, it used to emit a *cast*, so
        /// assigning a boxed 42 to a string member threw InvalidCastException where the interpreter
        /// converts it to "42". Refusing keeps the two backends on the same answer.
        /// </remarks>
        [Test]
        public void AnObjectTypedValueIsRefusedAndInterpretedInstead()
        {
            object boxed = 42;

            Assert.Throws<CompileErrorException>(
                () => Expression.Parse("Name", EvaluationMode.MustCompile).SetValue(new Holder(), boxed));

            var holder = new Holder();
            Expression.Parse("Name").SetValue(holder, boxed);
            Assert.AreEqual("42", holder.Name, "the interpreter converts, as it always has");
        }

        /// <summary>
        /// A member declared as object takes an object-typed value with nothing to decide, so it compiles.
        /// </summary>
        [Test]
        public void AnObjectTypedMemberTakesAnObjectTypedValue()
        {
            object boxed = 42;
            var holder = new Holder();

            Expression.Parse("Anything", EvaluationMode.MustCompile).SetValue(holder, boxed);
            Assert.AreEqual(42, holder.Anything);
        }

        /// <summary>
        /// Naming the type restores compilation - and buys the cast's own failure mode with it.
        /// </summary>
        /// <remarks>
        /// "as" is C#'s cast (see CLAUDE.md, "The cast operator: ruled"), so it converts nothing: a
        /// variable holding 42 fails the cast on **both** backends rather than being turned into "42".
        /// That is the trade the caller is making by writing it - compilation, in exchange for promising
        /// the type. Without the cast the same assignment is interpreted and converts. Do not "fix"
        /// either half: they are two different requests.
        /// </remarks>
        [Test]
        public void ACastRestoresCompilationAndFailsTheSameWayOnBothBackends()
        {
            var text = new Dictionary<string, object> { { "v", "Ana" } };
            var number = new Dictionary<string, object> { { "v", 42 } };

            Assert.AreEqual("Ana",
                Expression.ParseGetter<Holder, object>("Name = #v as string", EvaluationMode.MustCompile)
                    .GetValue(new Holder(), text));

            Assert.AreEqual("Ana",
                Expression.ParseGetter<Holder, object>("Name = as<string>(#v)", EvaluationMode.MustCompile)
                    .GetValue(new Holder(), text));

            Assert.Throws<InvalidCastException>(
                () => Expression.ParseGetter<Holder, object>("Name = #v as string", EvaluationMode.MustCompile)
                    .GetValue(new Holder(), number));

            Assert.Throws<InvalidCastException>(
                () => Expression.ParseGetter<Holder, object>("Name = #v as string", EvaluationMode.MustInterpret)
                    .GetValue(new Holder(), number));

            // and without the cast, the interpreter converts
            var holder = new Holder();
            Expression.Parse("Name = #v").GetValue(holder, number);
            Assert.AreEqual("42", holder.Name);
        }

        /// <summary>
        /// A compiled write binds the member against the **declared** context type; the interpreter binds
        /// against the runtime type. Routing weak writes through the compiler brings that difference to
        /// writes for the first time - it is the same trade reads have always made.
        /// </summary>
        /// <remarks>
        /// Do not "fix" one side of this. Which member a name selects when the declared and runtime types
        /// disagree is the subject of member-binding-semantics.md, and it is one question for reads and
        /// writes alike, not something to settle differently here.
        /// </remarks>
        [Test]
        public void AWeakWriteBindsAgainstTheDeclaredContextType()
        {
            var compiled = new Derived();
            Expression.Parse("Label").SetValue<Base, string>(compiled, "written");

            Assert.AreEqual("written", ((Base)compiled).Label, "the declared type's member is the one written");
            Assert.AreEqual("derived", compiled.Label, "the hidden member is untouched");

            var declaredAsDerived = new Derived();
            Expression.Parse("Label").SetValue(declaredAsDerived, "written");

            Assert.AreEqual("base", ((Base)declaredAsDerived).Label);
            Assert.AreEqual("written", declaredAsDerived.Label);

            // the interpreter resolves against the runtime type whatever the declared one is
            var interpreted = new Derived();
            Expression.Parse("Label", EvaluationMode.MustInterpret).SetValue<Base, string>(interpreted, "written");

            Assert.AreEqual("base", ((Base)interpreted).Label);
            Assert.AreEqual("written", interpreted.Label);
        }
    }
}

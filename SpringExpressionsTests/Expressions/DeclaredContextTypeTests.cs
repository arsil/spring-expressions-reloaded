using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class HidingAnimal
    {
        public virtual string Speak() { return "Animal.Speak"; }
        public string Name() { return "Animal.Name"; }
    }

    public class HidingDog : HidingAnimal
    {
        public override string Speak() { return "Dog.Speak"; }
        public new string Name() { return "Dog.Name"; }
    }

    /// <summary>
    /// <see cref="IExpression"/> takes its context generically, so the type the caller declared reaches the
    /// compiler instead of being erased to <c>object</c> at the boundary. Members then bind the way C#
    /// binds them for a variable of that type.
    /// </summary>
    [TestFixture]
    public class DeclaredContextTypeTests
    {
        /// <summary>
        /// A declared type is a contract: <c>Name</c> is hidden by <c>new</c> in the derived class, and a
        /// context declared as the base type resolves the base member - as it would in C#.
        /// </summary>
        [Test]
        public void HiddenMemberBindsAgainstTheDeclaredType()
        {
            HidingAnimal declaredAsAnimal = new HidingDog();

            Assert.AreEqual("Animal.Name", Expression.Parse("Name()").GetValue(declaredAsAnimal));
        }

        /// <summary>
        /// An <c>object</c>-typed caller states no contract, so the runtime type is still the best type
        /// available and the older behaviour stands.
        /// </summary>
        [Test]
        public void ObjectTypedContextStillBindsAgainstTheRuntimeType()
        {
            object declaredAsObject = new HidingDog();

            Assert.AreEqual("Dog.Name", Expression.Parse("Name()").GetValue(declaredAsObject));
        }

        [Test]
        public void OverriddenMemberDispatchesVirtuallyWhicheverTypeIsDeclared()
        {
            HidingAnimal declaredAsAnimal = new HidingDog();
            object declaredAsObject = new HidingDog();

            Assert.AreEqual("Dog.Speak", Expression.Parse("Speak()").GetValue(declaredAsAnimal));
            Assert.AreEqual("Dog.Speak", Expression.Parse("Speak()").GetValue(declaredAsObject));
        }

        /// <summary>
        /// A null context is legitimate, which is why the type parameter carries no not-null constraint. A
        /// typed null also compiles better than an untyped one: the tree is built from the declared type
        /// rather than from a null constant, so a member access fails at evaluation the way C# would fail,
        /// instead of failing to resolve at compile time.
        /// </summary>
        [Test]
        public void TypedNullContextIsValid()
        {
            HidingAnimal nullAnimal = null;

            Assert.AreEqual(2, Expression.Parse("2").GetValue(nullAnimal));
            Assert.Throws<NullReferenceException>(
                () => Expression.Parse("Name()").GetValue(nullAnimal));
        }

        [Test]
        public void VariablesFlowThroughTheGenericOverload()
        {
            HidingAnimal declaredAsAnimal = new HidingDog();
            IExpression expression = Expression.Parse("#x");

            Assert.AreEqual(
                7, expression.GetValue(declaredAsAnimal, new Dictionary<string, object> { { "x", 7 } }));
            Assert.AreEqual(
                8, expression.GetValue(declaredAsAnimal, new Dictionary<string, object> { { "x", 8 } }));
        }

        [Test]
        public void EachContextTypeGetsATreeCompiledForIt()
        {
            Assert.AreEqual(3, Expression.Parse("Length").GetValue("abc"));
            Assert.AreEqual(3, Expression.Parse("Length").GetValue(new int[3]));
        }

        /// <summary>
        /// One instance serves any number of context types, and they do not displace each other.
        /// </summary>
        /// <remarks>
        /// The compiled form belongs to the expression object, which keeps one per declared context type,
        /// rather than to an AST node with a single slot. While it lived on the node this threw
        /// `InvalidCastException` on the second type - first the root's cast, then the delegate's.
        /// </remarks>
        [Test]
        public void OneInstanceServesManyContextTypes()
        {
            IExpression expression = Expression.Parse("Length");

            Assert.AreEqual(3, expression.GetValue("abc"));
            Assert.AreEqual(3, expression.GetValue(new int[3]));
            Assert.AreEqual(2, expression.GetValue(new string[2]));

            // The first one still works after the others: entries accumulate rather than replace.
            Assert.AreEqual(3, expression.GetValue("abc"));
        }

        /// <summary>
        /// The same holds for a caller with nothing to declare. An object-typed context is compiled for the
        /// runtime type of the value, and each runtime type gets its own compiled form - where the node used
        /// to bake the first type it saw and fail the cast for every later one.
        /// </summary>
        [Test]
        public void ObjectTypedContextAlsoServesManyRuntimeTypes()
        {
            IExpression expression = Expression.Parse("Length");

            Assert.AreEqual(3, expression.GetValue((object)"abc"));
            Assert.AreEqual(3, expression.GetValue((object)new int[3]));
            Assert.AreEqual(2, expression.GetValue((object)new string[2]));
            Assert.AreEqual(3, expression.GetValue((object)"abc"));
        }
    }
}

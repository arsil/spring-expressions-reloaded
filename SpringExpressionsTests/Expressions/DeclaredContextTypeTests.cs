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
        /// One expression instance holds one compiled delegate, so it serves one context type. Evaluating
        /// the same instance against a second, unrelated context type throws.
        /// </summary>
        /// <remarks>
        /// This is the captured-root defect in its new form rather than its removal: the signature change
        /// means the root no longer has to be guessed from the first value seen, but the single delegate
        /// slot still ties an instance to the first context type it compiled for - the cast that fails is
        /// now the delegate's, not the root's. Caching one delegate per context type would remove the
        /// limitation, at which point this test should assert the second value instead of the throw.
        /// </remarks>
        [Test]
        public void OneInstanceServesOneContextTypeOnly()
        {
            IExpression expression = Expression.Parse("Length");

            Assert.AreEqual(3, expression.GetValue("abc"));
            Assert.Throws<InvalidCastException>(() => expression.GetValue(new int[3]));
        }
    }
}

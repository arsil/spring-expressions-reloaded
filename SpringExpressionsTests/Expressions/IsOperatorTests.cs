using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// <c>is</c> asks about the value, so it emits a runtime test.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The compiled path used to answer with a constant computed from
    /// <c>target.IsAssignableFrom(operand.Type)</c> - the <i>static</i> type - so it never looked at
    /// what the operand held. Both ordinary uses of the operator were wrong: testing an
    /// <c>object</c>-typed value, and testing a base-typed variable for a derived type. A null string
    /// was reported as being a string, which is the row that shows it was answering a different
    /// question rather than answering strictly.
    /// </p>
    /// <p>
    /// <c>LExpression.TypeIs</c> is what C# compiles <c>is</c> to, and it needed no ruling: measured on
    /// every shape here it matches the interpreter and matches C#. Open-issues item 22.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class IsOperatorTests : BaseCompiledTests
    {
        public class Animal { }
        public class Dog : Animal { }

        public class Root
        {
            public int Number { get; set; } = 45;
            public int? NullableNumber { get; set; } = 7;
            public int? NoNumber { get; set; }
            public DateTime? NullableDate { get; set; } = new DateTime(2020, 1, 1);
            public string Name { get; set; } = "Ana";
            public string NullName { get; set; }
            public object AnyInt { get; set; } = 45;
            public object AnyString { get; set; } = "text";
            public object AnyNull { get; set; }
            public Animal AsAnimal { get; set; } = new Dog();
            public Animal PlainAnimal { get; set; } = new Animal();
            public Dog AsDog { get; set; } = new Dog();
            public IList<int> Ints { get; set; } = new List<int> { 1 };
            public IEnumerable Old { get; set; } = new ArrayList { 1 };
        }

        static void Both(string expression, bool expected)
        {
            var root = new Root();

            Assert.AreEqual(expected,
                CompileGetter<Root, object>(expression).GetValue(root), "compiled: " + expression);
            Assert.AreEqual(expected,
                InterpretGetter<Root, object>(expression).GetValue(root), "interpreted: " + expression);
        }

        const string Dogged = "T(SpringExpressionsTests.Expressions.IsOperatorTests+Dog)";
        const string Animalish = "T(SpringExpressionsTests.Expressions.IsOperatorTests+Animal)";

        /// <summary>
        /// An object-typed operand: the reason the operator exists, and it answered false for every
        /// type before.
        /// </summary>
        [Test]
        public void AnObjectTypedOperandIsTestedByWhatItHolds()
        {
            Both("AnyInt is T(System.Int32)", true);
            Both("AnyInt is T(System.String)", false);
            Both("AnyString is T(System.String)", true);
            Both("AnyInt is T(System.Object)", true);
            Both("AnyNull is T(System.String)", false);
        }

        /// <summary>
        /// A base-typed variable holding a derived instance: the other reason the operator exists.
        /// </summary>
        [Test]
        public void ABaseTypedOperandIsTestedByItsRuntimeType()
        {
            Both("AsAnimal is " + Dogged, true);
            Both("PlainAnimal is " + Dogged, false);
            Both("AsDog is " + Animalish, true);
            Both("AsDog is " + Dogged, true);
        }

        [Test]
        public void AnInterfaceTypedOperandIsTestedByItsRuntimeType()
        {
            Both("Ints is T(System.Collections.Generic.List`1[System.Int32])", true);
            Both("Old is T(System.Collections.ArrayList)", true);
            Both("Old is T(System.Collections.Hashtable)", false);
        }

        /// <summary>
        /// A nullable holding a value is its underlying type; one holding nothing is not. Both backends
        /// and C# agree, and this is the row the ledger recorded before the emitter was fixed.
        /// </summary>
        [Test]
        public void ANullableIsItsUnderlyingTypeWhenItHoldsAValue()
        {
            Both("NullableNumber is T(System.Int32)", true);
            Both("NoNumber is T(System.Int32)", false);
            Both("NullableDate is T(System.DateTime)", true);

            // C# itself, as the reference
            int? seven = 7;
            int? nothing = null;
            Assert.IsTrue(seven is int);
            Assert.IsFalse(nothing is int);
        }

        /// <summary>
        /// A null is not an instance of anything - including its own declared type, which the constant
        /// answer got backwards.
        /// </summary>
        [Test]
        public void ANullIsNotAnInstanceOfItsOwnDeclaredType()
        {
            Both("NullName is T(System.String)", false);
            Both("NullName is T(System.Object)", false);
            Both("null is T(System.String)", false);

            string nothing = null;
            Assert.IsFalse(nothing is string);
        }

        [Test]
        public void APlainOperandIsStillItsOwnType()
        {
            Both("Number is T(System.Int32)", true);
            Both("Number is T(System.Object)", true);
            Both("Number is T(System.String)", false);
            Both("Name is T(System.String)", true);
            Both("45 is T(System.Int32)", true);
            Both("'x' is T(System.String)", true);
        }

        /// <summary>
        /// A <c>Nullable&lt;T&gt;</c> target needs no special case, and this is the row where guessing
        /// would have cost a working shape.
        /// </summary>
        /// <remarks>
        /// It looks as though the interpreter could never say yes to one - it reads
        /// <c>instance.GetType()</c>, and boxing a nullable that holds a value yields the plain boxed
        /// <c>T</c>, so <c>Nullable&lt;int&gt;</c> is never what it sees. But
        /// <c>typeof(int?).IsAssignableFrom(typeof(int))</c> is <b>true</b>, so it answers true anyway,
        /// and <c>TypeIs</c> matches it on every row including the empty nullable. A refusal was written
        /// for this case on the strength of the first reading and removed once it was measured.
        /// </remarks>
        [Test]
        public void ANullableTargetIsAnsweredAndTheBackendsAgree()
        {
            Both("NullableNumber is T(System.Nullable`1[System.Int32])", true);
            Both("NoNumber is T(System.Nullable`1[System.Int32])", false);
            Both("Number is T(System.Nullable`1[System.Int32])", true);
            Both("Name is T(System.Nullable`1[System.Int32])", false);

            Assert.IsTrue(typeof(int?).IsAssignableFrom(typeof(int)),
                "the reflection fact the rows above rest on");
        }

        /// <summary>
        /// The weakly typed route falls back for the refused shape and compiles the rest, so a caller
        /// gets the same answer either way.
        /// </summary>
        [Test]
        public void TheWeaklyTypedRouteAnswersEveryShape()
        {
            var root = new Root();

            Assert.AreEqual(true, Expression.Parse("AnyInt is T(System.Int32)").GetValue(root));
            Assert.AreEqual(true, Expression.Parse("AsAnimal is " + Dogged).GetValue(root));
            Assert.AreEqual(false, Expression.Parse("NullName is T(System.String)").GetValue(root));
            Assert.AreEqual(true,
                Expression.Parse("NullableNumber is T(System.Nullable`1[System.Int32])").GetValue(root));
        }
    }
}

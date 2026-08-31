using System;

using NUnit.Framework;

using System.Collections.Generic;
using System.Linq;


namespace SpringExpressionsTests.Expressions
{
    [TestFixture]
    public class EqualityTestes : BaseCompiledTests
    {
        [Test]
        public void OldEqualsTestForEqualAndNotEqualOperators()
        {
            var ctx = new List<OnlyOldEquals>
                { new OnlyOldEquals("1"), new OnlyOldEquals("2"), new OnlyOldEquals("1") };

            // will use Object.Equals
            var defaultComparer = EqualityComparer<OnlyOldEquals>.Default;

            Assert.IsFalse(defaultComparer.Equals(ctx[0], ctx[1]));
            Assert.IsTrue(defaultComparer.Equals(ctx[0], ctx[2]));


            Assert.IsFalse(InterpretGetter<List<OnlyOldEquals>, bool>("[0] == [1]").GetValue(ctx));
            Assert.IsFalse(CompileGetter  <List<OnlyOldEquals>, bool>("[0] == [1]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyOldEquals>, bool>("[1] == [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter  <List<OnlyOldEquals>, bool>("[1] == [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyOldEquals>, bool>("[0] == [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter  <List<OnlyOldEquals>, bool>("[0] == [2]").GetValue(ctx));


            Assert.IsTrue(InterpretGetter<List<OnlyOldEquals>, bool>("[0] != [1]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyOldEquals>, bool>("[0] != [1]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyOldEquals>, bool>("[1] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyOldEquals>, bool>("[1] != [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyOldEquals>, bool>("[0] != [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyOldEquals>, bool>("[0] != [2]").GetValue(ctx));
        }

        [Test]
        public void OldEqualsTestForDistinct()
        {
            var ctx = new List<OnlyOldEquals>
                { new OnlyOldEquals("1"), new OnlyOldEquals("2"), null, new OnlyOldEquals("1") };

            {
                var distinctInterpreted = InterpretGetter<List<OnlyOldEquals>, List<OnlyOldEquals>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(2, distinctInterpreted.Count);
                Assert.That(distinctInterpreted.Select(i => i.Id), Is.EquivalentTo(new[] { "2", "1" }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<OnlyOldEquals>, object>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(2, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject.Cast<OnlyOldEquals>().Select(i => i.Id), Is.EquivalentTo(new[] { "2", "1" }));


                var distinctCompiled = CompileGetter<List<OnlyOldEquals>, List<OnlyOldEquals>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(2, distinctCompiled.Count);
                Assert.That(distinctCompiled.Select(i => i.Id), Is.EquivalentTo(new[] { "2", "1" }));
            }

            {
                var distinctInterpreted = InterpretGetter<List<OnlyOldEquals>, List<OnlyOldEquals>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpreted.Count);
                Assert.That(distinctInterpreted.Select(i => i?.Id), Is.EquivalentTo(new[] { "2", "1", null }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<OnlyOldEquals>, object>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject.Cast<OnlyOldEquals>().Select(i => i?.Id), Is.EquivalentTo(new[] { "2", "1", null }));


                var distinctCompiled = CompileGetter<List<OnlyOldEquals>, List<OnlyOldEquals>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctCompiled.Count);
                Assert.That(distinctCompiled.Select(i => i?.Id), Is.EquivalentTo(new[] { "2", "1", null }));
            }
        }

        [Test]
        public void OnlyEquatableTestForEqualityOperator()
        {
            var ctx = new List<OnlyEquatable>
                { new OnlyEquatable("1"), new OnlyEquatable("2"), new OnlyEquatable("1") };

            // will use IEquatable<>.Equals
            var defaultComparer = EqualityComparer<OnlyEquatable>.Default;

            Assert.IsFalse(defaultComparer.Equals(ctx[0], ctx[1]));
            Assert.IsTrue(defaultComparer.Equals(ctx[0], ctx[2]));

            Assert.IsFalse(InterpretGetter<List<OnlyEquatable>, bool>("[0] == [1]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyEquatable>, bool>("[0] == [1]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyEquatable>, bool>("[1] == [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyEquatable>, bool>("[1] == [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyEquatable>, bool>("[0] == [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyEquatable>, bool>("[0] == [2]").GetValue(ctx));



            Assert.IsTrue(InterpretGetter<List<OnlyEquatable>, bool>("[0] != [1]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyEquatable>, bool>("[0] != [1]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyEquatable>, bool>("[1] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyEquatable>, bool>("[1] != [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyEquatable>, bool>("[0] != [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyEquatable>, bool>("[0] != [2]").GetValue(ctx));
        }

        [Test]
        public void OnlyEquatableTestForDistinct()
        {
            var ctx = new List<OnlyEquatable>
                { new OnlyEquatable("1"), new OnlyEquatable("2"), null, new OnlyEquatable("1") };

            {
                var distinctInterpreted = InterpretGetter<List<OnlyEquatable>, List<OnlyEquatable>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(2, distinctInterpreted.Count);
                Assert.That(distinctInterpreted.Select(i => i.Id), Is.EquivalentTo(new[] { "2", "1" }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<OnlyEquatable>, object>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(2, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject.Cast<OnlyEquatable>().Select(i => i.Id), Is.EquivalentTo(new[] { "2", "1" }));


                var distinctCompiled = CompileGetter<List<OnlyEquatable>, List<OnlyEquatable>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(2, distinctCompiled.Count);
                Assert.That(distinctCompiled.Select(i => i.Id), Is.EquivalentTo(new[] { "2", "1" }));
            }

            {
                var distinctInterpreted = InterpretGetter<List<OnlyEquatable>, List<OnlyEquatable>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpreted.Count);
                Assert.That(distinctInterpreted.Select(i => i?.Id), Is.EquivalentTo(new[] { "2", "1", null }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<OnlyEquatable>, object>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject.Cast<OnlyEquatable>().Select(i => i?.Id), Is.EquivalentTo(new[] { "2", "1", null }));


                var distinctCompiled = CompileGetter<List<OnlyEquatable>, List<OnlyEquatable>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctCompiled.Count);
                Assert.That(distinctCompiled.Select(i => i?.Id), Is.EquivalentTo(new[] { "2", "1", null }));
            }
        }

        [Test]
        public void TestForInt()
        {
            var intComparer = EqualityComparer<int>.Default;

            Assert.IsTrue(intComparer.Equals(999, 999));
            Assert.IsFalse(intComparer.Equals(989, 999));

            var ctx = new List<int> { 1, 5, 1, 9, 5 };

            Assert.IsFalse(InterpretGetter<List<int>, bool>("[1] == [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<int>, bool>("[1] == [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<int>, bool>("[0] == [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<int>, bool>("[0] == [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<int>, bool>("[1] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<int>, bool>("[1] != [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<int>, bool>("[0] != [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<int>, bool>("[0] != [2]").GetValue(ctx));

            {
                var distinctInterpreted = InterpretGetter<List<int>, List<int>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpreted.Count);
                Assert.That(distinctInterpreted, Is.EquivalentTo(new[] { 1, 9, 5 }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<int>, object>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject, Is.EquivalentTo(new[] { 1, 9, 5 }));


                var distinctCompiled = CompileGetter<List<int>, List<int>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctCompiled.Count);
                Assert.That(distinctCompiled, Is.EquivalentTo(new[] { 1, 9, 5 }));
            }

            {
                var distinctInterpreted = InterpretGetter<List<int>, List<int>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpreted.Count);
                Assert.That(distinctInterpreted, Is.EquivalentTo(new[] { 1, 9, 5 }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<int>, object>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject, Is.EquivalentTo(new[] { 1, 9, 5 }));


                var distinctCompiled = CompileGetter<List<int>, List<int>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(3, distinctCompiled.Count);
                Assert.That(distinctCompiled, Is.EquivalentTo(new[] { 1, 9, 5 }));
            }
        }

        [Test]
        public void TestForNullableInt()
        {
            var nullableIntComparer = EqualityComparer<int?>.Default;

            Assert.IsTrue(nullableIntComparer.Equals(999, 999));
            Assert.IsFalse(nullableIntComparer.Equals(989, 999));
            Assert.IsFalse(nullableIntComparer.Equals(null, 999));
            Assert.IsFalse(nullableIntComparer.Equals(998, null));
            Assert.IsTrue(nullableIntComparer.Equals(null, null));

            //                         0  1  2     3, 4, 5,    6
            var ctx = new List<int?> { 1, 5, 1, null, 9, 5, null };

            Assert.IsFalse(InterpretGetter<List<int?>, bool>("[1] == [2]").GetValue(ctx));
            Assert.IsFalse(  CompileGetter<List<int?>, bool>("[1] == [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<int?>, bool>("[0] == [2]").GetValue(ctx));
            Assert.IsTrue(  CompileGetter<List<int?>, bool>("[0] == [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<int?>, bool>("[3] == [2]").GetValue(ctx));
            Assert.IsFalse(  CompileGetter<List<int?>, bool>("[3] == [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<int?>, bool>("[2] == [3]").GetValue(ctx));
            Assert.IsFalse(  CompileGetter<List<int?>, bool>("[2] == [3]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<int?>, bool>("[3] == [6]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<int?>, bool>("[3] == [6]").GetValue(ctx));



            Assert.IsTrue(InterpretGetter<List<int?>, bool>("[1] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<int?>, bool>("[1] != [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<int?>, bool>("[0] != [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<int?>, bool>("[0] != [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<int?>, bool>("[3] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<int?>, bool>("[3] != [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<int?>, bool>("[2] != [3]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<int?>, bool>("[2] != [3]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<int?>, bool>("[3] != [6]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<int?>, bool>("[3] != [6]").GetValue(ctx));

            {
                var distinctInterpreted = InterpretGetter<List<int?>, List<int?>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpreted.Count);
                Assert.That(distinctInterpreted, Is.EquivalentTo(new int?[] { 1, 9, 5 }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<int?>, object>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject, Is.EquivalentTo(new int?[] { 1, 9, 5 }));


                var distinctCompiled = CompileGetter<List<int?>, List<int?>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctCompiled.Count);
                Assert.That(distinctCompiled, Is.EquivalentTo(new int?[] { 1, 9, 5 }));
            }

            {
                var distinctInterpreted = InterpretGetter<List<int?>, List<int?>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpreted.Count);
                Assert.That(distinctInterpreted, Is.EquivalentTo(new int?[] { 1, null, 9, 5 }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<int?>, object>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject, Is.EquivalentTo(new int?[] { 1, null, 9, 5 }));


                var distinctCompiled = CompileGetter<List<int?>, List<int?>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(4, distinctCompiled.Count);
                Assert.That(distinctCompiled, Is.EquivalentTo(new int? [] { 1, 9, null, 5 }));
            }
        }

        [Test]
        public void TestForStrings()
        {
            var nullableIntComparer = EqualityComparer<string>.Default;

            Assert.IsTrue(nullableIntComparer.Equals("999", "999"));
            Assert.IsFalse(nullableIntComparer.Equals("989", "999"));
            Assert.IsFalse(nullableIntComparer.Equals(null, "999"));
            Assert.IsFalse(nullableIntComparer.Equals("998", null));
            Assert.IsTrue(nullableIntComparer.Equals(null, null));

            //                            0    1    2      3,  4,   5,     6
            var ctx = new List<string> { "1", "5", "1", null, "9", "5", null };

            Assert.IsFalse(InterpretGetter<List<string>, bool>("[1] == [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<string>, bool>("[1] == [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<string>, bool>("[0] == [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<string>, bool>("[0] == [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<string>, bool>("[3] == [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<string>, bool>("[3] == [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<string>, bool>("[2] == [3]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<string>, bool>("[2] == [3]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<string>, bool>("[3] == [6]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<string>, bool>("[3] == [6]").GetValue(ctx));



            Assert.IsTrue(InterpretGetter<List<string>, bool>("[1] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<string>, bool>("[1] != [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<string>, bool>("[0] != [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<string>, bool>("[0] != [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<string>, bool>("[3] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<string>, bool>("[3] != [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<string>, bool>("[2] != [3]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<string>, bool>("[2] != [3]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<string>, bool>("[3] != [6]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<string>, bool>("[3] != [6]").GetValue(ctx));

            {
                var distinctInterpreted = InterpretGetter<List<string>, List<string>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpreted.Count);
                Assert.That(distinctInterpreted, Is.EquivalentTo(new [] { "1", "9", "5" }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<string>, object>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject, Is.EquivalentTo(new [] { "1", "9", "5" }));


                var distinctCompiled = CompileGetter<List<string>, List<string>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctCompiled.Count);
                Assert.That(distinctCompiled, Is.EquivalentTo(new [] { "1", "9", "5" }));
            }

            {
                var distinctInterpreted = InterpretGetter<List<string>, List<string>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpreted.Count);
                Assert.That(distinctInterpreted, Is.EquivalentTo(new [] { "1", null, "9", "5" }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<string>, object>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject, Is.EquivalentTo(new [] { "1", null, "9", "5" }));


                var distinctCompiled = CompileGetter<List<string>, List<string>>(
                    "distinct(true)").GetValue(ctx);
                Assert.AreEqual(4, distinctCompiled.Count);
                Assert.That(distinctCompiled, Is.EquivalentTo(new [] { "1", "9", null, "5" }));
            }
        }

        /// <summary>
        /// A type declaring <c>operator ==</c> and no <c>Equals</c> override. The engine used to ignore
        /// the operator and answer by <c>EqualityComparer&lt;T&gt;.Default</c>, which without an
        /// <c>Equals</c> override is reference equality - so two instances with the same Id compared
        /// unequal, where C# says they are equal. That was pinned here as a limitation; the operator is
        /// consulted now and the engine agrees with C#.
        /// </summary>
        /// <remarks>
        /// <c>distinct()</c> deliberately still answers by the comparer, so it and <c>==</c> disagree
        /// for a type like this. That is not an oversight and not a divergence between the backends -
        /// it is exactly C#'s own split, asserted side by side below: <c>Enumerable.Distinct</c> uses
        /// <c>EqualityComparer</c> while <c>==</c> uses the operator, and this test shows the engine
        /// matching both. Do not "fix" one of them into the other.
        /// </remarks>
        [Test]
        public void OnlyEqualityOperatorTest_TheOperatorIsHonouredAndDistinctStillIsNot()
        {
            var a1 = new OnlyEqualityOperator("A");
            var a2 = new OnlyEqualityOperator("A");
            var b = new OnlyEqualityOperator("B");

            // what C# itself says
            Assert.IsTrue(a1 == a2);
            Assert.IsTrue(a1 != b);
            Assert.IsTrue(a2 != b);

            // and what the comparer says, which is what the engine used to answer: without an Equals
            // override it is reference equality, so even a1 and a2 come out unequal
            var cmp = EqualityComparer<OnlyEqualityOperator>.Default;
            Assert.IsFalse(cmp.Equals(a1, a2));
            Assert.IsFalse(cmp.Equals(b, a2));
            Assert.IsFalse(cmp.Equals(a1, b));

            var ctx = new List<OnlyEqualityOperator> { a1, b, a2 };

            // the engine now answers as C# does - [0] and [2] are the two "A"s
            Assert.IsFalse(InterpretGetter<List<OnlyEqualityOperator>, bool>("[0] == [1]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyEqualityOperator>, bool>("[0] == [1]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyEqualityOperator>, bool>("[0] == [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyEqualityOperator>, bool>("[0] == [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyEqualityOperator>, bool>("[1] == [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyEqualityOperator>, bool>("[1] == [2]").GetValue(ctx));

            // != stays the exact negation of ==, which is the engine's standing rule: the type's own
            // op_Inequality is deliberately not looked up
            Assert.IsTrue(InterpretGetter<List<OnlyEqualityOperator>, bool>("[0] != [1]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyEqualityOperator>, bool>("[0] != [1]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyEqualityOperator>, bool>("[0] != [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyEqualityOperator>, bool>("[0] != [2]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyEqualityOperator>, bool>("[1] != [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyEqualityOperator>, bool>("[1] != [2]").GetValue(ctx));

            {
                // distinct() answers by the comparer, not the operator - and so does LINQ's own, which
                // is the point: the engine matches C# on both sides of the split.
                Assert.AreEqual(3, ctx.Distinct().Count());

                var distinctInterpreted = InterpretGetter<List<OnlyEqualityOperator>, List<OnlyEqualityOperator>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpreted.Count);
                Assert.That(distinctInterpreted.Select(i => i?.Id).Distinct(), Is.EquivalentTo(new[] { "A", "B" }));


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<OnlyEqualityOperator>, object>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctInterpretedAsObject.Count);
                Assert.That(distinctInterpretedAsObject.Cast<OnlyEqualityOperator>().Select(i => i?.Id).Distinct(), Is.EquivalentTo(new[] { "A", "B" }));


                var distinctCompiled = CompileGetter<List<OnlyEqualityOperator>, List<OnlyEqualityOperator>>(
                    "distinct()").GetValue(ctx);
                Assert.AreEqual(3, distinctCompiled.Count);
                Assert.That(distinctCompiled.Select(i => i?.Id).Distinct(), Is.EquivalentTo(new[] { "A", "B" }));
            }

        }

             // todo: error: nullable vs notNullable


        // Equality always works because object always has Equals(object) implementation !!!



        class OnlyOldEquals : IdClass
        {
            public OnlyOldEquals(string id) : base(id) { }

            public override bool Equals(object obj)
            {
                if (obj is OnlyOldEquals other)
                    return Id == other.Id;

                return false;
            }

            public override int GetHashCode()
                => Id.GetHashCode();
        }


        class OnlyEquatable : IdClass, IEquatable<OnlyEquatable>
        {
            public OnlyEquatable(string id) : base(id) { }

            public bool Equals(OnlyEquatable other)
            {
                if (other != null)
                    return Id == other.Id;

                return false;
            }

            public override int GetHashCode()
                => Id.GetHashCode();
        }


        #pragma warning disable CS0660, CS0661
        class OnlyEqualityOperator : IdClass
        {
            public OnlyEqualityOperator(string id) : base(id) { }

            public static bool operator ==(OnlyEqualityOperator t1, OnlyEqualityOperator t2)
                => t1?.Id == t2?.Id;

            public static bool operator !=(OnlyEqualityOperator t1, OnlyEqualityOperator t2)
                => t1?.Id != t2?.Id;
        }
        #pragma warning restore CS0660, CS0661


        abstract class IdClass
        {
            protected IdClass(string id)
            { Id = id; }

            public string Id { get; }

            public override string ToString() => Id;
        }







    }
}

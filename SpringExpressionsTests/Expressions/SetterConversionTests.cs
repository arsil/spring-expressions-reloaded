using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// What the compiled setter may convert on the way in, and what it refuses.
    /// </summary>
    /// <remarks>
    /// The rule is <c>ArrayElementConversions</c>, borrowed from <c>new T[] {…}</c> and reused by
    /// <c>params</c>: identity, a retyped null literal, reference or boxing assignability, and C#'s
    /// implicit numeric widenings. Assignment gains no rule of its own.
    /// <p>
    /// Every refusal below still <b>works</b> - the interpreter serves it and lands the right value -
    /// so this fixture is about which shapes have a compiled form, not about which shapes are legal.
    /// Each refused row asserts the interpreted answer beside it, because that answer is the reason
    /// the row is refused: where the interpreter's conversion and an emitted one would reach different
    /// values, only one of them can be right and the engine declines to guess.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class SetterConversionTests
    {
        public class Target
        {
            public string Name { get; set; }
            public int Number { get; set; }
            public long Big { get; set; }
            public double Real { get; set; }
            public decimal Amount { get; set; }
            public short Small { get; set; }
            public DateTime When { get; set; }
            public string[] Tags { get; set; }
            public object Anything { get; set; }

            public long BigField;
        }

        [Test]
        public void AnExactTypeStillCompiles()
        {
            AssertCompilesAndLands<string>("Name", "ok", "ok");
            AssertCompilesAndLands<int>("Number", 45, 45);
            AssertCompilesAndLands<decimal>("Amount", 45m, 45m);
            AssertCompilesAndLands<string[]>("Tags", new[] { "a" }, new[] { "a" });
        }

        [Test]
        public void AWideningWriteCompilesNow()
        {
            // These are C# implicit conversions and both backends already agreed on the value; they
            // refused only because the emitted assignment demanded an exact type match.
            AssertCompilesAndLands<int>("Big", 45, 45L);
            AssertCompilesAndLands<int>("Real", 45, 45d);
            AssertCompilesAndLands<int>("Amount", 45, 45m);
            AssertCompilesAndLands<long>("Real", 45L, 45d);
            AssertCompilesAndLands<float>("Real", 1.5f, 1.5d);
            AssertCompilesAndLands<short>("Number", (short)45, 45);
        }

        [Test]
        public void AWideningWriteToAFieldCompilesToo()
        {
            var target = new Target();

            Expression.ParseSetter<Target, int>("BigField", EvaluationMode.MustCompile)
                .SetValue(target, 45);

            Assert.AreEqual(45L, target.BigField);
        }

        [Test]
        public void AReferenceWideningAndBoxingCompile()
        {
            AssertCompilesAndLands<string>("Anything", "ok", "ok");
            AssertCompilesAndLands<int>("Anything", 45, 45);
        }

        [Test]
        public void ARealIntoAnIntegralMemberIsRefusedBecauseTheInterpreterRounds()
        {
            // The measurement that decides it: 45.6 lands 46 interpreted, where an emitted conversion
            // would truncate to 45. Same disagreement MethodNode.ConvertParameters already refuses for
            // method arguments - do not "fix" one side without ruling the other.
            AssertRefusedButInterpreted<double>("Number", 45.6, 46);
            AssertRefusedButInterpreted<decimal>("Number", 45.6m, 46);
            AssertRefusedButInterpreted<double>("Big", 45.6, 46L);
        }

        [Test]
        public void AStringIntoATypedMemberIsRefusedBecauseTheInterpreterParsesIt()
        {
            // C# has no such conversion, and neither does this engine's cast operator - '45' as T(int)
            // refuses compiled. The interpreter converts through TypeConverter/ChangeType, which does.
            AssertRefusedButInterpreted<string>("Number", "45", 45);
            AssertRefusedButInterpreted<string>("When", "2020-01-01", new DateTime(2020, 1, 1));
        }

        [Test]
        public void IntegralNarrowingIsRefusedOverTheExceptionItWouldThrow()
        {
            // The values agree and both sides fail on overflow, so this is the closest call in the
            // fixture. It stays refused because the two failures are not the same failure: the
            // interpreter throws the inherited TypeMismatchException and an emitted ConvertChecked
            // would throw OverflowException, so a caller catching the inherited one would stop seeing
            // it precisely when the shape happened to compile.
            AssertRefusedButInterpreted<int>("Small", 45, (short)45);
            AssertRefusedButInterpreted<long>("Number", 45L, 45);

            var overflow = Expression.ParseSetter<Target, int>("Small", EvaluationMode.MustInterpret);
            Assert.Throws<SpringCore.TypeMismatchException>(
                () => overflow.SetValue(new Target(), 40000));
        }

        [Test]
        public void ACollectionCompilesIntoAnArrayMemberWhenTheItemTypesMatch()
        {
            // Ruled 2026-09-10: the compiled setter builds the target's collection kind when the two
            // sides hold the *same item type*. There is nothing to convert then - only a container to
            // build - so the backends agree by construction, on the runtime type as well as the value.
            //
            // This used to be refused, and the entry recording it claimed "emitting a ToArray would
            // agree with the interpreter". It would not, in general: the interpreter converts element
            // by element, which is the next test.
            AssertCompilesAndLands<List<string>>(
                "Tags", new List<string> { "SPELL" }, new[] { "SPELL" });

            AssertCompilesAndLands<HashSet<string>>(
                "Tags", new HashSet<string> { "SPELL" }, new[] { "SPELL" });
        }

        [Test]
        public void ACollectionWhoseItemsNeedConvertingIsStillRefused()
        {
            // The row the rule deliberately leaves behind, and the reason it is drawn at the item
            // type: the interpreter turns each 1 into "1" through its own converter, and nothing
            // emitted here reproduces that. Guessing at a per-element conversion is how the two
            // backends drift, so this keeps falling back and the interpreter answers.
            AssertRefusedButInterpreted<List<int>>(
                "Tags", new List<int> { 1, 2 }, new[] { "1", "2" });
        }

        [Test]
        public void AnObjectTypedValueAgainstATypedMemberIsStillRefused()
        {
            // Unchanged and separately ruled: whether it fits depends on the runtime value, and the
            // interpreter converts where an emitted cast would only cast.
            AssertRefusedButInterpreted<object>("Number", 45, 45);
            AssertRefusedButInterpreted<object>("Name", "ok", "ok");
        }

        [Test]
        public void ARefusalNamesBothTypes()
        {
            var refusal = Assert.Throws<CompileErrorException>(
                () => Expression.ParseSetter<Target, string>("Number", EvaluationMode.MustCompile));

            Assert.That(refusal.Message, Does.Contain("System.String"));
            Assert.That(refusal.Message, Does.Contain("System.Int32"));
            Assert.That(refusal.Message, Does.Contain("Number"));
            Assert.That(refusal.Message, Does.Not.Contain("internal compiler error"));
        }

        private static void AssertCompilesAndLands<TValue>(
            string member, TValue value, object expected)
        {
            var compiledTarget = new Target();
            Expression.ParseSetter<Target, TValue>(member, EvaluationMode.MustCompile)
                .SetValue(compiledTarget, value);

            var interpretedTarget = new Target();
            Expression.ParseSetter<Target, TValue>(member, EvaluationMode.MustInterpret)
                .SetValue(interpretedTarget, value);

            var compiled = Read(compiledTarget, member);
            var interpreted = Read(interpretedTarget, member);

            Assert.AreEqual(expected, compiled, member + " compiled");
            Assert.AreEqual(expected, interpreted, member + " interpreted");
            Assert.AreEqual(
                compiled.GetType(), interpreted.GetType(), member + " runtime type");
        }

        private static void AssertRefusedButInterpreted<TValue>(
            string member, TValue value, object expected)
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseSetter<Target, TValue>(member, EvaluationMode.MustCompile),
                member + " <- " + typeof(TValue).Name);

            var target = new Target();
            Expression.ParseSetter<Target, TValue>(member).SetValue(target, value);

            Assert.AreEqual(expected, Read(target, member), member + " interpreted");
        }

        private static object Read(Target target, string member)
        {
            var property = typeof(Target).GetProperty(member);

            return property != null
                ? property.GetValue(target, null)
                : typeof(Target).GetField(member).GetValue(target);
        }
    }
}

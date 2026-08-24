using System;

using NUnit.Framework;

using SpringCore.TypeResolution;
using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Comparing an enum to a string, ruled: the string names a member, and == and != are exact
    /// negations of each other on both backends.
    /// </summary>
    /// <remarks>
    /// Three things were wrong before. The rule lived in OpEqual alone, so "Type == 'One'" answered true
    /// while "Type != 'One'" threw - the author's own note said as much ("bo not eq tego nie robi").
    /// The compiled path had no rule at all: EqualityHelper handed any pair with a string on either side
    /// to LExpression.Equal, which threw InvalidOperationException out of the emitter, past the
    /// compile-refusal convention, so the weak path could not fall back either. And Enum.Parse also
    /// accepts a numeric literal, which made "FooType.One == '0'" answer true - an accident of the
    /// parser, ruled out here: a string that does not name a member is an ArgumentException.
    /// </remarks>
    [TestFixture]
    public class EnumStringEqualityTests : BaseCompiledTests
    {
        [Flags]
        public enum Sides
        {
            None = 0,
            Left = 1,
            Right = 2,
            Both = Left | Right
        }

        public class SidesHolder
        {
            public Sides Side { get; set; } = Sides.Left;
        }

        [SetUp]
        public void RegisterTypes()
        {
            TypeRegistry.RegisterType("FooType", typeof(FooType));
        }

        [Test]
        public void AnEnumEqualsTheStringThatNamesIt()
        {
            var foo = new Foo(FooType.One);

            TestCompiledVsInterpreted<Foo, object>("Type == 'One'", foo).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Foo, object>("Type == 'Two'", foo).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Foo, object>("'One' == Type", foo).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Foo, object>("'Two' == Type", foo).ResultEqualsTo(false);
        }

        /// <summary>
        /// The half that did not exist: != is the exact negation of ==, in both operand orders.
        /// </summary>
        [Test]
        public void AnEnumIsUnequalToAStringThatNamesAnotherMember()
        {
            var foo = new Foo(FooType.One);

            TestCompiledVsInterpreted<Foo, object>("Type != 'One'", foo).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Foo, object>("Type != 'Two'", foo).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Foo, object>("'One' != Type", foo).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Foo, object>("'Two' != Type", foo).ResultEqualsTo(true);
        }

        /// <summary>
        /// A string that names no member is an error on both backends, at evaluation - the compiled path
        /// emits a call to the same method the interpreter runs, so the exception is the same one.
        /// Matching is case-sensitive, as it always was here.
        /// </summary>
        [Test]
        public void AStringThatNamesNoMemberIsAnError()
        {
            var foo = new Foo(FooType.One);

            foreach (var expression in new[]
            {
                "Type == 'Nonsense'", "Type != 'Nonsense'", "'Nonsense' == Type",
                "Type == 'one'", "Type == 'ONE'", "Type == ''"
            })
            {
                Assert.Throws<ArgumentException>(
                    () => CompileGetter<Foo, object>(expression).GetValue(foo), expression);
                Assert.Throws<ArgumentException>(
                    () => InterpretGetter<Foo, object>(expression).GetValue(foo), expression);
            }
        }

        /// <summary>
        /// The numeric form Enum.Parse accepts is gone: "FooType.One == '0'" used to be true interpreted,
        /// because FooType.One is 0 and Enum.Parse reads "0" as a value rather than a name.
        /// </summary>
        [Test]
        public void ANumericStringNoLongerMatchesByValue()
        {
            var foo = new Foo(FooType.One);

            Assert.Throws<ArgumentException>(() => CompileGetter<Foo, object>("Type == '0'").GetValue(foo));
            Assert.Throws<ArgumentException>(() => InterpretGetter<Foo, object>("Type == '0'").GetValue(foo));

            Assert.Throws<ArgumentException>(() => CompileGetter<Foo, object>("Type != '0'").GetValue(foo));
            Assert.Throws<ArgumentException>(() => InterpretGetter<Foo, object>("Type != '0'").GetValue(foo));
        }

        /// <summary>
        /// A comma-separated list of names stays legal, because that is how a [Flags] combination is
        /// written - only the numeric form was ruled out.
        /// </summary>
        [Test]
        public void AFlagsCombinationIsWrittenAsItsNames()
        {
            var holder = new SidesHolder { Side = Sides.Both };

            TestCompiledVsInterpreted<SidesHolder, object>("Side == 'Both'", holder).ResultEqualsTo(true);
            TestCompiledVsInterpreted<SidesHolder, object>("Side == 'Left, Right'", holder).ResultEqualsTo(true);
            TestCompiledVsInterpreted<SidesHolder, object>("Side != 'Left'", holder).ResultEqualsTo(true);
        }

        /// <summary>
        /// An enum against anything but the same enum or a member name has no compiled form. It used to
        /// answer false compiled - the boxing tail of EqualityHelper compares two boxed values of
        /// different types, which are never equal - while the interpreter refused the pair. Nobody chose
        /// that false; the interpreter's refusal is the answer, and the compiled path now says so.
        /// </summary>
        [Test]
        public void AnEnumAgainstANumberIsRefusedCompiledAndAnErrorInterpreted()
        {
            var foo = new Foo(FooType.One);

            Assert.Throws<CompileErrorException>(() => CompileGetter<Foo, object>("Type == 1"));
            Assert.Throws<ArgumentException>(() => InterpretGetter<Foo, object>("Type == 1").GetValue(foo));

            Assert.Throws<CompileErrorException>(() => CompileGetter<Foo, object>("Type != 1"));
            Assert.Throws<ArgumentException>(() => InterpretGetter<Foo, object>("Type != 1").GetValue(foo));
        }

        /// <summary>
        /// Only equality reads a string as a member name; the relational operators never did, and both
        /// backends still refuse the pair.
        /// </summary>
        [Test]
        public void RelationalOperatorsStillRefuseAnEnumAgainstAString()
        {
            var foo = new Foo(FooType.One);

            Assert.Throws<ArgumentException>(() => CompileGetter<Foo, object>("Type > 'One'").GetValue(foo));
            Assert.Throws<ArgumentException>(() => InterpretGetter<Foo, object>("Type > 'One'").GetValue(foo));
        }
    }
}

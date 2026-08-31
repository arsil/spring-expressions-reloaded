using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// A type's own arithmetic operators, honoured on both backends.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The engine had no notion of these. A type joined in arithmetic only by *converting* to a
    /// built-in number, so a decimal-like struct worked and everything else was refused compiled and
    /// threw interpreted - <c>TimeSpan</c> included, which is why the engine had no TimeSpan
    /// arithmetic at all. <c>TimeSpan</c> was never special; it is simply the operator-bearing struct
    /// in the BCL that people notice.
    /// </p>
    /// <p>
    /// <b>The lookup runs before the conversion path, which is C#'s order.</b> That matters for a type
    /// declaring both: it used to erase itself to the type it converts to. See
    /// <see cref="AConvertibleTypeWithOperatorsKeepsItsOwnType"/>.
    /// </p>
    /// <p>
    /// <b>Exact operand types only.</b> C#'s operator resolution is a chapter of the specification;
    /// none of it is here. A declared operator whose parameters do not match the operands exactly is
    /// not found, and both backends refuse together - see
    /// <see cref="AWidenedOperandIsNotAnExactMatchAndIsRefused"/>, which exists because the first
    /// attempt was not exact and the sweep caught it.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class UserDefinedOperatorTests : BaseCompiledTests
    {
        /// <summary>Operators, and no conversion to any built-in numeric type.</summary>
        public struct Money
        {
            public readonly decimal Amount;
            public Money(decimal amount) { Amount = amount; }

            public static Money operator +(Money a, Money b) { return new Money(a.Amount + b.Amount); }
            public static Money operator -(Money a, Money b) { return new Money(a.Amount - b.Amount); }
            public static Money operator *(Money a, int by) { return new Money(a.Amount * by); }
            public static Money operator /(Money a, int by) { return new Money(a.Amount / by); }
            public static Money operator %(Money a, Money b) { return new Money(a.Amount % b.Amount); }

            public override string ToString() { return "Money(" + Amount + ")"; }
            public override bool Equals(object o) { return o is Money m && m.Amount == Amount; }
            public override int GetHashCode() { return Amount.GetHashCode(); }
        }

        /// <summary>An implicit conversion to decimal <b>and</b> its own operators.</summary>
        public struct Convertible
        {
            public readonly decimal Amount;
            public Convertible(decimal amount) { Amount = amount; }

            public static implicit operator decimal(Convertible value) { return value.Amount; }
            public static Convertible operator +(Convertible a, Convertible b) { return new Convertible(a.Amount + b.Amount); }

            public override string ToString() { return "Convertible(" + Amount + ")"; }
            public override bool Equals(object o) { return o is Convertible c && c.Amount == Amount; }
            public override int GetHashCode() { return Amount.GetHashCode(); }
        }

        /// <summary>
        /// The full comparison set plus both unary operators, and no conversion. Kept apart from
        /// <see cref="Money"/> so the arithmetic pins above are provably untouched by stage 1.
        /// </summary>
        public struct Ordered
        {
            public readonly int Value;
            public Ordered(int value) { Value = value; }

            public static bool operator <(Ordered a, Ordered b) { return a.Value < b.Value; }
            public static bool operator >(Ordered a, Ordered b) { return a.Value > b.Value; }
            public static bool operator <=(Ordered a, Ordered b) { return a.Value <= b.Value; }
            public static bool operator >=(Ordered a, Ordered b) { return a.Value >= b.Value; }

            public static Ordered operator -(Ordered a) { return new Ordered(-a.Value); }
            public static Ordered operator +(Ordered a) { return new Ordered(a.Value + 1000); }

            public override string ToString() { return "Ordered(" + Value + ")"; }
            public override bool Equals(object o) { return o is Ordered p && p.Value == Value; }
            public override int GetHashCode() { return Value; }
        }

        /// <summary>
        /// A conversion to decimal <b>and</b> its own comparison and unary operators, deliberately
        /// answering something the converted decimal never would - so a pin can tell which path ran.
        /// </summary>
        public struct OrderedConvertible
        {
            public readonly decimal Amount;
            public OrderedConvertible(decimal amount) { Amount = amount; }

            public static implicit operator decimal(OrderedConvertible value) { return value.Amount; }

            // Always false, whichever way round - a decimal comparison could not answer this.
            public static bool operator <(OrderedConvertible a, OrderedConvertible b) { return false; }
            public static bool operator >(OrderedConvertible a, OrderedConvertible b) { return false; }

            // Multiplies rather than negates, for the same reason.
            public static OrderedConvertible operator -(OrderedConvertible a)
            {
                return new OrderedConvertible(a.Amount * 100);
            }

            public override string ToString() { return "OrderedConvertible(" + Amount + ")"; }
            public override bool Equals(object o) { return o is OrderedConvertible c && c.Amount == Amount; }
            public override int GetHashCode() { return Amount.GetHashCode(); }
        }

        /// <summary>
        /// <c>operator ==</c> disagreeing with <c>Equals</c> on purpose: the operator compares only the
        /// first character, <c>Equals</c> the whole tag. Whichever the engine consults is visible in
        /// the answer.
        /// </summary>
        public struct Tagged
        {
            public readonly string Tag;
            public Tagged(string tag) { Tag = tag; }

            public static bool operator ==(Tagged a, Tagged b) { return a.Tag[0] == b.Tag[0]; }
            public static bool operator !=(Tagged a, Tagged b) { return a.Tag[0] != b.Tag[0]; }

            public override string ToString() { return "Tagged(" + Tag + ")"; }
            public override bool Equals(object o) { return o is Tagged t && t.Tag == Tag; }
            public override int GetHashCode() { return Tag.GetHashCode(); }
        }

        /// <summary>No operators at all - the fallback stays the default comparer.</summary>
        public struct Plain
        {
            public readonly int Value;
            public Plain(int value) { Value = value; }

            public override string ToString() { return "Plain(" + Value + ")"; }
            public override bool Equals(object o) { return o is Plain p && p.Value == Value; }
            public override int GetHashCode() { return Value; }
        }

        /// <summary>An <c>operator ==</c> that answers an int rather than a bool.</summary>
        public struct OddEquals
        {
            public readonly int Value;
            public OddEquals(int value) { Value = value; }

            public static int operator ==(OddEquals a, OddEquals b) { return 1; }
            public static int operator !=(OddEquals a, OddEquals b) { return 0; }

            public override string ToString() { return "OddEquals(" + Value + ")"; }
            public override bool Equals(object o) { return o is OddEquals p && p.Value == Value; }
            public override int GetHashCode() { return Value; }
        }

        /// <summary>An <c>operator &lt;</c> that answers an int rather than a bool.</summary>
        public struct OddLess
        {
            public readonly int Value;
            public OddLess(int value) { Value = value; }

            public static int operator <(OddLess a, OddLess b) { return -1; }
            public static int operator >(OddLess a, OddLess b) { return 1; }

            public override string ToString() { return "OddLess(" + Value + ")"; }
            public override bool Equals(object o) { return o is OddLess p && p.Value == Value; }
            public override int GetHashCode() { return Value; }
        }

        public class Root
        {
            public Money Ten { get; set; } = new Money(10);
            public Money Three { get; set; } = new Money(3);
            public Convertible ConvTen { get; set; } = new Convertible(10);
            public Convertible ConvThree { get; set; } = new Convertible(3);

            public Ordered Five { get; set; } = new Ordered(5);
            public Ordered Two { get; set; } = new Ordered(2);

            public OrderedConvertible OrdConvOne { get; set; } = new OrderedConvertible(1);
            public OrderedConvertible OrdConvTwo { get; set; } = new OrderedConvertible(2);

            public OddLess OddOne { get; set; } = new OddLess(1);
            public OddLess OddTwo { get; set; } = new OddLess(2);

            public Tagged Apple { get; set; } = new Tagged("apple");
            public Tagged Avocado { get; set; } = new Tagged("avocado");
            public Tagged Banana { get; set; } = new Tagged("banana");

            public Plain PlainOne { get; set; } = new Plain(1);
            public Plain PlainOneAgain { get; set; } = new Plain(1);
            public Plain PlainTwo { get; set; } = new Plain(2);

            public OddEquals OddEqOne { get; set; } = new OddEquals(1);
            public OddEquals OddEqTwo { get; set; } = new OddEquals(2);

            public Guid GuidA { get; set; } = new Guid("11111111-1111-1111-1111-111111111111");
            public Guid GuidB { get; set; } = new Guid("22222222-2222-2222-2222-222222222222");

            public char LetterA { get; set; } = 'a';
            public char LetterB { get; set; } = 'b';
            public int? NullInt { get; set; } = null;

            public TimeSpan TwoDays { get; set; } = TimeSpan.FromDays(2);
            public TimeSpan SixHours { get; set; } = TimeSpan.FromHours(6);
            public DateTime When { get; set; } = new DateTime(2001, 1, 1);

            public decimal Decimal { get; set; } = 10m;
            public int Int { get; set; } = 3;
            public double Double { get; set; } = 2.0;
        }

        [Test]
        public void ATypeWithOperatorsAndNoConversionJoinsInArithmetic()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Ten + Three", root).ResultEqualsTo(new Money(13));
            TestCompiledVsInterpreted<Root, object>("Ten - Three", root).ResultEqualsTo(new Money(7));
            TestCompiledVsInterpreted<Root, object>("Ten * Int", root).ResultEqualsTo(new Money(30));
            TestCompiledVsInterpreted<Root, object>("Ten % Three", root).ResultEqualsTo(new Money(1));
        }

        /// <summary>
        /// TimeSpan is the same shape, which is the whole point: it needed no special case.
        /// </summary>
        /// <remarks>
        /// Only <c>+</c> and <c>-</c> are exercised here, because they are the only TimeSpan arithmetic
        /// operators that exist on every target framework. **.NET Framework's TimeSpan declares no
        /// op_Multiply and no op_Division at all** - measured by reflection on net472, where the
        /// operator list is addition, subtraction, the six comparisons and the two unary ones; .NET
        /// Core added multiplication and division later. So <c>TwoDays / SixHours</c> compiles on
        /// net8.0 and refuses on net472, and both are correct: the engine honours whatever the BCL of
        /// the running framework declares. That is a property of the feature worth having - it tracks
        /// the platform rather than a hard-coded list - but it makes TimeSpan a poor subject for a
        /// cross-TFM test, so <see cref="Money"/> carries the multiply and divide rows instead.
        /// </remarks>
        [Test]
        public void TimeSpanArithmeticIsJustAUserDefinedOperator()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("TwoDays + SixHours", root)
                .ResultEqualsTo(TimeSpan.FromHours(54));
            TestCompiledVsInterpreted<Root, object>("TwoDays - SixHours", root)
                .ResultEqualsTo(TimeSpan.FromHours(42));
        }

        /// <summary>
        /// The row the ordering ruling exists for. With the conversion consulted first, this answered
        /// a <c>decimal</c> - the type erased itself, silently, on both backends. Nothing in either
        /// suite declared such a type, which is why it was never caught.
        /// </summary>
        [Test]
        public void AConvertibleTypeWithOperatorsKeepsItsOwnType()
        {
            TestCompiledVsInterpreted<Root, object>("ConvTen + ConvThree", new Root())
                .ResultEqualsTo(new Convertible(13));
        }

        /// <summary>
        /// A type with a conversion and *no* operator for the pair still converts, exactly as before -
        /// which is what leaves <c>CustomRealTypesTests</c> untouched: MoneyLike and SpeedLike declare
        /// a conversion and no operators at all, so this lookup finds nothing for them.
        /// </summary>
        [Test]
        public void AConvertibleTypeWithoutAMatchingOperatorStillConverts()
        {
            // Convertible declares no operator-(Convertible, Convertible), so subtraction converts
            TestCompiledVsInterpreted<Root, object>("ConvTen - ConvThree", new Root())
                .ResultEqualsTo(7m);
        }

        /// <summary>
        /// Built-in numeric pairs never reach the lookup: the promotion rules keep that whole space,
        /// which is what keeps every existing numeric result exactly as it was. decimal declares
        /// op_Addition(decimal, decimal), so without the guard this would have started intercepting
        /// ordinary decimal arithmetic.
        /// </summary>
        [Test]
        public void BuiltInNumericPairsStayOnThePromotionPath()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Decimal + Int", root).ResultEqualsTo(13m);
            TestCompiledVsInterpreted<Root, object>("Int + Int", root).ResultEqualsTo(6);
            TestCompiledVsInterpreted<Root, object>("Decimal * Int", root).ResultEqualsTo(30m);
        }

        /// <summary>
        /// The lookup is exact, and this is the test that says so - written because the first attempt
        /// was not.
        /// </summary>
        /// <remarks>
        /// <c>Type.GetMethod</c> with a null binder uses <c>Type.DefaultBinder</c>, which widens: asking
        /// for <c>(TimeSpan, int)</c> hands back <c>op_Division(TimeSpan, double)</c>. Emitting that
        /// failed inside the LINQ factory - "the operands for operator 'Divide' do not match the
        /// parameters of method 'op_Division'" - and <c>CompilationNeverLeaksTests</c> caught it as a
        /// new kind of absorbed defect. Do not "fix" this by widening the operands without ruling on
        /// it: the interpreter would have to widen identically, and reflection's Invoke widens
        /// arguments silently.
        /// </remarks>
        [Test]
        public void AWidenedOperandIsNotAnExactMatchAndIsRefused()
        {
            // Money declares operator /(Money, int) and operator *(Money, int). A double operand is
            // not an int, and is not widened to reach one - so both backends refuse together.
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Root, object>("Ten / Double", EvaluationMode.MustCompile));

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<Root, object>("Ten / Double", EvaluationMode.MustInterpret)
                    .GetValue(new Root()));

            // spelled with the declared parameter type, it is exact and works
            TestCompiledVsInterpreted<Root, object>("Ten / Int", new Root())
                .ResultEqualsTo(new Money(10m / 3));
        }

        /// <summary>
        /// DateTime + TimeSpan needed no branch of its own in the end - it is a user-defined operator
        /// like any other, and the same lookup finds it.
        /// </summary>
        [Test]
        public void TheBclDateTimeOperatorsAreFoundTheSameWay()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("When + TwoDays", root)
                .ResultEqualsTo(new DateTime(2001, 1, 3));
            TestCompiledVsInterpreted<Root, object>("When - TwoDays", root)
                .ResultEqualsTo(new DateTime(2000, 12, 30));
        }

        // ===== stage 1: the four relational nodes and the two unary operators =====
        //
        // Every shape below threw on both backends before stage 1, which is why it went first: it can
        // only turn errors into answers. The four relational nodes each consult their own operator
        // (op_LessThan and friends) and return the bool it produces - no ordering is derived from two
        // booleans, so no operator the expression did not mention is ever invoked.

        [Test]
        public void ATypeWithComparisonOperatorsIsCompared()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Five > Two", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Five < Two", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("Five >= Two", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Five <= Two", root).ResultEqualsTo(false);

            TestCompiledVsInterpreted<Root, object>("Two < Five", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Two >= Five", root).ResultEqualsTo(false);
        }

        [Test]
        public void ATypeWithUnaryOperatorsIsNegatedAndPlussed()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("-Five", root).ResultEqualsTo(new Ordered(-5));

            // Ordered.operator+ adds 1000, so a pin can tell it ran rather than being a no-op.
            TestCompiledVsInterpreted<Root, object>("+Five", root).ResultEqualsTo(new Ordered(1005));
        }

        /// <summary>
        /// The operator runs before the conversion path, which is C#'s order and the rule the
        /// arithmetic half established. Both operators here answer something the converted decimal
        /// never would, so these pins fail if the conversion wins: <c>1 &lt; 2</c> would be true and
        /// <c>-1</c> would be minus one.
        /// </summary>
        [Test]
        public void AConvertibleTypeComparesAndNegatesByItsOwnOperators()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("OrdConvOne < OrdConvTwo", root)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("OrdConvTwo > OrdConvOne", root)
                .ResultEqualsTo(false);

            TestCompiledVsInterpreted<Root, object>("-OrdConvOne", root)
                .ResultEqualsTo(new OrderedConvertible(100));
        }

        /// <summary>
        /// A type declaring no <c>&lt;=</c> keeps the old behaviour for that operator while its
        /// <c>&lt;</c> works - the lookup is per operator, not per type.
        /// </summary>
        [Test]
        public void TheLookupIsPerOperatorNotPerType()
        {
            var root = new Root();

            // OrderedConvertible declares < and > but neither <= nor >=; those fall through to the
            // conversion path, which compares the decimals it converts to.
            TestCompiledVsInterpreted<Root, object>("OrdConvOne < OrdConvTwo", root)
                .ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("OrdConvOne <= OrdConvTwo", root)
                .ResultEqualsTo(true);
        }

        /// <summary>
        /// A relational operator must answer a <c>bool</c>. A type declaring one that answers an int
        /// is left to the existing paths - a comparison node has nowhere to put a non-boolean answer,
        /// and the lookup itself only rejects <c>void</c>, which is all arithmetic needs.
        /// </summary>
        /// <remarks>
        /// Measured: the existing path *compiles* this (it boxes and defers to the runtime comparer)
        /// and then fails at evaluation with "At least one object must implement IComparable", the
        /// same on both backends. That is exactly the behaviour before stage 1, which is the point of
        /// the pin - the operator lookup declines the type rather than changing anything about it.
        /// </remarks>
        [Test]
        public void ARelationalOperatorThatDoesNotAnswerABoolIsLeftAlone()
        {
            var compiled = Expression.ParseGetter<Root, object>(
                "OddOne < OddTwo", EvaluationMode.MustCompile);
            var interpreted = Expression.ParseGetter<Root, object>(
                "OddOne < OddTwo", EvaluationMode.MustInterpret);

            Assert.Throws<ArgumentException>(() => compiled.GetValue(new Root()));
            Assert.Throws<ArgumentException>(() => interpreted.GetValue(new Root()));
        }

        /// <summary>
        /// Built-in numerics never reach the lookup - <c>decimal</c> declares <c>op_LessThan</c> and
        /// <c>op_UnaryNegation</c>, so without IsOwnedByNumericPromotion this would quietly take
        /// ordinary comparison and negation off the promotion path.
        /// </summary>
        [Test]
        public void BuiltInNumericComparisonAndNegationStayOnThePromotionPath()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Decimal > Int", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Int > Double", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Decimal <= Decimal", root).ResultEqualsTo(true);

            TestCompiledVsInterpreted<Root, object>("-Decimal", root).ResultEqualsTo(-10m);
            TestCompiledVsInterpreted<Root, object>("-Int", root).ResultEqualsTo(-3);
            TestCompiledVsInterpreted<Root, object>("+Int", root).ResultEqualsTo(3);

            // and the shapes that were never numeric comparison in the first place
            TestCompiledVsInterpreted<Root, object>("LetterB > LetterA", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("NullInt > 5", root).ResultEqualsTo(false);
        }

        // ===== equality: one rule replacing three special cases =====
        //
        // The engine already honoured a type's operator for numerics, string and DateTime - three
        // hand-written branches in EqualityHelper routed to LExpression.Equal, which resolves a
        // declared op_Equality. Every other same-typed pair fell to EqualityComparer<T>.Default, so a
        // type's own operator was never called. The same-type branch consults op_Equality first now,
        // with EqualityComparer as the fallback, and EqualityUtils.CreateMethod is the interpreter's
        // twin - one rule, two backends.

        /// <summary>
        /// The operator compares first characters, <c>Equals</c> compares whole tags. Before, both
        /// backends answered by <c>Equals</c>; now both answer by the operator, so 'apple' and
        /// 'avocado' are equal and 'apple' and 'banana' are not.
        /// </summary>
        [Test]
        public void ATypeWithAnEqualityOperatorIsComparedByIt()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Apple == Avocado", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Apple == Banana", root).ResultEqualsTo(false);
        }

        /// <summary>
        /// <c>!=</c> stays the exact negation of <c>==</c> - the engine's standing rule, which the
        /// enum-name ruling insisted on - so a type's own <c>op_Inequality</c> is deliberately not
        /// looked up. Two operators that disagree with each other have no coherent reading here.
        /// </summary>
        [Test]
        public void InequalityIsTheNegationOfEqualityNotItsOwnOperator()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Apple != Avocado", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("Apple != Banana", root).ResultEqualsTo(true);
        }

        /// <summary>
        /// A type declaring no equality operator keeps the default comparer, which is what every type
        /// used before.
        /// </summary>
        [Test]
        public void ATypeWithoutAnEqualityOperatorKeepsTheDefaultComparer()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("PlainOne == PlainOneAgain", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("PlainOne == PlainTwo", root).ResultEqualsTo(false);
        }

        /// <summary>
        /// An equality operator must answer a <c>bool</c>, exactly as a relational one must. A type
        /// declaring one that answers an int keeps the default comparer.
        /// </summary>
        [Test]
        public void AnEqualityOperatorThatDoesNotAnswerABoolIsLeftAlone()
        {
            var root = new Root();

            // OddEquals.operator== always answers 1; if it were consulted these would both be true.
            TestCompiledVsInterpreted<Root, object>("OddEqOne == OddEqTwo", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("OddEqOne == OddEqOne", root).ResultEqualsTo(true);
        }

        /// <summary>
        /// A BCL struct that declares <c>op_Equality</c> and used to reach the default comparer
        /// instead. Its operator and its <c>Equals</c> agree, so the answer does not move - which is
        /// the point: the rule reroutes these without changing them.
        /// </summary>
        [Test]
        public void BclTypesThatDeclareItAnswerTheSameEitherWay()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("GuidA == GuidA", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("GuidA == GuidB", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("TwoDays == TwoDays", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("TwoDays == SixHours", root).ResultEqualsTo(false);
            TestCompiledVsInterpreted<Root, object>("When == When", root).ResultEqualsTo(true);
        }

        /// <summary>
        /// Built-in numerics never reach the lookup, so <c>double</c>/<c>float</c> keep whatever they
        /// did - which matters, because <c>NaN</c> is the one BCL value where a type's own operator and
        /// its <c>Equals</c> disagree, and that divergence is its own item rather than this one's.
        /// </summary>
        [Test]
        public void BuiltInNumericEqualityStaysOnThePromotionPath()
        {
            var root = new Root();

            TestCompiledVsInterpreted<Root, object>("Decimal == Decimal", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Int == Int", root).ResultEqualsTo(true);
            TestCompiledVsInterpreted<Root, object>("Decimal == Int", root).ResultEqualsTo(false);
        }

        /// <summary>
        /// Not stage 1, and pinned so the boundary is visible rather than assumed: <c>between</c>,
        /// <c>min()</c> and <c>max()</c> go through CompareUtils.Compare, which needs an int ordering
        /// where an operator yields a bool. Deriving one would invoke an operator the expression never
        /// mentioned, so it is a ruling of its own - see open-issues item 12.
        /// </summary>
        [Test]
        public void TheAggregatorsStillRefuseAndThatIsDeliberate()
        {
            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<Root, object>(
                    "Five between {Two, Five}", EvaluationMode.MustInterpret).GetValue(new Root()));

            Assert.Throws<ArgumentException>(
                () => Expression.ParseGetter<Root, object>(
                    "{Two, Five}.min()", EvaluationMode.MustInterpret).GetValue(new Root()));
        }
    }
}

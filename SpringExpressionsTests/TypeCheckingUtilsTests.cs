using System;

using NUnit.Framework;

namespace SpringUtil
{
    /// <summary>
    /// A caller's own real-valued struct: not float, double or decimal by name, but implicitly
    /// convertible to decimal.
    /// </summary>
    public struct ImplicitlyDecimal
    {
        private readonly decimal _value;

        public ImplicitlyDecimal(decimal value) { _value = value; }

        public static implicit operator decimal(ImplicitlyDecimal value) { return value._value; }
    }

    public struct ImplicitlyDouble
    {
        private readonly double _value;

        public ImplicitlyDouble(double value) { _value = value; }

        public static implicit operator double(ImplicitlyDouble value) { return value._value; }
    }

    /// <summary>
    /// Convertible to decimal only explicitly: the caller must write the cast, so the engine must not
    /// treat the type as real-valued on its own.
    /// </summary>
    public struct ExplicitlyDecimal
    {
        private readonly decimal _value;

        public ExplicitlyDecimal(decimal value) { _value = value; }

        public static explicit operator decimal(ExplicitlyDecimal value) { return value._value; }
    }

    public struct ImplicitlyInt
    {
        private readonly int _value;

        public ImplicitlyInt(int value) { _value = value; }

        public static implicit operator int(ImplicitlyInt value) { return value._value; }
    }

    public class ImplicitlyDecimalClass
    {
        public static implicit operator decimal(ImplicitlyDecimalClass value) { return 0m; }
    }

    public struct ImplicitlyDecimalAndDouble
    {
        public static implicit operator double(ImplicitlyDecimalAndDouble value) { return 0d; }
        public static implicit operator decimal(ImplicitlyDecimalAndDouble value) { return 0m; }
    }

    [TestFixture]
    public class TypeCheckingUtilsTests
    {
        private enum SomeEnum { One }

        [Test]
        public void IsRealTypeAcceptsTheBuiltInRealTypes()
        {
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(float)));
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(double)));
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(decimal)));

            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(float?)));
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(double?)));
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(decimal?)));
        }

        [Test]
        public void IsRealTypeRejectsIntegralAndUnrelatedTypes()
        {
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(int)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(long)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(int?)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(char)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(bool)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(string)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(object)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(SomeEnum)));
        }

        /// <summary>
        /// The catalog of real-valued types is open: a user type with an implicit conversion to a
        /// built-in real type is real-valued too, struct or class.
        /// </summary>
        [Test]
        public void IsRealTypeAcceptsUserTypesImplicitlyConvertibleToARealType()
        {
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(ImplicitlyDecimal)));
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(ImplicitlyDouble)));
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(ImplicitlyDecimalClass)));

            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(ImplicitlyDecimal?)));
        }

        /// <summary>
        /// An explicit conversion is the caller's to write, and an implicit conversion to an integral
        /// type does not make a type real-valued.
        /// </summary>
        [Test]
        public void IsRealTypeRejectsExplicitOnlyAndIntegralConversions()
        {
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(ExplicitlyDecimal)));
            Assert.IsFalse(TypeCheckingUtils.IsRealType(typeof(ImplicitlyInt)));
        }

        [Test]
        public void IsIntegralKindAcceptsIntegralsCharsAndEnums()
        {
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(sbyte)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(byte)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(short)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(ushort)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(int)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(uint)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(long)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(ulong)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(char)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(SomeEnum)));

            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(int?)));
            Assert.IsTrue(TypeCheckingUtils.IsIntegralKind(typeof(SomeEnum?)));
        }

        [Test]
        public void IsIntegralKindRejectsRealsAndUnrelatedTypes()
        {
            Assert.IsFalse(TypeCheckingUtils.IsIntegralKind(typeof(float)));
            Assert.IsFalse(TypeCheckingUtils.IsIntegralKind(typeof(double)));
            Assert.IsFalse(TypeCheckingUtils.IsIntegralKind(typeof(decimal)));
            Assert.IsFalse(TypeCheckingUtils.IsIntegralKind(typeof(bool)));
            Assert.IsFalse(TypeCheckingUtils.IsIntegralKind(typeof(string)));
            Assert.IsFalse(TypeCheckingUtils.IsIntegralKind(typeof(object)));
        }

        [Test]
        public void IsIntegerOnValuesChecksTheBoxedRuntimeType()
        {
            Assert.IsTrue(TypeCheckingUtils.IsInteger((object)1));
            Assert.IsTrue(TypeCheckingUtils.IsInteger((object)1UL));
            Assert.IsTrue(TypeCheckingUtils.IsInteger((object)(int?)1));

            Assert.IsFalse(TypeCheckingUtils.IsInteger((object)1.5));
            Assert.IsFalse(TypeCheckingUtils.IsInteger((object)1.5m));
            Assert.IsFalse(TypeCheckingUtils.IsInteger((object)SomeEnum.One));
            Assert.IsFalse(TypeCheckingUtils.IsInteger((object)null));
        }

        [Test]
        public void IsNumberAcceptsTheBclNumerics()
        {
            Assert.IsTrue(TypeCheckingUtils.IsNumber(1));
            Assert.IsTrue(TypeCheckingUtils.IsNumber(1.5f));
            Assert.IsTrue(TypeCheckingUtils.IsNumber(1.5));
            Assert.IsTrue(TypeCheckingUtils.IsNumber(1.5m));

            Assert.IsFalse(TypeCheckingUtils.IsNumber("1"));
            Assert.IsFalse(TypeCheckingUtils.IsNumber(true));
            Assert.IsFalse(TypeCheckingUtils.IsNumber(null));
        }

        /// <summary>
        /// IsNumber sees implicit conversion operators: a struct whose claim to numberhood is an
        /// implicit operator to decimal is a number, agreeing with the Type-taking IsRealType. Its
        /// TypeDescriptor branch alone was blind to operators, which once made these two predicates
        /// disagree about the same type.
        /// </summary>
        [Test]
        public void IsNumberSeesImplicitConversionOperators()
        {
            Assert.IsTrue(TypeCheckingUtils.IsNumber(new ImplicitlyDecimal(1m)));
            Assert.IsTrue(TypeCheckingUtils.IsRealType(typeof(ImplicitlyDecimal)));

            Assert.IsFalse(TypeCheckingUtils.IsNumber(new ExplicitlyDecimal(1m)));
        }

        /// <summary>
        /// The conversion finder prefers decimal over double over float when a type offers several,
        /// and does not count explicit operators.
        /// </summary>
        [Test]
        public void TryGetImplicitRealConversionPrefersDecimal()
        {
            Assert.IsTrue(TypeCheckingUtils.TryGetImplicitRealConversion(
                typeof(ImplicitlyDecimalAndDouble), out var conversion));
            Assert.AreEqual(typeof(decimal), conversion.ReturnType);

            Assert.IsFalse(TypeCheckingUtils.TryGetImplicitRealConversion(typeof(ExplicitlyDecimal), out _));
            Assert.IsFalse(TypeCheckingUtils.TryGetImplicitRealConversion(typeof(decimal), out _));
        }

        [Test]
        public void IsIntegerAcceptsExactlyTheEightIntegralPrimitives()
        {
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(sbyte)));
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(byte)));
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(short)));
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(ushort)));
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(int)));
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(uint)));
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(long)));
            Assert.IsTrue(TypeCheckingUtils.IsInteger(typeof(ulong)));

            Assert.IsFalse(TypeCheckingUtils.IsInteger(typeof(float)));
            Assert.IsFalse(TypeCheckingUtils.IsInteger(typeof(char)));
            Assert.IsFalse(TypeCheckingUtils.IsInteger(typeof(SomeEnum)));

            // Pins the behavior this predicate has always had: it does not unwrap nullables.
            Assert.IsFalse(TypeCheckingUtils.IsInteger(typeof(int?)));
        }

        /// <summary>
        /// C#'s implicit numeric conversion table, spot-checked from every family: widening rows
        /// answer true, every narrowing or lossy row answers false, and enums and nullables are not
        /// numeric conversions at all.
        /// </summary>
        [Test]
        public void CSharpImplicitNumericConversionFollowsTheSpecTable()
        {
            Assert.IsTrue(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int), typeof(long)));
            Assert.IsTrue(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int), typeof(double)));
            Assert.IsTrue(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int), typeof(decimal)));
            Assert.IsTrue(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(byte), typeof(ulong)));
            Assert.IsTrue(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(char), typeof(int)));
            Assert.IsTrue(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(float), typeof(double)));
            Assert.IsTrue(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(ulong), typeof(decimal)));

            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(long), typeof(int)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(double), typeof(float)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(double), typeof(decimal)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(decimal), typeof(double)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int), typeof(uint)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int), typeof(char)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(SomeEnum), typeof(int)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int?), typeof(long)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int), typeof(string)));
            Assert.IsFalse(TypeCheckingUtils.IsCSharpImplicitNumericConversion(typeof(int), typeof(object)));
        }

        /// <summary>
        /// The widening tier's applicability predicate: the numeric table, plus a custom real-valued
        /// type reaching a target through its own implicit operator first - and nothing else.
        /// Identity and reference assignability are deliberately excluded; those belong to the
        /// legacy tier.
        /// </summary>
        [Test]
        public void HasImplicitWideningConversionSeesCustomRealsThroughTheirOperator()
        {
            Assert.IsTrue(TypeCheckingUtils.HasImplicitWideningConversion(typeof(int), typeof(double)));

            // ImplicitlyDecimal reaches decimal directly; double would need decimal-to-double, which
            // is not implicit, so it stays out - exactly why DblOrDec(customReal) picks decimal.
            Assert.IsTrue(TypeCheckingUtils.HasImplicitWideningConversion(typeof(ImplicitlyDecimal), typeof(decimal)));
            Assert.IsFalse(TypeCheckingUtils.HasImplicitWideningConversion(typeof(ImplicitlyDecimal), typeof(double)));

            // ImplicitlyDouble reaches double directly and float never (double-to-float narrows).
            Assert.IsTrue(TypeCheckingUtils.HasImplicitWideningConversion(typeof(ImplicitlyDouble), typeof(double)));
            Assert.IsFalse(TypeCheckingUtils.HasImplicitWideningConversion(typeof(ImplicitlyDouble), typeof(float)));

            Assert.IsFalse(TypeCheckingUtils.HasImplicitWideningConversion(typeof(ExplicitlyDecimal), typeof(decimal)));
            Assert.IsFalse(TypeCheckingUtils.HasImplicitWideningConversion(typeof(int), typeof(int)));
            Assert.IsFalse(TypeCheckingUtils.HasImplicitWideningConversion(typeof(string), typeof(object)));
            Assert.IsFalse(TypeCheckingUtils.HasImplicitWideningConversion(typeof(int?), typeof(long)));
            Assert.IsFalse(TypeCheckingUtils.HasImplicitWideningConversion(typeof(int), typeof(long?)));
        }

        /// <summary>
        /// C#'s betterness over conversion targets: a target that converts implicitly into its rival
        /// wins - by the numeric table or by reference/boxing assignability, so a derived class beats
        /// its base and any class beats object. Where neither direction converts - double against
        /// decimal, string against Uri - neither is better, which is the CS0121 ambiguity the
        /// resolution tiers refuse.
        /// </summary>
        [Test]
        public void BetternessRanksTargetsExactlyWhereCSharpDoes()
        {
            Assert.IsTrue(TypeCheckingUtils.IsBetterConversionTarget(typeof(long), typeof(double)));
            Assert.IsFalse(TypeCheckingUtils.IsBetterConversionTarget(typeof(double), typeof(long)));

            Assert.IsFalse(TypeCheckingUtils.IsBetterConversionTarget(typeof(double), typeof(decimal)));
            Assert.IsFalse(TypeCheckingUtils.IsBetterConversionTarget(typeof(decimal), typeof(double)));

            // reference targets: more specific wins, unrelated ties, equal is never better
            Assert.IsTrue(TypeCheckingUtils.IsBetterConversionTarget(typeof(string), typeof(object)));
            Assert.IsFalse(TypeCheckingUtils.IsBetterConversionTarget(typeof(object), typeof(string)));
            Assert.IsTrue(TypeCheckingUtils.IsBetterConversionTarget(typeof(ArgumentException), typeof(Exception)));
            Assert.IsFalse(TypeCheckingUtils.IsBetterConversionTarget(typeof(string), typeof(Uri)));
            Assert.IsFalse(TypeCheckingUtils.IsBetterConversionTarget(typeof(Uri), typeof(string)));
            Assert.IsFalse(TypeCheckingUtils.IsBetterConversionTarget(typeof(string), typeof(string)));

            // boxing counts as an implicit conversion: a value type beats object
            Assert.IsTrue(TypeCheckingUtils.IsBetterConversionTarget(typeof(int), typeof(object)));

            Assert.AreEqual(0, TypeCheckingUtils.IndexOfUniqueBestParameterSet(
                new[] { new[] { typeof(long) }, new[] { typeof(double) } }));

            Assert.AreEqual(-1, TypeCheckingUtils.IndexOfUniqueBestParameterSet(
                new[] { new[] { typeof(double) }, new[] { typeof(decimal) } }));

            // Multi-argument betterness: every position at least as good, one strictly better.
            Assert.AreEqual(1, TypeCheckingUtils.IndexOfUniqueBestParameterSet(
                new[]
                {
                    new[] { typeof(double), typeof(string) },
                    new[] { typeof(long), typeof(string) },
                }));

            // Split verdicts across positions: neither candidate beats the other.
            Assert.AreEqual(-1, TypeCheckingUtils.IndexOfUniqueBestParameterSet(
                new[]
                {
                    new[] { typeof(long), typeof(double) },
                    new[] { typeof(double), typeof(long) },
                }));
        }
    }
}

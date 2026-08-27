using NUnit.Framework;
using System;

using System.Collections.Generic;
using System.Dynamic;
using System.IO;

using System.Text;
using SpringCore;
using SpringCore.TypeResolution;
using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;
using SpringExpressions.Parser.antlr;

using Expression = SpringExpressions.Expression;
using System.Collections;
using System.Text.RegularExpressions;

namespace SpringExpressionsTests.Expressions
{
    [TestFixture]
    public sealed class CompiledExpressionTests : BaseCompiledTests
    {
        [SetUp]
        public void SetUp()
        {
            TypeRegistry.RegisterType("Society", typeof(Society));
        }

        [Test]
        public void MyEnumComplexTest()
        {
            Assert.AreEqual(1024, CompileAndExecuteGetter<int>("2 ^ ({2,5,10}[2])"));

            Assert.AreEqual(8, CompileAndExecuteGetter<int>(
                "2 ^ ( {0, 1, 2, 3, 4, 5, 6} [ T(System.Convert).ToInt32(date('2024/06/05').DayOfWeek)] )"));
        }

        [Test]
        public void TestConstantRead()
        {
            var expr = CompileGetter<bool>("Society.ByteConst == 1");
            Assert.AreEqual(true, expr.GetValue());
        }

        [Test]
        public void TestMixedAddition()
        {
            var expr = CompileGetter<string>("'123' + 1");
            Assert.AreEqual("1231", expr.GetValue());
        }

        [Test(Description = "SPRNET-1507 - Test 1")]
        public void TestExpandoObject()
        {
            dynamic dynamicObject = new ExpandoObject();
            //add property at run-time
            dynamicObject.IssueId = "1507";

            var interpreted = Expression.ParseGetter<ExpandoObject, object>(
                "IssueId", EvaluationMode.MustInterpret);
            Assert.AreEqual("1507", interpreted.GetValue(dynamicObject));

            var expr = CompileGetter<ExpandoObject, object>("IssueId");
            Assert.AreEqual("1507", expr.GetValue(dynamicObject));
        }

        [Test(Description = "SPRNET-1507 - Test 2")]
        public void TestExpandoObjectWithNotExistedProperty()
        {
            try
            {
                dynamic dynamicObject = new ExpandoObject();
                CompileGetter<ExpandoObject, object>("PropertyName").GetValue(dynamicObject);

                Assert.Fail();
            }
            catch (InvalidPropertyException ex)
            {
                Assert.AreEqual(
                    "'PropertyName' node cannot be resolved for the specified context [System.Dynamic.ExpandoObject].",
                    ex.Message);
            }
        }

        [Test(Description = "SPRNET-944")]
        public void DateTests()
        {
            var expr = CompileGetter<string>("'date'");
            Assert.AreEqual("date", expr.GetValue());
        }

        // https://github.com/spring-projects/spring-net/blob/main/changelog.txt
        [Test(Description = "http://jira.springframework.org/browse/SPRNET-944")]
        public void TestDateVariableExpression()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["date"] = "2008-05-15";
            var expr = CompileGetter<string>("#date as T(string)");
            Assert.That(expr.GetValue(vars), Is.EqualTo("2008-05-15"));

            // the same cast in the bare type spelling
            var bareExpr = CompileGetter<string>("#date as string");
            Assert.That(bareExpr.GetValue(vars), Is.EqualTo("2008-05-15"));
        }

        // https://github.com/spring-projects/spring-net/blob/main/changelog.txt
        [Test(Description = "http://jira.springframework.org/browse/SPRNET-1155")]
        public void TestDateVariableExpressionCamelCased()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["Date"] = "2008-05-15";
            var expr = CompileGetter<string>("#Date as T(string)");
            Assert.That(expr.GetValue(vars), Is.EqualTo("2008-05-15"));

            // the same cast in the bare type spelling
            var bareExpr = CompileGetter<string>("#Date as string");
            Assert.That(bareExpr.GetValue(vars), Is.EqualTo("2008-05-15"));
        }

        [Test]
        public void ThrowsSyntaxErrorException()
        {
            try
            {
                Expression.ParseGetter<object>("'date");// unclose string literal
                Assert.Fail();
            }
            catch (RecognitionException ex)
            {
                Assert.AreEqual("Syntax Error on line 1, column 6: expecting ''', found '<EOF>' in expression" + Environment.NewLine + "''date'", ex.Message);
            }
        }

        // todo: error: fixme - compilation or execution error? two cases?
        /*
        /// <summary>
        /// Should throw exception for null root object
        /// </summary>
        [Test]
        public void NullRoot()
        {
            // ?
            var expr = CompileGetter<object, object>()
            Assert.Throws<NullValueInNestedPathException>(
                () => ExpressionEvaluator.GetValue(null, "dummy.expression"));
        }

        /// <summary>
        /// Should throw exception for null root object
        /// </summary>
        [Test]
        public void TryingToSetTheValueOfNonSettableNode()
        {
            Assert.Throws<NotSupportedException>(() => ExpressionEvaluator.SetValue(null, "10", 5));
        }
        */

        // todo: error: illegal?
        /*
        /// <summary>
        /// Should return root itself for empty expression
        /// </summary>
        [Test]
        public void GetNullOrEmptyExpression()
        {
            DateTime now = DateTime.Now;
            Assert.AreEqual(ExpressionEvaluator.GetValue(now, null), now);
            Assert.AreEqual(ExpressionEvaluator.GetValue(now, ""), now);
        }
        */

        /*
        /// <summary>
        /// Should fail when setting value for the empty expression
        /// </summary>
        [Test]
        public void SetNullOrEmptyExpression()
        {
            Assert.Throws<NotSupportedException>(
                () => ExpressionEvaluator.SetValue("xyz", null, "abc"));
        }
        */


        /// <summary>
        /// Tests null literal.
        /// </summary>
        [Test]
        public void TestNullLiteral()
        {
            Assert.IsNull(CompileGetter<object>("null").GetValue());
            Assert.IsNull(CompileGetter<object, object>("null").GetValue(null));

            Assert.IsFalse(CompileGetter<bool>("'xyz' == null").GetValue());
            Assert.IsFalse(CompileGetter<object, bool>("'xyz' == null").GetValue(null));

            Assert.IsTrue(CompileGetter<bool>("null != 'xyz'").GetValue());
            Assert.IsTrue(CompileGetter<object, bool>("null != 'xyz'").GetValue(null));
        }

        [Test]
        public void TestUnicode()
        {
            Assert.AreEqual("\u6f22\u5b57", CompileGetter<string>("'\u6f22\u5b57'").GetValue());
        }
        /// <summary>
        /// Tests string literals.
        /// </summary>
        [Test]
        public void TestStringLiterals()
        {
            Assert.AreEqual("literal string", CompileGetter<string>("'literal string'").GetValue());
            Assert.AreEqual("literal 'string", CompileGetter<string>("'literal ''string'").GetValue());
            Assert.AreEqual(string.Empty, CompileGetter<string>("''").GetValue());
            Assert.AreEqual("escaped \t string \n", CompileGetter<string>("'escaped \t string \n'").GetValue());
        }

        /// <summary>
        /// Tests integer literals.
        /// </summary>
        [Test]
        public void TestIntLiterals()
        {
            var int32 = CompileGetter<int>(int.MaxValue.ToString()).GetValue();
            Assert.AreEqual(int32, int.MaxValue);

            Assert.AreEqual(32, CompileGetter<int>("0x20").GetValue());

            Assert.AreEqual(long.MaxValue.ToString(), 
                CompileGetter<string>(long.MaxValue.ToString() + ".ToString()").GetValue());

            Assert.AreEqual(long.MaxValue.ToString(), ExpressionEvaluator.GetValue(null, "long.MaxValue.ToString()"));

            var int64 = CompileGetter<long>(long.MaxValue.ToString()).GetValue();
            Assert.AreEqual(int64, long.MaxValue);
        }

        /// <summary>
        /// Tests hexadecimal integer literals.
        /// </summary>
        [Test]
        public void TestHexLiterals()
        {
            var exp = CompileGetter<int>("0x20");
            Assert.AreEqual(32, exp.GetValue());
            Assert.AreEqual(32, exp.GetValue());


            Assert.AreEqual(255, CompileGetter<int>("0xFF").GetValue());
// todo: error: fixme? ------- should convert if possible!
//            Assert.AreEqual(255, CompileGetter<long>("0xFF").GetValue());

            Assert.AreEqual(typeof(int), CompileGetter<object>("0xFF").GetValue().GetType());

            Assert.AreEqual(int.MaxValue, CompileGetter<int>("0x7FFFFFFF").GetValue());
            Assert.AreEqual(int.MinValue, CompileGetter<int>("0x80000000").GetValue());

            Assert.AreEqual(long.MaxValue, CompileGetter<long>("0x7FFFFFFFFFFFFFFF").GetValue());
            Assert.AreEqual(long.MinValue, CompileGetter<long>("0x8000000000000000").GetValue());
        }

        /// <summary>
        /// Tests real literals.
        /// </summary>
        [Test]
        public void TestRealLiterals()
        {
            var exp = CompileGetter<object>("3.402823E+38");
            exp.GetValue();
            var s = exp.GetValue();
            var d = CompileGetter<object>("1.797693E+308").GetValue();
            var dec = CompileGetter<object>("1000.00m").GetValue();

            Assert.IsTrue(s is double);
            Assert.IsTrue(d is double);
            Assert.IsTrue(dec is decimal);

            Assert.AreEqual(3.402823E+38, s);
            Assert.AreEqual(1.797693E+308, d);
            Assert.AreEqual(1000m, dec);

            Assert.AreEqual(3.402823E+38, CompileGetter<double>("3.402823E+38").GetValue());
            Assert.AreEqual(1.797693E+308, CompileGetter<double>("1.797693E+308").GetValue());
            Assert.AreEqual(1000m, CompileGetter<decimal>("1000.00m").GetValue());


            Assert.AreEqual(5.25F, CompileGetter<object>("5.25f").GetValue());
            Assert.AreEqual(typeof(float), CompileGetter<object>("5.25f").GetValue().GetType());
            Assert.AreEqual(5.25F, CompileGetter<float>("5.25f").GetValue());

            Assert.AreEqual(0.75d, CompileGetter<object>("0.75D").GetValue());
            Assert.AreEqual(typeof(double), CompileGetter<object>("0.75D").GetValue().GetType());
            Assert.AreEqual(0.75d, CompileGetter<double>("0.75D").GetValue());

            Assert.IsTrue(CompileGetter<bool>("1000 == 1e3 and 1e+4 != 1000").GetValue());
            Assert.IsTrue(CompileGetter<bool>("100 < 1000.00m and 10000.00 > 1000").GetValue());
            Assert.IsTrue(CompileGetter<bool>("100 < 1000.00 and 10000.00m > 1e2m").GetValue());

            var dec2 = CompileGetter<object>("1e2m").GetValue();
            Assert.IsTrue(dec2 is decimal);
            Assert.AreEqual(100m, dec2);

            TestCompiledVsInterpreted<decimal>("1e2m").ResultEqualsTo(100m);

            // todo: error: type casting tryouts!
            //TestCompiledVsInterpreted<decimal>("to<decimal>(1e2)").ResultEqualsTo(100m);
            //TestCompiledVsInterpreted<decimal>("to[decimal].from(1e2)").ResultEqualsTo(100m);
            //TestCompiledVsInterpreted<decimal>("from(1e2).to(decimal").ResultEqualsTo(100m);

            //TestCompiledVsInterpreted<decimal>("Cast(1e2).ToDecimal()").ResultEqualsTo(100m);
            //TestCompiledVsInterpreted<decimal>("1e2.To(decimal)").ResultEqualsTo(100m);

            // local function node
            //            TestCompiledVsInterpreted<decimal>("$Cast(1e2).ToDecimal()").ResultEqualsTo(100m);
            // function node
            //TestCompiledVsInterpreted<decimal>("#Cast(1e2).ToDecimal()").ResultEqualsTo(100m);


            //TestCompiledVsInterpreted<decimal>("_Cast(1e2).ToDecimal()").ResultEqualsTo(100m);
            //TestCompiledVsInterpreted<decimal>("to{decimal}(1e2)").ResultEqualsTo(100m);
            //TestCompiledVsInterpreted<decimal>("cast(1e2, T(decimal))").ResultEqualsTo(100m);
            TestCompiledVsInterpreted<decimal>("1e2 as T(decimal)").ResultEqualsTo(100m);
            TestCompiledVsInterpreted<decimal>("1e2 as decimal").ResultEqualsTo(100m);
            // the prefix position of the same cast - the "to<decimal>(1e2)" wish above, granted under 'as'
            TestCompiledVsInterpreted<decimal>("as<decimal>(1e2)").ResultEqualsTo(100m);
            //TestCompiledVsInterpreted<decimal>("{decimal}1e2").ResultEqualsTo(100m);
            //TestCompiledVsInterpreted<decimal>("{decimal, 1e2}").ResultEqualsTo(100m);

        }

        /// <summary>
        /// Tests boolean literals.
        /// </summary>
        [Test]
        public void TestBooleanLiterals()
        {
            Assert.AreEqual(typeof(bool), CompileGetter<object>("true").GetValue().GetType());
            Assert.AreEqual(typeof(bool), CompileGetter<object>("false").GetValue().GetType());

            Assert.IsTrue(CompileGetter<bool>("true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false").GetValue());
        }

        /// <summary>
        /// Tests date literals.
        /// </summary>
        [Test]
        public void TestDateLiterals()
        {
            var exp = CompileGetter<DateTime>("date('1974/08/24')");
            Assert.AreEqual(new DateTime(1974, 8, 24), exp.GetValue());
            Assert.AreEqual(new DateTime(1974, 8, 24), exp.GetValue());

            Assert.AreEqual(new DateTime(1974, 8, 24), CompileAndExecuteGetter<DateTime>("date('1974-08-24')"));
            Assert.AreEqual(new DateTime(1974, 8, 24), CompileAndExecuteGetter<DateTime>("date('08-24-1974', 'MM-dd-yyyy')"));
            Assert.AreEqual(new DateTime(1974, 8, 24), CompileAndExecuteGetter<DateTime>("date('08/24/1974', 'MM/dd/yyyy')"));
            Assert.AreEqual(new DateTime(1974, 8, 24, 12, 35, 6),
                CompileAndExecuteGetter<DateTime>("date('1974-08-24 12:35:06Z', 'u')"));
            Assert.AreEqual(typeof(DateTime), CompileAndExecuteGetter<object>("date('1974-08-24')").GetType());

            Assert.AreEqual(typeof(int), CompileGetter<object>("date('1974/08/24').Year").GetValue().GetType());
            Assert.AreEqual(1974, CompileGetter<int>("date('1974/08/24').Year").GetValue());
            Assert.AreEqual(2005, CompileGetter<int>("date('1974/08/24').AddYears(31).Year").GetValue());
        }

        /// <summary>
        /// Tests simple property and field accessors and mutators
        /// </summary>
        [Test]
        public void TestSimplePropertyAccess()
        {
            Assert.AreEqual(DateTime.Today, CompileAndExecuteGetter<DateTime>("DateTime.Today"));


            var inventor = GetTesla();
            Assert.AreEqual("Nikola Tesla", CompileGetter<Inventor, string>("Name").GetValue(inventor));
            Assert.AreEqual(new DateTime(1856, 7, 9), CompileGetter<Inventor, DateTime>("DOB").GetValue(inventor));
            Assert.AreEqual(1856, CompileGetter<Inventor, int>("DOB.Year").GetValue(inventor));


            var setterExpression = Expression.ParseSetter<Inventor, string>("PlaceOfBirth.Country",
                EvaluationMode.MustCompile);

            setterExpression.SetValue(inventor, "Croatia");

            Assert.AreEqual("Croatia", CompileGetter<Inventor, string>("PlaceOfBirth.Country").GetValue(inventor));

            setterExpression.SetValue(inventor, "Biedaszyb");
            Assert.AreEqual("Biedaszyb", inventor.PlaceOfBirth.Country);

            setterExpression.SetValue(GetPulpin(), "Other object");
            Assert.AreEqual("Biedaszyb", inventor.PlaceOfBirth.Country);


            var pupin = GetPulpin();
            Assert.AreEqual("Idvor", CompileGetter<Inventor, string>("PlaceOfBirth.City").GetValue(pupin));

            var setName = Expression.ParseSetter<Inventor, string>("Name", 
                EvaluationMode.MustCompile);

            setName.SetValue(pupin, "Michael Pupin");

            Assert.AreEqual("Michael Pupin", CompileGetter<Inventor, string>("Name").GetValue(pupin));
            Assert.AreEqual("Michael Pupin", pupin.Name);
        }

        /// <summary>
        /// Tests that simple property and field accessors and mutators are case-insensitive.
        /// </summary>
        [Test]
        public void TestSimplePropertyAccessIsCaseInsensitive()
        {
            var tesla = GetTesla();
            Assert.AreEqual("Nikola Tesla", CompileGetter<Inventor, string>("nAme").GetValue(tesla));

            var pupin = GetPulpin();
            Assert.AreEqual("Idvor", CompileGetter<Inventor, string>("Placeofbirth.city").GetValue(pupin));


            var setterExpression = Expression.ParseSetter<Inventor, string>("PlaceOfBirth.CountRY",
                EvaluationMode.MustCompile);
            setterExpression.SetValue(tesla, "Croatia");

            Assert.AreEqual("Croatia", CompileGetter<Inventor, string>("Placeofbirth.COUNtry").GetValue(tesla));

            setterExpression = Expression.ParseSetter<Inventor, string>("NAME",
                EvaluationMode.MustCompile);
            setterExpression.SetValue(pupin, "Michael Pupin");

            Assert.AreEqual("Michael Pupin", CompileGetter<Inventor, string>("name").GetValue(pupin));
            Assert.AreEqual(new DateTime(1856, 7, 9), CompileGetter<Inventor, DateTime>("dob").GetValue(tesla));
            Assert.AreEqual(1856, CompileGetter<Inventor, int>("DOb.YEar").GetValue(tesla));
        }

        /// <summary>
        /// Tests setting and getting shadowed properties
        /// </summary>
        [Test]
        public void TestShadowedPropertyAccess()
        {
            ShadowingTestsMostSpezializedClass o;

            // test read
            o = new ShadowingTestsMostSpezializedClass();
            o.SomeValue = "SomeString";
            Assert.AreEqual("SomeString", 
                CompileGetter<ShadowingTestsMostSpezializedClass, string>("SomeValue").GetValue(o));

            // test write
            o = new ShadowingTestsMostSpezializedClass();

            var setter1 = Expression.ParseSetter<ShadowingTestsMostSpezializedClass, string>("SomeValue", 
                EvaluationMode.MustCompile);
            setter1.SetValue(o, "SomeOtherString");

            Assert.AreEqual("SomeOtherString", o.SomeValue);

            // test readonly shadowed
            o = new ShadowingTestsMostSpezializedClass();
            ((ShadowingTestsBaseClass)o).ReadonlyShadowedValue = "SomeString1";
            Assert.AreEqual("SomeString1", 
                CompileGetter<ShadowingTestsMostSpezializedClass, string>("ReadonlyShadowedValue").GetValue(o));
            try
            {
                // fails at compile time! 
                Expression.ParseSetter<ShadowingTestsMostSpezializedClass, string>("ReadonlyShadowedValue",
                    EvaluationMode.MustCompile);

                Assert.Fail("Setting readonly property should throw NotWritablePropertyException");
            }
            catch (NotWritablePropertyException)
            { }

            Assert.AreEqual("SomeString1",
                CompileGetter<ShadowingTestsMostSpezializedClass, string>("ReadonlyShadowedValue").GetValue(o));


            // test write-only shadowed
            o = new ShadowingTestsMostSpezializedClass();
            ExpressionEvaluator.SetValue(o, "WriteonlyShadowedValue", "SomeString3");
            Assert.AreEqual("SomeString3", ((ShadowingTestsBaseClass)o).WriteonlyShadowedValue);

            // Reading a write-only property is refused at compile time and reported by the interpreter
            // at evaluation. The compiled path used to raise NotReadablePropertyException itself, which
            // the weakly typed path's fallback cannot see - so the shape was a hard failure rather than
            // an interpreted expression that then fails with the exception a caller expects.
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ShadowingTestsMostSpezializedClass, string>("WriteonlyShadowedValue"));

            Assert.Throws<NotReadablePropertyException>(
                () => Expression.Parse("WriteonlyShadowedValue")
                    .GetValue<ShadowingTestsMostSpezializedClass>(o));
        }


        /// <summary>
        /// Tests indexed property and field accessors and mutators
        /// </summary>
        [Test]
        public void TestIndexedPropertyAccess()
        {
            TypeRegistry.RegisterType("Society", typeof(Society));
            TypeRegistry.RegisterType("Inventor", typeof(Inventor));

            var ieee = GetIEEE(
                tesla: out var tesla, 
                pupin: out var pupin);


            // arrays and lists
            Assert.AreEqual("Induction motor", CompileGetter<Inventor, string>("Inventions[3]").GetValue(tesla));
            Assert.AreEqual("Nikola Tesla", CompileGetter<Society, string>("Members[0].Name").GetValue(ieee));
            Assert.AreEqual("Wireless communication", 
                CompileGetter<Society, string>("Members[0].Inventions[6]").GetValue(ieee));

            // todo: error: casts! strong type!
            // maps
            Assert.AreEqual(pupin, CompileGetter<Society, object>("Officers['president']").GetValue(ieee));
            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers['president'] as T(Inventor)").GetValue(ieee));

            // every cast above and below also has a bare type spelling; both are shown
            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers['president'] as Inventor").GetValue(ieee));


            Assert.AreEqual("Idvor",
                CompileGetter<Society, string>("(Officers['president'] as T(Inventor)).PlaceOfBirth.City").GetValue(ieee));
            Assert.AreEqual("Idvor",
                CompileGetter<Society, string>("(Officers['president'] as Inventor).PlaceOfBirth.City").GetValue(ieee));

            Assert.AreEqual(tesla, CompileGetter<Society, Inventor>("(Officers['advisors'] as T(SpringExpressions.Inventor[]))[0]").GetValue(ieee));
            Assert.AreEqual(tesla, CompileGetter<Society, Inventor>("(Officers['advisors'] as SpringExpressions.Inventor[])[0]").GetValue(ieee));

            Assert.AreEqual("Polyphase alternating-current system",
                CompileGetter<Society, string>("(Officers['advisors'] as T(SpringExpressions.Inventor[]))[0].Inventions[2]").GetValue(ieee));
            Assert.AreEqual("Polyphase alternating-current system",
                CompileGetter<Society, string>("(Officers['advisors'] as SpringExpressions.Inventor[])[0].Inventions[2]").GetValue(ieee));

            // maps with non-literal parameters
            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["prez"] = "president";
            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers[#prez as T(string)] as T(Inventor)").GetValue(ieee, vars));
            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers[#prez as string] as Inventor").GetValue(ieee, vars));

            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers[Society.President] as T(Inventor)").GetValue(ieee));
            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers[Society.President] as Inventor").GetValue(ieee));
            // Officers is a non-generic IDictionary, so every value read out of it is statically
            // 'object' and the next link has nothing to bind against. The cast is what gives the
            // compiled path a type to continue from; the uncast forms are pinned as refusals in
            // TestIndexedPropertyAccessWithoutCastIsRefusedButStillEvaluates.
            Assert.AreEqual("Idvor",
                CompileGetter<Society, string>("(Officers[Society.President] as Inventor).PlaceOfBirth.City").GetValue(ieee));
            Assert.AreEqual(tesla, CompileGetter<Society, Inventor>("(Officers[Society.Advisors] as Inventor[])[0]").GetValue(ieee));
            Assert.AreEqual("Polyphase alternating-current system",
                CompileGetter<Society, string>("(Officers[Society.Advisors] as Inventor[])[0].Inventions[2]").GetValue(ieee));


            // try to set some values
            // setter for: ExpressionEvaluator.SetValue(ieee, "Officers['advisors'][0].PlaceOfBirth.Country", "Croatia");
            Expression.ParseSetter<Society, string>("(Officers['advisors'] as Inventor[])[0].PlaceOfBirth.Country",
                    EvaluationMode.MustCompile)
                .SetValue(ieee, "Croatia");
            Assert.AreEqual("Croatia", CompileGetter<Inventor, string>("PlaceOfBirth.Country").GetValue(tesla));

            // setter for: ExpressionEvaluator.SetValue(ieee, "Officers['president'].Name", "Michael Pupin");
            Expression.ParseSetter<Society, string>("(Officers['president'] as Inventor).Name",
                    EvaluationMode.MustCompile)
                .SetValue(ieee, "Michael Pupin");

            Assert.AreEqual("Michael Pupin", CompileGetter<Inventor, string>("Name").GetValue(pupin));

            // setter for: ExpressionEvaluator.SetValue(ieee, "Officers['advisors']", new [] { pupin, tesla });
            Expression.ParseSetter<Society, Inventor[]>("Officers['advisors']",
                    EvaluationMode.MustCompile)
                .SetValue(ieee, new[] { pupin, tesla });

            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("(Officers['advisors'] as Inventor[])[0]").GetValue(ieee));
            Assert.AreEqual(tesla, CompileGetter<Society, Inventor>("(Officers['advisors'] as Inventor[])[1]").GetValue(ieee));

            // generic indexer
            var bar = new Bar();
            var exp = CompileGetter<Bar,object>("[1]");
            Assert.AreEqual(2, exp.GetValue(bar));
            Assert.AreEqual(2, exp.GetValue(bar));

            var foo = new Foo();
            Assert.AreEqual("test_1", CompileGetter<Foo, object>("[1, 'test']").GetValue(foo));
        }

        /// <summary>
        /// The uncast counterparts of the casts in <see cref="TestIndexedPropertyAccess"/>. Society.Officers
        /// is a non-generic IDictionary, so every value read out of it is statically 'object' - the compiled
        /// backend binds members and indexers against static types, and 'object' declares neither
        /// PlaceOfBirth nor an indexer, so there is nothing to emit. Only the runtime value knows what it is,
        /// which is the interpreter's job. So the strongly typed path must refuse with CompileErrorException,
        /// and the weakly typed path must still answer, through its interpreter fallback. Do not "fix" one
        /// side of a pair: adding a cast is what makes these shapes compilable, and that is what
        /// TestIndexedPropertyAccess shows.
        /// </summary>
        [Test]
        public void TestIndexedPropertyAccessWithoutCastIsRefusedButStillEvaluates()
        {
            TypeRegistry.RegisterType("Society", typeof(Society));
            TypeRegistry.RegisterType("Inventor", typeof(Inventor));

            var ieee = GetIEEE(tesla: out var tesla, pupin: out _);

            // a member on a dictionary value
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<Society, string>("Officers[Society.President].PlaceOfBirth.City"));
            Assert.AreEqual("Idvor",
                Expression.Parse("Officers[Society.President].PlaceOfBirth.City").GetValue(ieee));

            // an indexer on a dictionary value
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<Society, Inventor>("Officers[Society.Advisors][0]"));
            Assert.AreEqual(tesla, Expression.Parse("Officers[Society.Advisors][0]").GetValue(ieee));

            // and the same, one link deeper
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<Society, string>("Officers[Society.Advisors][0].Inventions[2]"));
            Assert.AreEqual("Polyphase alternating-current system",
                Expression.Parse("Officers[Society.Advisors][0].Inventions[2]").GetValue(ieee));

            // setters refuse for the same reason, and the weak path sets the value anyway
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => Expression.ParseSetter<Society, string>("Officers['advisors'][0].PlaceOfBirth.Country",
                    EvaluationMode.MustCompile));
            Expression.Parse("Officers['advisors'][0].PlaceOfBirth.Country").SetValue(ieee, "Croatia");
            Assert.AreEqual("Croatia", CompileGetter<Inventor, string>("PlaceOfBirth.Country").GetValue(tesla));

            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => Expression.ParseSetter<Society, string>("Officers['president'].Name",
                    EvaluationMode.MustCompile));
            Expression.Parse("Officers['president'].Name").SetValue(ieee, "Michael Pupin");
            Assert.AreEqual("Michael Pupin", Expression.Parse("Officers[Society.President].Name").GetValue(ieee));
        }

        /// <summary>
        /// Tests indexer access with invalid number of indices. A wrong index count has no compiled
        /// form - LExpression.ArrayIndex used to throw ArgumentException while the tree was being
        /// built, which the weak path's fallback cannot catch - so compilation refuses with
        /// CompileErrorException, and the interpreter reports the InvalidPropertyException at
        /// evaluation, as upstream always did.
        /// </summary>
        [Test]
        public void TestIndexedPropertyAccessWithInvalidNumberOfIndices()
        {
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<Inventor, object>("Inventions[3, 2]"));

            var tesla = new Inventor("Nikola Tesla", new DateTime(1856, 7, 9), "Serbian");
            tesla.Inventions = new[] { "One", "Two" };

            IExpression weak = Expression.Parse("Inventions[3, 2]");
            Assert.Throws<InvalidPropertyException>(() => weak.GetValue(tesla));
        }

        /// <summary>
        /// Tests method accessors
        /// </summary>
        [Test]
        public void TestMethodAccess()
        {
            Guid guid = Guid.NewGuid();

            TypeRegistry.RegisterType("Guid", typeof(Guid));
            
            Assert.AreEqual(guid.ToString(), CompileGetter<Guid, string>("ToString()").GetValue(guid));
            Assert.AreEqual(guid.ToString("n"), CompileGetter<Guid, string>("ToString('n')").GetValue(guid));

            Assert.AreEqual(16, CompileGetter<int>("Guid.NewGuid().ToByteArray().Length").GetValue());

            var ieee = GetIEEE(out var tesla, out _);

            Assert.AreEqual(2005 - tesla.DOB.Year,
                CompileGetter<Society, int>("Members[0].GetAge(date('2005-01-01'))").GetValue(ieee));
        }

        /// <summary>
        /// One compiled expression, then two roots of different types - a decimal and an int, each of
        /// which has ToString(string, IFormatProvider) while the declared root type, object, has no
        /// two-argument ToString at all. Which method to call is knowable only from the value in hand, so
        /// there is nothing to emit: the strongly typed path refuses, and the weakly typed path answers
        /// through its interpreter fallback, binding afresh per evaluation.
        /// <p>
        /// This test used to demand the compiled answer (the author's own "todo: error: fixme!") and was
        /// red for as long as it existed. Making it pass means per-call late binding inside compiled
        /// code, which is a ruling nobody has made; until then the boundary is what gets recorded. Do not
        /// "fix" the refusal half without that ruling.
        /// </p>
        /// </summary>
        [Test]
        public void TestMethodEvaluationOnDifferentContextType()
        {
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<object, object>("ToString('dummy', null)"));

            IExpression weak = Expression.Parse("ToString('dummy', null)");
            Assert.AreEqual("dummy", weak.GetValue(0m));
            Assert.AreEqual("dummy", weak.GetValue(0));
        }

        /// <summary>
        /// The same boundary one level down, on an argument rather than the root: Foo(string) and
        /// Foo(int) are both candidates, and #var1 is a dictionary lookup - statically object - so the
        /// overload is decided by whatever the caller put in the variable, evaluation by evaluation. The
        /// compiled path says so in as many words ("Add a cast to pick an overload") and refuses; the
        /// interpreter chooses from the runtime value, which is what the weak path serves.
        /// <p>
        /// The author's note on this one was "todo: error; won't run!!!!! not fixable????" - correct, as
        /// written. A cast makes it compilable again, which the third block shows. Do not "fix" the
        /// refusal half without a late-binding ruling.
        /// </p>
        /// </summary>
        [Test]
        public void TestMethodEvaluationOnDifferentArgumentTypes()
        {
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<MethodInvocationCases, object>("Foo(#var1)"));

            var testContext = new MethodInvocationCases();
            var args = new Dictionary<string, object>();

            IExpression weak = Expression.Parse("Foo(#var1)");

            args["var1"] = "myString";
            Assert.AreEqual("myString", weak.GetValue(testContext, args));

            args["var1"] = 12;
            Assert.AreEqual(12, weak.GetValue(testContext, args));

            // naming the type restores the compiled form, which is what the refusal message promises
            var compiledAsString = CompileGetter<MethodInvocationCases, object>("Foo(#var1 as string)");
            args["var1"] = "myString";
            Assert.AreEqual("myString", compiledAsString.GetValue(testContext, args));

            var compiledAsInt = CompileGetter<MethodInvocationCases, object>("Foo(#var1 as int)");
            args["var1"] = 12;
            Assert.AreEqual(12, compiledAsInt.GetValue(testContext, args));
        }

        /// <summary>
        /// Tests missing method accessors. A method that does not exist has no compiled form like any
        /// other unbindable shape - the compiled path cannot tell "misspelled" from "not on the declared
        /// type but there at runtime", which is a shape it must fall back on - so it refuses, and the
        /// interpreter reports the ArgumentException at evaluation exactly as it always did. This test
        /// asserted that ArgumentException against the *compile* call, which predates the refusal
        /// convention (the author's own "todo: error fixme! wrong exception!").
        /// </summary>
        [Test]
        public void TestMissingMethodAccess()
        {
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<string, string>("ToStringilyLingily()"));

            Assert.Throws<ArgumentException>(
                () => Expression.Parse("ToStringilyLingily()").GetValue("some string"));
        }

        /// <summary>
        /// Tests projection node
        /// </summary>
        [Test]
        public void TestProjection()
        {
            TypeRegistry.RegisterType(typeof(Inventor));

            var ieee = GetIEEE(out _, out _);
            var placesOfBirth = CompileGetter<Society, IList>("Members.!{PlaceOfBirth.City}").GetValue(ieee);

            Assert.AreEqual(2, placesOfBirth.Count);
            Assert.AreEqual("Smiljan", placesOfBirth[0]);
            Assert.AreEqual("Idvor", placesOfBirth[1]);

            var names = CompileGetter<Society, IList>("(Officers['advisors'] as T(Inventor[])).!{Name}").GetValue(ieee);
            Assert.AreEqual(2, names.Count);
            Assert.AreEqual("Nikola Tesla", names[0]);
            Assert.AreEqual("Mihajlo Pupin", names[1]);

            // the same cast in the bare type spelling
            var bareNames = CompileGetter<Society, IList>("(Officers['advisors'] as Inventor[]).!{Name}").GetValue(ieee);
            Assert.AreEqual(2, bareNames.Count);
            Assert.AreEqual("Nikola Tesla", bareNames[0]);
            Assert.AreEqual("Mihajlo Pupin", bareNames[1]);
        }

        /// <summary>
        /// Tests selection node
        /// </summary>
        [Test]
        public void TestSelection()
        {
            TypeRegistry.RegisterType(typeof(Inventor));
            var ieee = GetIEEE(out _, out _);

            var memberSelection =
                CompileGetter<Society, IList>("Members.?{PlaceOfBirth.City == 'Smiljan'}").GetValue(ieee);

            Assert.AreEqual(1, memberSelection.Count);
            Assert.AreEqual("Nikola Tesla", ((Inventor)memberSelection[0]).Name);

            var serbianOfficers =
                CompileGetter<Society, IList>("(Officers['advisors'] as T(Inventor[])).?{Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual(2, serbianOfficers.Count);
            Assert.AreEqual("Nikola Tesla", ((Inventor)serbianOfficers[0]).Name);
            Assert.AreEqual("Mihajlo Pupin", ((Inventor)serbianOfficers[1]).Name);

            // the same cast in the bare type spelling
            var bareSerbianOfficers =
                CompileGetter<Society, IList>("(Officers['advisors'] as Inventor[]).?{Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual(2, bareSerbianOfficers.Count);
            Assert.AreEqual("Nikola Tesla", ((Inventor)bareSerbianOfficers[0]).Name);
            Assert.AreEqual("Mihajlo Pupin", ((Inventor)bareSerbianOfficers[1]).Name);

                  // todo: error? implement or not!!!!!
            var first =
                CompileGetter<Society, Inventor>("(Officers['advisors'] as T(Inventor[])).^{Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual("Nikola Tesla", first.Name);

            var bareFirst =
                CompileGetter<Society, Inventor>("(Officers['advisors'] as Inventor[]).^{Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual("Nikola Tesla", bareFirst.Name);

            var last =
                CompileGetter<Society, Inventor>("(Officers['advisors'] as T(Inventor[])).${Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual("Mihajlo Pupin", last.Name);

            var bareLast =
                CompileGetter<Society, Inventor>("(Officers['advisors'] as Inventor[]).${Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual("Mihajlo Pupin", bareLast.Name);
        }

        /// <summary>
        /// Tests type node
        /// </summary>
        [Test]
        public void TestTypeNode()
        {
            var exp = CompileGetter<Type>("T(DateTime)");
            exp.GetValue();
            Assert.AreEqual(typeof(DateTime), exp.GetValue());

            var expObj = CompileGetter<object>("T(DateTime)");
            expObj.GetValue();
            Assert.AreEqual(typeof(DateTime), expObj.GetValue());


            Assert.AreEqual(typeof(DateTime), CompileGetter<Type>("T(System.DateTime)").GetValue());
            Assert.AreEqual(typeof(DateTime[]), CompileGetter<Type>("T(System.DateTime[], mscorlib)").GetValue());

            Assert.AreEqual(typeof(ExpressionEvaluator), CompileGetter<Type>(
                "T(SpringExpressions.ExpressionEvaluator, SpringExpressions)").GetValue());

            var tesla = GetTesla();
            Assert.IsTrue(CompileGetter<Inventor, bool>("T(System.DateTime) == DOB.GetType()").GetValue(tesla));
        }

        /// <summary>
        /// Tests type node
        /// </summary>
        [Test]
        public void TestTypeNodeWithArrays()
        {
            Assert.AreEqual(typeof(DateTime[]), CompileGetter<Type>("T(System.DateTime[])").GetValue());
            Assert.AreEqual(typeof(DateTime[,]), CompileGetter<Type>("T(System.DateTime[,])").GetValue());
            Assert.AreEqual(typeof(DateTime[]), CompileGetter<Type>("T(System.DateTime[], mscorlib)").GetValue());
            Assert.AreEqual(typeof(DateTime[,]), CompileGetter<Type>("T(System.DateTime[,], mscorlib)").GetValue());
        }

        /// <summary>
        /// Tests type node
        /// </summary>
        [Test]
        public void TestTypeNodeWithAssemblyQualifiedName()
        {
            Assert.AreEqual(typeof(ExpressionEvaluator),
                CompileGetter<Type>($"T({typeof(ExpressionEvaluator).AssemblyQualifiedName})").GetValue());
        }

        /// <summary>
        /// Tests type node
        /// </summary>
        [Test]
        public void TestTypeNodeWithGenericAssemblyQualifiedName()
        {
            Assert.AreEqual(typeof(int?), CompileGetter<Type>("T(System.Nullable`1[System.Int32], mscorlib)").GetValue());
            Assert.AreEqual(typeof(int?), 
                CompileGetter<Type>("T(System.Nullable`1[[System.Int32, mscorlib]], mscorlib)").GetValue());
            Assert.AreEqual(typeof(int?), 
                CompileGetter<Type>("T(System.Nullable`1[[int]], mscorlib)").GetValue());
            Assert.AreEqual(typeof(Dictionary<string, bool>), 
                CompileGetter<Type>("T(System.Collections.Generic.Dictionary`2[System.String,System.Boolean],mscorlib)").GetValue());
        }

        [Test]
        public void TestGenericDictionary()
        {
            Assert.AreEqual(typeof(Dictionary<string, bool>),
                CompileGetter<Type>(
                    "T(System.Collections.Generic.Dictionary`2[System.String,System.Boolean],mscorlib)").GetValue()); 
        }

        /// <summary>
        /// Tests type node
        /// </summary>
        [Test]
        public void TestTypeNodeWithAliasedGenericArguments()
        {
            Assert.AreEqual(typeof(Dictionary<string, bool>), 
                CompileGetter<Type>("T(System.Collections.Generic.Dictionary`2[string,bool],mscorlib)").GetValue());
        }

        /// <summary>
        /// Tests type node
        /// </summary>
        [Test]
        public void TestTypeNodeWithGenericAssemblyQualifiedArrayName()
        {
            Assert.AreEqual(typeof(int?[,]), 
                CompileGetter<Type>("T(System.Nullable`1[[System.Int32, mscorlib]][,], mscorlib)").GetValue());
        }

        /// <summary>
        /// Tests constructor node
        /// </summary>
        [Test]
        public void TestConstructor()
        {
            Assert.AreEqual(1000, CompileAndExecuteGetter<decimal>("new Decimal(1000)"));

            var exp = CompileGetter<string, DateTime>("new System.DateTime(2004, 8, 14)");

            Assert.AreEqual(new DateTime(2004, 8, 14), exp.GetValue(null));
            Assert.AreEqual(new DateTime(2004, 8, 14), exp.GetValue("xyz"));

            Assert.AreEqual(new DateTime(1974, 8, 24),
                CompileGetter<DateTime>("new DateTime(2004, 8, 14).AddDays(10).AddYears(-30)").GetValue());
        }

        [Test]
        public void TestParamConversion()
        {
            Assert.AreEqual(new DateTime(1974, 8, 24),
                Expression.ParseGetter<DateTime>(
                    "new DateTime(2004, 8, 14).AddDays(10m).AddYears(-30)", 
                    EvaluationMode.MustInterpret)
                .GetValue());

            // implicit casting from decimal to double
            Assert.AreEqual(new DateTime(1974, 8, 24),
                CompileGetter<DateTime>("new DateTime(2004, 8, 14).AddDays(10m).AddYears(-30)").GetValue());
        }

        [Test]
        public void TestConstructorWithNamedArguments()
        {
            TypeRegistry.RegisterType(typeof(Inventor));

            // A named argument is a member assigned after construction, not a constructor parameter.
            // This block used to pass by accident: every child was emitted as a positional argument with
            // its name discarded, and these three values happen to line up with
            // Inventor(string, DateTime, string) in this order. They are MemberInit bindings now.
            var ana = CompileGetter<Inventor>(
                    "new Inventor(Name = 'Ana Maria Seovic', DOB = date('2004-08-14'), Nationality = 'American')")
                .GetValue();
            Assert.AreEqual("Ana Maria Seovic", ana.Name);
            Assert.AreEqual(new DateTime(2004, 8, 14), ana.DOB);
            Assert.AreEqual("American", ana.Nationality);

            // Positional and named together - "constructor node searches for 4 param node", as the
            // author's note put it, so this refused to compile at all. Only the three positional
            // arguments reach the constructor now.
            var aleks = CompileGetter<Inventor>(
                    "new Inventor('Aleksandar Seovic', date('1974-08-24'), 'Serbian', Nationality = 'Serbian-American')")
                .GetValue();
            Assert.AreEqual("Aleksandar Seovic", aleks.Name);
            Assert.AreEqual(new DateTime(1974, 8, 24), aleks.DOB);
            Assert.AreEqual("Serbian-American", aleks.Nationality);
        }

        /// <summary>
        /// The order the names are written in must not matter, which is the whole point of naming them.
        /// It used to: with the names discarded and the values passed positionally, this expression built
        /// an Inventor named "American" of nationality "Ana" - silently, because those two values are
        /// both strings and the accident type-checks. The interpreter always got it right.
        /// </summary>
        [Test]
        public void TestConstructorNamedArgumentsAreOrderIndependent()
        {
            TypeRegistry.RegisterType(typeof(Inventor));

            // Inventor has no value equality, so TestCompiledVsInterpreted's Assert.AreEqual would fail
            // on two freshly built instances however right they both are: the backends are compared
            // member by member here instead.
            const string reordered = "new Inventor(Nationality = 'American', DOB = date('2004-08-14'), Name = 'Ana')";

            var compiled = CompileGetter<Inventor>(reordered).GetValue();
            Assert.AreEqual("Ana", compiled.Name);
            Assert.AreEqual("American", compiled.Nationality);
            Assert.AreEqual(new DateTime(2004, 8, 14), compiled.DOB);

            var interpreted = InterpretGetter<Inventor>(reordered).GetValue();
            Assert.AreEqual("Ana", interpreted.Name);
            Assert.AreEqual("American", interpreted.Nationality);
            Assert.AreEqual(new DateTime(2004, 8, 14), interpreted.DOB);

            // the name is matched case-insensitively, as the interpreter's Expression.ParseProperty does
            const string lowercased = "new Inventor('Aleks', date('1974-08-24'), 'Serbian', nationality = 'lower')";

            Assert.AreEqual("lower", CompileGetter<Inventor>(lowercased).GetValue().Nationality);
            Assert.AreEqual("lower", InterpretGetter<Inventor>(lowercased).GetValue().Nationality);
        }

        /// <summary>
        /// What a compiled named argument cannot express, refused so the interpreter serves it.
        /// <p>
        /// The value case is not a gap to close casually: the interpreter assigns through the property
        /// setter's own coercion, which converts shapes LINQ has no conversion for - a list literal into
        /// a string[] here - and reproducing that is the weakly typed setter's job. Do not "fix" the
        /// refusal half without it.
        /// </p>
        /// </summary>
        [Test]
        public void TestConstructorNamedArgumentsRefusedWhenTheyCannotBeEmitted()
        {
            TypeRegistry.RegisterType(typeof(Inventor));

            const string listIntoArray =
                "new Inventor('Aleksandar Seovic', date('1974-08-24'), 'Serbian', Inventions = {'SPELL'})";

            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<Inventor>(listIntoArray));

            var aleks = (Inventor)Expression.Parse(listIntoArray).GetValue();
            Assert.AreEqual("Aleksandar Seovic", aleks.Name);
            Assert.AreEqual(new DateTime(1974, 8, 24), aleks.DOB);
            Assert.AreEqual("Serbian", aleks.Nationality);
            Assert.AreEqual(1, aleks.Inventions.Length);
            Assert.AreEqual("SPELL", aleks.Inventions[0]);

            // a name that is no member at all: a refusal compiled, the interpreter's own error weakly
            const string noSuchMember = "new Inventor('Aleks', date('1974-08-24'), 'Serbian', Nope = 'x')";

            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<Inventor>(noSuchMember));
            Assert.Throws<InvalidPropertyException>(
                () => Expression.Parse(noSuchMember).GetValue());
        }

        /// <summary>
        /// Tests missing constructor - the constructor twin of TestMissingMethodAccess, and the same
        /// pairing: no compiled form, so the compiled path refuses and the interpreter reports its
        /// ArgumentException at evaluation.
        /// </summary>
        [Test]
        public void TestMissingConstructor()
        {
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<decimal>("new Decimal('xyz')"));

            Assert.Throws<ArgumentException>(() => Expression.Parse("new Decimal('xyz')").GetValue());
        }

        /// <summary>
        /// Tests expression list node
        /// </summary>
        [Test]
        public void TestExpressionList()
        {
            TypeRegistry.RegisterType("Inventor", typeof(Inventor));

            var ieee = GetIEEE(out _, out _);

            Assert.AreEqual(3, CompileGetter<IList<Inventor>, int>(
                "(Add(new Inventor('Aleksandar Seovic', date('1974-08-24'), 'Serbian')); Count)")
                .GetValue(ieee.Members));

            Assert.AreEqual(3, CompileGetter<Society, int>(
                "Members.(Add(new Inventor('Ana Maria Seovic', date('2004-08-14'), 'Serbian')); RemoveAt(1); Count)")
                .GetValue(ieee));

            Assert.AreEqual("Aleksandar Seovic", CompileGetter<IList<Inventor>, string>(
                "([1].PlaceOfBirth.City = 'Beograd'; [1].PlaceOfBirth.Country = 'Serbia'; [1].Name)")
                .GetValue(ieee.Members));

            Assert.AreEqual("Beograd", (ieee.Members[1]).PlaceOfBirth.City);
        }

        /// <summary>
        /// Tests assignment node
        /// </summary>
        [Test]
        public void TestAssignNode()
        {
            var inventor = new Inventor();

            Assert.AreEqual("Aleksandar Seovic", CompileGetter<Inventor, string>(
                "Name = 'Aleksandar Seovic'").GetValue(inventor));

            Assert.AreEqual(new DateTime(1974, 8, 24), CompileGetter<Inventor, DateTime>(
                "DOB = date('1974-08-24')").GetValue(inventor));

            Assert.AreEqual("Serbian", CompileGetter<Inventor, string>(
                "Nationality = 'Serbian'").GetValue(inventor));

            Assert.AreEqual("Ana Maria Seovic", CompileGetter<Inventor, string>(
                "(DOB = date('2004-08-14'); Name = 'Ana Maria Seovic')").GetValue(inventor));
            Assert.AreEqual(new DateTime(2004, 8, 14), inventor.DOB);

            var ieee = GetIEEE(out _, out _);

            Expression.ParseVoidExpression<Society>("Members[0].Name = 'CowCzuk'").Execute(ieee);
            Assert.AreEqual("CowCzuk", ieee.Members[0].Name);


            Assert.IsNull(ieee.Officers["vp"]);
            Expression.ParseVoidExpression<Society>("Officers['vp'] = Members[0]").Execute(ieee);
            // ReSharper disable once PossibleInvalidCastException
            Assert.AreEqual("CowCzuk", ((Inventor)ieee.Officers["vp"]).Name);


            // this is not a setter expression! it calls set_Item method!
            // CompileSetter<Society, object>("Officers['vp'] = Members[0]").SetValue(ieee, null);
        }

        /// <summary>
        /// Tests default node
        /// </summary>
        [Test]
        public void TestDefaultNode()
        {
            var tesla = GetTesla();

            Assert.AreEqual("default", CompileGetter<string>("null ?? 'default'").GetValue());
            Assert.AreEqual(1, CompileGetter<int>("null ?? 2 * 2 - 3").GetValue());
            Assert.AreEqual("Nikola Tesla", CompileGetter<Inventor, string>("null ?? #root.Name").GetValue(tesla));

            Assert.AreEqual("default", CompileGetter<string>("'default' ?? 'xyz'").GetValue());
            Assert.AreEqual(1, CompileGetter<int>("2 * 2 - 3 ?? 5").GetValue());
            Assert.AreEqual("Nikola Tesla", CompileGetter<Inventor, string>("#root.Name ?? 'Pupin'").GetValue(tesla));

            int? nullableInt = 6;
            Assert.AreEqual(6, CompileGetter<int?, int>("#root ?? 997").GetValue(nullableInt));


            Assert.AreEqual(6, Expression.ParseGetter<int?, int?>(
                    "#root ?? 997", EvaluationMode.MustInterpret)
                .GetValue(nullableInt));
            Assert.AreEqual(6, CompileGetter<int?, int?>("#root ?? 997").GetValue(nullableInt));

            nullableInt = null;
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.AreEqual(997, CompileGetter<int?, int>("#root ?? 997").GetValue(nullableInt));

            var nullableHolder = new NullableIntHolder
                { Value = 6 };

            Assert.AreEqual(6, CompileGetter<NullableIntHolder, int>("Value ?? 997").GetValue(nullableHolder));

            nullableHolder.Value = null;
            Assert.AreEqual(997, CompileGetter<NullableIntHolder, int>("Value ?? 997").GetValue(nullableHolder));
        }

        /// <summary>
        /// Tests variable node
        /// </summary>
        [Test]
        public void TestVariableNode()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["newName"] = "Aleksandar Seovic";

            Assert.AreEqual("Aleksandar Seovic", vars["newName"]);
            Assert.AreEqual("Aleksandar Seovic",
                CompileGetter<object>("#newName").GetValue(vars));

            Assert.AreEqual("Ana Maria Seovic",
                CompileGetter<object>("#newName = 'Ana Maria Seovic'").GetValue(vars));

            var tesla = GetTesla();

            // A variable is statically object, so assigning one to a typed member has no compiled form:
            // whether it fits is decided by whatever the caller put in the variable. This used to compile
            // to a cast - "special object handling", as the note here called it - which threw
            // InvalidCastException on a variable holding anything but a string, where the interpreter
            // converts it. The weak path interprets the shape instead, and a cast buys compilation back.
            // WeakSetterEvaluationTests pins all three.
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(
                () => CompileGetter<Inventor, object>("Name = #newName"));

            Assert.AreEqual("Ana Maria Seovic",
                Expression.Parse("Name = #newName").GetValue(tesla, vars));
            Assert.AreEqual("Ana Maria Seovic",
                CompileGetter<Inventor, object>("Name = #newName as string").GetValue(tesla, vars));

            Assert.AreEqual("Nikola Tesla",
                CompileGetter<Inventor, object>("(#oldName = Name; Name = 'Nikola Tesla')").GetValue(tesla, vars));
            Assert.AreEqual("Nikola Tesla", CompileGetter<Inventor, Inventor>("#this").GetValue(tesla, vars).Name);
            Assert.AreEqual("Nikola Tesla",
                CompileGetter<Inventor, string>("(Nationality = 'Srbin'; #this).Name").GetValue(tesla, vars));
            Assert.AreEqual("Nikola Tesla", tesla.Name);
            Assert.AreEqual("Srbin", tesla.Nationality);
            Assert.AreEqual("Ana Maria Seovic", vars["oldName"]);

            Assert.AreEqual(tesla, CompileGetter<Inventor, Inventor>("#root").GetValue(tesla, vars));
        }

        /// <summary>
        /// Try to set 'this' variable
        /// </summary>
        [Test]
        public void TryToSetThis()
        {
            Assert.Throws<ArgumentException>(
                () => Expression.ParseSetter<string>("#this", 
                    EvaluationMode.MustCompile));
        }

        /// <summary>
        /// Try to set 'root' variable
        /// </summary>
        [Test]
        public void TryToSetRoot()
        {
            Assert.Throws<ArgumentException>(
                () => Expression.ParseSetter<string>("#root",
                EvaluationMode.MustCompile));
        }

        /// <summary>
        /// Tests ternary node
        /// </summary>
        [Test]
        public void TestTernaryNode()
        {
            var exp = CompileGetter<string>("true ? 'trueExp' : 'falseExp'");
            exp.GetValue();

            Assert.AreEqual("trueExp", exp.GetValue());
            Assert.AreEqual("falseExp", CompileGetter<string>("false ? 'trueExp' : 'falseExp'").GetValue());
            Assert.AreEqual("trueExp", CompileGetter<string>("(true ? 'trueExp' : 'falseExp')").GetValue());
            Assert.AreEqual("falseExp", CompileGetter<string>("(false ? 'trueExp' : 'falseExp')").GetValue());

            var ieee = GetIEEE(out _, out _);

            CompileSetter<Society, string>("Name").SetValue(ieee, "IEEE");

            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["queryName"] = "Nikola Tesla";

            string expression =
                @"IsMember(#queryName)
                    ? #queryName + ' is a member of the ' + Name + ' Society'
                    : #queryName + ' is not a member of ' + Name + ' Society'";

            Assert.AreEqual("Nikola Tesla is a member of the IEEE Society",
                CompileGetter<Society, string>(expression).GetValue(ieee, vars));
        }

        /// <summary>
        /// Tests logical OR operator
        /// </summary>
        [Test]
        public void TestLogicalOrOperator()
        {
            Assert.AreEqual(typeof(bool), CompileGetter<object>("true or true").GetValue().GetType());
            Assert.IsTrue(CompileGetter<bool>("true or true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("false or true").GetValue());

            string expression = @"IsMember('Nikola Tesla') or IsMember('Albert Einstien')";
            var ieee = GetIEEE(out _, out _);
            Assert.IsTrue(CompileGetter<Society, bool>(expression).GetValue(ieee));
        }


        /// <summary>
        /// Tests bitwise OR operator
        /// </summary>
        [Test]
        public void TestBitwiseOrOperator()
        {
            Assert.AreEqual(typeof(int), CompileGetter<object>("1 or 2").GetValue().GetType());
            Assert.AreEqual(1 | 2, CompileGetter<int>("1 or 2").GetValue());
            Assert.AreEqual(1 | -2, CompileGetter<int>("1 or -2").GetValue());

            Assert.AreEqual(
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
                Expression.ParseGetter<object>(
                    "T(System.Text.RegularExpressions.RegexOptions).IgnoreCase " +
                    "or T(System.Text.RegularExpressions.RegexOptions).Compiled").GetValue());


            Assert.AreEqual(typeof(RegexOptions),
                Expression.ParseGetter<object>(
                    "T(System.Text.RegularExpressions.RegexOptions).IgnoreCase " +
                    "or T(System.Text.RegularExpressions.RegexOptions).Compiled",
                    EvaluationMode.MustInterpret).GetValue().GetType());


            Assert.AreEqual(
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
                Expression.ParseGetter<RegexOptions>(
                    "T(System.Text.RegularExpressions.RegexOptions).IgnoreCase " +
                    "or T(System.Text.RegularExpressions.RegexOptions).Compiled").GetValue());


            var vars = new Dictionary<string, object>
                {
                    ["Compiled"] = RegexOptions.Compiled
                };

            Assert.AreEqual(
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
                Expression.ParseGetter<RegexOptions>(
                    "T(System.Text.RegularExpressions.RegexOptions).IgnoreCase " +
                    "or #Compiled").GetValue(vars));
        }

        /// <summary>
        /// Tests logical AND operator
        /// </summary>
        [Test]
        public void TestLogicalAndOperator()
        {
            Assert.AreEqual(typeof(bool), CompileGetter<object>("true and true").GetValue().GetType());
            Assert.IsTrue(CompileGetter<bool>("true and true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false and false").GetValue());
            Assert.IsFalse(CompileGetter<bool>("true and false").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false and true").GetValue());

            string expression = @"IsMember('Nikola Tesla') and IsMember('Mihajlo Pupin')";
            var ieee = GetIEEE(out _, out _);
            Assert.IsTrue(CompileGetter<Society, bool>(expression).GetValue(ieee));
        }

        /// <summary>
        /// Tests bitwise OR operator
        /// </summary>
        [Test]
        public void TestBitwiseAndOperator()
        {
            Assert.AreEqual(typeof(int), CompileGetter<object>("1 and 3").GetValue().GetType());
            Assert.AreEqual(1 & 3, CompileGetter<int>("1 and 3").GetValue());

            Assert.AreEqual(1 & -1, CompileGetter<int>("1 and -1").GetValue());

            TypeRegistry.RegisterType(typeof(TestEnumFlags));

            // interpreter
            Assert.AreEqual(
                typeof(TestEnumFlags), 
                Expression.ParseGetter<object>(
                    "TestEnumFlags.TwoAndFourCombined and TestEnumFlags.Four", EvaluationMode.MustInterpret)
                    .GetValue().GetType());

            Assert.AreEqual(
                TestEnumFlags.Four,
                Expression.ParseGetter<object>(
                        "TestEnumFlags.TwoAndFourCombined and TestEnumFlags.Four", EvaluationMode.MustInterpret)
                    .GetValue());

            // compilation:
            Assert.AreEqual(
                typeof(TestEnumFlags),
                CompileGetter<object>(
                        "TestEnumFlags.TwoAndFourCombined and TestEnumFlags.Four")
                    .GetValue().GetType());

            Assert.AreEqual(
                TestEnumFlags.Four,
                CompileGetter<object>(
                        "TestEnumFlags.TwoAndFourCombined and TestEnumFlags.Four")
                    .GetValue());

            var vars = new Dictionary<string, object>
                {
                    ["ALL"] = (RegexOptions)0xFFFF
                };

            Assert.AreEqual(RegexOptions.IgnoreCase, 
                ExpressionEvaluator.GetValue(null, "T(System.Text.RegularExpressions.RegexOptions).IgnoreCase and #ALL", vars));
        }

        [Flags]
        enum TestEnumFlags
        {
            None = 0,

            One = 1,
            Two = 2,
            Four = 4,

            TwoAndFourCombined = Two | Four
        }

        [Test]
        public void TestInvalidEnumMultiplication()
        {
            TypeRegistry.RegisterType(typeof(TestEnumFlags));

            // interpreter
            Assert.Throws<ArgumentException>( ()=>
                Expression.ParseGetter<object>(
                        "TestEnumFlags.TwoAndFourCombined * TestEnumFlags.Four", EvaluationMode.MustInterpret)
                    .GetValue());

            // compiler - a refusal, as the author's own note here asked for ("should throw compile
            // exception"): multiplying two enums has no compiled form, so the weak path falls back and
            // the interpreter reports the ArgumentException above
            Assert.Throws<SpringExpressions.Expressions.Compiling.Expressions.CompileErrorException>(() =>
                Expression.ParseGetter<object>(
                    "TestEnumFlags.TwoAndFourCombined * TestEnumFlags.Four",
                    EvaluationMode.MustCompile));
        }

        /// <summary>
        /// Tests logical NOT operator
        /// </summary>
        [Test]
        public void TestLogicalNotOperator()
        {
            TestCompiledVsInterpreted<bool>("!true");
            TestCompiledVsInterpreted<bool>("!false");

            Assert.IsFalse(CompileGetter<bool>("!true").GetValue());
            Assert.IsTrue(CompileGetter<bool>("!false").GetValue());
            
            string expression = @"IsMember('Nikola Tesla') and !IsMember('Mihajlo Pupin')";

            var ieee = GetIEEE(out _, out _);

            Assert.IsFalse(CompileGetter<Society, bool>(expression).GetValue(ieee));

            TestCompiledVsInterpreted<RegexOptions>("!T(System.Text.RegularExpressions.RegexOptions).Compiled");

            
            Assert.AreEqual(~RegexOptions.Compiled,
                CompileGetter<RegexOptions>("!T(System.Text.RegularExpressions.RegexOptions).Compiled").GetValue());
        }

        /// <summary>
        /// Tests bitwise XOR operator
        /// </summary>
        [Test]
        public void TestXorOperator()
        {
            TestCompiledVsInterpreted<int>("1 xor 3");
            TestCompiledVsInterpreted<bool>("true xor true");

            Assert.AreEqual(1 ^ 3, CompileGetter<int>("1 xor 3").GetValue());
            Assert.AreEqual(1 ^ -1, CompileGetter<int>("1 xor -1").GetValue());
            Assert.AreEqual(true ^ false, CompileGetter<bool>("true xor false").GetValue());
            Assert.AreEqual(true ^ true, CompileGetter<bool>("true xor true").GetValue());
            Assert.AreEqual(RegexOptions.IgnoreCase ^ RegexOptions.Compiled, 
                CompileGetter<RegexOptions>("T(System.Text.RegularExpressions.RegexOptions).IgnoreCase " +
                    "xor T(System.Text.RegularExpressions.RegexOptions).Compiled").GetValue());
        }

        /// <summary>
        /// Tests logical operator precedence
        /// </summary>
        [Test]
        public void TestLogicalOperatorPrecedence()
        {
            // NOT over AND
            Assert.IsFalse(CompileGetter<bool>("!false and false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("!false and true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("!true and false").GetValue());
            Assert.IsFalse(CompileGetter<bool>("!true and true").GetValue());

            Assert.IsTrue(CompileGetter<bool>("!(false and false)").GetValue());
            Assert.IsTrue(CompileGetter<bool>("!(false and true)").GetValue());
            Assert.IsTrue(CompileGetter<bool>("!(true and false)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("!(true and true)").GetValue());

            // NOT over OR
            Assert.IsTrue(CompileGetter<bool>("!false or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("!false or true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("!true or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("!true or true").GetValue());

            Assert.IsTrue(CompileGetter<bool>("!(false or false)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("!(false or true)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("!(true or false)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("!(true or true)").GetValue());

            // AND over OR
            Assert.IsFalse(CompileGetter<bool>("false and false or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("false and false or true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false and true or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("false and true or true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("true and false or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true and false or true").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true and true or false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true and true or true").GetValue());

            Assert.IsFalse(CompileGetter<bool>("false and (false or false)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false and (false or true)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false and (true or false)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false and (true or true)").GetValue());
            Assert.IsFalse(CompileGetter<bool>("true and (false or false)").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true and (false or true)").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true and (true or false)").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true and (true or true)").GetValue());
        }

        /// <summary>
        /// Tests equality operator.
        /// </summary>
        [Test]
        public void TestEqualityOperator()
        {
            // Null
            TestCompiledVsInterpreted<bool>("null == null");
            Assert.IsTrue(CompileGetter<bool>("null == null").GetValue());
            Assert.IsFalse(CompileGetter<bool>("null == 'xyz'").GetValue());
            Assert.IsFalse(CompileGetter<bool>("123 == null").GetValue());
            Assert.IsFalse(CompileGetter<bool>("null == 123").GetValue());

            // Bool
            Assert.IsTrue(CompileGetter<bool>("false == false").GetValue());
            Assert.IsTrue(CompileGetter<bool>("true == true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("false == true").GetValue());
            Assert.IsFalse(CompileGetter<bool>("true == false").GetValue());

            // Int
            Assert.IsTrue(CompileGetter<bool>("2 == 2").GetValue());
            Assert.IsTrue(CompileGetter<bool>("-5 == -5").GetValue());
            Assert.IsFalse(CompileGetter<bool>("2 == -5").GetValue());
            Assert.IsFalse(CompileGetter<bool>("-5 == 2").GetValue());

            // String
            Assert.IsTrue(CompileGetter<bool>("'test' == 'test'").GetValue());
            Assert.IsFalse(CompileGetter<bool>("'Test' == 'test'").GetValue());
            Assert.IsFalse(CompileGetter<bool>("'test' == 'Test'").GetValue());

            // DateTime
            Assert.IsTrue(CompileGetter<bool>("date('1974-08-24') == date('1974-08-24')").GetValue());
            Assert.IsTrue(CompileGetter<bool>("DateTime.Today == DateTime.Today").GetValue());
            Assert.IsFalse(CompileGetter<bool>("DateTime.Today == date('1974-08-24')").GetValue());
            Assert.IsFalse(CompileGetter<bool>("date('1974-08-24') == DateTime.Today").GetValue());

            // Enums
            Foo foo = new Foo(FooType.One);
            TypeRegistry.RegisterType("FooType", typeof(FooType));

            Assert.IsTrue(CompileGetter<Foo, bool>("Type == FooType.One").GetValue(foo));

            // todo: error:  enum (FooType) vs String... should it work? !!!!! YES, it should!  !!!!
            Assert.IsTrue(CompileGetter<Foo, bool>("Type == 'One'").GetValue(foo));
            Assert.IsFalse(CompileGetter<Foo, bool>("Type == 'Two'").GetValue(foo));
            Assert.IsTrue(CompileGetter<Foo, bool>("FooType.One == Type").GetValue(foo));
            Assert.IsTrue(CompileGetter<Foo, bool>("'One' == Type").GetValue(foo));
            Assert.IsFalse(CompileGetter<Foo, bool>("'Two' == Type").GetValue(foo));
        }

        /// <summary>
        /// Tests inequality operator.
        /// </summary>
        [Test]
        public void TestInequalityOperator()
        {
            // Null
            TestCompiledVsInterpreted<bool>("null != null");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null != null"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("123 != null"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("null != 'xyz'"));

            // Bool
            TestCompiledVsInterpreted<bool>("false != false");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("false != false"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("true != true"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("false != true"));

            // Int
            TestCompiledVsInterpreted<bool>("2 != 2.0");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("2 != 2.0"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("-5.0 != -5"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("2.0 != -5"));

            // String
            TestCompiledVsInterpreted<bool>("'test' != 'test'");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("'test' != 'test'"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("'Test' != 'test'"));

            // DateTime
            TestCompiledVsInterpreted<bool>("date('1974-08-24') != date('1974-08-24')");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("date('1974-08-24') != date('1974-08-24')"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("DateTime.Today != DateTime.Today"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("DateTime.Today != date('1974-08-24')"));
        }

        /// <summary>
        /// Tests less than operator.
        /// </summary>
        [Test]
        public void TestLessThanOperator()
        {
            // Bool
            TestCompiledVsInterpreted<bool>("false < true");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("false < true"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("true < true"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("true < false"));

            // Int
            TestCompiledVsInterpreted<bool>("2 < 2.0");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("2 < 2.0"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("-5.0 < 2"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("2 < -5.0"));

            // String
            TestCompiledVsInterpreted<bool>("'test' < 'test'");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("'test' < 'test'"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("'Test' < 'test'"));

            // DateTime
            TestCompiledVsInterpreted<bool>("date('1974-08-24') < date('1974-08-24')");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("date('1974-08-24') < date('1974-08-24')"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("date('1974-08-24') < DateTime.Today"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("DateTime.Today < date('1974-08-24')"));

            // Null
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null < null"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("123 < null"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("null < 'xyz'"));
        }

        /// <summary>
        /// Tests less than or equal operator.
        /// </summary>
        [Test]
        public void TestLessThanOrEqualOperator()
        {
            // Null
            TestCompiledVsInterpreted<bool>("null <= null");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("null <= null"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("123 <= null"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("null <= 'xyz'"));

            // Bool
            TestCompiledVsInterpreted<bool>("false <= true");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("false <= true"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("true <= true"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("true <= false"));

            // Int
            TestCompiledVsInterpreted<bool>("2 <= 2.0");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("2 <= 2.0"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("-5.0 <= 2"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("2.0 <= -5"));

            // String
            TestCompiledVsInterpreted<bool>("'test' <= 'test'");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("'test' <= 'test'"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("'Test' <= 'test'"));

            // DateTime
            TestCompiledVsInterpreted<bool>("date('1974-08-24') <= date('1974-08-24')");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("date('1974-08-24') <= date('1974-08-24')"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("date('1974-08-24') <= DateTime.Today"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("DateTime.Today <= date('1974-08-24')"));
        }

        /// <summary>
        /// Tests greater than operator.
        /// </summary>
        [Test]
        public void TestGreaterThanOperator()
        {
            // Null
            TestCompiledVsInterpreted<bool>("null > null");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null > null"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("123 > null"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null > 'xyz'"));

            // Bool
            TestCompiledVsInterpreted<bool>("false > true");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("false > true"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("true > true"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("true > false"));

            // Int
            TestCompiledVsInterpreted<bool>("2 > 2.0");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("2 > 2.0"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("-5.0 > 2"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("2 > -5.0"));

            // String
            TestCompiledVsInterpreted<bool>("'test' > 'test'");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("'test' > 'test'"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("'Test' > 'test'"));

            // DateTime
            TestCompiledVsInterpreted<bool>("DateTime.Today > date('1974-08-24')");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("date('1974-08-24') > date('1974-08-24')"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("date('1974-08-24') > DateTime.Today"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("DateTime.Today > date('1974-08-24')"));
        }

        /// <summary>
        /// Tests greater than or equal operator.
        /// </summary>
        [Test]
        public void TestGreaterThanOrEqualOperator()
        {
            // Null
            TestCompiledVsInterpreted<bool>("null >= null");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("null >= null"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("123 >= null"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null >= 'xyz'"));

            // Bool
            TestCompiledVsInterpreted<bool>("false >= true");
            Assert.IsFalse(CompileAndExecuteGetter<bool>("false >= true"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("true >= true"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("true >= false"));

            // Int
            TestCompiledVsInterpreted<bool>("2 >= 2.0");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("2.0 >= 2"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("-5 >= 2.0"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("2.0 >= -5"));

            // String
            TestCompiledVsInterpreted<bool>("'test' >= 'test'");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("'test' >= 'test'"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("'Test' >= 'test'"));

            // DateTime
            TestCompiledVsInterpreted<bool>("DateTime.Today >= date('1974-08-24')");
            Assert.IsTrue(CompileAndExecuteGetter<bool>("date('1974-08-24') >= date('1974-08-24')"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("date('1974-08-24') >= DateTime.Today"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("DateTime.Today >= date('1974-08-24')"));
        }

        /// <summary>
        /// Tests IN operator.
        /// </summary>
        [Test]
        public void TestInOperator()
        {
            Assert.IsTrue(CompileAndExecuteGetter<bool>("3 in {1, 2, 3, 4, 5}"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("!(3 in {1, 2, 3, 4, 5})"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("'xyz' in new string[] {'abc', 'xyz'}"));

                   // todo: error: operator in does not make any sense for dictionary ..... ------------------------------------------
            Assert.IsFalse(
                CompileAndExecuteGetter<bool>("'xyz' in #{'abc' : 'Value 1', 'xyz2' : 'Value 2'}"));
            Assert.IsTrue(
                CompileAndExecuteGetter<bool>("'xyz' in #{'abc' : 'Value 1', 'xyz' : DateTime.Today}"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null in null"));
        }

        /// <summary>
        /// Tests IS operator.
        /// </summary>
        [Test]
        public void TestIsOperator()
        {
            TypeRegistry.RegisterType(typeof(IList));
            TypeRegistry.RegisterType(typeof(IList<>));
            TypeRegistry.RegisterType(typeof(IDictionary));

            Assert.IsFalse(CompileAndExecuteGetter<bool>("null is null"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("5 is null"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null is int"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("5 is int"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("!(5 is int)"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("{1, 2, 3, 4, 5} is IList"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("new string[] {'abc', 'xyz'} is T(string[])"));
            Assert.IsTrue(
                CompileAndExecuteGetter<bool>("#{'abc' : 'Value 1', 'xyz' : DateTime.Today} is IDictionary"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("{1, 2, 3, 4, 5} is T(IList<int>)"));

            TypeRegistry.RegisterType(typeof(IDictionary<,>));
            Assert.IsTrue(
                CompileAndExecuteGetter<bool>("#{'abc' : 1, 'xyz' : 2} is typeof(IDictionary<string, int>)"));


            // Keys and values unify independently: uniform keys survive mixed values into a
            // Dictionary<string, object> mid-tree, and uniform values survive mixed keys likewise.
            Assert.IsTrue(
                CompileAndExecuteGetter<bool>("#{'abc' : 'Value 1', 'xyz' : DateTime.Today} is typeof(IDictionary<string, object>)"));
            Assert.IsTrue(
                CompileAndExecuteGetter<bool>("#{'abc' : 1, DateTime.Today : 2} is typeof(IDictionary<object, int>)"));
        }

        /// <summary>
        /// Tests BETWEEN operator.
        /// </summary>
        [Test]
        public void TestBetweenOperator()
        {
            Assert.IsFalse(CompileAndExecuteGetter<bool>("0 between {1, 5}"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("1 between {1, 5}"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("3.4m between {1.2m, 5.3m}"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("5 between {1, 5}"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("6 between {1, 5}"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("!(6 between {1, 5})"));
            Assert.IsTrue(
                CompileAndExecuteGetter<bool>("DateTime.Today between {DateTime.Today, DateTime.Now}"));
            Assert.IsFalse(
                CompileAndExecuteGetter<bool>("DateTime.Today between {DateTime.Now, DateTime.Now}"));
            Assert.IsTrue(CompileAndExecuteGetter<bool>("'efg' between {'abc', 'xyz'}"));
            Assert.IsFalse(CompileAndExecuteGetter<bool>("null between {1, 5}"));
        }




        // todo: error: variables? może ExpandoOBject?
        // tood: error; variables - może zapisywać typ variable? ale co to da? skoro można dodać nową variable? a przecież można?




















        #region Helpers

        private static Inventor GetTesla()
        {
            return new Inventor("Nikola Tesla", new DateTime(1856, 7, 9), "Serbian")
            {
                Inventions = new[]
                {
                    "Telephone repeater", "Rotating magnetic field principle",
                    "Polyphase alternating-current system", "Induction motor",
                    "Alternating-current power transmission", "Tesla coil transformer",
                    "Wireless communication", "Radio", "Fluorescent lights"
                },
                PlaceOfBirth =
                {
                    City = "Smiljan"
                }
            };
        }

        private static Inventor GetPulpin()
        {
            return new Inventor("Mihajlo Pupin", new DateTime(1854, 10, 9), "Serbian")
            {
                Inventions = new[] { "Long distance telephony & telegraphy", "Secondary X-Ray radiation", "Sonar" },
                PlaceOfBirth =
                {
                    City = "Idvor",
                    Country = "Serbia"
                }
            };
        }

        private static Society GetIEEE(out Inventor tesla, out Inventor pupin)
        {
            tesla = GetTesla();
            pupin = GetPulpin();
            var ieee = new Society();
            ieee.Members.Add(tesla);
            ieee.Members.Add(pupin);
            ieee.Officers["president"] = pupin;
            ieee.Officers["advisors"] = new[] { tesla, pupin };

            return ieee;
        }

        class MethodInvocationCases
        {
            public string Foo(string stringArg) { return stringArg; }
            public int Foo(int intArg) { return intArg; }
        }

        #endregion
    }
}


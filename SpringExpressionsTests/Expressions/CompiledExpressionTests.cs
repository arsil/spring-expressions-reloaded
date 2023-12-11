using NUnit.Framework;
using System;

using System.Collections.Generic;
using System.Dynamic;
using System.IO;

using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using SpringCore;
using SpringCore.TypeResolution;
using SpringExpressions;
using SpringExpressions.Parser.antlr;

using Expression = SpringExpressions.Expression;
using System.Collections;
using System.Text.RegularExpressions;

namespace SpringExpressionsTests.Expressions
{
    [TestFixture]
    public sealed class CompiledExpressionTests
    {
        [SetUp]
        public void SetUp()
        {
            TypeRegistry.RegisterType("Society", typeof(Society));
        }

        /// <summary>
        /// This test ensures, that the default node-type is serializable.
        /// </summary>
        /// <remarks>
        /// date() is parsed into DateLiteralNode( down:&lt;default node type&gt; ).
        /// Normally antlr.CommonAST is the default node used by antlr. To enable serialization, Spring
        /// uses a custom ASTFactory in <see cref="Expression.Parse"/>
        /// </remarks>
        [Test]
        public void ExpressionDateLiteralNodeMaintainsStateAfterSerialization()
        {
            var exp = CompileGetter<DateTime>("date('08-24-1974', 'MM-dd-yyyy')");

            Assert.AreEqual(new DateTime(1974, 8, 24), exp.GetValue());

            exp = SerializeDeserializeExpression(exp);

            Assert.AreEqual(new DateTime(1974, 8, 24), exp.GetValue());
        }

        private static T SerializeDeserializeExpression<T>(T expression)
        {
            byte[] data;
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, expression);
                ms.Flush();
                data = ms.ToArray();
            }

            using (MemoryStream ms = new MemoryStream(data))
            {
                expression = (T)formatter.Deserialize(ms);
            }

            return expression;
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
                "IssueId", CompileOptions.MustUseInterpreter);
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

        [Test(Description = "http://jira.springframework.org/browse/SPRNET-944")]
        public void TestDateVariableExpression()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["date"] = "2008-05-15";
            var expr = CompileGetter<string>("#date");
            Assert.That(expr.GetValue(vars), Is.EqualTo("2008-05-15"));
        }

        [Test(Description = "http://jira.springframework.org/browse/SPRNET-1155")]
        public void TestDateVariableExpressionCamelCased()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["Date"] = "2008-05-15";
            var expr = CompileGetter<string>("#Date");
            Assert.That(expr.GetValue(vars), Is.EqualTo("2008-05-15"));
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
            Assert.IsTrue(CompileGetter<bool>("100 < 1000.00 and 10000.00m > 1e2").GetValue());
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
                CompileOptions.CompileOnParse | CompileOptions.MustCompile);

            setterExpression.SetValue(inventor, "Croatia");

            Assert.AreEqual("Croatia", CompileGetter<Inventor, string>("PlaceOfBirth.Country").GetValue(inventor));

            setterExpression.SetValue(inventor, "Biedaszyb");
            Assert.AreEqual("Biedaszyb", inventor.PlaceOfBirth.Country);

            setterExpression.SetValue(GetPulpin(), "Other object");
            Assert.AreEqual("Biedaszyb", inventor.PlaceOfBirth.Country);


            var pupin = GetPulpin();
            Assert.AreEqual("Idvor", CompileGetter<Inventor, string>("PlaceOfBirth.City").GetValue(pupin));

            var setName = Expression.ParseSetter<Inventor, string>("Name", 
                CompileOptions.CompileOnParse | CompileOptions.MustCompile);

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
                CompileOptions.CompileOnParse | CompileOptions.MustCompile);
            setterExpression.SetValue(tesla, "Croatia");

            Assert.AreEqual("Croatia", CompileGetter<Inventor, string>("Placeofbirth.COUNtry").GetValue(tesla));

            setterExpression = Expression.ParseSetter<Inventor, string>("NAME",
                CompileOptions.CompileOnParse | CompileOptions.MustCompile);
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
                CompileOptions.CompileOnParse | CompileOptions.MustCompile);
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
                    CompileOptions.CompileOnParse | CompileOptions.MustCompile);

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
            try
            {
                CompileGetter<ShadowingTestsMostSpezializedClass, string>("WriteonlyShadowedValue");
                Assert.Fail("Getting writeonly property should throw NotReadablePropertyException");
            }
            catch (NotReadablePropertyException)
            { }
        }


        /// <summary>
        /// Tests indexed property and field accessors and mutators
        /// </summary>
        [Test]
        public void TestIndexedPropertyAccess()
        {
            TypeRegistry.RegisterType("Society", typeof(Society));

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
            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers['president']").GetValue(ieee));
            

            Assert.AreEqual("Idvor", 
                CompileGetter<Society, string>("Officers['president'].PlaceOfBirth.City").GetValue(ieee));

            Assert.AreEqual(tesla, CompileGetter<Society, Inventor>("Officers['advisors'][0]").GetValue(ieee));

            Assert.AreEqual("Polyphase alternating-current system",
                CompileGetter<Society, string>("Officers['advisors'][0].Inventions[2]").GetValue(ieee));

            // maps with non-literal parameters
            Dictionary<string, object> vars = new Dictionary<string, object>();
            vars["prez"] = "president";
            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers[#prez]").GetValue(ieee, vars));

            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers[Society.President]").GetValue(ieee));
            Assert.AreEqual("Idvor",
                CompileGetter<Society, string>("Officers[Society.President].PlaceOfBirth.City").GetValue(ieee));
            Assert.AreEqual(tesla, CompileGetter<Society, Inventor>("Officers[Society.Advisors][0]").GetValue(ieee));
            Assert.AreEqual("Polyphase alternating-current system",
                CompileGetter<Society, string>("Officers[Society.Advisors][0].Inventions[2]").GetValue(ieee));


            // try to set some values
            // setter for: ExpressionEvaluator.SetValue(ieee, "Officers['advisors'][0].PlaceOfBirth.Country", "Croatia");
            Expression.ParseSetter<Society, string>("Officers['advisors'][0].PlaceOfBirth.Country",
                    CompileOptions.CompileOnParse | CompileOptions.MustCompile)
                .SetValue(ieee, "Croatia");
            Assert.AreEqual("Croatia", CompileGetter<Inventor, string>("PlaceOfBirth.Country").GetValue(tesla));

            // setter for: ExpressionEvaluator.SetValue(ieee, "Officers['president'].Name", "Michael Pupin");
            Expression.ParseSetter<Society, string>("Officers['president'].Name", 
                    CompileOptions.CompileOnParse | CompileOptions.MustCompile)
                .SetValue(ieee, "Michael Pupin");

            Assert.AreEqual("Michael Pupin", CompileGetter<Inventor, string>("Name").GetValue(pupin));

            // setter for: ExpressionEvaluator.SetValue(ieee, "Officers['advisors']", new [] { pupin, tesla });
            Expression.ParseSetter<Society, Inventor[]>("Officers['advisors']",
                    CompileOptions.CompileOnParse | CompileOptions.MustCompile)
                .SetValue(ieee, new[] { pupin, tesla });

            Assert.AreEqual(pupin, CompileGetter<Society, Inventor>("Officers['advisors'][0]").GetValue(ieee));
            Assert.AreEqual(tesla, CompileGetter<Society, Inventor>("Officers['advisors'][1]").GetValue(ieee));

            // generic indexer
            var bar = new Bar();
            var exp = CompileGetter<Bar,object>("[1]");
            Assert.AreEqual(2, exp.GetValue(bar));
            Assert.AreEqual(2, exp.GetValue(bar));

            var foo = new Foo();
            Assert.AreEqual("test_1", CompileGetter<Foo, object>("[1, 'test']").GetValue(foo));
        }

        /// <summary>
        /// Tests indexer access with invalid number of indices
        /// </summary>
        [Test]
        public void TestIndexedPropertyAccessWithInvalidNumberOfIndices()
        {
            Assert.Throws<InvalidPropertyException>(
                () => CompileGetter<Inventor, object>("Inventions[3, 2]"));
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

             // todo: error: fixme!
        [Test]
        public void TestMethodEvaluationOnDifferentContextType()
        {
            var expression = CompileGetter<object, object>("ToString('dummy', null)");
            Assert.AreEqual("dummy", expression.GetValue(0m));
            Assert.AreEqual("dummy", expression.GetValue(0));
        }

              // todo: error; won't run!!!!! not fixable????
        [Test]
        public void TestMethodEvaluationOnDifferentArgumentTypes()
        {
            var expression = CompileGetter<MethodInvocationCases, object>("Foo(#var1)");

            var testContext = new MethodInvocationCases();
            var args = new Dictionary<string, object>();

            args["var1"] = "myString";
            Assert.AreEqual("myString", expression.GetValue(testContext, args));

            args["var1"] = 12;
            Assert.AreEqual(12, expression.GetValue(testContext, args));
        }

          // todo: error fixme! wrong exception!
        /// <summary>
        /// Tests missing method accessors
        /// </summary>
        [Test]
        public void TestMissingMethodAccess()
        {
            Assert.Throws<ArgumentException>(
                () => CompileGetter<string, string>("ToStringilyLingily()"));
        }

        /// <summary>
        /// Tests projection node
        /// </summary>
        [Test]
        public void TestProjection()
        {
            var ieee = GetIEEE(out _, out _);
            var placesOfBirth = CompileGetter<Society, IList>("Members.!{PlaceOfBirth.City}").GetValue(ieee);

            Assert.AreEqual(2, placesOfBirth.Count);
            Assert.AreEqual("Smiljan", placesOfBirth[0]);
            Assert.AreEqual("Idvor", placesOfBirth[1]);

            // todo: error: CAST
            var names = CompileGetter<Society, IList>("Officers['advisors'].!{Name}").GetValue(ieee);
            Assert.AreEqual(2, names.Count);
            Assert.AreEqual("Nikola Tesla", names[0]);
            Assert.AreEqual("Mihajlo Pupin", names[1]);
        }

        /// <summary>
        /// Tests selection node
        /// </summary>
        [Test]
        public void TestSelection()
        {
            var ieee = GetIEEE(out _, out _);

            var memberSelection =
                CompileGetter<Society, IList>("Members.?{PlaceOfBirth.City == 'Smiljan'}").GetValue(ieee);

            Assert.AreEqual(1, memberSelection.Count);
            Assert.AreEqual("Nikola Tesla", ((Inventor)memberSelection[0]).Name);

            var serbianOfficers =
                CompileGetter<Society, IList>("Officers['advisors'].?{Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual(2, serbianOfficers.Count);
            Assert.AreEqual("Nikola Tesla", ((Inventor)serbianOfficers[0]).Name);
            Assert.AreEqual("Mihajlo Pupin", ((Inventor)serbianOfficers[1]).Name);

                  // todo: error? implement or not!!!!!
            var first =
                CompileGetter<Society, Inventor>("Officers['advisors'].^{Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual("Nikola Tesla", first.Name);

            var last =
                CompileGetter<Society, Inventor>("Officers['advisors'].${Nationality == 'Serbian'}").GetValue(ieee);
            Assert.AreEqual("Mihajlo Pupin", last.Name);
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
                    CompileOptions.MustUseInterpreter)
                .GetValue());

            // implicit casting from decimal to double
            Assert.AreEqual(new DateTime(1974, 8, 24),
                CompileGetter<DateTime>("new DateTime(2004, 8, 14).AddDays(10m).AddYears(-30)").GetValue());
        }

        [Test]
        public void TestConstructorWithNamedArguments()
        {
            TypeRegistry.RegisterType(typeof(Inventor));

                  // todo: error: FIXME works incidentally!!!!
            // test named arguments
            var ana = CompileGetter<Inventor>(
                    "new Inventor(Name = 'Ana Maria Seovic', DOB = date('2004-08-14'), Nationality = 'American')")
                .GetValue();
            Assert.AreEqual("Ana Maria Seovic", ana.Name);
            Assert.AreEqual(new DateTime(2004, 8, 14), ana.DOB);
            Assert.AreEqual("American", ana.Nationality);

                   // todo: error: FIXME no constructor! constructor node searches for 4 param node or something like that. NamedArguments are not part of the constructor!!!
            var aleks = CompileGetter<Inventor>(
                    "new Inventor('Aleksandar Seovic', date('1974-08-24'), 'Serbian', Inventions = {'SPELL'})")
                .GetValue();
            Assert.AreEqual("Aleksandar Seovic", aleks.Name);
            Assert.AreEqual(new DateTime(1974, 8, 24), aleks.DOB);
            Assert.AreEqual("Serbian", aleks.Nationality);
            Assert.AreEqual(1, aleks.Inventions.Length);
            Assert.AreEqual("SPELL", aleks.Inventions[0]);
        }

        /// <summary>
        /// Tests missing constructor
        /// </summary>
        [Test]
        public void TestMissingConstructor()
        {
            Assert.Throws<ArgumentException>(() => CompileGetter<decimal>("new Decimal('xyz')"));
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
                    "#root ?? 997", CompileOptions.MustUseInterpreter)
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

            // special object handling - we cast variable to assigning property/field type
            Assert.AreEqual("Ana Maria Seovic", CompileGetter<Inventor, object>("Name = #newName").GetValue(tesla, vars));

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
                    CompileOptions.CompileOnParse | CompileOptions.MustCompile));
        }

        /// <summary>
        /// Try to set 'root' variable
        /// </summary>
        [Test]
        public void TryToSetRoot()
        {
            Assert.Throws<ArgumentException>(
                () => Expression.ParseSetter<string>("#root",
                CompileOptions.CompileOnParse | CompileOptions.MustCompile));
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
                    CompileOptions.MustUseInterpreter).GetValue().GetType());


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
                    "TestEnumFlags.TwoAndFourCombined and TestEnumFlags.Four", CompileOptions.MustUseInterpreter)
                    .GetValue().GetType());

            Assert.AreEqual(
                TestEnumFlags.Four,
                Expression.ParseGetter<object>(
                        "TestEnumFlags.TwoAndFourCombined and TestEnumFlags.Four", CompileOptions.MustUseInterpreter)
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

        // todo: error: variables? może ExpandoOBject?
        // tood: error; variables - może zapisywać typ variable? ale co to da? skoro można dodać nową variable? a przecież można?




















        #region Helpers


        private static IGetterExpression<TRoot, TResult> CompileGetter<TRoot, TResult>(string expression)
        {
            return Expression.ParseGetter<TRoot, TResult>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }

        private static IGetterExpression<TResult> CompileGetter<TResult>(string expression)
        {
            return Expression.ParseGetter<TResult>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }

        private static TResult CompileAndExecuteGetter<TResult>(string expression)
            => CompileGetter<TResult>(expression).GetValue();

        private static ISetterExpression<TRoot, TArg> CompileSetter<TRoot, TArg>(string expression)
        {
            return Expression.ParseSetter<TRoot, TArg>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }

        private static ISetterExpression<TArg> CompileSetter<TArg>(string expression)
        {
            return Expression.ParseSetter<TArg>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }

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
            ieee.Officers["advisors"] = new[] { tesla, tesla };

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

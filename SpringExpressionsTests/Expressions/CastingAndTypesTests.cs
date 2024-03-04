using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringCore.TypeResolution;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    [TestFixture]
    public class CastingAndTypesTests : BaseCompiledTests
    {
        [Test]
        public void Test1()
        {
            var context = new[] { "item1", "item2" };
            var arrayGetter = CompileGetter<object, object>("#root as T(string[])");
            Assert.AreEqual(typeof(string[]), arrayGetter.GetValue(context).GetType());

            TypeRegistry.RegisterType(typeof(Inventor));
            var ieee = GetIEEE(out _, out _);

            {
                var names = InterpretGetter<Society, IList>("(Officers['advisors'] as T(SpringExpressions.Inventor[])).!{Name}")
                    .GetValue(ieee);
                Assert.AreEqual(2, names.Count);
                Assert.AreEqual("Nikola Tesla", names[0]);
                Assert.AreEqual("Mihajlo Pupin", names[1]);
            }
        }

        [Test]
        public void Test2()
        {
            {
                var doubleGetter = CompileGetter<object>("45.4");
                Assert.AreEqual(typeof(double), doubleGetter.GetValue().GetType());
                Assert.AreEqual(45.4, doubleGetter.GetValue());

                var decimalGetter = CompileGetter<object>("45.4 as T(decimal)");
                Assert.AreEqual(typeof(decimal), decimalGetter.GetValue().GetType());
                Assert.AreEqual(45.4m, decimalGetter.GetValue());
            }

            {
                var doubleGetter = InterpretGetter<object>("45.4");
                Assert.AreEqual(typeof(double), doubleGetter.GetValue().GetType());
                Assert.AreEqual(45.4, doubleGetter.GetValue());

                var decimalGetter = CompileGetter<object>("45.4 as T(decimal)");
                Assert.AreEqual(typeof(decimal), decimalGetter.GetValue().GetType());
                Assert.AreEqual(45.4m, decimalGetter.GetValue());
            }

            TestCompiledVsInterpreted<int>("45.4 as T(int)").ResultEqualsTo(45);

            {
                var context = new[] { "item1", "item2" };
                var arrayGetter = CompileGetter<object, object>("#root as T(string[])");
                Assert.AreEqual(typeof(string[]), arrayGetter.GetValue(context).GetType());

                var result = TestCompiledVsInterpreted<object, object>("#root as T(string[])", context).Result;
                Assert.AreEqual(typeof(string[]), result.GetType());
            }

            TestCompiledVsInterpreted<int[]>("null as T(int[])");
        }

        [Test]
        public void TestNullCasting()
        {
            var arrayGetter = CompileGetter<object>("null as T(int[])");

            // todo: error: fixme!!!! why this doesn't work? maybe null has no type(?)
            Assert.AreEqual(typeof(int[]), arrayGetter.GetValue());

            // todo: error: but this works?
            TestCompiledVsInterpreted<int[]>("null as T(int[])");
        }

        [Test]
        public void ResolveTypeSpecialCases()
        {
            TypeRegistry.RegisterType(typeof(List<>));
            TypeRegistry.RegisterType(typeof(Dictionary<,>));
            TypeRegistry.RegisterType(typeof(Tuple<,>));


            var dupa = new string[3, 4];
            Assert.AreEqual(3, dupa.GetLength(0));
            Assert.AreEqual(4, dupa.GetLength(1));

            // note reversed notation
            Assert.AreEqual(typeof(string[][,][,,]), Type.GetType("System.String[,,][,][]"));
            Assert.AreEqual(typeof(string[][,]), Type.GetType("System.String[,][]"));


            // jagged: multi-dim array of arrays 
            Assert.AreEqual("System.String[][,]", typeof(string[,][]).ToString());
            Assert.AreEqual("String[][,]", typeof(string[,][]).Name);
            Assert.AreEqual(typeof(string[,][]), TestCompiledVsInterpreted<object>("T(string[,][])").Result);


            {
                // .Name is useless... 
                Assert.AreEqual("List`1[,]", typeof(List<string>[,]).Name);

                Assert.AreEqual("System.Collections.Generic.List`1[System.String][,]", 
                    typeof(List<string>[,]).ToString());

                Assert.IsTrue(GenericArgumentsHolder.TryCreateGenericArgumentsHolder(
                    "List`1[string][,]", out var genericArgs));

                Assert.AreEqual("List`1", genericArgs.GenericTypeName);
                Assert.IsTrue(genericArgs.ContainsGenericArguments);
                Assert.AreEqual(1, genericArgs.GetGenericArguments().Length);
                Assert.AreEqual("string", genericArgs.GetGenericArguments()[0]);

                Assert.AreEqual(true, genericArgs.IsArrayDeclaration);
                Assert.AreEqual("[,]", genericArgs.GetArrayDeclaration());
            }

            {
                Assert.IsTrue(GenericArgumentsHolder.TryCreateGenericArgumentsHolder(
                    "List<string>[,]", out var genericArgs));

                Assert.AreEqual("List`1", genericArgs.GenericTypeName);
                Assert.IsTrue(genericArgs.ContainsGenericArguments);
                Assert.AreEqual(1, genericArgs.GetGenericArguments().Length);
                Assert.AreEqual("string", genericArgs.GetGenericArguments()[0]);

                Assert.AreEqual(true, genericArgs.IsArrayDeclaration);
                Assert.AreEqual("[,]", genericArgs.GetArrayDeclaration());
            }

            {
                // .Name is useless
                Assert.AreEqual("Dictionary`2", typeof(Dictionary<string[], List<Tuple<int, string>>[,]>).Name);
                Assert.AreEqual(
                    "System.Collections.Generic.Dictionary`2[" +
                        "System.String[]," +
                        "System.Collections.Generic.List`1[" +
                        "System.Tuple`2[System.Int32,System.String]][,]" +
                        "]", 
                    typeof(Dictionary<string[], List<Tuple<int, string>>[,]>).ToString());


                Assert.IsTrue(GenericArgumentsHolder.TryCreateGenericArgumentsHolder(
                    "System.Collections.Generic.Dictionary`2[" +
                    "System.String[]," +
                    "System.Collections.Generic.List`1[" +
                    "System.Tuple`2[System.Int32,System.String]][,]" +
                    "]", out var genericArgs));

                Assert.AreEqual("System.Collections.Generic.Dictionary`2", genericArgs.GenericTypeName);
                Assert.IsTrue(genericArgs.ContainsGenericArguments);
                Assert.AreEqual(2, genericArgs.GetGenericArguments().Length);
                Assert.AreEqual("System.String[]", genericArgs.GetGenericArguments()[0]);
                Assert.AreEqual("System.Collections.Generic.List`1[System.Tuple`2[System.Int32,System.String]][,]", genericArgs.GetGenericArguments()[1]);

                Assert.AreEqual(false, genericArgs.IsArrayDeclaration);


                Assert.AreEqual(typeof(Dictionary<string[], List<Tuple<int, string>>[,]>), 
                    TypeResolutionUtils.ResolveType(
                        "System.Collections.Generic.Dictionary`2[" +
                        "System.String[]," +
                        "System.Collections.Generic.List`1[" +
                        "System.Tuple`2[System.Int32,System.String]][,]" +
                        "]"));

                // aliases
                Assert.AreEqual(typeof(Dictionary<string[], List<Tuple<int, string>>[,]>),
                    TypeResolutionUtils.ResolveType(
                        "Dictionary<string[], List<Tuple<int, string>>[,]>"));

            }

            {
                // .Name is useless...
                Assert.AreEqual("List`1", typeof(List<>).Name);

                Assert.AreEqual("System.Collections.Generic.List`1[T]",
                    typeof(List<>).ToString());

                Assert.AreEqual("System.Collections.Generic.Dictionary`2[TKey,TValue]",
                    typeof(Dictionary<,>).ToString());


                Assert.IsTrue(GenericArgumentsHolder.TryCreateGenericArgumentsHolder(
                    "Dictionary<,>", out var genericArgs));

                Assert.AreEqual("Dictionary`2", genericArgs.GenericTypeName);
                Assert.IsTrue(genericArgs.ContainsGenericArguments);
                Assert.AreEqual(2, genericArgs.GetGenericArguments().Length);
                Assert.AreEqual("", genericArgs.GetGenericArguments()[0]);
                Assert.AreEqual("", genericArgs.GetGenericArguments()[1]);

                Assert.AreEqual(false, genericArgs.IsArrayDeclaration);

                Assert.AreEqual(typeof(Dictionary<,>), TypeResolutionUtils.ResolveType("Dictionary<,>"));
                Assert.AreEqual(typeof(List<>), TypeResolutionUtils.ResolveType("List<>"));
            }

            var test1 = new List<string>[2,5];
            Assert.AreEqual("List`1[,]", test1.GetType().Name);


            Assert.AreEqual(typeof(List<string>[,]), Type.GetType("System.Collections.Generic.List`1[System.String][,]"));
            Assert.AreEqual("System.Collections.Generic.List`1[System.String][,]", typeof(List<string>[,]).ToString());

            Assert.AreEqual(
                typeof(List<string>[,]).Name, 
                TypeResolutionUtils.ResolveType("System.Collections.Generic.List`1[System.String][,]").Name);

            Assert.AreEqual(
                typeof(List<string>[,]).Name,
                TypeResolutionUtils.ResolveType("System.Collections.Generic.List`1[string][,]").Name);

            Assert.AreEqual(
                typeof(List<string>[,]).Name,
                TypeResolutionUtils.ResolveType("System.Collections.Generic.List<string>[,]").Name);



            Assert.AreEqual(
                typeof(List<string>[,]).Name,
                TypeResolutionUtils.ResolveType("List`1[string][,]").Name);

            Assert.AreEqual(
                typeof(List<string>[,]),
                TypeResolutionUtils.ResolveType("List`1[string][,]"));

            Assert.AreEqual("List`1[,]", TypeResolutionUtils.ResolveType("List<string>[,]").Name);

            Assert.AreEqual(typeof(List<string>[,]).Name, TypeResolutionUtils.ResolveType("List<string>[,]").Name);


            Assert.AreEqual(typeof(List<string>[,]), TestCompiledVsInterpreted<object>("T(List<string>[,])").Result);

            {
                Assert.IsTrue(ArrayArgumentHolder.TryCreateArrayArgumentHolder("string[]", out var arrayArgs));
                Assert.AreEqual("[]", arrayArgs.ArrayDeclaration);
                Assert.AreEqual("string", arrayArgs.ArrayItemTypeName);
            }
            {
                Assert.IsTrue(ArrayArgumentHolder.TryCreateArrayArgumentHolder("string[,]", out var arrayArgs));
                Assert.AreEqual("[,]", arrayArgs.ArrayDeclaration);
                Assert.AreEqual("string", arrayArgs.ArrayItemTypeName);
            }


            Assert.AreEqual(typeof(string[,]), TestCompiledVsInterpreted<object>("T(string[,])").Result);
            
            // jagged: multi-dim array of arrays 
            Assert.AreEqual(typeof(string[,][]), TestCompiledVsInterpreted<object>("T(string[,][])").Result);

            // jagged: array of multi-dim arrays
            Assert.AreEqual(typeof(string[][,]), TestCompiledVsInterpreted<object>("T(string[][,])").Result);


            Assert.AreEqual(typeof(List<string>[]), TestCompiledVsInterpreted<object>("T(List<string>[])").Result);

            Assert.AreEqual(typeof(string[]), TestCompiledVsInterpreted<object>("T(string[])").Result);

            Assert.AreEqual(typeof(List<string>[,][]), TestCompiledVsInterpreted<object>("T(List<string>[,][])").Result);
        }

        [Test]
        public void ResolveTypeInNewOperatorSpecialCases()
        {
            Assert.AreEqual(typeof(string[,]), TestCompiledVsInterpreted<object>("new string[2, 4]").Result.GetType());


            {
                var array = new string[2, 4][];
                Assert.AreEqual("String[][,]", array.GetType().Name);

                // todo: error: fixme - parse error!
                //Assert.AreEqual(typeof(string[,][]),
                //    TestCompiledVsInterpreted<object>("new string[2, 4][]").Result.GetType());
            }

            {
                var array = new List<string>();
                Assert.AreEqual("System.Collections.Generic.List`1[System.String]", array.GetType().ToString());

                // todo: error: fixme - parse error!
                //Assert.AreEqual(typeof(List<string>),
                //    TestCompiledVsInterpreted<object>("new List<string>()").Result.GetType());

                // illegal as it should be
                // Assert.AreEqual(typeof(List<string>),
                //    TestCompiledVsInterpreted<object>("new List`1[string]()").Result.GetType());

                // also illegal
                //Assert.AreEqual(typeof(List<string>),
                //    TestCompiledVsInterpreted<object>("new T(List<string>)()").Result.GetType());

            }

        }

        [Test]
        public void ProjectionSelectionTests()
        {
            TypeRegistry.RegisterType(typeof(Inventor));

            var ieee = GetIEEE(out _, out _);

            {
                var names = InterpretGetter<Society, IList>("(Officers['advisors'] as T(SpringExpressions.Inventor[])).!{Name}")
                    .GetValue(ieee);
                Assert.AreEqual(2, names.Count);
                Assert.AreEqual("Nikola Tesla", names[0]);
                Assert.AreEqual("Mihajlo Pupin", names[1]);
            }

            {
                var names = InterpretGetter<Society, IList>("(Officers['advisors'] as T(Inventor[])).!{Name}")
                    .GetValue(ieee);
                Assert.AreEqual(2, names.Count);
                Assert.AreEqual("Nikola Tesla", names[0]);
                Assert.AreEqual("Mihajlo Pupin", names[1]);
            }
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
            ieee.Officers["advisors"] = new[] { tesla, pupin };

            return ieee;
        }
    }
}

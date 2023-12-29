using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    [TestFixture]
    public class NullableMathTests : BaseCompiledTests
    {
        [Test]
        public void DivTest()
        {

            // todo: error: debug for exception!!!------------------------------------------------------------------------------------------------------------------------
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            Assert.AreEqual(typeof(decimal), (64.3m / 3).GetType());
            Assert.AreEqual(typeof(decimal), (3 / 3m).GetType());

            Assert.AreEqual(typeof(double), (3 / 3.3).GetType());
            Assert.AreEqual(typeof(double), (3.4 / 3).GetType());

            Assert.AreEqual(typeof(float), (3 / 3.3f).GetType());
            Assert.AreEqual(typeof(float), (3.4f / 3).GetType());

            Assert.AreEqual(typeof(decimal), CompileGetter<object>("64.3m / 3").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("3 / 3m").GetValue().GetType());
            TestCompiledVsInterpreted<decimal>("64.3m / 3").ResultEqualsTo(64.3m / 3);
            TestCompiledVsInterpreted<decimal>("3 / 3m").ResultEqualsTo(3 / 3m);

            Assert.AreEqual(typeof(double), CompileGetter<object>("3 / 3.3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("3.3 / 3").GetValue().GetType());
            TestCompiledVsInterpreted<double>("3 / 3.3").ResultEqualsTo(3 / 3.3);
            TestCompiledVsInterpreted<double>("3.3 / 3").ResultEqualsTo(3.3 / 3);

            Assert.AreEqual(typeof(float), CompileGetter<object>("3 / 3.3f").GetValue().GetType());
            Assert.AreEqual(typeof(float), CompileGetter<object>("3.3f / 3").GetValue().GetType());
            TestCompiledVsInterpreted<float>("3 / 3.3f").ResultEqualsTo(3.0f / 3.3f);
            TestCompiledVsInterpreted<float>("3.3f / 3").ResultEqualsTo(3.3f / 3.0f);

            #pragma warning disable CS0458
            Assert.AreEqual(null, 64.3m / null);
            #pragma warning restore CS0458

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };
                Assert.AreEqual(null, ctxDecDec.Value1 / ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("Value1 / Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("Value1 / Value2", ctxDecDec);
            }

            {
                var ctxDecInt = new NHolder2<decimal, int> { Value1 = null, Value2 = 3 };
                Assert.AreEqual(null, (ctxDecInt.Value1 / ctxDecInt.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 / Value2").GetValue(ctxDecInt));

                ctxDecInt.Value1 = 4m;
                ctxDecInt.Value2 = 2;
                Assert.AreEqual(typeof(decimal), (ctxDecInt.Value1 / ctxDecInt.Value2).GetType());
                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 / Value2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(2m,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 / Value2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 / Value2", ctxDecInt)
                    .ResultEqualsTo(2m);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 / 2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(2m,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 / 2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 / 2", ctxDecInt)
                    .ResultEqualsTo(2m);
            }

            {
                var ctxIntDec = new NHolder2<int, decimal> { Value1 = 666, Value2 = null };
                Assert.AreEqual(null, (ctxIntDec.Value1 / ctxIntDec.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 / Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, object>("Value1 / Value2", ctxIntDec);

                ctxIntDec.Value2 = 2.5m;
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 / ctxIntDec.Value2).GetType());
                Assert.AreEqual(266.4m, ctxIntDec.Value1 / ctxIntDec.Value2);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 / Value2").GetValue(ctxIntDec).GetType());
                Assert.AreEqual(266.4m,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 / Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 / Value2", ctxIntDec)
                    .ResultEqualsTo(266.4m);

                Assert.AreEqual(266.4m, 666 / ctxIntDec.Value2);
                Assert.AreEqual(typeof(decimal), (666 / ctxIntDec.Value2).GetType());

                Assert.AreEqual(266.4m,
                    CompileGetter<NHolder2<int, decimal>, object>("666 / Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("666 / Value2", ctxIntDec)
                    .ResultEqualsTo(266.4m);

                Assert.AreEqual(266.4m, ctxIntDec.Value1 / 2.5m);
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 / 2.5m).GetType());

                Assert.AreEqual(266.4m,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 / 2.5m").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 / 2.5m", ctxIntDec)
                    .ResultEqualsTo(266.4m);
            }

            // illegal
            // Assert.AreEqual(typeof(decimal), (64.3m / 3.6).GetType());
            // Assert.AreEqual(typeof(decimal), (3.45 / 3m).GetType());
        }


        [Test]
        public void AddTest()
        {
            Assert.AreEqual(typeof(decimal), (64.3m + 3).GetType());
            Assert.AreEqual(typeof(decimal), (3 + 3m).GetType());

            Assert.AreEqual(typeof(double), (3 + 3.3).GetType());
            Assert.AreEqual(typeof(double), (3.4 + 3).GetType());

            Assert.AreEqual(typeof(float), (3 + 3.3f).GetType());
            Assert.AreEqual(typeof(float), (3.4f + 3).GetType());

            Assert.AreEqual(typeof(decimal), CompileGetter<object>("64.3m + 3").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("3 + 3m").GetValue().GetType());
            TestCompiledVsInterpreted<decimal>("64.3m + 3").ResultEqualsTo(64.3m + 3);
            TestCompiledVsInterpreted<decimal>("3 + 3m").ResultEqualsTo(3 + 3m);

            Assert.AreEqual(typeof(double), CompileGetter<object>("3 + 3.3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("3.3 + 3").GetValue().GetType());
            TestCompiledVsInterpreted<double>("3 + 3.3").ResultEqualsTo(3 + 3.3);
            TestCompiledVsInterpreted<double>("3.3 + 3").ResultEqualsTo(3.3 + 3);

            Assert.AreEqual(typeof(float), CompileGetter<object>("3 + 3.3f").GetValue().GetType());
            Assert.AreEqual(typeof(float), CompileGetter<object>("3.3f + 3").GetValue().GetType());
            TestCompiledVsInterpreted<float>("3 + 3.3f").ResultEqualsTo(3.0f + 3.3f);
            TestCompiledVsInterpreted<float>("3.3f + 3").ResultEqualsTo(3.3f + 3.0f);

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };
                Assert.AreEqual(null, ctxDecDec.Value1 / ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("Value1 + Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("Value1 + Value2", ctxDecDec);
            }

            {
                var ctxDecInt = new NHolder2<decimal, int> { Value1 = null, Value2 = 3 };
                Assert.AreEqual(null, (ctxDecInt.Value1 + ctxDecInt.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 + Value2").GetValue(ctxDecInt));

                ctxDecInt.Value1 = 4m;
                ctxDecInt.Value2 = 2;
                Assert.AreEqual(typeof(decimal), (ctxDecInt.Value1 + ctxDecInt.Value2).GetType());
                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 + Value2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(6m,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 + Value2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 + Value2", ctxDecInt)
                    .ResultEqualsTo(6m);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 + 2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(6m,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 + 2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 + 2", ctxDecInt)
                    .ResultEqualsTo(6m);
            }

            {
                var ctxIntDec = new NHolder2<int, decimal> { Value1 = 666, Value2 = null };
                Assert.AreEqual(null, (ctxIntDec.Value1 + ctxIntDec.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 + Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, object>("Value1 + Value2", ctxIntDec);

                ctxIntDec.Value2 = 2.5m;
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 + ctxIntDec.Value2).GetType());
                Assert.AreEqual(668.5m, ctxIntDec.Value1 + ctxIntDec.Value2);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 + Value2").GetValue(ctxIntDec).GetType());
                Assert.AreEqual(668.5m,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 + Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 + Value2", ctxIntDec)
                    .ResultEqualsTo(668.5m);

                Assert.AreEqual(668.5m, 666 + ctxIntDec.Value2);
                Assert.AreEqual(typeof(decimal), (666 + ctxIntDec.Value2).GetType());

                Assert.AreEqual(668.5m,
                    CompileGetter<NHolder2<int, decimal>, object>("666 + Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("666 + Value2", ctxIntDec)
                    .ResultEqualsTo(668.5m);

                Assert.AreEqual(668.5m, ctxIntDec.Value1 + 2.5m);
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 + 2.5m).GetType());

                Assert.AreEqual(668.5m,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 + 2.5m").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 + 2.5m", ctxIntDec)
                    .ResultEqualsTo(668.5m);
            }
        }

        [Test]
        public void SubTest()
        {
            Assert.AreEqual(typeof(decimal), (64.3m - 3).GetType());
            Assert.AreEqual(typeof(decimal), (3 - 3m).GetType());

            Assert.AreEqual(typeof(double), (3 - 3.3).GetType());
            Assert.AreEqual(typeof(double), (3.4 - 3).GetType());

            Assert.AreEqual(typeof(float), (3 - 3.3f).GetType());
            Assert.AreEqual(typeof(float), (3.4f - 3).GetType());

            Assert.AreEqual(typeof(decimal), CompileGetter<object>("64.3m - 3").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("3 - 3m").GetValue().GetType());
            TestCompiledVsInterpreted<decimal>("64.3m - 3").ResultEqualsTo(64.3m - 3);
            TestCompiledVsInterpreted<decimal>("3 - 3m").ResultEqualsTo(3 - 3m);

            Assert.AreEqual(typeof(double), CompileGetter<object>("3 - 3.3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("3.3 - 3").GetValue().GetType());
            TestCompiledVsInterpreted<double>("3 - 3.3").ResultEqualsTo(3 - 3.3); ;
            TestCompiledVsInterpreted<double>("3.3 - 3").ResultEqualsTo(3.3 - 3); ;

            Assert.AreEqual(typeof(float), CompileGetter<object>("3 - 3.3f").GetValue().GetType());
            Assert.AreEqual(typeof(float), CompileGetter<object>("3.3f - 3").GetValue().GetType());
            TestCompiledVsInterpreted<float>("3 - 3.3f").ResultEqualsTo(3.0f - 3.3f);
            TestCompiledVsInterpreted<float>("3.3f - 3").ResultEqualsTo(3.3f - 3.0f);

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };
                Assert.AreEqual(null, ctxDecDec.Value1 / ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("Value1 - Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("Value1 - Value2", ctxDecDec);
            }

            {
                var ctxDecInt = new NHolder2<decimal, int> { Value1 = null, Value2 = 3 };
                Assert.AreEqual(null, (ctxDecInt.Value1 - ctxDecInt.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 - Value2").GetValue(ctxDecInt));

                ctxDecInt.Value1 = 4m;
                ctxDecInt.Value2 = 2;
                Assert.AreEqual(typeof(decimal), (ctxDecInt.Value1 - ctxDecInt.Value2).GetType());
                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 - Value2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(2m,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 - Value2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 - Value2", ctxDecInt)
                    .ResultEqualsTo(2m);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 - 2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(2m,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 - 2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 - 2", ctxDecInt)
                    .ResultEqualsTo(2m);
            }

            {
                var ctxIntDec = new NHolder2<int, decimal> { Value1 = 666, Value2 = null };
                Assert.AreEqual(null, (ctxIntDec.Value1 - ctxIntDec.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 - Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, object>("Value1 - Value2", ctxIntDec);

                ctxIntDec.Value2 = 2.5m;
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 - ctxIntDec.Value2).GetType());
                Assert.AreEqual(663.5m, ctxIntDec.Value1 - ctxIntDec.Value2);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 - Value2").GetValue(ctxIntDec).GetType());
                Assert.AreEqual(663.5m,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 - Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 - Value2", ctxIntDec)
                    .ResultEqualsTo(663.5m);

                Assert.AreEqual(663.5m, 666 - ctxIntDec.Value2);
                Assert.AreEqual(typeof(decimal), (666 - ctxIntDec.Value2).GetType());

                Assert.AreEqual(663.5m,
                    CompileGetter<NHolder2<int, decimal>, object>("666 - Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("666 - Value2", ctxIntDec)
                    .ResultEqualsTo(663.5m);

                Assert.AreEqual(663.5m, ctxIntDec.Value1 - 2.5m);
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 - 2.5m).GetType());

                Assert.AreEqual(663.5m,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 - 2.5m").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 - 2.5m", ctxIntDec)
                    .ResultEqualsTo(663.5m);
            }
        }

        [Test]
        public void MultTest()
        {
            Assert.AreEqual(typeof(decimal), (64.3m * 3).GetType());
            Assert.AreEqual(typeof(decimal), (3 * 3m).GetType());

            Assert.AreEqual(typeof(double), (3 * 3.3).GetType());
            Assert.AreEqual(typeof(double), (3.4 * 3).GetType());

            Assert.AreEqual(typeof(float), (3 * 3.3f).GetType());
            Assert.AreEqual(typeof(float), (3.4f * 3).GetType());

            Assert.AreEqual(typeof(decimal), CompileGetter<object>("64.3m * 3").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("3 * 3m").GetValue().GetType());
            TestCompiledVsInterpreted<decimal>("64.3m * 3").ResultEqualsTo(64.3m * 3);
            TestCompiledVsInterpreted<decimal>("3 * 3m").ResultEqualsTo(3 * 3m);

            Assert.AreEqual(typeof(double), CompileGetter<object>("3 * 3.3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("3.3 * 3").GetValue().GetType());
            TestCompiledVsInterpreted<double>("3 * 3.3").ResultEqualsTo(3 * 3.3); ;
            TestCompiledVsInterpreted<double>("3.3 * 3").ResultEqualsTo(3.3 * 3); ;

            Assert.AreEqual(typeof(float), CompileGetter<object>("3 * 3.3f").GetValue().GetType());
            Assert.AreEqual(typeof(float), CompileGetter<object>("3.3f * 3").GetValue().GetType());
            TestCompiledVsInterpreted<float>("3 * 3.3f").ResultEqualsTo(3.0f * 3.3f);
            TestCompiledVsInterpreted<float>("3.3f * 3").ResultEqualsTo(3.3f * 3.0f);

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };
                Assert.AreEqual(null, ctxDecDec.Value1 / ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("Value1 * Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("Value1 * Value2", ctxDecDec);
            }


            {
                var ctxDecInt = new NHolder2<decimal, int> { Value1 = null, Value2 = 3 };
                Assert.AreEqual(null, (ctxDecInt.Value1 * ctxDecInt.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 * Value2").GetValue(ctxDecInt));

                var expected = 8m;

                ctxDecInt.Value1 = 4m;
                ctxDecInt.Value2 = 2;
                Assert.AreEqual(typeof(decimal), (ctxDecInt.Value1 * ctxDecInt.Value2).GetType());
                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 * Value2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 * Value2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 * Value2", ctxDecInt)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 * 2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 * 2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 * 2", ctxDecInt)
                    .ResultEqualsTo(expected);
            }

            {
                var ctxIntDec = new NHolder2<int, decimal> { Value1 = 666, Value2 = null };
                Assert.AreEqual(null, (ctxIntDec.Value1 * ctxIntDec.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 * Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, object>("Value1 * Value2", ctxIntDec);
                
                var expected = 1665.00m;

                ctxIntDec.Value2 = 2.5m;
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 * ctxIntDec.Value2).GetType());
                Assert.AreEqual(expected, ctxIntDec.Value1 * ctxIntDec.Value2);
                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 * Value2").GetValue(ctxIntDec).GetType());
                
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 * Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 * Value2", ctxIntDec)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(expected, 666 * ctxIntDec.Value2);
                Assert.AreEqual(typeof(decimal), (666 * ctxIntDec.Value2).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("666 * Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("666 * Value2", ctxIntDec)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(expected, ctxIntDec.Value1 * 2.5m);
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 * 2.5m).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 * 2.5m").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 * 2.5m", ctxIntDec)
                    .ResultEqualsTo(expected);
            }
        }

        [Test]
        public void ModTest()
        {
            Assert.AreEqual(typeof(decimal), (64.3m % 3).GetType());
            Assert.AreEqual(typeof(decimal), (3 % 3m).GetType());

            Assert.AreEqual(typeof(double), (3 % 3.3).GetType());
            Assert.AreEqual(typeof(double), (3.4 % 3).GetType());

            Assert.AreEqual(typeof(float), (3 % 3.3f).GetType());
            Assert.AreEqual(typeof(float), (3.4f % 3).GetType());

            Assert.AreEqual(typeof(decimal), CompileGetter<object>("64.3m % 3").GetValue().GetType());
            Assert.AreEqual(typeof(decimal), CompileGetter<object>("3 % 3m").GetValue().GetType());
            TestCompiledVsInterpreted<decimal>("64.3m % 3").ResultEqualsTo(64.3m % 3);
            TestCompiledVsInterpreted<decimal>("3 % 3m").ResultEqualsTo(3 % 3m);

            Assert.AreEqual(typeof(double), CompileGetter<object>("3 % 3.3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("3.3 % 3").GetValue().GetType());
            TestCompiledVsInterpreted<double>("3 % 3.3").ResultEqualsTo(3 % 3.3); ;
            TestCompiledVsInterpreted<double>("3.3 % 3").ResultEqualsTo(3.3 % 3); ;

            Assert.AreEqual(typeof(float), CompileGetter<object>("3 % 3.3f").GetValue().GetType());
            Assert.AreEqual(typeof(float), CompileGetter<object>("3.3f % 3").GetValue().GetType());
            TestCompiledVsInterpreted<float>("3 % 3.3f").ResultEqualsTo(3.0f % 3.3f);
            TestCompiledVsInterpreted<float>("3.3f % 3").ResultEqualsTo(3.3f % 3.0f);

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };
                Assert.AreEqual(null, ctxDecDec.Value1 / ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("Value1 % Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("Value1 % Value2", ctxDecDec);
            }

            {
                var ctxDecInt = new NHolder2<decimal, int> { Value1 = null, Value2 = 3 };
                Assert.AreEqual(null, (ctxDecInt.Value1 % ctxDecInt.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 % Value2").GetValue(ctxDecInt));

                var expected = 1m;

                ctxDecInt.Value1 = 4m;
                ctxDecInt.Value2 = 3;
                Assert.AreEqual(typeof(decimal), (ctxDecInt.Value1 % ctxDecInt.Value2).GetType());
                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 % Value2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 % Value2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 % Value2", ctxDecInt)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 % 3").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 % 3").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, decimal>("Value1 % 3", ctxDecInt)
                    .ResultEqualsTo(expected);
            }

            {
                var ctxIntDec = new NHolder2<int, decimal> { Value1 = 666, Value2 = null };
                Assert.AreEqual(null, (ctxIntDec.Value1 % ctxIntDec.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 % Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, object>("Value1 % Value2", ctxIntDec);

                var expected = 1m;

                ctxIntDec.Value2 = 2.5m;
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 % ctxIntDec.Value2).GetType());
                Assert.AreEqual(expected, ctxIntDec.Value1 % ctxIntDec.Value2);

                Assert.AreEqual(typeof(decimal),
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 % Value2").GetValue(ctxIntDec).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 % Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 % Value2", ctxIntDec)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(expected, 666 % ctxIntDec.Value2);
                Assert.AreEqual(typeof(decimal), (666 % ctxIntDec.Value2).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("666 % Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("666 % Value2", ctxIntDec)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(expected, ctxIntDec.Value1 % 2.5m);
                Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 % 2.5m).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 % 2.5m").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, decimal>("Value1 % 2.5m", ctxIntDec)
                    .ResultEqualsTo(expected);
            }
        }

        [Test]
        public void PowerTest()
        {
            Assert.AreEqual(typeof(double), Math.Pow(3 , 3.3).GetType());
            Assert.AreEqual(typeof(double), Math.Pow(3.4 , 3).GetType());


            Assert.AreEqual(typeof(double), CompileGetter<object>("3 ^ 3.3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("3.3 ^ 3").GetValue().GetType());
            TestCompiledVsInterpreted<double>("3 ^ 3.3").ResultEqualsTo(Math.Pow(3, 3.3)); 
            TestCompiledVsInterpreted<double>("3.3 ^ 3").ResultEqualsTo(Math.Pow(3.3 , 3));

            Assert.AreEqual(typeof(double), CompileGetter<object>("3m ^ 3.3").GetValue().GetType());
            TestCompiledVsInterpreted<double>("3m ^ 3.3").ResultEqualsTo(Math.Pow(3, 3.3));

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };
                // no power operator in C#
                //Assert.AreEqual(null, ctxDecDec.Value1 ^ ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("Value1 ^ Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("Value1 ^ Value2", ctxDecDec);
            }

            {
                var ctxDecInt = new NHolder2<decimal, int> { Value1 = null, Value2 = 3 };

                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 ^ Value2").GetValue(ctxDecInt));

                var expected = 64.0;

                ctxDecInt.Value1 = 4m;
                ctxDecInt.Value2 = 3;
                Assert.AreEqual(typeof(decimal), (ctxDecInt.Value1 % ctxDecInt.Value2).GetType());
                Assert.AreEqual(typeof(double),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 ^ Value2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 ^ Value2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, double>("Value1 ^ Value2", ctxDecInt)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(typeof(double),
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 ^ 3").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<decimal, int>, object>("Value1 ^ 3").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<decimal, int>, double>("Value1 ^ 3", ctxDecInt)
                    .ResultEqualsTo(expected);
            }

            {
                var ctxIntDec = new NHolder2<int, decimal> { Value1 = 625, Value2 = null };
                Assert.AreEqual(null, (ctxIntDec.Value1 % ctxIntDec.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 ^ Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, object>("Value1 ^ Value2", ctxIntDec);

                var expected = 25;

                ctxIntDec.Value2 = 0.5m;
                Assert.AreEqual(expected, Math.Pow(ctxIntDec.Value1.Value, (double)ctxIntDec.Value2.Value));

                Assert.AreEqual(typeof(double),
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 ^ Value2").GetValue(ctxIntDec).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 ^ Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, double>("Value1 ^ Value2", ctxIntDec)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(expected, Math.Pow(625, (double)ctxIntDec.Value2.Value));
                Assert.AreEqual(typeof(double), Math.Pow(625 , (double)ctxIntDec.Value2.Value).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("625 ^ Value2").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, double>("625 ^ Value2", ctxIntDec)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(expected, Math.Pow(ctxIntDec.Value1.Value, 0.5));
                Assert.AreEqual(typeof(double), Math.Pow(ctxIntDec.Value1.Value, 0.5).GetType());

                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<int, decimal>, object>("Value1 ^ 0.5m").GetValue(ctxIntDec));

                TestCompiledVsInterpreted<NHolder2<int, decimal>, double>("Value1 ^ 0.5m", ctxIntDec)
                    .ResultEqualsTo(expected);
            }
        }

        [Test]
        public void UnaryMinusTest()
        {
            Assert.AreEqual(typeof(decimal), (-64.3m).GetType());
            Assert.AreEqual(typeof(int), (-3).GetType());

            Assert.AreEqual(typeof(int), (-3).GetType());
            Assert.AreEqual(typeof(double), (-3.4).GetType());

            Assert.AreEqual(typeof(float), (-3.4f).GetType());

            Assert.AreEqual(typeof(decimal), CompileGetter<object>("-64.3m").GetValue().GetType());
            Assert.AreEqual(typeof(int), CompileGetter<object>("-3").GetValue().GetType());
            TestCompiledVsInterpreted<decimal>("-64.3m").ResultEqualsTo(-64.3m);
            TestCompiledVsInterpreted<int>("-3").ResultEqualsTo(-3);

            Assert.AreEqual(typeof(int), CompileGetter<object>("-3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("-3.3").GetValue().GetType());
            TestCompiledVsInterpreted<int>("-3").ResultEqualsTo(-3);
            TestCompiledVsInterpreted<double>("-3.3").ResultEqualsTo(-3.3);

            Assert.AreEqual(typeof(int), CompileGetter<object>("-3").GetValue().GetType());
            Assert.AreEqual(typeof(float), CompileGetter<object>("-3.3f").GetValue().GetType());
            TestCompiledVsInterpreted<int>("-3").ResultEqualsTo(-3);
            TestCompiledVsInterpreted<float>("-3.3f").ResultEqualsTo(-3.3f);

            TestCompiledVsInterpreted<float>("- -3.3f").ResultEqualsTo(3.3f);

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };

                Assert.AreEqual(null, -ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("-Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("-Value2", ctxDecDec);

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("- -Value2", ctxDecDec);
            }
        }

        [Test]
        public void UnaryPlusTest()
        {
            Assert.AreEqual(typeof(decimal), (+64.3m).GetType());
            Assert.AreEqual(typeof(int), (+3).GetType());

            Assert.AreEqual(typeof(int), (+3).GetType());
            Assert.AreEqual(typeof(double), (+3.4).GetType());

            Assert.AreEqual(typeof(float), (+3.4f).GetType());

            Assert.AreEqual(typeof(decimal), CompileGetter<object>("+64.3m").GetValue().GetType());
            Assert.AreEqual(typeof(int), CompileGetter<object>("+3").GetValue().GetType());
            TestCompiledVsInterpreted<decimal>("+64.3m").ResultEqualsTo(+64.3m);
            TestCompiledVsInterpreted<int>("+3").ResultEqualsTo(+3);

            Assert.AreEqual(typeof(int), CompileGetter<object>("+3").GetValue().GetType());
            Assert.AreEqual(typeof(double), CompileGetter<object>("+3.3").GetValue().GetType());
            TestCompiledVsInterpreted<int>("+3").ResultEqualsTo(+3);
            TestCompiledVsInterpreted<double>("+3.3").ResultEqualsTo(+3.3);

            Assert.AreEqual(typeof(int), CompileGetter<object>("+3").GetValue().GetType());
            Assert.AreEqual(typeof(float), CompileGetter<object>("+3.3f").GetValue().GetType());
            TestCompiledVsInterpreted<int>("+3").ResultEqualsTo(+3);
            TestCompiledVsInterpreted<float>("+3.3f").ResultEqualsTo(+3.3f);

            TestCompiledVsInterpreted<float>("+ +3.3f").ResultEqualsTo(3.3f);

            {
                var ctxDecDec = new NHolder2<decimal, decimal> { Value1 = 64.3m, Value2 = null };

                Assert.AreEqual(null, +ctxDecDec.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<decimal, decimal>, object>("+Value2").GetValue(ctxDecDec));

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("+Value2", ctxDecDec);

                TestCompiledVsInterpreted<NHolder2<decimal, decimal>, object>("+ +Value2", ctxDecDec);
            }
        }

        [Test]
        public void AndTest()
        {
            Assert.AreEqual(typeof(ulong), (64ul & 3).GetType());
            Assert.AreEqual(typeof(ulong), (64ul + 3).GetType());


            Assert.AreEqual(typeof(ulong), (64ul & 3).GetType());
            Assert.AreEqual(typeof(ulong), (3 & 3ul).GetType());

            
            Assert.AreEqual(typeof(int), (3 & 1).GetType());
            Assert.AreEqual(typeof(long), (3 & 1L).GetType());

            Assert.AreEqual(typeof(ulong), CompileGetter<object>("64ul and 3u").GetValue().GetType());
            Assert.AreEqual(typeof(ulong), CompileGetter<object>("3u and 3ul").GetValue().GetType());

            Assert.AreEqual(typeof(long), CompileGetter<object>("3 and 3u").GetValue().GetType());
            Assert.AreEqual(typeof(int), CompileGetter<object>("3 and 3").GetValue().GetType());
            Assert.AreEqual(typeof(long), CompileGetter<object>("3 and 1L").GetValue().GetType());

            TestCompiledVsInterpreted<ulong>("64ul and 3u").ResultEqualsTo(64ul & 3);

            int i3 = 3;
            uint u3 = 3u;
            Assert.AreEqual(typeof(long), (i3 & u3).GetType());
            Assert.AreEqual(typeof(long), (i3 + u3).GetType());
            TestCompiledVsInterpreted<long>("3 and 3U").ResultEqualsTo(i3 & u3);
            

            TestCompiledVsInterpreted<int>("3 and 3").ResultEqualsTo(3 & 3);
            TestCompiledVsInterpreted<long>("3 and 1L").ResultEqualsTo(3 & 1L);

            {
                var ctxUintUlong = new NHolder2<uint, ulong> { Value1 = 64, Value2 = null };
                Assert.AreEqual(null, ctxUintUlong.Value1 & ctxUintUlong.Value2);
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<uint, ulong>, object>("Value1 and Value2").GetValue(ctxUintUlong));

                TestCompiledVsInterpreted<NHolder2<uint, ulong>, object>("Value1 and Value2", ctxUintUlong);
            }


            {
                var ctxDecInt = new NHolder2<long, int> { Value1 = null, Value2 = 3 };
                Assert.AreEqual(null, (ctxDecInt.Value1 & ctxDecInt.Value2));
                Assert.AreEqual(null,
                    CompileGetter<NHolder2<long, int>, object>("Value1 and Value2").GetValue(ctxDecInt));

                var expected = 1L;

                ctxDecInt.Value1 = 7;
                ctxDecInt.Value2 = 1;
                Assert.AreEqual(typeof(long), (ctxDecInt.Value1 & ctxDecInt.Value2).GetType());
                Assert.AreEqual(typeof(long),
                    CompileGetter<NHolder2<long, int>, object>("Value1 and Value2").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<long, int>, object>("Value1 and Value2").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<long, int>, long>("Value1 and Value2", ctxDecInt)
                    .ResultEqualsTo(expected);

                Assert.AreEqual(typeof(long),
                    CompileGetter<NHolder2<long, int>, object>("Value1 and 1").GetValue(ctxDecInt).GetType());
                Assert.AreEqual(expected,
                    CompileGetter<NHolder2<long, int>, object>("Value1 and 1").GetValue(ctxDecInt));

                TestCompiledVsInterpreted<NHolder2<long, int>, long>("Value1 and 1", ctxDecInt)
                    .ResultEqualsTo(expected);
            }

            {
                var ctxUIntInt = new NHolder2<uint, int> { Value1 = 666, Value2 = 2 };
                Assert.AreEqual(typeof(long), (ctxUIntInt.Value1 & ctxUIntInt.Value2).GetType());

                   // todo: error: fixme !!!!!
                Assert.AreEqual(typeof(long),
                    CompileGetter<NHolder2<uint, int>, object>("Value1 and Value2").GetValue(ctxUIntInt).GetType());
            }

            {
                var ctxIntDec = new NHolder2<decimal, int> { Value1 = 666, Value2 = null };

                try
                {
                    TestCompiledVsInterpreted<NHolder2<decimal, int>, object>("Value1 and Value2", ctxIntDec);
                    Assert.Fail("Should throw exception");
                }
                catch (Exception) { /* ignore */ }
                
                
                ctxIntDec.Value2 = 2;

                // illegal
                //Assert.AreEqual(typeof(decimal), (ctxIntDec.Value1 & ctxIntDec.Value2).GetType());
                //Assert.AreEqual(expected, ctxIntDec.Value1 & ctxIntDec.Value2);

                try
                {
                    Assert.AreEqual(typeof(decimal),
                        CompileGetter<NHolder2<decimal, int>, object>("Value1 and Value2").GetValue(ctxIntDec)
                            .GetType());
                }
                catch (Exception) { /* ignore */ }
            }
        }


        // todo: error: null [op] null <- does not work in interpreted path!!! but it could!!!



        class NHolder2<T1, T2>
            where T1 : struct
            where T2 : struct
        {
            public T1? Value1 { get; set; }
            public T2? Value2 { get; set; }
        }
    }
}

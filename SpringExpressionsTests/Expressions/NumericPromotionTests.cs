using System;

using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    using CtxUIntInt=Tuple<uint, int>;
    using CtxUShortUShort = Tuple<ushort, ushort>;
    using CtxShortByte = Tuple<short, byte>;
    using CtxIntLong = Tuple<int, long>;

    [TestFixture]
    public class NumericPromotionTests : BaseCompiledTests
    {
        [Test]
        public void UIntAndSmallerPromotedToLongTest()
        {
            var ctx = new CtxUIntInt(3, 5);
            Assert.AreEqual(typeof(long), (ctx.Item1 + ctx.Item2).GetType());

            Assert.AreEqual(typeof(long),
                CompileGetter<CtxUIntInt, object>("Item1 + Item2").GetValue(ctx).GetType());

            TestCompiledVsInterpreted<CtxUIntInt, long>("Item1 + Item2", ctx)
                .ResultEqualsTo(8L);
        }


        [Test]
        public void UShortAndUShortPromotedToIntTest()
        {
            var ctx = new CtxUShortUShort(3, 5);

            Assert.AreEqual(typeof(int), (ctx.Item1 + ctx.Item2).GetType());

            Assert.AreEqual(typeof(int),
                CompileGetter<CtxUShortUShort, object>("Item1 + Item2").GetValue(ctx).GetType());

            TestCompiledVsInterpreted<CtxUShortUShort, int>("Item1 + Item2", ctx)
                .ResultEqualsTo(8);

        }

        [Test]
        public void ShortAndBytePromotedToIntTest()
        {
            var ctx = new CtxShortByte(3, 5);

            Assert.AreEqual(typeof(int), (ctx.Item1 + ctx.Item2).GetType());

            Assert.AreEqual(typeof(int),
                CompileGetter<CtxShortByte, object>("Item1 + Item2").GetValue(ctx).GetType());

            TestCompiledVsInterpreted<CtxShortByte, int>("Item1 + Item2", ctx)
                .ResultEqualsTo(8);
            
        }

        [Test]
        public void IntAndLongPromotedToIntTest()
        {
            var ctx = new CtxIntLong(3, 1L);

            Assert.AreEqual(typeof(long), (ctx.Item1 + ctx.Item2).GetType());

            Assert.AreEqual(typeof(long),
                CompileGetter<CtxIntLong, object>("Item1 + Item2").GetValue(ctx).GetType());

            TestCompiledVsInterpreted<CtxIntLong, long>("Item1 + Item2", ctx)
                .ResultEqualsTo(4);
        }

        [Test]
        public void IllegalPromotions()
        {
            {
                // int + ulong
                var ctx = new Tuple<int, ulong>(3, 3ul);
                Assert.Throws<Exception>(() => CompileGetter<Tuple<int, ulong>, object >("Item1 + Item2").GetValue(ctx));
                //Assert.Throws<Exception>(() => InterpretGetter<Tuple<int, ulong>, object>("Item1 + Item2").GetValue(ctx));

                //TestCompiledVsInterpreted<ulong>("3 and 3UL").ResultEqualsTo(3 & 3ul);
            }
            {
                var ctx = new Tuple<double, decimal>(3d, 3m);
                CompileGetter<Tuple<double, decimal>, object>("Item1 + Item2").GetValue(ctx);
            }
        }

        [Test]
        public void CompilerDuctTypingConstExpressionTest()
        {
            // adding int to ulong is illegal. this wont compile!
            // "CS0034:  Operator '+' is ambiguous on operands of type 'ulong' and 'int'

            // var explicitIntNotCompileTimeConst = 3;
            // Assert.AreEqual(666, 64ul + explicitIntNotCompileTimeConst);


            // but adding "implicit" int to ulong is legal in "compile-time const" expression!
            Assert.AreEqual(67, 64ul + 3);
            Assert.AreEqual(typeof(ulong), (64ul + 3).GetType());
            Assert.AreEqual(12, 64ul + 3 - 55);
            Assert.AreEqual(typeof(ulong), (64ul + 3-55).GetType());

            // smallest unsigned long number:
            Assert.AreEqual(0, 64ul + 3 - 67);

            // Illegal: "CS0220: The operation overflows at compile time in checked mode"
            // Assert.AreEqual(666, 64ul + 3 - 68);
            // Assert.AreEqual(666, 64ul + 3 - 553);


            // there are no compile time const expressions in spring expressions! As yet...
            // So this will fail!
            try
            {
                TestCompiledVsInterpreted<ulong>("64ul + 3").ResultEqualsTo(64ul & 3);
                Assert.Fail("Should throw exception!");
            }
            catch (Exception) { /* ignored */ }


            Assert.AreEqual(typeof(uint), (3 & 3u).GetType());
            Assert.AreEqual(typeof(uint), (3 + 3u).GetType());

            const int const_i3 = 3;
            const uint const_u3 = 3u;

            Assert.AreEqual(typeof(uint), (const_i3 & const_u3).GetType());
            Assert.AreEqual(typeof(uint), (const_i3 + const_u3).GetType());


            // todo: error: tutaj nie ma duct typing
            int i3 = 3;
            uint u3 = 3u;

            Assert.AreEqual(typeof(long), (i3 & u3).GetType());
            Assert.AreEqual(typeof(long), (i3 + u3).GetType());
            TestCompiledVsInterpreted<long>("3 and 3U").ResultEqualsTo(3 & 3u);
        }

        // todo: uint and int => long



        [Test]
        public void UnaryPromotions()
        {
            {
                int i3 = 3;

                Assert.AreEqual(typeof(int), (-i3).GetType());
                Assert.AreEqual(-3, -i3);
            }

            // unsigned int is promoted to long for '-' operator
            {
                uint ui3 = 3;
                Assert.AreEqual(typeof(long), (-ui3).GetType());
                Assert.AreEqual(-3, -ui3);

                Assert.AreEqual(typeof(long), (-3u).GetType());
                Assert.AreEqual(-3, -3u);

                Assert.AreEqual(typeof(long),
                    CompileGetter<uint, object>("-#this").GetValue(ui3).GetType());

                TestCompiledVsInterpreted<uint, long>("-#this", ui3)
                    .ResultEqualsTo(-3);

                // unsigned int is NOT promoted to long (stays int) for '+' and '~' operators
                Assert.AreEqual(typeof(uint), (+ui3).GetType());
                Assert.AreEqual(3, +ui3);

                Assert.AreEqual(typeof(uint), (+3u).GetType());
                Assert.AreEqual(3, 3u);

                Assert.AreEqual(typeof(uint),
                    CompileGetter<uint, object>("+#this").GetValue(3).GetType());

                TestCompiledVsInterpreted<uint, uint>("+#this", 3)
                    .ResultEqualsTo(3u);

                Assert.AreEqual(typeof(uint), (~ui3).GetType());
                Assert.AreEqual(uint.MaxValue ^ 3, ~ui3);

                Assert.AreEqual(typeof(uint), (~3u).GetType());
                Assert.AreEqual(uint.MaxValue ^ 3, ~3u);

                Assert.AreEqual(typeof(uint),
                    CompileGetter<uint, object>("!#this").GetValue(ui3).GetType());

                TestCompiledVsInterpreted<uint, uint>("!#this", ui3)
                    .ResultEqualsTo(uint.MaxValue ^ 3);
            }

            // sbyte is always promoted to int
            {
                sbyte sb3 = 3;
                Assert.AreEqual(typeof(int), (-sb3).GetType());
                Assert.AreEqual(-3, -sb3);

                TestCompiledVsInterpreted<sbyte, int>("-#this", sb3)
                    .ResultEqualsTo(-3);

                TestCompiledVsInterpreted<sbyte, int>("+#this", sb3)
                    .ResultEqualsTo(3);

                TestCompiledVsInterpreted<sbyte, int>("!#this", sb3)
                    .ResultEqualsTo(unchecked((int)(uint.MaxValue ^ 3)));
            }

            // byte is always promoted to int
            {
                byte b3 = 3;
                Assert.AreEqual(typeof(int), (-b3).GetType());
                Assert.AreEqual(-3, -b3);

                TestCompiledVsInterpreted<byte, int>("-#this", b3)
                    .ResultEqualsTo(-3);

                TestCompiledVsInterpreted<byte, int>("+#this", b3)
                    .ResultEqualsTo(3);

                TestCompiledVsInterpreted<byte, int>("!#this", b3)
                    .ResultEqualsTo(unchecked((int)(uint.MaxValue ^ 3)));
            }

            // ushort is always promoted to int
            {
                ushort us3 = 3;
                Assert.AreEqual(typeof(int), (-us3).GetType());
                Assert.AreEqual(-3, -us3);

                TestCompiledVsInterpreted<ushort, int>("-#this", us3)
                    .ResultEqualsTo(-3);

                TestCompiledVsInterpreted<ushort, int>("+#this", us3)
                    .ResultEqualsTo(3);

                TestCompiledVsInterpreted<ushort, int>("!#this", us3)
                    .ResultEqualsTo(unchecked((int)(uint.MaxValue ^ 3)));
            }

            // short is always promoted to int
            {
                short s3 = 3;
                Assert.AreEqual(typeof(int), (-s3).GetType());
                Assert.AreEqual(-3, -s3);

                TestCompiledVsInterpreted<short, int>("-#this", s3)
                    .ResultEqualsTo(-3);

                TestCompiledVsInterpreted<short, int>("+#this", s3)
                    .ResultEqualsTo(3);

                TestCompiledVsInterpreted<short, int>("!#this", s3)
                    .ResultEqualsTo(unchecked((int)(uint.MaxValue ^ 3)));
            }

            // int stays int
            {
                int i3 = 3;
                Assert.AreEqual(typeof(int), (-i3).GetType());
                Assert.AreEqual(-3, -i3);

                TestCompiledVsInterpreted<int, int>("-#this", i3)
                    .ResultEqualsTo(-3);

                TestCompiledVsInterpreted<int, int>("+#this", i3)
                    .ResultEqualsTo(3);

                TestCompiledVsInterpreted<int, int>("!#this", i3)
                    .ResultEqualsTo(unchecked((int)(uint.MaxValue ^ 3)));
            }

            // long stays long
            {
                long l3 = 3;
                Assert.AreEqual(typeof(long), (-l3).GetType());
                Assert.AreEqual(-3, -l3);

                TestCompiledVsInterpreted<long, long>("-#this", l3)
                    .ResultEqualsTo(-3);

                TestCompiledVsInterpreted<long, long>("+#this", l3)
                    .ResultEqualsTo(3);

                TestCompiledVsInterpreted<long, long>("!#this", l3)
                    .ResultEqualsTo(unchecked((long)(ulong.MaxValue ^ 3)));
            }

            // unsigned longs!!!
            {
                ulong ul3 = 3;

                Assert.AreEqual(typeof(ulong), (+ul3).GetType());
                Assert.AreEqual(3, +ul3);

                TestCompiledVsInterpreted<ulong, ulong>("+#this", ul3)
                    .ResultEqualsTo(3);


                Assert.AreEqual(typeof(ulong), (~ul3).GetType());
                Assert.AreEqual(ulong.MaxValue ^ 3, ~ul3);

                TestCompiledVsInterpreted<ulong, ulong>("!#this", ul3)
                    .ResultEqualsTo(ulong.MaxValue ^ 3);

                // CS0023: Operator '-' cannot be applied to operand of type 'ulong'

                // invalid op!
                // Assert.AreEqual(typeof(long), (-ul3).GetType());
                //Assert.AreEqual(-3, -ul3);

                Assert.Throws<ArgumentException>(() => CompileGetter<ulong, ulong>("-#this"));
                Assert.Throws<ArgumentException>(() => InterpretGetter<ulong, ulong>("-#this").GetValue(ul3));
            }

            //ulong ul3 = 3;
            //Assert.AreEqual(-3, -ul3);
        }
    }
}

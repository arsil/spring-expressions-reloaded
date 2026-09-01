using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace SpringExpressionsTests.Expressions
{
    [TestFixture]
    public class ComparisonTests : BaseCompiledTests
    {
        [Test]
        public void OldComparableOnlyTest()
        {
            var ctx = new List<OnlyOldComparable>
            {
                new OnlyOldComparable("1"), // [0]
                new OnlyOldComparable("2"), // [1] 
                new OnlyOldComparable("1"), // [2]
                new OnlyOldComparable("3"), // [3]
            };

            // will use Object.Equals
            var defaultComparer = Comparer<OnlyOldComparable>.Default;

            Assert.IsTrue(0 >  defaultComparer.Compare(ctx[0], ctx[1])); // <
            Assert.IsTrue(0 <  defaultComparer.Compare(ctx[1], ctx[2])); // >
            Assert.IsTrue(0 == defaultComparer.Compare(ctx[0], ctx[2])); // ==
            Assert.IsTrue(0 >  defaultComparer.Compare(ctx[0], ctx[3])); // <
            Assert.IsTrue(0 <  defaultComparer.Compare(ctx[3], ctx[0])); // >


            Assert.IsFalse(InterpretGetter<List<OnlyOldComparable>, bool>("[0] > [1]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] > [1]").GetValue(ctx));
            Assert.IsFalse(InterpretGetter<List<OnlyOldComparable>, bool>("[0] >= [1]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] >= [1]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyOldComparable>, bool>("[0] < [1]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] < [1]").GetValue(ctx));
            Assert.IsTrue(InterpretGetter<List<OnlyOldComparable>, bool>("[0] <= [1]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] <= [1]").GetValue(ctx));


            Assert.IsTrue(InterpretGetter<List<OnlyOldComparable>, bool>("[0] >= [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] >= [2]").GetValue(ctx));
            Assert.IsTrue(InterpretGetter<List<OnlyOldComparable>, bool>("[0] <= [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] <= [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyOldComparable>, bool>("[0] > [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] > [2]").GetValue(ctx));
            Assert.IsFalse(InterpretGetter<List<OnlyOldComparable>, bool>("[0] < [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyOldComparable>, bool>(  "[0] < [2]").GetValue(ctx));
        }


        [Test]
        public void OldComparableSortTest()
        {
            var ctx = new List<OnlyOldComparable>
            {
                new OnlyOldComparable("1"), // [0]
                new OnlyOldComparable("2"), // [1] 
                new OnlyOldComparable("1"), // [2]
                new OnlyOldComparable("3"), // [3]
            };

            var sorted = new List<OnlyOldComparable>(ctx);
            sorted.Sort();

            Assert.AreEqual("1", sorted[0].Id);
            Assert.AreEqual("1", sorted[1].Id);
            Assert.AreEqual("2", sorted[2].Id);
            Assert.AreEqual("3", sorted[3].Id);

            {
                var distinctInterpreted = InterpretGetter<List<OnlyOldComparable>, List<OnlyOldComparable>>(
                    "sort()").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpreted.Count);

                Assert.AreEqual("1", distinctInterpreted[0].Id);
                Assert.AreEqual("1", distinctInterpreted[1].Id);
                Assert.AreEqual("2", distinctInterpreted[2].Id);
                Assert.AreEqual("3", distinctInterpreted[3].Id);


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<OnlyOldComparable>, object>(
                    "sort()").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpretedAsObject.Count);

                Assert.AreEqual("1", ((OnlyOldComparable)distinctInterpretedAsObject[0]).Id);
                Assert.AreEqual("1", ((OnlyOldComparable)distinctInterpretedAsObject[1]).Id);
                Assert.AreEqual("2", ((OnlyOldComparable)distinctInterpretedAsObject[2]).Id);
                Assert.AreEqual("3", ((OnlyOldComparable)distinctInterpretedAsObject[3]).Id);


                var distinctCompiled = CompileGetter<List<OnlyOldComparable>, List<OnlyOldComparable>>(
                    "sort()").GetValue(ctx);
                Assert.AreEqual(4, distinctCompiled.Count);

                Assert.AreEqual("1", distinctCompiled[0].Id);
                Assert.AreEqual("1", distinctCompiled[1].Id);
                Assert.AreEqual("2", distinctCompiled[2].Id);
                Assert.AreEqual("3", distinctCompiled[3].Id);
            }

            {
                var distinctInterpreted = InterpretGetter<List<OnlyOldComparable>, List<OnlyOldComparable>>(
                    "sort(false)").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpreted.Count);

                Assert.AreEqual("3", distinctInterpreted[0].Id);
                Assert.AreEqual("2", distinctInterpreted[1].Id);
                Assert.AreEqual("1", distinctInterpreted[2].Id);
                Assert.AreEqual("1", distinctInterpreted[3].Id);


                var distinctInterpretedAsObject = (List<object>)InterpretGetter<List<OnlyOldComparable>, object>(
                    "sort(false)").GetValue(ctx);
                Assert.AreEqual(4, distinctInterpretedAsObject.Count);

                Assert.AreEqual("3", ((OnlyOldComparable)distinctInterpretedAsObject[0]).Id);
                Assert.AreEqual("2", ((OnlyOldComparable)distinctInterpretedAsObject[1]).Id);
                Assert.AreEqual("1", ((OnlyOldComparable)distinctInterpretedAsObject[2]).Id);
                Assert.AreEqual("1", ((OnlyOldComparable)distinctInterpretedAsObject[3]).Id);


                var distinctCompiled = CompileGetter<List<OnlyOldComparable>, List<OnlyOldComparable>>(
                    "sort(false)").GetValue(ctx);
                Assert.AreEqual(4, distinctCompiled.Count);

                Assert.AreEqual("3", distinctCompiled[0].Id);
                Assert.AreEqual("2", distinctCompiled[1].Id);
                Assert.AreEqual("1", distinctCompiled[2].Id);
                Assert.AreEqual("1", distinctCompiled[3].Id);
            }
        }


        [Test]
        public void NewComparableOnlyTest()
        {
            var ctx = new List<OnlyNewComparable>
            {
                new OnlyNewComparable("1"), // [0]
                new OnlyNewComparable("2"), // [1] 
                new OnlyNewComparable("1"), // [2]
                new OnlyNewComparable("3"), // [3]
            };

            // will use Object.Equals
            var defaultComparer = Comparer<OnlyNewComparable>.Default;

            Assert.IsTrue(0 > defaultComparer.Compare(ctx[0], ctx[1]));  // <
            Assert.IsTrue(0 < defaultComparer.Compare(ctx[1], ctx[2]));  // >
            Assert.IsTrue(0 == defaultComparer.Compare(ctx[0], ctx[2])); // ==
            Assert.IsTrue(0 > defaultComparer.Compare(ctx[0], ctx[3]));  // <
            Assert.IsTrue(0 < defaultComparer.Compare(ctx[3], ctx[0]));  // >

            Assert.IsFalse(InterpretGetter<List<OnlyNewComparable>, bool>("[0] > [1]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] > [1]").GetValue(ctx));
            Assert.IsFalse(InterpretGetter<List<OnlyNewComparable>, bool>("[0] >= [1]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] >= [1]").GetValue(ctx));

            Assert.IsTrue(InterpretGetter<List<OnlyNewComparable>, bool>("[0] < [1]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] < [1]").GetValue(ctx));
            Assert.IsTrue(InterpretGetter<List<OnlyNewComparable>, bool>("[0] <= [1]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] <= [1]").GetValue(ctx));


            Assert.IsTrue(InterpretGetter<List<OnlyNewComparable>, bool>("[0] >= [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] >= [2]").GetValue(ctx));
            Assert.IsTrue(InterpretGetter<List<OnlyNewComparable>, bool>("[0] <= [2]").GetValue(ctx));
            Assert.IsTrue(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] <= [2]").GetValue(ctx));

            Assert.IsFalse(InterpretGetter<List<OnlyNewComparable>, bool>("[0] > [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] > [2]").GetValue(ctx));
            Assert.IsFalse(InterpretGetter<List<OnlyNewComparable>, bool>("[0] < [2]").GetValue(ctx));
            Assert.IsFalse(CompileGetter<List<OnlyNewComparable>, bool>(  "[0] < [2]").GetValue(ctx));
        }

        [Test]
        public void ExceptionTestsDotNet()
        {
            var ctx = new List<NotComparable>
                { new NotComparable("a"), new NotComparable("b") };
            var comparer = Comparer<NotComparable>.Default;

            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            Assert.Throws<ArgumentException>(
                () => comparer.Compare(ctx[0], ctx[1]));
        }

        [Test]
        public void ExceptionTests()
        {
            var ctx = new List<NotComparable>
                { new NotComparable("a"), new NotComparable("b") };

            var interpreted = InterpretGetter<List<NotComparable>, bool>("[0] > [1]");
            var compiled = CompileGetter<List<NotComparable>, bool>("[0] > [1]");

            Assert.Throws<ArgumentException>(() => interpreted.GetValue(ctx));
            Assert.Throws<ArgumentException>(() => compiled.GetValue(ctx));
        }

        [Test]
        public void MixedNumbersTestsDotNet()
        {
            var holder1 = new NHolder<int>
                { Value = null };

            var comparer = Comparer<int?>.Default;

            Assert.IsTrue(comparer.Compare(holder1.Value, 3) < 0);
            Assert.IsTrue(comparer.Compare(3, holder1.Value) > 0);

            Assert.AreEqual(0, comparer.Compare(holder1.Value, holder1.Value));

            Assert.IsTrue(comparer.Compare(3, 0) > 0);
            Assert.IsTrue(comparer.Compare(0, 3) < 0);
            Assert.IsTrue(comparer.Compare(999, 999) == 0);

            var holder2 = new NHolder<int>
                { Value = 3 };

            Assert.IsTrue(comparer.Compare(holder1.Value, holder2.Value) < 0);
            Assert.IsTrue(comparer.Compare(holder2.Value, holder1.Value) > 0);

                   // todo: error! co z tym zrobić!!!!!! ale dowcip!!!!!-----------------------------------------------------------
            // comparison null with everything returns always false!!!! shit!!!!!
            #pragma warning disable CS0464 once
            Assert.IsFalse(3 > (int?) null);
            Assert.IsFalse(3 >= (int?)null);
            Assert.IsFalse(3 > holder1.Value);
            Assert.IsFalse(3 >= holder1.Value);

            // holder has 3
            Assert.IsFalse(1 > holder2.Value);

            // both are null
            holder1.Value = null;
            holder2.Value = null;
            Assert.IsFalse(holder1.Value > holder2.Value);
            Assert.IsFalse(holder1.Value < holder2.Value);
            Assert.IsFalse(holder1.Value >= holder2.Value);
            Assert.IsFalse(holder1.Value <= holder2.Value);

            Assert.IsFalse(holder1.Value != holder2.Value);
            Assert.IsTrue (holder1.Value == holder2.Value);

            var longHolder = new NHolder<long>();
            Assert.IsFalse(longHolder.Value > holder2.Value);
            Assert.IsFalse(longHolder.Value < holder2.Value);
            Assert.IsFalse(longHolder.Value >= holder2.Value);
            Assert.IsFalse(longHolder.Value <= holder2.Value);

            Assert.IsFalse(longHolder.Value != holder2.Value);
            Assert.IsTrue( longHolder.Value == holder2.Value);

        }

        /*
            Console.WriteLine(one < nll);  // false
            Console.WriteLine(one > nll);  // false
            Console.WriteLine(one <= nll); // false
            Console.WriteLine(one >= nll); // false
            Console.WriteLine(one == nll); // false
            Console.WriteLine(one != nll);


            Console.WriteLine(nll < nll);  // false
            Console.WriteLine(nll > nll);  // false
            Console.WriteLine(nll <= nll); // false
            Console.WriteLine(nll >= nll); // false

            Console.WriteLine(nll == nll); // true
            Console.WriteLine(nll != nll); // false
         */
        // todo: error: nullable datetime or bool?

        /// <summary>
        /// A nullable holding no value sorts before every value, on both backends. The author's own two
        /// markers used to sit on these lines - <c>to jednak działa!</c> above a commented-out
        /// interpreter assertion, and <c>to oczywiście nie działa...</c> above the compiled ones - which
        /// is the whole of open-issues item 17 in two comments: the interpreter was right and the
        /// compiled path was not. The interpreter assertion is live again below.
        /// </summary>
        /// <remarks>
        /// This is the engine deviating from C#, deliberately. C# answers false for any comparison
        /// against a null, and the raw-C# lines in <see cref="NullableDateTimeTests"/> assert that as
        /// the reference. Here a null literal, a null reference and a nullable holding nothing are one
        /// kind of nothing, and nothing sorts first - which the frozen suite has always pinned for the
        /// literal (<c>null &lt; 'xyz'</c> is True), so the compiled path was the only place a nullable
        /// was ordered differently from the other two.
        /// </remarks>
        [Test]
        public void MixedNumbersTests()
        {
            var ctx = new NHolder<int> { Value = null };

            Assert.IsTrue(InterpretGetter<NHolder<int>, bool>("Value <= 3").GetValue(ctx));

            Assert.IsTrue(CompileGetter<NHolder<int>, bool>("Value <= 3").GetValue(ctx));
            Assert.IsFalse(CompileGetter<NHolder<int>, bool>("Value >= 3").GetValue(ctx));
            Assert.IsTrue(CompileGetter<NHolder<int>, bool>("Value <  3").GetValue(ctx));
            Assert.IsFalse(CompileGetter<NHolder<int>, bool>("Value >  3").GetValue(ctx));

            var ctx2 = new NHolder<long> { Value = null };
            Assert.IsTrue(CompileGetter<NHolder<long>, bool>("Value <= 3").GetValue(ctx2));
            Assert.IsFalse(CompileGetter<NHolder<long>, bool>("Value >= 3").GetValue(ctx2));
            Assert.IsTrue(CompileGetter<NHolder<long>, bool>("Value <  3").GetValue(ctx2));
            Assert.IsFalse(CompileGetter<NHolder<long>, bool>("Value >  3").GetValue(ctx2));

            Assert.IsTrue(CompileGetter<NHolder<long>, bool>("Value <= 3.6").GetValue(ctx2));
            Assert.IsFalse(CompileGetter<NHolder<long>, bool>("Value >= 3.6").GetValue(ctx2));
            Assert.IsTrue(CompileGetter<NHolder<long>, bool>("Value <  3.6").GetValue(ctx2));
            Assert.IsFalse(CompileGetter<NHolder<long>, bool>("Value >  3.6").GetValue(ctx2));

            Assert.IsTrue(CompileGetter<NHolder<long>, bool>("Value <= 3.6m").GetValue(ctx2));
            Assert.IsFalse(CompileGetter<NHolder<long>, bool>("Value >= 3.6m").GetValue(ctx2));
            Assert.IsTrue(CompileGetter<NHolder<long>, bool>("Value <  3.6m").GetValue(ctx2));
            Assert.IsFalse(CompileGetter<NHolder<long>, bool>("Value >  3.6m").GetValue(ctx2));

            // and the interpreter agrees on every one of them, which is the point of the change
            Assert.IsTrue(InterpretGetter<NHolder<long>, bool>("Value <  3.6m").GetValue(ctx2));
            Assert.IsFalse(InterpretGetter<NHolder<long>, bool>("Value >  3.6m").GetValue(ctx2));
        }

        /**/
        private void InvalidComparison()
        {
            Assert.IsFalse(false == new NHolder<bool>().Value);
            Assert.IsFalse(false != new NHolder<bool>().Value);

            //Assert.IsFalse(false < new NHolder<bool>().Value);
            //Assert.IsFalse(false < true);
        }
        /**/
        // booleans cannot be compared using <> etc.

        /// <summary>
        /// The first four lines are C# itself, kept as the reference: C# answers false for every
        /// comparison against a null. **The engine deliberately answers otherwise** - nothing sorts
        /// before everything - so this test is the record of a chosen deviation rather than a mismatch.
        /// See open-issues item 17 for why: the frozen suite pins the sorting answer for a null literal,
        /// so following C# here would have made a nullable the one kind of nothing ordered differently.
        /// </summary>
        [Test]
        public void NullableDateTimeTests()
        {
            var ctx = new NHolder<DateTime>();

            // C#, for contrast
            Assert.IsFalse(new DateTime(2022, 12, 10) < ctx.Value);
            Assert.IsFalse(new DateTime(2022, 12, 10) > ctx.Value);
            Assert.IsFalse(new DateTime(2022, 12, 10) <= ctx.Value);
            Assert.IsFalse(new DateTime(2022, 12, 10) >= ctx.Value);

            // the engine: Value is nothing, so it sorts below the date
            Assert.IsTrue(CompileGetter<NHolder<DateTime>, bool>("Value <= date('2022-12-12', 'yyyy-MM-dd')").GetValue(ctx));
            Assert.IsFalse(CompileGetter<NHolder<DateTime>, bool>("Value >= date('2022-12-12', 'yyyy-MM-dd')").GetValue(ctx));
            Assert.IsTrue(CompileGetter<NHolder<DateTime>, bool>("Value <  date('2022-12-12', 'yyyy-MM-dd')").GetValue(ctx));
            Assert.IsFalse(CompileGetter<NHolder<DateTime>, bool>("Value >  date('2022-12-12', 'yyyy-MM-dd')").GetValue(ctx));

            // and the interpreter has always said so
            Assert.IsTrue(InterpretGetter<NHolder<DateTime>, bool>("Value <  date('2022-12-12', 'yyyy-MM-dd')").GetValue(ctx));
            Assert.IsFalse(InterpretGetter<NHolder<DateTime>, bool>("Value >  date('2022-12-12', 'yyyy-MM-dd')").GetValue(ctx));
        }

        [Test]
        public void NullableDateTimeOffsetTests()
        {
            var ctx = new NHolder2<DateTimeOffset, DateTimeOffset>();

            Assert.IsFalse(new DateTimeOffset(new DateTime(2022, 12, 10)) < ctx.Value1);
            Assert.IsFalse(new DateTimeOffset(new DateTime(2022, 12, 10)) > ctx.Value1);
            Assert.IsFalse(new DateTimeOffset(new DateTime(2022, 12, 10)) <= ctx.Value1);
            Assert.IsFalse(new DateTimeOffset(new DateTime(2022, 12, 10)) >= ctx.Value1);
            Assert.IsFalse(ctx.Value2 < ctx.Value1);
            Assert.IsFalse(ctx.Value2 > ctx.Value1);
            Assert.IsFalse(ctx.Value2 <= ctx.Value1);
            Assert.IsFalse(ctx.Value2 >= ctx.Value1);


            // both are nothing, so they sort equal: '<=' and '>=' hold, '<' and '>' do not
            Assert.IsTrue(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 <= Value2").GetValue(ctx));
            Assert.IsTrue(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 >= Value2").GetValue(ctx));
            Assert.IsFalse(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 <  Value2").GetValue(ctx));
            Assert.IsFalse(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 >  Value2").GetValue(ctx));

            ctx.Value1 = new DateTimeOffset(new DateTime(2022, 12, 10));

            // a value on the left, nothing on the right: the value sorts above
            Assert.IsFalse(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 <= Value2").GetValue(ctx));
            Assert.IsTrue(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 >= Value2").GetValue(ctx));
            Assert.IsFalse(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 <  Value2").GetValue(ctx));
            Assert.IsTrue(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 >  Value2").GetValue(ctx));

            ctx.Value1 = null;
            ctx.Value2 = new DateTimeOffset(new DateTime(2022, 12, 10));

            // and the mirror image, which is what makes the three outcomes three rather than two
            Assert.IsTrue(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 <= Value2").GetValue(ctx));
            Assert.IsFalse(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 >= Value2").GetValue(ctx));
            Assert.IsTrue(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 <  Value2").GetValue(ctx));
            Assert.IsFalse(
                CompileGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 >  Value2").GetValue(ctx));

            // the interpreter answers the same, on the asymmetric case that used to differ
            Assert.IsTrue(
                InterpretGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 <  Value2").GetValue(ctx));
            Assert.IsFalse(
                InterpretGetter<NHolder2<DateTimeOffset, DateTimeOffset>, bool>("Value1 >  Value2").GetValue(ctx));
        }

        // todo: error: string, numeryczne, nullable

        // todo: error; sort na klasę bez IComparable i <>... żeby poleciał wyjątek! tak samo w Equalit!
        // todo: error; może cacheować metodę rzucającą wyjątek???? WSZĘDZIE?


        // todo: error: jest jeszcze stringcomparer .... pytanie, co on robi? i czy jest używany w linq

        // todo: error: nullable vs notNullable

        class NHolder<T> where T : struct
        {
            public T? Value { get; set; }
        }

        class NHolder2<T1, T2> 
            where T1: struct
            where T2 : struct
        {
            public T1? Value1 { get; set; }
            public T2? Value2 { get; set; }
        }

        class OnlyOldComparable : IdClass, IComparable
        {
            public OnlyOldComparable(string id) : base(id)
            { }

            public int CompareTo(object obj)
            {
                if (obj == null)
                    return 1;

                if (obj is OnlyOldComparable otherTemperature)
                    return string.Compare(Id, otherTemperature.Id, StringComparison.Ordinal);

                throw new ArgumentException("Object is not a OnlyOldComparable");
            }
        }

        class OnlyNewComparable : IdClass, IComparable<OnlyNewComparable>
        {
            public OnlyNewComparable(string id) : base(id)
            { }

            public int CompareTo(OnlyNewComparable obj)
            {
                if (obj == null)
                    return 1;

                return string.Compare(Id, obj.Id, StringComparison.Ordinal);
            }
        }

        class NotComparable : IdClass
        {
            public NotComparable(string id) : base(id)
            {
            }
        }

        abstract class IdClass
        {
            protected IdClass(string id)
            { Id = id; }

            public string Id { get; }

            public override string ToString() => Id;
        }
    }
}

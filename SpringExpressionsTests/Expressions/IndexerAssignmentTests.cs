using NUnit.Framework;

using System.Collections.Generic;

using SpringCore;
using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class IndexerAssignmentHolder
    {
        public int[] Numbers { get; set; } = new int[3];
        public string[] Names { get; set; } = new string[3];
        public long[] Longs { get; set; } = new long[3];

        public long Big { get; set; }
        public decimal Amount { get; set; }
        public string Name { get; set; } = "a";

        public List<int> Ints { get; set; } = new List<int> { 0, 0, 0 };
        public List<long> LongList { get; set; } = new List<long> { 0, 0, 0 };
        public Dictionary<string, int> Map { get; set; } = new Dictionary<string, int>();

        public int Number { get; set; }
    }

    /// <summary>
    /// Writing through an indexer: an array element write has a compiled form, and every assignment
    /// evaluates to the value on the right of the <c>=</c>, on both backends.
    /// </summary>
    /// <remarks>
    /// Two defects met here and they were separate. **An array element write had no compiled form at
    /// all** - the emit built <c>LExpression.ArrayIndex</c>, which yields a read-only node, so
    /// <c>Assign</c> refused it with <i>"Expression must be writeable"</i>, surfaced as a type
    /// mismatch between two identical types. <c>ArrayAccess</c> yields an assignable
    /// <c>IndexExpression</c>. **And a compiled indexer write evaluated to nothing** - a call to a
    /// void set accessor has no value - where the interpreter answers the assigned value, because
    /// <c>AssignNode.Get</c> returns what it read before writing it. Assigning through the indexer as
    /// an <c>IndexExpression</c> closes both: the node is an <c>Assign</c>, so it carries the value,
    /// and the void-expression compiler goes on accepting it (it admits a void body or an
    /// <c>Assign</c>, and would have refused a <c>Block</c>).
    /// <p>
    /// <b>Neither was visible to any sweep</b>: no row of the shared corpus contains an indexer
    /// assignment, counted - 0 of 10,989. Both were found by asking whether an expression can fill an
    /// array it just constructed.
    /// </p>
    /// </remarks>
    [TestFixture]
    public class IndexerAssignmentTests : BaseCompiledTests
    {
        [Test]
        public void AnArrayElementWriteCompilesAndAnswersTheValueAssigned()
        {
            TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Numbers[0] = 5", new IndexerAssignmentHolder())
                .ResultEqualsTo(5);

            TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Names[0] = 'x'", new IndexerAssignmentHolder())
                .ResultEqualsTo("x");

            // A null literal is retyped to the element type, as it is in an array initialiser.
            TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Names[0] = null", new IndexerAssignmentHolder())
                .ResultEqualsTo(null);
        }

        [Test]
        public void AnArrayElementWriteActuallyWrites()
        {
            var holder = new IndexerAssignmentHolder();

            Expression.ParseGetter<IndexerAssignmentHolder, object>(
                    "Numbers[1] = 5", EvaluationMode.MustCompile)
                .GetValue(holder);

            Assert.AreEqual(5, holder.Numbers[1]);
            Assert.AreEqual(0, holder.Numbers[0]);

            var interpreted = new IndexerAssignmentHolder();

            Expression.ParseGetter<IndexerAssignmentHolder, object>(
                    "Numbers[1] = 5", EvaluationMode.MustInterpret)
                .GetValue(interpreted);

            Assert.AreEqual(5, interpreted.Numbers[1]);
        }

        /// <summary>
        /// A list or dictionary write compiled to a call on the void set accessor, so the expression
        /// evaluated to nothing while the interpreter answered the value.
        /// </summary>
        [Test]
        public void AListOrDictionaryWriteAnswersTheValueAssigned()
        {
            TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Ints[0] = 5", new IndexerAssignmentHolder())
                .ResultEqualsTo(5);

            TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Map['k'] = 5", new IndexerAssignmentHolder())
                .ResultEqualsTo(5);
        }

        /// <summary>
        /// A property or local write answers the value assigned too, so a future change to what an
        /// assignment evaluates to has to move every one of these together.
        /// </summary>
        [Test]
        public void APropertyOrLocalWriteAnswersTheValueAssignedToo()
        {
            TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Number = 5", new IndexerAssignmentHolder())
                .ResultEqualsTo(5);

            TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "$x = 5", new IndexerAssignmentHolder())
                .ResultEqualsTo(5);
        }

        /// <summary>
        /// A widening write into a property answers the value as the <b>member</b> holds it, which
        /// is C#'s rule: the result of an assignment has the type of its left operand.
        /// </summary>
        /// <remarks>
        /// <b>The interpreter used to answer the value it read</b> - <c>AssignNode.Get</c> returned
        /// the right-hand side untouched, which is upstream Spring.NET's code character for
        /// character - so <c>Big = 5</c> was <c>Int32:5</c> interpreted and <c>Int64:5</c> compiled
        /// once the setter-widening tier made such writes compile at all. Measured against C#:
        /// <c>(big = 5)</c> on a <c>long</c> field is <c>Int64:5</c>, and the same for
        /// <c>decimal</c>, for <c>arr[0] = 5</c> and for <c>list[0] = 5</c>. So the compiled path was
        /// right and the interpreter moved. Nothing in the frozen suite pinned the old answer - it
        /// writes exclusively through <c>ExpressionEvaluator.SetValue</c> and never spells an
        /// <c>=</c> assignment.
        /// </remarks>
        [Test]
        public void AWideningPropertyWriteAnswersTheValueTheMemberHolds()
        {
            var big = TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Big = 5", new IndexerAssignmentHolder())
                .Result;

            Assert.AreEqual(typeof(long), big.GetType());
            Assert.AreEqual(5L, big);

            var amount = TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Amount = 5", new IndexerAssignmentHolder())
                .Result;

            Assert.AreEqual(typeof(decimal), amount.GetType());
            Assert.AreEqual(5m, amount);
        }

        /// <summary>
        /// A conversion only the interpreter can perform still refuses compiled, and the value it
        /// answers is the converted one - the same rule, on a conversion C# does not have.
        /// </summary>
        [Test]
        public void AConversionOnlyTheInterpreterPerformsAnswersTheConvertedValue()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<IndexerAssignmentHolder, object>(
                    "Name = 5", EvaluationMode.MustCompile));

            var name = Expression.ParseGetter<IndexerAssignmentHolder, object>(
                    "Name = 5", EvaluationMode.MustInterpret)
                .GetValue(new IndexerAssignmentHolder());

            Assert.AreEqual(typeof(string), name.GetType());
            Assert.AreEqual("5", name);
        }

        /// <summary>
        /// A widening write into an array answers the widened value, because that is what the array
        /// holds - and both backends say so.
        /// </summary>
        /// <remarks>
        /// <c>Array.SetValue</c> widens by the CLR's primitive table on the interpreted side, so an
        /// <c>int</c> written into a <c>long[]</c> is a <c>long</c> from that moment; the compiled
        /// path emits the same conversion. C# agrees: <c>(arr[0] = 5)</c> on a <c>long[]</c> is an
        /// <c>Int64</c>.
        /// </remarks>
        [Test]
        public void AWideningWriteIntoAnArrayAnswersTheWidenedValue()
        {
            var value = TestCompiledVsInterpreted<IndexerAssignmentHolder, object>(
                    "Longs[0] = 5", new IndexerAssignmentHolder())
                .Result;

            Assert.AreEqual(typeof(long), value.GetType());
            Assert.AreEqual(5L, value);
        }

        /// <summary>
        /// A write the interpreter cannot perform at all is refused compiled, so both paths fail
        /// rather than one answering.
        /// </summary>
        /// <remarks>
        /// <c>LongList</c> is a <c>List&lt;long&gt;</c>, and the interpreter writes a list through
        /// the <b>non-generic</b> <see cref="System.Collections.IList"/> indexer, which will not take
        /// a boxed <c>int</c> for a <c>long</c> slot - it raises
        /// <c>InvalidPropertyException</c>. The compiled path could convert and succeed, and that is
        /// exactly why it must not: it would answer where the interpreter throws, and which backend
        /// runs is not the caller's choice.
        /// </remarks>
        [Test]
        public void AWriteTheInterpreterCannotPerformIsRefusedRatherThanCompiled()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<IndexerAssignmentHolder, object>(
                    "LongList[0] = 5", EvaluationMode.MustCompile));

            Assert.Throws<InvalidPropertyException>(
                () => Expression.ParseGetter<IndexerAssignmentHolder, object>(
                        "LongList[0] = 5", EvaluationMode.MustInterpret)
                    .GetValue(new IndexerAssignmentHolder()));
        }

        /// <summary>
        /// The write still has a void form. It would not if the value were carried by wrapping the
        /// call in a block: a void expression must emit a void call or an assignment node, and a
        /// block is neither.
        /// </summary>
        [Test]
        public void AnIndexerWriteStillHasAVoidForm()
        {
            foreach (var expression in new[] { "Numbers[0] = 5", "Ints[0] = 5", "Map['k'] = 5" })
            {
                var holder = new IndexerAssignmentHolder();

                Expression.ParseVoidExpression<IndexerAssignmentHolder>(
                        expression, EvaluationMode.MustCompile)
                    .Execute(holder);

                Assert.AreEqual(5, holder.Numbers[0] + holder.Ints[0] + (holder.Map.ContainsKey("k") ? holder.Map["k"] : 0),
                    expression + " wrote nothing");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    #region Test model

    /// <summary>A value type, to exercise '?.' where the left side is a struct.</summary>
    public struct NullConditionalPoint
    {
        public int X { get; set; }
    }

    public class NullConditionalAuthor
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string[] Tags { get; set; }

        /// <summary>Records the last void call, so a skipped call can be told from an executed one.</summary>
        public string LastAction;

        public string Greet()
        {
            return "hello " + Name;
        }

        public string Greet(string greeting)
        {
            return greeting + " " + Name;
        }

        public void Record(string action)
        {
            LastAction = action;
        }
    }

    public class NullConditionalArticle
    {
        public NullConditionalAuthor Author { get; set; }
        public string Title { get; set; }
    }

    public class NullConditionalRoot
    {
        public NullConditionalArticle[] Items;
        public IList<NullConditionalArticle> ItemList;
        public IDictionary<string, NullConditionalArticle> Map;

        public NullConditionalAuthor Author;

        public int Age = 42;
        public int? NullableAge;
        public NullConditionalPoint Point;
        public NullConditionalPoint? NullablePoint;

        /// <summary>Counts invocations, so that "evaluated exactly once" can be asserted.</summary>
        public int GetItemsCallCount;

        public NullConditionalArticle[] GetItems()
        {
            GetItemsCallCount++;
            return Items;
        }
    }

    #endregion

    /// <summary>
    /// Tests for the null-conditional access operators '?.' and '?['.
    /// </summary>
    /// <remarks>
    /// A node reached through one of these operators is skipped when the value flowing into it is null,
    /// and the rest of the access chain is abandoned with it, so the chain yields null instead of failing.
    /// Nearly every case runs through <see cref="BaseCompiledTests.TestCompiledVsInterpreted{TRoot,TResult}"/>
    /// so that the interpreted and compiled backends are both covered and are asserted to agree.
    /// </remarks>
    [TestFixture]
    public class NullConditionalOperatorTests : BaseCompiledTests
    {
        private static NullConditionalAuthor Tesla()
        {
            return new NullConditionalAuthor { Name = "Nikola Tesla", Age = 86 };
        }

        /// <summary>A fully populated graph: every link in the test chains is non-null.</summary>
        private static NullConditionalRoot FullGraph()
        {
            var author = Tesla();

            return new NullConditionalRoot
            {
                Items = new[] { new NullConditionalArticle { Author = author, Title = "Alternating Current" } },
                ItemList = new List<NullConditionalArticle> { new NullConditionalArticle { Title = "From a list" } },
                Map = new Dictionary<string, NullConditionalArticle>
                    { { "first", new NullConditionalArticle { Title = "From a map" } } },
                Author = author,
                NullableAge = 7,
                NullablePoint = new NullConditionalPoint { X = 5 },
                Point = new NullConditionalPoint { X = 9 }
            };
        }

        /// <summary>Everything nullable is left null.</summary>
        private static NullConditionalRoot EmptyGraph()
        {
            return new NullConditionalRoot();
        }

        #region Property access

        [Test]
        public void PropertyAccessReturnsTheValueWhenTheContextIsNotNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author?.Name", FullGraph())
                .ResultEqualsTo("Nikola Tesla");
        }

        [Test]
        public void PropertyAccessReturnsNullInsteadOfFailingWhenTheContextIsNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author?.Name", EmptyGraph())
                .ResultEqualsTo(null);
        }

        [Test]
        public void PropertyAccessStillWorksWhenMixedWithPlainDottedAccess()
        {
            var graph = FullGraph();

            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author?.Name", graph).ResultEqualsTo("Nikola Tesla");
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author.Name", graph).ResultEqualsTo("Nikola Tesla");
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Items[0]?.Title", graph)
                .ResultEqualsTo("Alternating Current");
        }

        #endregion

        #region Method calls

        [Test]
        public void MethodCallIsInvokedWhenTheContextIsNotNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author?.Greet()", FullGraph())
                .ResultEqualsTo("hello Nikola Tesla");
        }

        [Test]
        public void MethodCallWithArgumentsIsInvokedWhenTheContextIsNotNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author?.Greet('good day')", FullGraph())
                .ResultEqualsTo("good day Nikola Tesla");
        }

        [Test]
        public void MethodCallYieldsNullAndIsNotInvokedWhenTheContextIsNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author?.Greet()", EmptyGraph())
                .ResultEqualsTo(null);
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Author?.Greet('good day')", EmptyGraph())
                .ResultEqualsTo(null);
        }

        #endregion

        #region Indexers

        [Test]
        public void ArrayIndexerYieldsTheElementOrNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Items?[0]?.Title", FullGraph())
                .ResultEqualsTo("Alternating Current");
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Items?[0]?.Title", EmptyGraph())
                .ResultEqualsTo(null);
        }

        [Test]
        public void ListIndexerYieldsTheElementOrNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("ItemList?[0]?.Title", FullGraph())
                .ResultEqualsTo("From a list");
            TestCompiledVsInterpreted<NullConditionalRoot, string>("ItemList?[0]?.Title", EmptyGraph())
                .ResultEqualsTo(null);
        }

        [Test]
        public void DictionaryIndexerYieldsTheElementOrNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Map?['first']?.Title", FullGraph())
                .ResultEqualsTo("From a map");
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Map?['first']?.Title", EmptyGraph())
                .ResultEqualsTo(null);
        }

        [Test]
        public void IndexerYieldsNullWhenTheIndexedElementItselfIsNull()
        {
            var graph = new NullConditionalRoot { Items = new NullConditionalArticle[] { null } };

            TestCompiledVsInterpreted<NullConditionalRoot, string>("Items?[0]?.Title", graph).ResultEqualsTo(null);
        }

        #endregion

        #region Long chains and the extent of the short circuit

        [Test]
        public void LongChainYieldsTheValueWhenEveryLinkIsPresent()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Items?[0]?.Author?.Name", FullGraph())
                .ResultEqualsTo("Nikola Tesla");
        }

        [Test]
        public void LongChainYieldsNullWhateverStageIsNull()
        {
            // null container
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Items?[0]?.Author?.Name", EmptyGraph())
                .ResultEqualsTo(null);

            // null element
            TestCompiledVsInterpreted<NullConditionalRoot, string>(
                    "Items?[0]?.Author?.Name",
                    new NullConditionalRoot { Items = new NullConditionalArticle[] { null } })
                .ResultEqualsTo(null);

            // null property part way along
            TestCompiledVsInterpreted<NullConditionalRoot, string>(
                    "Items?[0]?.Author?.Name",
                    new NullConditionalRoot { Items = new[] { new NullConditionalArticle() } })
                .ResultEqualsTo(null);
        }

        [Test]
        public void ShortCircuitAbandonsTheWholeRemainderOfTheChain()
        {
            // Only the indexer is null-conditional. When the container is null the trailing plain accesses
            // '.Author.Name' must be skipped too, rather than being attempted against null.
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Items?[0].Author.Name", EmptyGraph())
                .ResultEqualsTo(null);
        }

        [Test]
        public void PlainAccessAfterAShortCircuitedLinkIsStillUnprotected()
        {
            // '?.' guards only the context flowing into its own node. Here the container and element are
            // present, so the chain proceeds and the trailing plain '.Name' meets a null Author - which must
            // still fail rather than being silently treated as null.
            // The two backends report this through different exception types, so only the fact that both
            // reject it is asserted.
            var graph = new NullConditionalRoot { Items = new[] { new NullConditionalArticle() } };

            AssertBothBackendsThrow<NullConditionalRoot, string>("Items?[0].Author.Name", graph);
        }

        #endregion

        #region Value types

        [Test]
        public void ValueTypedResultIsReturnedNormallyWhenNothingIsNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, int?>("Author?.Age", FullGraph()).ResultEqualsTo(86);
        }

        [Test]
        public void ValueTypedResultBecomesNullRatherThanZeroWhenShortCircuited()
        {
            // The distinction matters: a widened result carries null, so a skipped chain is distinguishable
            // from one that genuinely evaluated to 0.
            TestCompiledVsInterpreted<NullConditionalRoot, int?>("Author?.Age", EmptyGraph()).ResultEqualsTo(null);
            TestCompiledVsInterpreted<NullConditionalRoot, int?>("Items?[0]?.Author?.Age", EmptyGraph())
                .ResultEqualsTo(null);
        }

        [Test]
        public void NullableValueTypeOnTheLeftIsTestedForAValue()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>("NullableAge?.ToString()", FullGraph())
                .ResultEqualsTo("7");
            TestCompiledVsInterpreted<NullConditionalRoot, string>("NullableAge?.ToString()", EmptyGraph())
                .ResultEqualsTo(null);
        }

        [Test]
        public void NullableStructOnTheLeftIsUnwrappedForTheAccess()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, int?>("NullablePoint?.X", FullGraph()).ResultEqualsTo(5);
            TestCompiledVsInterpreted<NullConditionalRoot, int?>("NullablePoint?.X", EmptyGraph()).ResultEqualsTo(null);
        }

        [Test]
        public void NonNullableValueTypeOnTheLeftBehavesAsAPlainAccess()
        {
            // Such a context can never be null, so there is nothing to short circuit and the access simply
            // happens. The result is therefore not widened either.
            TestCompiledVsInterpreted<NullConditionalRoot, int>("Point?.X", FullGraph()).ResultEqualsTo(9);
            TestCompiledVsInterpreted<NullConditionalRoot, string>("Age?.ToString()", FullGraph())
                .ResultEqualsTo("42");
        }

        #endregion

        #region Evaluation happens exactly once

        [Test]
        public void ContextIsEvaluatedExactlyOnceWhenTheChainCompletes()
        {
            AssertGetItemsCallCount("GetItems()?[0]?.Author?.Name", FullGraph, expectedCalls: 1);
        }

        [Test]
        public void ContextIsEvaluatedExactlyOnceWhenTheChainShortCircuits()
        {
            // The interesting case: the method must run once to produce the null that triggers the short
            // circuit, and must not be run again by the null test itself.
            AssertGetItemsCallCount("GetItems()?[0]?.Author?.Name", EmptyGraph, expectedCalls: 1);
        }

        private static void AssertGetItemsCallCount(
            string expression,
            Func<NullConditionalRoot> graphFactory,
            int expectedCalls)
        {
            var interpretedRoot = graphFactory();
            Expression.ParseGetter<NullConditionalRoot, object>(expression, CompileOptions.MustUseInterpreter)
                .GetValue(interpretedRoot);
            Assert.AreEqual(expectedCalls, interpretedRoot.GetItemsCallCount,
                "interpreted: '" + expression + "' evaluated its context the wrong number of times");

            var compiledRoot = graphFactory();
            Expression.ParseGetter<NullConditionalRoot, object>(expression, CompileOptions.CompileOnParse)
                .GetValue(compiledRoot);
            Assert.AreEqual(expectedCalls, compiledRoot.GetItemsCallCount,
                "compiled: '" + expression + "' evaluated its context the wrong number of times");
        }

        #endregion

        #region Interaction with other operators

        [Test]
        public void NullCoalescingCanSupplyAFallbackForAShortCircuitedChain()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, string>(
                    "Items?[0]?.Author?.Name ?? 'unknown'", EmptyGraph())
                .ResultEqualsTo("unknown");
            TestCompiledVsInterpreted<NullConditionalRoot, string>(
                    "Items?[0]?.Author?.Name ?? 'unknown'", FullGraph())
                .ResultEqualsTo("Nikola Tesla");
        }

        [Test]
        public void ShortCircuitedChainComparesEqualToNull()
        {
            TestCompiledVsInterpreted<NullConditionalRoot, bool>("Author?.Name == null", EmptyGraph())
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<NullConditionalRoot, bool>("Author?.Name == null", FullGraph())
                .ResultEqualsTo(false);
        }

        [Test]
        public void OtherQuestionMarkOperatorsStillLexCorrectly()
        {
            // '?.' and '?[' share their first character with the ternary '?', the null-coalescing '??' and
            // the selection '?{', so adding them must not steal any of those.
            Assert.DoesNotThrow(() => Expression.Parse("{1,2}.?{#this > 0}"), "selection '?{' must still parse");

            TestCompiledVsInterpreted<string>("null ?? 'fallback'").ResultEqualsTo("fallback");
            TestCompiledVsInterpreted<string>("true ? 'yes' : 'no'").ResultEqualsTo("yes");
        }

        #endregion

        #region Void expressions

        [Test]
        public void VoidCallRunsWhenTheContextIsNotNull()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                var graph = new NullConditionalRoot { Author = Tesla() };

                Expression.ParseVoidExpression<NullConditionalRoot>("Author?.Record('done')", options)
                    .Execute(graph);

                Assert.AreEqual("done", graph.Author.LastAction, "with " + options);
            }
        }

        [Test]
        public void VoidCallIsSkippedWithoutFailingWhenTheContextIsNull()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                var graph = EmptyGraph();

                // Nothing to call and nothing to report: this must simply do nothing.
                Assert.DoesNotThrow(
                    () => Expression.ParseVoidExpression<NullConditionalRoot>("Author?.Record('done')", options)
                        .Execute(graph),
                    "with " + options);
            }
        }

        #endregion

        #region Assignment is refused

        [Test]
        public void AssigningThroughANullConditionalAccessIsRefused()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                var graph = FullGraph();

                Assert.Throws<NotSupportedException>(
                    () => Expression.ParseSetter<NullConditionalRoot, string>("Author?.Name", options)
                        .SetValue(graph, "someone else"),
                    "with " + options);
            }
        }

        [Test]
        public void AssigningThroughANullConditionalIndexerIsRefused()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                var graph = FullGraph();

                Assert.Throws<NotSupportedException>(
                    () => Expression.ParseSetter<NullConditionalRoot, string>("Items?[0].Title", options)
                        .SetValue(graph, "new title"),
                    "with " + options);
            }
        }

        [Test]
        public void AssignmentWrittenInsideTheExpressionIsAlsoRefused()
        {
            // '=' inside an expression reaches the target through a different route than the setter API
            // above, so it needs covering separately. Both spellings of the assignment must be rejected.
            foreach (var options in InterpretedAndCompiled())
            {
                Assert.Throws<NotSupportedException>(
                    () => Expression.ParseVoidExpression<NullConditionalRoot>("Author?.Name = 'someone else'", options)
                        .Execute(FullGraph()),
                    "as a statement, with " + options);

                Assert.Throws<NotSupportedException>(
                    () => Expression.ParseGetter<NullConditionalRoot, object>("Author?.Name = 'someone else'", options)
                        .GetValue(FullGraph()),
                    "as a value-producing expression, with " + options);
            }
        }

        [Test]
        public void AssignmentInsideTheExpressionThroughANullConditionalIndexerIsAlsoRefused()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                Assert.Throws<NotSupportedException>(
                    () => Expression.ParseVoidExpression<NullConditionalRoot>("Items?[0].Title = 'new title'", options)
                        .Execute(FullGraph()),
                    "with " + options);
            }
        }

        [Test]
        public void RefusalDoesNotWriteAnythingBeforeThrowing()
        {
            // The target must be rejected outright rather than part way through, so the graph is untouched.
            var graph = FullGraph();
            var originalName = graph.Author.Name;

            Assert.Throws<NotSupportedException>(
                () => Expression.ParseVoidExpression<NullConditionalRoot>(
                        "Author?.Name = 'someone else'", CompileOptions.MustUseInterpreter)
                    .Execute(graph));

            Assert.AreEqual(originalName, graph.Author.Name);
        }

        [Test]
        public void OrdinaryAssignmentIsUnaffected()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                // Through the setter API...
                var viaApi = FullGraph();
                Expression.ParseSetter<NullConditionalRoot, string>("Author.Name", options)
                    .SetValue(viaApi, "someone else");
                Assert.AreEqual("someone else", viaApi.Author.Name, "setter API, with " + options);

                // ...and written inside the expression.
                var viaExpression = FullGraph();
                Expression.ParseVoidExpression<NullConditionalRoot>("Author.Name = 'someone else'", options)
                    .Execute(viaExpression);
                Assert.AreEqual("someone else", viaExpression.Author.Name, "'=' in expression, with " + options);
            }
        }

        [Test]
        public void ReadingThroughANullConditionalAccessIsStillAllowedOnTheRightHandSide()
        {
            // Only the assignment target is restricted. The operator remains usable in the value being
            // assigned, where short-circuiting is meaningful.
            foreach (var options in InterpretedAndCompiled())
            {
                var graph = FullGraph();

                Expression.ParseVoidExpression<NullConditionalRoot>("Author.Name = Items?[0]?.Title", options)
                    .Execute(graph);

                Assert.AreEqual("Alternating Current", graph.Author.Name, "with " + options);

                var emptyItems = new NullConditionalRoot { Author = Tesla() };
                Expression.ParseVoidExpression<NullConditionalRoot>("Author.Name = Items?[0]?.Title", options)
                    .Execute(emptyItems);

                Assert.IsNull(emptyItems.Author.Name, "a short-circuited right-hand side assigns null, with " + options);
            }
        }

        #endregion

        #region Variables as the left side

        // A '#variable' has a setter of its own - assigning to one writes into the variables dictionary -
        // so a chain rooted in a variable reaches the assignment machinery by yet another route and needs
        // covering separately.
        //
        // Reading a member of a variable is currently interpreter-only: a variable's value is typed as
        // object, and the compiled backend cannot resolve a member against object. That applies equally
        // with and without '?.' ("#a.Name" fails compiled exactly as "#a?.Name" does), so the read tests
        // here use the interpreter, while the refusal tests cover both backends.

        private static IDictionary<string, object> VariablesWith(object value)
        {
            return new Dictionary<string, object> { { "author", value } };
        }

        [Test]
        public void NullConditionalOnAVariableShortCircuitsWhenReading()
        {
            var expression = InterpretGetter<object, string>("#author?.Name");

            Assert.AreEqual("Nikola Tesla", expression.GetValue(null, VariablesWith(Tesla())));
            Assert.IsNull(expression.GetValue(null, VariablesWith(null)));
        }

        [Test]
        public void NullConditionalChainOnAVariableShortCircuitsAtEveryStage()
        {
            var expression = InterpretGetter<object, string>("#author?.Name?.ToString()");

            Assert.AreEqual("Nikola Tesla", expression.GetValue(null, VariablesWith(Tesla())));
            Assert.IsNull(expression.GetValue(null, VariablesWith(null)));
            Assert.IsNull(expression.GetValue(null, VariablesWith(new NullConditionalAuthor())),
                "a null Name must short circuit the trailing call");
        }

        [Test]
        public void NullConditionalOnAVariableIsRefusedAsAnAssignmentTarget()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                Assert.Throws<NotSupportedException>(
                    () => Expression.ParseVoidExpression<object>("#author?.Name = 'someone else'", options)
                        .Execute(null, VariablesWith(Tesla())),
                    "with " + options);
            }
        }

        [Test]
        public void NullConditionalIndexerOnAVariableIsRefusedAsAnAssignmentTarget()
        {
            foreach (var options in InterpretedAndCompiled())
            {
                Assert.Throws<NotSupportedException>(
                    () => Expression.ParseVoidExpression<object>("#author?.Tags?[0] = 'new tag'", options)
                        .Execute(null, VariablesWith(Tesla())),
                    "with " + options);
            }
        }

        [Test]
        public void AssigningToAVariableItselfIsUnaffected()
        {
            var variables = VariablesWith(Tesla());

            Expression.ParseVoidExpression<object>("#author = 'replaced'", CompileOptions.MustUseInterpreter)
                .Execute(null, variables);

            Assert.AreEqual("replaced", variables["author"]);
        }

        [Test]
        public void NullConditionalOnAVariableIsStillAllowedAsTheAssignedValue()
        {
            var target = new NullConditionalRoot { Author = Tesla() };

            Expression.ParseVoidExpression<NullConditionalRoot>(
                    "Author.Name = #author?.Name", CompileOptions.MustUseInterpreter)
                .Execute(target, VariablesWith(new NullConditionalAuthor { Name = "from variable" }));

            Assert.AreEqual("from variable", target.Author.Name);

            Expression.ParseVoidExpression<NullConditionalRoot>(
                    "Author.Name = #author?.Name", CompileOptions.MustUseInterpreter)
                .Execute(target, VariablesWith(null));

            Assert.IsNull(target.Author.Name, "a short-circuited variable chain assigns null");
        }

        #endregion

        #region Helpers

        /// <summary>The two option sets that force each backend, so a test can assert against both.</summary>
        private static IEnumerable<CompileOptions> InterpretedAndCompiled()
        {
            yield return CompileOptions.MustUseInterpreter;
            yield return CompileOptions.CompileOnParse;
        }

        /// <summary>
        /// Asserts that both backends reject the expression, without requiring them to agree on the
        /// exception type.
        /// </summary>
        private static void AssertBothBackendsThrow<TRoot, TResult>(string expression, TRoot root)
        {
            foreach (var options in InterpretedAndCompiled())
            {
                Exception caught = null;
                try
                {
                    Expression.ParseGetter<TRoot, TResult>(expression, options).GetValue(root);
                }
                catch (Exception exception)
                {
                    caught = exception;
                }

                Assert.IsNotNull(caught, "'" + expression + "' should have failed with " + options);
            }
        }

        #endregion
    }
}

using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Context for the exits: methods to call, properties of varying declared type, and a collection
    /// the caller owns.
    /// </summary>
    public class ConstructedSetExitsContext
    {
        public object Received;

        public object ObjProp { get; set; }
        public HashSet<int> HashSetOfIntProp { get; set; }
        public ISet<object> SetOfObjectProp { get; set; }

        /// <summary>
        /// A collection the caller owns, to sit beside the ones the engine builds: it must leave by
        /// every exit as the very instance, which is what stops the reshaping being unconditional.
        /// </summary>
        public List<int> Owned { get; set; } = new List<int> { 9, 8 };

        public string Take(object value)
        {
            Received = value;
            return "taken";
        }

        /// <summary>
        /// Hands its argument straight back, so the exit can be observed from the caller's side rather
        /// than from inside the object graph. This is the shape that made the item-type divergence
        /// reach a consumer: they cast what the expression returned.
        /// </summary>
        public object Wrap(object value)
        {
            Received = value;
            return value;
        }

        /// <summary>
        /// A parameter that names the item type, so the reshaping has to leave the collection alone.
        /// </summary>
        public string TakeSetOfInt(ISet<int> value)
        {
            Received = value;
            return "typed";
        }

        /// <summary>
        /// A parameter that names a collection but no item type. It accepts the typed shape and the
        /// interpreter's alike, which is exactly why it has to be given the interpreter's.
        /// </summary>
        public string TakeNonGenericList(IList value)
        {
            Received = value;
            return "non-generic";
        }

        /// <summary>The same question as a member rather than a parameter.</summary>
        public IList NonGenericListProp { get; set; }
    }

    /// <summary>
    /// A set the engine builds is a plain BCL HashSet wherever it surfaces.
    /// </summary>
    /// <remarks>
    /// A collection the engine built is reshaped at every exit, so both backends hand out the same
    /// shape. Which collections it built is recorded on the CompilationContext, by registering the
    /// emitted expression, and deliberately not by giving the collections a type of their own: a type
    /// travels with the value, and these tests are the exits it would travel through. They were written
    /// while an internal HashSet subclass was in use, and every one of them showed it reaching user code.
    ///
    /// Until 2026-09-11 only two exits were reshaped - the value a getter returns, and a local's slot -
    /// and the seven below all handed out the typed collection the compiled path had built while the
    /// interpreter handed out one of object. Five tests here asserted that typed shape: they were
    /// written to catch the subclass and blessed the item type by accident, which is why they now read
    /// the other way. Each has an interpreted twin beside it, so the agreement is pinned from both
    /// sides and neither can move alone.
    ///
    /// The assertions compare the exact runtime type rather than using IsInstanceOf, which would pass on a
    /// subclass and so miss precisely what these tests are for.
    /// </remarks>
    [TestFixture]
    public class ConstructedSetExitsTests : BaseCompiledTests
    {
        // ---------- returned as the value of a getter: reshaped to what the interpreter would build ----------

        [Test]
        public void ReturnedFromAGetterAskedForASetOfInt()
        {
            var value = CompileGetter<ConstructedSetExitsContext, ISet<int>>("{1,2} + {3}")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<int>), value.GetType());
        }

        [Test]
        public void ReturnedFromAWeaklyTypedGetter()
        {
            var value = CompileGetter<ConstructedSetExitsContext, object>("{1,2} + {3}")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<object>), value.GetType());
        }

        // ---------- every other exit: reshaped as well, so the backends agree ----------

        /// <summary>
        /// A literal holding a collection has no compiled form - a collection is a non-sealed reference
        /// type, and the literal rule declines an element the runtime could narrow - so the interpreter
        /// serves this shape and its set is object-typed as always.
        /// </summary>
        /// <remarks>
        /// This was a compiled pin until the literal rule landed. It is kept as the record of what that
        /// rule costs at this exit, and its interpreted half is unchanged.
        /// </remarks>
        [Test]
        public void NestedInAReturnedListIsNotCompiled()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ConstructedSetExitsContext, object>("{ {1,2} + {3} }"));
        }

        [Test]
        public void NestedInAReturnedListByTheInterpreter()
        {
            var outer = (IList)InterpretGetter<ConstructedSetExitsContext, object>("{ {1,2} + {3} }")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<object>), outer[0].GetType());
        }

        /// <summary>
        /// A map literal holding a collection has no compiled form, for the same reason as the list
        /// above: a collection is a non-sealed reference type, and the literal rule declines a
        /// component the runtime could narrow.
        /// </summary>
        [Test]
        public void AsAValueInAReturnedMapIsNotCompiled()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ConstructedSetExitsContext, object>("#{1 : {1,2} + {3}}"));
        }

        [Test]
        public void AsAValueInAReturnedMapByTheInterpreter()
        {
            var map = (IDictionary)InterpretGetter<ConstructedSetExitsContext, object>("#{1 : {1,2} + {3}}")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<object>), map[1].GetType());
        }

        /// <summary>
        /// An array item is an exit too, and it is the one that was missed when the others were first
        /// measured - the row was added to the probe only after the change had landed, so it had to be
        /// measured separately against the old code to confirm it had diverged like the rest.
        /// </summary>
        [Test]
        public void AsAnItemOfAConstructedObjectArray()
        {
            var array = (IList)CompileGetter<ConstructedSetExitsContext, object>("new object[] { {1,2} + {3} }")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<object>), array[0].GetType());
        }

        [Test]
        public void AsAnItemOfAConstructedObjectArrayByTheInterpreter()
        {
            var array = (IList)InterpretGetter<ConstructedSetExitsContext, object>("new object[] { {1,2} + {3} }")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<object>), array[0].GetType());
        }

        [Test]
        public void PassedToAMethodOnTheContext()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("Take({1,2} + {3})").GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.Received.GetType());
        }

        [Test]
        public void PassedToAMethodOnTheContextByTheInterpreter()
        {
            var context = new ConstructedSetExitsContext();

            InterpretGetter<ConstructedSetExitsContext, object>("Take({1,2} + {3})").GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.Received.GetType());
        }

        /// <summary>
        /// The worst of the exits, because it is the only one where the difference reached the caller:
        /// Wrap hands its argument back, so a consumer casting the expression's result got an exception
        /// on one backend and not the other, decided by their declared context type rather than by
        /// anything they wrote.
        /// </summary>
        [Test]
        public void ReturnedBackOutOfAMethodToTheCaller()
        {
            var value = CompileGetter<ConstructedSetExitsContext, object>("Wrap({1,2} + {3})")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<object>), value.GetType());
        }

        [Test]
        public void ReturnedBackOutOfAMethodToTheCallerByTheInterpreter()
        {
            var value = InterpretGetter<ConstructedSetExitsContext, object>("Wrap({1,2} + {3})")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<object>), value.GetType());
        }

        [Test]
        public void AssignedToAnObjectProperty()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("ObjProp = {1,2} + {3}").GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.ObjProp.GetType());
        }

        [Test]
        public void AssignedToAnObjectPropertyByTheInterpreter()
        {
            var context = new ConstructedSetExitsContext();

            InterpretGetter<ConstructedSetExitsContext, object>("ObjProp = {1,2} + {3}").GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.ObjProp.GetType());
        }

        [Test]
        public void StoredInTheCallersVariables()
        {
            var variables = new Dictionary<string, object>();

            CompileGetter<ConstructedSetExitsContext, object>("#x = {1,2} + {3}")
                .GetValue(new ConstructedSetExitsContext(), variables);

            Assert.AreEqual(typeof(HashSet<object>), variables["x"].GetType());
        }

        [Test]
        public void StoredInTheCallersVariablesByTheInterpreter()
        {
            var variables = new Dictionary<string, object>();

            InterpretGetter<ConstructedSetExitsContext, object>("#x = {1,2} + {3}")
                .GetValue(new ConstructedSetExitsContext(), variables);

            Assert.AreEqual(typeof(HashSet<object>), variables["x"].GetType());
        }

        // ---------- a sink that names the item type keeps it ----------

        /// <summary>
        /// The declared type of the sink is what decides: reshaping is not unconditional, or every typed
        /// assignment and every typed parameter would lose its compiled form.
        /// </summary>
        [Test]
        public void AssignedToAHashSetOfIntProperty()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("HashSetOfIntProp = {1,2} + {3}")
                .GetValue(context);

            Assert.AreEqual(typeof(HashSet<int>), context.HashSetOfIntProp.GetType());
        }

        [Test]
        public void PassedToAParameterThatNamesTheItemType()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("TakeSetOfInt({1,2} + {3})").GetValue(context);

            Assert.AreEqual(typeof(HashSet<int>), context.Received.GetType());
        }

        /// <summary>
        /// And the interpreter cannot make that call at all - it has only a set of object to offer an
        /// ISet&lt;int&gt; parameter - so one backend answers where the other throws.
        /// </summary>
        /// <remarks>
        /// DO NOT FIX ONE SIDE. This is pre-existing and was measured identical before and after the
        /// exits were reshaped: the sink names the item type, so nothing about this row changed. It is
        /// the mirror image of the divergence this fixture is about - there the compiled path knew an
        /// item type the interpreter did not, here a parameter demands one the interpreter cannot
        /// produce. Closing it means deciding what the interpreter should do with a typed parameter,
        /// which is a ruling of its own and is recorded in _Docs/open-issues.md.
        /// </remarks>
        [Test]
        public void PassedToAParameterThatNamesTheItemTypeThrowsInTheInterpreter()
        {
            Assert.Throws<InvalidCastException>(
                () => InterpretGetter<ConstructedSetExitsContext, object>("TakeSetOfInt({1,2} + {3})")
                    .GetValue(new ConstructedSetExitsContext()));
        }

        // ---------- a sink that names no item type gets the interpreter's shape ----------

        /// <summary>
        /// The non-generic System.Collections.IList accepts a List&lt;int&gt; and a List&lt;object&gt;
        /// alike, so it cannot settle which one to hand over - and the answer is the interpreter's,
        /// exactly as for an object-typed sink.
        /// </summary>
        /// <remarks>
        /// These thirteen rows survived the first pass at reshaping the exits, because the question
        /// being asked was "does the sink accept the typed shape". It does, so the item type was kept
        /// and the interpreter still handed over a collection of object. The right question is whether
        /// the sink accepts the INTERPRETER's shape: if it does, that is what both backends hand over.
        /// Found by measuring the parameter surface for a different question, not by a test.
        /// </remarks>
        /// <summary>
        /// A projection, not a literal: a literal keeps its own item type on both backends now, so it
        /// no longer exercises the reshaping at all. A projection is computed, stays object-typed, and
        /// is what these rows are about.
        /// </summary>
        [Test]
        public void PassedToANonGenericCollectionParameter()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("TakeNonGenericList(Owned.!{#this})")
                .GetValue(context);

            Assert.AreEqual(typeof(List<object>), context.Received.GetType());
        }

        [Test]
        public void PassedToANonGenericCollectionParameterByTheInterpreter()
        {
            var context = new ConstructedSetExitsContext();

            InterpretGetter<ConstructedSetExitsContext, object>("TakeNonGenericList(Owned.!{#this})")
                .GetValue(context);

            Assert.AreEqual(typeof(List<object>), context.Received.GetType());
        }

        [Test]
        public void AssignedToANonGenericListProperty()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("NonGenericListProp = Owned.!{#this}")
                .GetValue(context);

            Assert.AreEqual(typeof(List<object>), context.NonGenericListProp.GetType());
        }

        [Test]
        public void AssignedToANonGenericListPropertyByTheInterpreter()
        {
            var context = new ConstructedSetExitsContext();

            InterpretGetter<ConstructedSetExitsContext, object>("NonGenericListProp = Owned.!{#this}")
                .GetValue(context);

            Assert.AreEqual(typeof(List<object>), context.NonGenericListProp.GetType());
        }

        /// <summary>
        /// And the same at the root, which is where the predicate lives: a non-generic requested type
        /// gets the interpreter's shape, while one that names the item type still gets it.
        /// </summary>
        [Test]
        public void RequestedAsANonGenericList()
        {
            var value = CompileGetter<ConstructedSetExitsContext, IList>("Owned.!{#this}")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(List<object>), value.GetType());
        }

        [Test]
        public void RequestedAsAListOfInt()
        {
            var value = CompileGetter<ConstructedSetExitsContext, List<int>>("Owned.!{#this}")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(List<int>), value.GetType());
        }

        // ---------- a collection the caller owns is never reshaped ----------

        /// <summary>
        /// Only a collection the engine built is registered, so the caller's own object leaves by every
        /// exit as the very instance. Reference identity, not merely an equal shape: copying it would be
        /// a wrong answer, and it is the reason the registry exists rather than the reshaping being
        /// applied to anything that happens to be a collection.
        /// </summary>
        [Test]
        public void ACallerOwnedCollectionPassedToAMethodIsTheVeryInstanceOnBothBackends()
        {
            var compiledContext = new ConstructedSetExitsContext();
            CompileGetter<ConstructedSetExitsContext, object>("Take(Owned)").GetValue(compiledContext);

            var interpretedContext = new ConstructedSetExitsContext();
            InterpretGetter<ConstructedSetExitsContext, object>("Take(Owned)").GetValue(interpretedContext);

            Assert.AreSame(compiledContext.Owned, compiledContext.Received);
            Assert.AreSame(interpretedContext.Owned, interpretedContext.Received);
        }

        /// <summary>
        /// Nesting the caller's own collection in a literal is interpreter-only for the same reason as
        /// the test above - the element is a collection - and the instance still comes through
        /// untouched, which is what this row is really guarding.
        /// </summary>
        [Test]
        public void ACallerOwnedCollectionNestedInABuiltListIsTheVeryInstance()
        {
            var context = new ConstructedSetExitsContext();
            var outer = (IList)InterpretGetter<ConstructedSetExitsContext, object>("{ Owned }")
                .GetValue(context);

            Assert.AreSame(context.Owned, outer[0]);

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ConstructedSetExitsContext, object>("{ Owned }"));
        }

        [Test]
        public void ACallerOwnedCollectionAssignedToAnObjectPropertyIsTheVeryInstanceOnBothBackends()
        {
            var compiledContext = new ConstructedSetExitsContext();
            CompileGetter<ConstructedSetExitsContext, object>("ObjProp = Owned").GetValue(compiledContext);

            var interpretedContext = new ConstructedSetExitsContext();
            InterpretGetter<ConstructedSetExitsContext, object>("ObjProp = Owned")
                .GetValue(interpretedContext);

            Assert.AreSame(compiledContext.Owned, compiledContext.ObjProp);
            Assert.AreSame(interpretedContext.Owned, interpretedContext.ObjProp);
        }

        [Test]
        public void ACallerOwnedCollectionStoredInTheCallersVariablesIsTheVeryInstanceOnBothBackends()
        {
            var compiledContext = new ConstructedSetExitsContext();
            var compiledVariables = new Dictionary<string, object>();
            CompileGetter<ConstructedSetExitsContext, object>("#x = Owned")
                .GetValue(compiledContext, compiledVariables);

            var interpretedContext = new ConstructedSetExitsContext();
            var interpretedVariables = new Dictionary<string, object>();
            InterpretGetter<ConstructedSetExitsContext, object>("#x = Owned")
                .GetValue(interpretedContext, interpretedVariables);

            Assert.AreSame(compiledContext.Owned, compiledVariables["x"]);
            Assert.AreSame(interpretedContext.Owned, interpretedVariables["x"]);
        }

        // ---------- a property whose item type is object ----------

        /// <summary>
        /// Assigning a built set to an ISet&lt;object&gt; property compiles, and this is a capability the
        /// reshaping gained rather than one it was aimed at.
        /// </summary>
        /// <remarks>
        /// It used to refuse, with a CompileErrorException, for a reason that was true as written:
        /// ISet&lt;T&gt; is invariant, so there is no conversion from a HashSet&lt;int&gt; for the
        /// compiled tree to emit, and the weakly typed path fell back to the interpreter. Reshaping the
        /// value on the way into the member removes the obstacle instead of working around it - what
        /// arrives is a HashSet&lt;object&gt;, which the property simply accepts.
        ///
        /// The test that pinned the refusal said in its own remarks that it should fail if the boundary
        /// ever learned to reshape a value on assignment. It did, so this replaces it. The two tests
        /// below were written to assert the contract rather than which backend honours it, and they pass
        /// unchanged - which is what a pin phrased that way is for.
        /// </remarks>
        [Test]
        public void AssignedToASetOfObjectPropertyCompilesNow()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("SetOfObjectProp = {1,2} + {3}")
                .GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.SetOfObjectProp.GetType());
        }

        /// <summary>
        /// The interpreter manages it, because the set it builds already has object as its item type.
        /// </summary>
        [Test]
        public void AssignedToASetOfObjectPropertyTheInterpreterSucceeds()
        {
            var context = new ConstructedSetExitsContext();

            InterpretGetter<ConstructedSetExitsContext, object>("SetOfObjectProp = {1,2} + {3}")
                .GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.SetOfObjectProp.GetType());
        }

        /// <summary>
        /// And so the weakly typed path assigns successfully: refusing the compiled form as a
        /// CompileErrorException is what lets it fall back to the interpreter.
        /// </summary>
        [Test]
        public void AssignedToASetOfObjectPropertyTheWeaklyTypedPathFallsBack()
        {
            var context = new ConstructedSetExitsContext();

            Expression.Parse("SetOfObjectProp = {1,2} + {3}").GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.SetOfObjectProp.GetType());
        }
    }
}

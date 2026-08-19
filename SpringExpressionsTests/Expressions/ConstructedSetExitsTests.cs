using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Context for the exits: a method to call, and properties of varying declared type.
    /// </summary>
    public class ConstructedSetExitsContext
    {
        public object Received;

        public object ObjProp { get; set; }
        public HashSet<int> HashSetOfIntProp { get; set; }
        public ISet<object> SetOfObjectProp { get; set; }

        public string Take(object value)
        {
            Received = value;
            return "taken";
        }
    }

    /// <summary>
    /// A set the engine builds is a plain BCL HashSet wherever it surfaces.
    /// </summary>
    /// <remarks>
    /// Compiler reshapes a constructed set at one point only - the value a getter returns - so it has to
    /// know which collections it built. That is recorded on the CompilationContext, by registering the
    /// emitted expression, and deliberately not by giving the collections a type of their own: a type
    /// travels with the value, and these tests are the exits it would travel through. They were written
    /// while an internal HashSet subclass was in use, and every one of them showed it reaching user code.
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

        // ---------- every other exit: a plain HashSet, keeping the item type it was built with ----------

        [Test]
        public void NestedInAReturnedList()
        {
            var outer = (IList)CompileGetter<ConstructedSetExitsContext, object>("{ {1,2} + {3} }")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<int>), outer[0].GetType());
        }

        [Test]
        public void AsAValueInAReturnedMap()
        {
            var map = (IDictionary)CompileGetter<ConstructedSetExitsContext, object>("#{1 : {1,2} + {3}}")
                .GetValue(new ConstructedSetExitsContext());

            Assert.AreEqual(typeof(HashSet<int>), map[1].GetType());
        }

        [Test]
        public void PassedToAMethodOnTheContext()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("Take({1,2} + {3})").GetValue(context);

            Assert.AreEqual(typeof(HashSet<int>), context.Received.GetType());
        }

        [Test]
        public void AssignedToAnObjectProperty()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("ObjProp = {1,2} + {3}").GetValue(context);

            Assert.AreEqual(typeof(HashSet<int>), context.ObjProp.GetType());
        }

        [Test]
        public void AssignedToAHashSetOfIntProperty()
        {
            var context = new ConstructedSetExitsContext();

            CompileGetter<ConstructedSetExitsContext, object>("HashSetOfIntProp = {1,2} + {3}")
                .GetValue(context);

            Assert.AreEqual(typeof(HashSet<int>), context.HashSetOfIntProp.GetType());
        }

        [Test]
        public void StoredInTheCallersVariables()
        {
            var variables = new Dictionary<string, object>();

            CompileGetter<ConstructedSetExitsContext, object>("#x = {1,2} + {3}")
                .GetValue(new ConstructedSetExitsContext(), variables);

            Assert.AreEqual(typeof(HashSet<int>), variables["x"].GetType());
        }

        // ---------- what the interpreter does at the same exit ----------

        /// <summary>
        /// The interpreter has only boxed values, so it builds a set of object wherever the compiled path
        /// builds a set of int. The two therefore still differ in item type at every exit except a getter's
        /// value, which is the one place the compiled result is reshaped. Both are plain HashSets.
        /// </summary>
        [Test]
        public void PassedToAMethodOnTheContextByTheInterpreter()
        {
            var context = new ConstructedSetExitsContext();

            InterpretGetter<ConstructedSetExitsContext, object>("Take({1,2} + {3})").GetValue(context);

            Assert.AreEqual(typeof(HashSet<object>), context.Received.GetType());
        }

        // ---------- a property whose item type is object ----------

        /// <summary>
        /// A set of int cannot currently be assigned to an ISet&lt;object&gt; property - ISet&lt;T&gt; is
        /// invariant, so there is no conversion for the compiled tree to emit - and what this pins is that
        /// the refusal is reported as a CompileErrorException.
        /// </summary>
        /// <remarks>
        /// The exception type is the point, not the limitation. Only CompileErrorException lets the weakly
        /// typed path fall back, and this site threw a raw ArgumentException out of LExpression.Assign until
        /// BaseNode.BuildAssign wrapped it - which made the whole expression a hard failure for a weak
        /// caller. Read together with the two tests below: they show the expression is meaningful and that a
        /// consumer gets the right answer, which is what stops this one passing for an unrelated reason such
        /// as a mistyped property name.
        ///
        /// If the boundary later learns to reshape a value on assignment, this test should fail - update it
        /// then. The two below should keep passing either way, because they assert the contract rather than
        /// which backend honours it.
        /// </remarks>
        [Test]
        public void AssignedToASetOfObjectPropertyTheRefusalIsACompileError()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<ConstructedSetExitsContext, object>("SetOfObjectProp = {1,2} + {3}")
                    .GetValue(new ConstructedSetExitsContext()));
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

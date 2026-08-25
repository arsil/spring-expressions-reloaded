using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// Holds one stable instance, so a test can tell a set that was handed through from one that was copied.
    /// </summary>
    public class TypedSetHolder
    {
        public HashSet<int> IntSet { get; } = new HashSet<int> { 1, 2 };
    }

    /// <summary>
    /// Whether the two backends agree on the runtime type of a collection-operator result.
    /// </summary>
    /// <remarks>
    /// <see cref="BaseCompiledTests.TestCompiledVsInterpreted{TResult}"/> compares the runtime type as
    /// well as the value, which is the whole point here. The compiled path emits GenericsUnion&lt;T&gt;
    /// when both operands share an item type and so returns HashSet&lt;T&gt;, while the interpreter has
    /// only boxed values to work from and always returns HashSet&lt;object&gt;. So the same expression has
    /// two different result types depending on which backend ran it - and which backend runs it is not
    /// something the caller controls, since the weakly typed path compiles when it can and interprets when
    /// it cannot. The typed-operand tests below fail on that mismatch until the compiled result is
    /// normalised at the boundary, where no more specific type was requested.
    /// </remarks>
    [TestFixture]
    public class SetOperatorAgreementTests : BaseCompiledTests
    {
        [Test]
        public void UnionOfIntegers()
        {
            TestCompiledVsInterpreted<object>("{1,2,3} + {3,4,5}");
        }

        [Test]
        public void UnionOfStrings()
        {
            TestCompiledVsInterpreted<object>("{'a','b'} + {'b','c'}");
        }

        [Test]
        public void ChainedUnionOfIntegers()
        {
            TestCompiledVsInterpreted<object>("{1,2} + {2,3} + {4,5}");
        }

        /// <summary>
        /// Mixed item types leave the compiled path no common T, so it falls to TypelessUnion and both
        /// backends already produce HashSet&lt;object&gt;. This one passes today; it is here to pin that it
        /// keeps doing so.
        /// </summary>
        [Test]
        public void UnionOfMixedItemTypes()
        {
            TestCompiledVsInterpreted<object>("{1,2,3} + {3,4,5} + {'ivan', 5}");
        }

        /// <summary>
        /// Reading a set is not building one: the value handed back is the caller's own object, so it must
        /// arrive unchanged and unwrapped even though its item type is not object.
        /// </summary>
        /// <remarks>
        /// Reprojecting here would copy the caller's collection and lose reference identity - and the
        /// interpreter cannot lose it, because it simply returns what it read, so the two backends would
        /// disagree on a plain property access. Normalizing the root is therefore restricted to the
        /// operators that construct a set.
        /// </remarks>
        [Test]
        public void ReadingATypedSetReturnsThatVeryInstance()
        {
            var holder = new TypedSetHolder();

            var compiled = CompileGetter<TypedSetHolder, object>("IntSet").GetValue(holder);
            var interpreted = InterpretGetter<TypedSetHolder, object>("IntSet").GetValue(holder);

            Assert.AreSame(holder.IntSet, compiled, "compiled path returned a copy");
            Assert.AreSame(holder.IntSet, interpreted, "interpreted path returned a copy");
        }

        /// <summary>
        /// The same set read from a property, but now a union is built from it: that result is new, so it
        /// is normalized like any other constructed set.
        /// </summary>
        [Test]
        public void UnionOfATypedSetAndALiteral()
        {
            TestCompiledVsInterpreted<TypedSetHolder, object>("IntSet + {3}", new TypedSetHolder());
        }

        /// <summary>
        /// Asking for a set of object gets one, even though the operands share a narrower item type: the
        /// root result is reprojected to satisfy the request.
        /// </summary>
        /// <remarks>
        /// This used to throw, because ISet&lt;T&gt; is invariant and so a HashSet&lt;int&gt; body cannot be
        /// returned as ISet&lt;object&gt;.
        /// </remarks>
        [Test]
        public void ASetOfObjectCanBeRequestedFromTypedOperands()
        {
            var result = CompileGetter<ISet<object>>("{1,2} + {3}").GetValue();

            Assert.IsInstanceOf<HashSet<object>>(result);
            Assert.AreEqual(3, result.Count);
        }

        /// <summary>
        /// Asking for a set of the item type the operands share gets exactly a HashSet&lt;T&gt; - never the
        /// internal type the engine uses to mark a set it built.
        /// </summary>
        /// <remarks>
        /// The compiled union keeps the operands' item type, so the boundary copies it into a set of the item
        /// type actually asked for. Asserting the exact runtime type is the point: it pins that a plain BCL
        /// HashSet comes back, and would catch any wrapper or subclass being introduced between the two.
        /// </remarks>
        [Test]
        public void TheInternalMarkerTypeNeverReachesTheCaller()
        {
            var result = CompileGetter<ISet<int>>("{1,2} + {3}").GetValue();

            Assert.AreEqual(typeof(HashSet<int>), result.GetType());
            Assert.AreEqual(3, result.Count);
        }

        /// <summary>
        /// Asking for a set whose item type the tree can neither produce nor be reprojected to has to be
        /// refused with a <see cref="CompileErrorException"/> rather than the ArgumentException that
        /// LExpression.Lambda raises when it validates the body against the delegate's return type.
        /// </summary>
        /// <remarks>
        /// Only CompileErrorException lets the weakly typed path fall back to the interpreter rather than
        /// failing outright. Not reachable from that path today, which always asks for object - but it is
        /// what makes reprojecting the root safe, since that is where a request can turn out to be
        /// unsatisfiable.
        /// </remarks>
        [Test]
        public void RequestingAnUnsatisfiableSetTypeIsACompileError()
        {
            Assert.Throws<CompileErrorException>(
                () =>
                {
                    var getter = Expression.ParseGetter<ISet<string>>(
                        "{1,2} + {3}", EvaluationMode.MustCompile);
                    getter.GetValue();
                });
        }

        /// <summary>
        /// A typed request is satisfied by both backends - the compiled path keeps its HashSet&lt;T&gt;,
        /// the interpreted one reprojects its HashSet&lt;object&gt; - and both land on exactly a
        /// HashSet&lt;T&gt;.
        /// </summary>
        [Test]
        public void TypedRequestsAgreeOnAUnion()
        {
            var result = TestCompiledVsInterpreted<HashSet<int>>("{1,2} + {3}").Result;

            Assert.AreEqual(typeof(HashSet<int>), result.GetType());
            Assert.AreEqual(new HashSet<int> { 1, 2, 3 }, result);

            Assert.AreEqual(typeof(HashSet<int>),
                TestCompiledVsInterpreted<ISet<int>>("{1,2} + {3}").Result.GetType());
        }

        [Test]
        public void TypedRequestsAgreeOnAUnionOfATypedSetAndALiteral()
        {
            var holder = new TypedSetHolder();

            var result = TestCompiledVsInterpreted<TypedSetHolder, HashSet<int>>("IntSet + {3}", holder)
                .Result;

            Assert.AreEqual(typeof(HashSet<int>), result.GetType());
            Assert.AreEqual(new HashSet<int> { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Intersection has no compiled form - it is refused with the CompileErrorException the weak
        /// path's fallback can see - and the interpreted path still satisfies a typed request through
        /// the reprojection.
        /// </summary>
        [Test]
        public void IntersectionSatisfiesATypedRequestInterpretedOnly()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<HashSet<int>>(
                    "{1,2,3} * {2,3,4}", EvaluationMode.MustCompile));

            var interpreted = InterpretGetter<HashSet<int>>("{1,2,3} * {2,3,4}").GetValue();

            Assert.AreEqual(typeof(HashSet<int>), interpreted.GetType());
            Assert.AreEqual(new HashSet<int> { 2, 3 }, interpreted);
        }

        /// <summary>
        /// Difference has no compiled form either; same paired shape as the intersection.
        /// </summary>
        [Test]
        public void DifferenceSatisfiesATypedRequestInterpretedOnly()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<HashSet<int>>(
                    "{1,2,3} - {2}", EvaluationMode.MustCompile));

            var interpreted = InterpretGetter<HashSet<int>>("{1,2,3} - {2}").GetValue();

            Assert.AreEqual(typeof(HashSet<int>), interpreted.GetType());
            Assert.AreEqual(new HashSet<int> { 1, 3 }, interpreted);
        }
    }
}


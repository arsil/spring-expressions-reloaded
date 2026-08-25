using System;
using System.Reflection;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class IdxWidening
    {
        public string this[long i] { get { return "long:" + i; } }
    }

    public class IdxTie
    {
        public string this[double d] { get { return "double:" + d; } }
        public string this[decimal d] { get { return "decimal:" + d; } }
    }

    public class IdxGate
    {
        public object Payload { get { return "payload"; } }
        public string this[object o] { get { return "object version"; } }
        public string this[string s] { get { return "string version"; } }
    }

    public class IdxHier
    {
        public ResolutionDerived DerivedHoldingSealed { get { return new ResolutionSealed(); } }
        public string this[object o] { get { return "object version"; } }
        public string this[ResolutionDerived d] { get { return "derived version"; } }
    }

    public class IdxMoney
    {
        public MoneyLike Amount { get { return new MoneyLike(45.5m); } }
        public string this[decimal d] { get { return "decimal:" + d; } }
    }

    public class IdxSettable
    {
        private readonly System.Collections.Generic.Dictionary<long, string> _values
            = new System.Collections.Generic.Dictionary<long, string>();

        public string this[long i]
        {
            get { return _values[i]; }
            set { _values[i] = value; }
        }
    }

    public class IdxSetGate
    {
        public string LastSet { get; private set; }

        public string this[object o] { set { LastSet = "object:" + value; } }
        public string this[string s] { set { LastSet = "string:" + value; } }
    }

    /// <summary>
    /// The overload-resolution ruling applied to indexers: an indexer's accessor is an ordinary
    /// method, so the compiled path resolves it through MethodNode's tiers, gate and betterness, and
    /// the interpreter's indexer lookup runs the same tiers after its legacy exact lookup - see
    /// OverloadResolutionTests and ConstructorResolutionTests for the siblings. The compiled path
    /// used to resolve through the exact-type GetMethod, whose DefaultBinder widening succeeded
    /// where the interpreter's exact GetProperty threw - "[45]" against this[long] was a
    /// succeeds-versus-throws divergence.
    /// </summary>
    [TestFixture]
    public class IndexerResolutionTests : BaseCompiledTests
    {
        [Test]
        public void SingleIndexerWidensOnEveryPath()
        {
            TestCompiledVsInterpreted<IdxWidening, string>("[45]", new IdxWidening())
                .ResultEqualsTo("long:45");
        }

        /// <summary>
        /// The indexer differs from methods and constructors on ties, and deliberately so: the
        /// interpreter's legacy lookup is GetProperty with the DefaultBinder, which widens int to
        /// double and cannot see decimal - so "[45]" against this[double]/this[decimal] has always
        /// answered "double" interpreted, a legacy pick preserved verbatim. The compiled path refuses
        /// the tie (C#'s CS0121), the weak path falls back, and the legacy answer is the answer -
        /// no silent divergence, because the compiled path never answers differently.
        /// </summary>
        [Test]
        public void WideningTieRefusesCompiledAndTheLegacyBinderPickAnswers()
        {
            var ctx = new IdxTie();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<IdxTie, string>("[45]"));

            Assert.AreEqual("double:45",
                InterpretGetter<IdxTie, string>("[45]").GetValue(ctx));

            IExpression weak = Expression.Parse("[45]");
            Assert.AreEqual("double:45", weak.GetValue(ctx));
        }

        [Test]
        public void ObjectTypedIndexRefusesCompiledAndTheInterpreterDecides()
        {
            var ctx = new IdxGate();

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<IdxGate, string>("[Payload]"));

            IExpression weak = Expression.Parse("[Payload]");
            Assert.AreEqual("string version", weak.GetValue(ctx));
        }

        /// <summary>
        /// A null index picks this[object] on both backends - NOT the most specific overload the
        /// method side picks for Show(null). The interpreter's legacy lookup maps a null value to
        /// typeof(object) and exact-matches the object indexer; the compiled path replays that tier
        /// (TryExactAccessorForNullLiterals) so the backends agree on the legacy answer.
        /// </summary>
        [Test]
        public void NullLiteralPicksTheObjectIndexerEverywhere()
        {
            TestCompiledVsInterpreted<IdxGate, string>("[null]", new IdxGate())
                .ResultEqualsTo("object version");
        }

        [Test]
        public void CustomRealIndexBindsOnEveryPath()
        {
            TestCompiledVsInterpreted<IdxMoney, string>("[Amount]", new IdxMoney())
                .ResultEqualsTo("decimal:" + 45.5m);
        }

        [Test]
        public void DerivedTypedIndexCompilesToTheSpecificIndexer()
        {
            TestCompiledVsInterpreted<IdxHier, string>("[DerivedHoldingSealed]", new IdxHier())
                .ResultEqualsTo("derived version");
        }

        /// <summary>
        /// The exact-match rows of the ruling's table: a double or decimal index picks its own
        /// overload on every path, in every era - pinned so the tie and widening tests above cannot
        /// be mistaken for "mixed real indexes never work".
        /// </summary>
        [Test]
        public void ExactIndexTypesKeepTheirPicks()
        {
            var ctx = new IdxTie();

            TestCompiledVsInterpreted<IdxTie, string>("[4.5]", ctx)
                .ResultEqualsTo("double:" + 4.5);
            TestCompiledVsInterpreted<IdxTie, string>("[4.5m]", ctx)
                .ResultEqualsTo("decimal:" + 4.5m);
        }

        /// <summary>
        /// The set accessor resolves through the same tiers as the get accessor: a compiled setter
        /// widens an int index into this[long], and the weakly typed setter - which is
        /// interpreter-served - resolves and converts the same way, so both writes land.
        /// </summary>
        [Test]
        public void SetterResolvesThroughTheSameTiers()
        {
            var ctx = new IdxSettable();

            Expression.ParseSetter<IdxSettable, string>(
                    "[45]", EvaluationMode.MustCompile)
                .SetValue(ctx, "compiled");
            Assert.AreEqual("compiled", ctx[45L]);

            IExpression weak = Expression.Parse("[46]");
            weak.SetValue(ctx, "weak");
            Assert.AreEqual("weak", ctx[46L]);
        }

        /// <summary>
        /// The null quirk holds for setters too: a null index picks the this[object] setter on both
        /// paths - the compiled setter replays the legacy null-to-object tier exactly like the getter.
        /// </summary>
        [Test]
        public void NullIndexSetterPicksTheObjectIndexerEverywhere()
        {
            var compiledTarget = new IdxSetGate();
            Expression.ParseSetter<IdxSetGate, string>(
                    "[null]", EvaluationMode.MustCompile)
                .SetValue(compiledTarget, "x");
            Assert.AreEqual("object:x", compiledTarget.LastSet);

            var weakTarget = new IdxSetGate();
            Expression.Parse("[null]").SetValue(weakTarget, "y");
            Assert.AreEqual("object:y", weakTarget.LastSet);
        }

        /// <summary>
        /// A wrong index count against an array used to throw ArgumentException out of
        /// LExpression.ArrayIndex while the tree was being built - the leak class again. It refuses
        /// now, and the interpreter's InvalidPropertyException at evaluation is the answer.
        /// </summary>
        [Test]
        public void WrongArrayIndexCountRefusesCompiledAndThrowsAtEvaluationInterpreted()
        {
            var array = new[] { 1, 2, 3 };

            Assert.Throws<CompileErrorException>(
                () => CompileGetter<int[], int>("[1, 2]"));

            IExpression weak = Expression.Parse("[1, 2]");
            Assert.Throws<SpringCore.InvalidPropertyException>(() => weak.GetValue(array));
        }
    }
}


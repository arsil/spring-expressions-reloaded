using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    /// <summary>
    /// What a <see cref="Type"/>-valued receiver means: a static-call target for the type it
    /// represents, and an ordinary object for everything <see cref="Type"/> itself declares.
    /// </summary>
    /// <remarks>
    /// <b>Inherited, not a fork idea.</b> <c>MethodNode.Initialize</c> has read
    /// <c>context is Type ? context as Type : context.GetType()</c> since <c>8f5bbe3</c>, so searching
    /// the represented type is upstream behaviour - and so was the missing check that the method found
    /// there be <i>static</i>, which is what this fixture's first test covers.
    /// </remarks>
    [TestFixture]
    public class TypeValuedReceiverTests
    {
        public class Holder
        {
            public Type TypeProp { get { return typeof(string); } }

            public Type IntType { get { return typeof(int); } }

            public string Name { get { return "holder"; } }
        }

        [Test]
        public void AMemberOfSystemTypeIsNotTakenFromTheTypeTheValueRepresents()
        {
            // The defect: the interpreter searched the represented type and did not require a static
            // match, so `someTypeValue.ToString()` bound String.ToString() - an instance method - and
            // then invoked it with the Type object as its target. InvalidCastException, where the
            // compiled path answered "System.String".
            //
            // It survived because a member the represented type does *not* declare falls through to
            // the System.Type probe: `TypeProp.Name` worked only because String has no Name, which is
            // the branch that was doing the real work all along.
            foreach (var expression in new[]
                     {
                         "TypeProp.ToString()", "Name.GetType().ToString()"
                     })
            {
                Assert.AreEqual(
                    "System.String",
                    Expression.ParseGetter<Holder, object>(
                        expression, EvaluationMode.MustCompile).GetValue(new Holder()),
                    expression);

                Assert.AreEqual(
                    "System.String",
                    Expression.ParseGetter<Holder, object>(
                        expression, EvaluationMode.MustInterpret).GetValue(new Holder()),
                    expression);
            }

            Assert.AreEqual(
                false,
                Expression.ParseGetter<Holder, object>(
                    "TypeProp.Equals(1)", EvaluationMode.MustInterpret).GetValue(new Holder()));

            // The member that always worked, and only by luck - String declares no Name.
            Assert.AreEqual(
                "String",
                Expression.ParseGetter<Holder, object>(
                    "TypeProp.Name", EvaluationMode.MustInterpret).GetValue(new Holder()));
        }

        [Test]
        public void AStaticMemberIsReachedThroughTheTypeANameStandsFor()
        {
            // The capability the represented-type lookup exists for, and it is untouched: a *static*
            // method genuinely can be called that way, because it needs no instance.
            Assert.AreEqual(
                5,
                Expression.ParseGetter<Holder, object>(
                    "T(System.Int32).Parse('5')", EvaluationMode.MustCompile).GetValue(new Holder()));

            Assert.AreEqual(
                5,
                Expression.ParseGetter<Holder, object>(
                    "T(System.Int32).Parse('5')", EvaluationMode.MustInterpret).GetValue(new Holder()));
        }

        /// <summary>
        /// <b>Deliberate, and do not "fix" one side.</b> A static call whose target type is decided by
        /// a value at run time is late binding, which this engine refuses everywhere else - the
        /// overload gate, per-call method dispatch, <c>Foo(#var1)</c>. Here the interpreter can serve
        /// it, because at evaluation the receiver's runtime value <i>is</i> the type; the compiled path
        /// cannot, because statically the receiver is only <c>System.Type</c> and nothing says which
        /// type's <c>Parse</c> to emit.
        /// </summary>
        /// <remarks>
        /// Ruled 2026-09-09 rather than refused on both backends: the capability is inherited (see the
        /// fixture note), it is genuinely useful - a type chosen by configuration, then parsed against -
        /// and the compiled path declining is an honest answer rather than a gap. Refusing it on both
        /// sides for symmetry would remove something that works and break inherited behaviour to buy
        /// only tidiness.
        /// <p>
        /// The escape, if a caller needs the compiled form, is the constant spelling:
        /// <c>T(System.Int32).Parse('5')</c>, pinned above.
        /// </p>
        /// </remarks>
        [Test]
        public void AStaticCallThroughARuntimeTypeValueIsServedByTheInterpreterAlone()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>(
                    "IntType.Parse('5')", EvaluationMode.MustCompile));

            Assert.AreEqual(
                5,
                Expression.ParseGetter<Holder, object>(
                    "IntType.Parse('5')", EvaluationMode.MustInterpret).GetValue(new Holder()));

            // What a caller actually gets: the answer, through the fallback. The refusal costs speed,
            // never the result.
            Assert.AreEqual(
                5,
                Expression.ParseGetter<Holder, object>(
                    "IntType.Parse('5')", EvaluationMode.CompileOrInterpret).GetValue(new Holder()));

            Assert.AreEqual(
                "1",
                Expression.ParseGetter<Holder, object>(
                    "TypeProp.Format('{0}', 1)", EvaluationMode.CompileOrInterpret)
                    .GetValue(new Holder()));
        }

        /// <summary>
        /// <b>Do not align the compiled side by skipping non-static matches - it was tried and it
        /// turned four tests red.</b>
        /// </summary>
        /// <remarks>
        /// The constant-<c>Type</c> branch emits its argument nodes against the <i>type-name</i>
        /// context rather than against <c>#this</c>, so `long.Parse(ToString())` builds
        /// <c>Parse(typeof(long).ToString())</c> - "System.Int64" - and throws <c>FormatException</c>.
        /// Today the shape is refused instead, because a non-static match leaves no instance for
        /// <c>LExpression.Call</c> and <c>BuildCall</c> converts the failure: <b>the refusal is
        /// accidentally what protects the caller from a wrong answer.</b> Letting it fall through to
        /// <c>System.Type</c> replaced two green tests' answers with the wrong number.
        /// <p>
        /// So the argument-binding defect has to be fixed before this side can be aligned, and until
        /// then the compiled path declines while the interpreter answers - agreement through the
        /// fallback, which is what the last assertion here checks.
        /// </p>
        /// </remarks>
        [Test]
        public void TheConstantFormDeclinesAnInstanceMemberAndTheInterpreterAnswersIt()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<Holder, object>(
                    "T(System.String).ToString()", EvaluationMode.MustCompile));

            Assert.AreEqual(
                "System.String",
                Expression.ParseGetter<Holder, object>(
                    "T(System.String).ToString()", EvaluationMode.MustInterpret).GetValue(new Holder()));

            Assert.AreEqual(
                "System.String",
                Expression.ParseGetter<Holder, object>(
                    "T(System.String).ToString()", EvaluationMode.CompileOrInterpret)
                    .GetValue(new Holder()));
        }
    }
}

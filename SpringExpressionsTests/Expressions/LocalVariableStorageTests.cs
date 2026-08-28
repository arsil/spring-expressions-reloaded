using System;
using System.Collections.Generic;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressionsTests.Expressions
{
    public class LocalStorageCases
    {
        public int Number { get; set; }
        public string Name { get { return "abc"; } }
        public decimal Price { get { return 2.5m; } }

        public List<int> Ints { get { return new List<int> { 3, 1, 2 }; } }

        public int Twice(int n) { return n * 2; }
        public string Join(string a, string b) { return a + "|" + b; }
    }

    /// <summary>
    /// Free <c>$local</c>s are one object-typed block variable per name, not entries in a dictionary.
    /// Identical semantics - an unassigned variable is null, any type may be assigned and reassigned,
    /// the storage lives one invocation - with the allocation and the hash lookup gone, so these pins
    /// are about several locals coexisting, being reassigned, and carrying their values across a
    /// whole expression.
    /// </summary>
    /// <remarks>
    /// Every row that can agree is run through TestCompiledVsInterpreted, which compares runtime type
    /// as well as value at object and at the requested TResult. The rows that cannot compile assert
    /// the refusal under MustCompile and the interpreter's answer beside it, because a local is
    /// object-typed and arithmetic on it needs a cast - which is the one cost of this storage and is
    /// pinned here in both directions.
    /// </remarks>
    [TestFixture]
    public class LocalVariableStorageTests : BaseCompiledTests
    {
        // ----- several locals at once

        [Test]
        public void ThreeLocalsHoldTheirOwnValues()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 1; $b = 2; $c = 3; $a as int + $b as int + $c as int)", new LocalStorageCases())
                .ResultEqualsTo(6);
        }

        [Test]
        public void LocalsOfDifferentTypesCoexist()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($n = 5; $s = 'five'; $b = true; $s as string + $n as int + $b as bool)",
                new LocalStorageCases())
                .ResultEqualsTo("five5True");
        }

        [Test]
        public void SimilarlyNamedLocalsAreDistinctSlots()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($x = 'a'; $xx = 'b'; $x1 = 'c'; $X = 'd';"
                + " $x as string + $xx as string + $x1 as string + $X as string)",
                new LocalStorageCases())
                .ResultEqualsTo("abcd");
        }

        /// <summary>
        /// Names are case sensitive, unlike member names - a local is not a member lookup.
        /// </summary>
        [Test]
        public void LocalNamesAreCaseSensitive()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($v = 'lower'; $V = 'upper'; $v)", new LocalStorageCases())
                .ResultEqualsTo("lower");

            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($v = 'lower'; $V = 'upper'; $V)", new LocalStorageCases())
                .ResultEqualsTo("upper");
        }

        [Test]
        public void OneLocalIsReadByAnother()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 4; $b = $a; $b)", new LocalStorageCases())
                .ResultEqualsTo(4);
        }

        [Test]
        public void ManyLocalsAreAllDeclared()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 1; $b = 2; $c = 3; $d = 4; $e = 5; $f = 6; $g = 7; $h = 8;"
                + " $a as int + $b as int + $c as int + $d as int"
                + " + $e as int + $f as int + $g as int + $h as int)",
                new LocalStorageCases())
                .ResultEqualsTo(36);
        }

        // ----- reassignment

        [Test]
        public void ALocalIsReassignedToAnotherValueOfTheSameType()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 1; $a = 2; $a = 3; $a)", new LocalStorageCases())
                .ResultEqualsTo(3);
        }

        /// <summary>
        /// The slot is object-typed, so a local may be reassigned to a value of an entirely different
        /// type. This is exactly why the compiled storage cannot be typed without a language change:
        /// see open-issues items 14 and 15.
        /// </summary>
        [Test]
        public void ALocalIsReassignedToADifferentType()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 1; $a = 'one'; $a)", new LocalStorageCases())
                .ResultEqualsTo("one");

            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 'one'; $a = 1; $a)", new LocalStorageCases())
                .ResultEqualsTo(1);
        }

        [Test]
        public void ALocalIsReassignedFromItself()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 1; $a = $a as int + 1; $a = $a as int + 1; $a)", new LocalStorageCases())
                .ResultEqualsTo(3);
        }

        [Test]
        public void ALocalTakesNullAndThenAValue()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = null; $a == null)", new LocalStorageCases())
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 5; $a = null; $a == null)", new LocalStorageCases())
                .ResultEqualsTo(true);
        }

        // ----- unassigned locals

        [Test]
        public void UnassignedLocalsAreAllNull()
        {
            var compiled = Expression.ParseGetter<LocalStorageCases, object>(
                "($a = 1; $b)", EvaluationMode.MustCompile);
            var interpreted = Expression.ParseGetter<LocalStorageCases, object>(
                "($a = 1; $b)", EvaluationMode.MustInterpret);

            Assert.IsNull(compiled.GetValue(new LocalStorageCases()));
            Assert.IsNull(interpreted.GetValue(new LocalStorageCases()));
        }

        /// <summary>
        /// Assigning one local does not disturb another - the point of a slot per name.
        /// </summary>
        [Test]
        public void AssigningOneLocalLeavesTheOthersAlone()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 1; $b = 2; $a = 9; $b)", new LocalStorageCases())
                .ResultEqualsTo(2);
        }

        // ----- values from the root

        [Test]
        public void LocalsCarryValuesReadFromTheRoot()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($n = Number; $s = Name; $s as string + $n as int)",
                new LocalStorageCases { Number = 7 })
                .ResultEqualsTo("abc7");
        }

        [Test]
        public void ALocalHoldsACollection()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($xs = Ints; ($xs as T(System.Collections.ICollection)).Count)", new LocalStorageCases())
                .ResultEqualsTo(3);
        }

        [Test]
        public void ALocalIsPassedToAMethodOnTheRoot()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($n = 4; Twice($n as int))", new LocalStorageCases())
                .ResultEqualsTo(8);

            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 'x'; $b = 'y'; Join($a as string, $b as string))", new LocalStorageCases())
                .ResultEqualsTo("x|y");
        }

        // ----- the cast, which is the cost of object-typed storage

        /// <summary>
        /// A local is object-typed, so arithmetic on one has no compiled form and a cast buys it
        /// back. Both halves are pinned deliberately: the refusal is not a defect, it is the standing
        /// object-typed-operand story, and item 15 - declared typed locals - is the only thing that
        /// would remove the cast. Do not "fix" the refusal without ruling on that.
        /// </summary>
        [Test]
        public void ArithmeticOnALocalIsRefusedUncastAndCompilesCast()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<LocalStorageCases, object>(
                    "($a = 1; $b = 2; $a + $b)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<LocalStorageCases, object>(
                "($a = 1; $b = 2; $a + $b)", EvaluationMode.MustInterpret);
            Assert.AreEqual(3, interpreted.GetValue(new LocalStorageCases()));

            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 1; $b = 2; $a as int + $b as int)", new LocalStorageCases())
                .ResultEqualsTo(3);
        }

        /// <summary>
        /// The cast is the ordinary one, so it means what the cast ruling says it means - and casting
        /// a local to the wrong type fails as a cast does, on both backends.
        /// </summary>
        [Test]
        public void CastingALocalToTheWrongTypeFailsAsACastDoes()
        {
            var compiled = Expression.ParseGetter<LocalStorageCases, object>(
                "($a = 'text'; $a as int)", EvaluationMode.MustCompile);
            var interpreted = Expression.ParseGetter<LocalStorageCases, object>(
                "($a = 'text'; $a as int)", EvaluationMode.MustInterpret);

            Assert.Throws<InvalidCastException>(() => compiled.GetValue(new LocalStorageCases()));
            Assert.Throws<InvalidCastException>(() => interpreted.GetValue(new LocalStorageCases()));
        }

        [Test]
        public void ALocalCastToADecimalTakesPartInDecimalArithmetic()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($p = Price; $p as decimal * 2)", new LocalStorageCases())
                .ResultEqualsTo(5.0m);
        }

        /// <summary>
        /// A member read off a local needs the cast for the same reason arithmetic does: an
        /// object-typed operand binds nothing.
        /// </summary>
        [Test]
        public void AMemberReadOffALocalIsRefusedUncastAndCompilesCast()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<LocalStorageCases, object>(
                    "($s = Name; $s.Length)", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<LocalStorageCases, object>(
                "($s = Name; $s.Length)", EvaluationMode.MustInterpret);
            Assert.AreEqual(3, interpreted.GetValue(new LocalStorageCases()));

            // Parenthesised, because a dotted type name is taken greedily - 'x as string.Length'
            // reads 'string.Length' as the type, which is C# parity and the cast ruling's own note.
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($s = Name; ($s as string).Length)", new LocalStorageCases())
                .ResultEqualsTo(3);
        }

        // ----- lifetime

        /// <summary>
        /// A block variable per invocation: every call starts with every local null again, and one
        /// compiled expression evaluated repeatedly never carries a value forward.
        /// </summary>
        [Test]
        public void EveryInvocationStartsWithFreshLocals()
        {
            var reading = Expression.ParseGetter<LocalStorageCases, object>(
                "($a)", EvaluationMode.MustCompile);

            var root = new LocalStorageCases();

            Assert.IsNull(reading.GetValue(root));
            Assert.IsNull(reading.GetValue(root));

            var assigning = Expression.ParseGetter<LocalStorageCases, object>(
                "($a = Number; $a)", EvaluationMode.MustCompile);

            Assert.AreEqual(1, assigning.GetValue(new LocalStorageCases { Number = 1 }));
            Assert.AreEqual(2, assigning.GetValue(new LocalStorageCases { Number = 2 }));
            Assert.IsNull(reading.GetValue(root));
        }

        [Test]
        public void LocalsDoNotCrossBetweenExpressions()
        {
            var root = new LocalStorageCases();

            Expression.ParseGetter<LocalStorageCases, object>(
                "($a = 1; $b = 2; $a)", EvaluationMode.MustCompile).GetValue(root);

            var other = Expression.ParseGetter<LocalStorageCases, object>(
                "($a)", EvaluationMode.MustCompile);

            Assert.IsNull(other.GetValue(root));
        }

        // ----- interaction with the rest of the language

        /// <summary>
        /// The compiled storage is a block variable of the enclosing compilation, and a projection or
        /// selection body is compiled by its own Compile() call and handed in as a constant delegate,
        /// so locals are not in scope there. Refused compiled, served by the interpreter, whose
        /// locals live on the evaluation context a projection shares. Do not "fix" one side.
        /// </summary>
        [Test]
        public void LocalsInsideAProjectionAreRefusedButStillEvaluate()
        {
            Assert.Throws<CompileErrorException>(
                () => Expression.ParseGetter<LocalStorageCases, object>(
                    "($n = 10; Ints.!{ $n })", EvaluationMode.MustCompile));

            var interpreted = Expression.ParseGetter<LocalStorageCases, object>(
                "($n = 10; Ints.!{ $n })", EvaluationMode.MustInterpret);

            Assert.AreEqual(
                new List<object> { 10, 10, 10 }, interpreted.GetValue(new LocalStorageCases()));
        }

        /// <summary>
        /// A local assigned from a projection result is fine - it is the local *inside* the body that
        /// has no scope, not one that merely holds what the projection produced.
        /// </summary>
        [Test]
        public void ALocalHoldsTheResultOfAProjection()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($xs = Ints.!{ #this * 2 }; ($xs as T(System.Collections.ICollection)).Count)",
                new LocalStorageCases())
                .ResultEqualsTo(3);
        }

        /// <summary>
        /// Parking an engine-built collection in a local costs it the root reshaping, so the compiled
        /// path hands back the <c>List&lt;int&gt;</c> the projection built where the interpreter hands
        /// back <c>List&lt;object&gt;</c>. The items are equal and only the item type differs.
        /// </summary>
        /// <remarks>
        /// Not this storage's doing, and not new: <c>Compiler</c> reshapes only an expression the
        /// compilation registered as a constructed collection, and a local read is a different
        /// expression from the projection call that was registered. It is the documented non-root
        /// exit - the same contrast <c>PassedToAMethodOnTheContextByTheInterpreter</c> pins for a
        /// method argument - with a local as one more exit. Do not "fix" one side: making the read
        /// inherit the registration means tracking values through assignments, which is the flow
        /// analysis open-issues item 14 declines.
        /// </remarks>
        [Test]
        public void AConstructedCollectionParkedInALocalKeepsItsItemType()
        {
            var compiled = Expression.ParseGetter<LocalStorageCases, object>(
                "($xs = Ints.!{ #this * 2 }; $xs)", EvaluationMode.MustCompile);
            var interpreted = Expression.ParseGetter<LocalStorageCases, object>(
                "($xs = Ints.!{ #this * 2 }; $xs)", EvaluationMode.MustInterpret);

            Assert.AreEqual(
                typeof(List<int>), compiled.GetValue(new LocalStorageCases()).GetType());
            Assert.AreEqual(
                typeof(List<object>), interpreted.GetValue(new LocalStorageCases()).GetType());

            Assert.AreEqual(
                new List<object> { 6, 2, 4 }, interpreted.GetValue(new LocalStorageCases()));

            // Straight out of the projection, with nothing parked, both reshape to List<object>.
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "Ints.!{ #this * 2 }", new LocalStorageCases())
                .ResultEqualsTo(new List<object> { 6, 2, 4 });
        }

        [Test]
        public void ALocalIsUsedInAConditional()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "($a = 5; $a as int > 3 ? 'big' : 'small')", new LocalStorageCases())
                .ResultEqualsTo("big");
        }

        /// <summary>
        /// An assignment in a branch that is not taken leaves the local as it was - null, since
        /// nothing else wrote to it. That both backends answer null here is exactly what makes the
        /// storage object-typed rather than inferred from the assignment: a slot typed <c>int</c> from
        /// that assignment would answer <c>0</c> compiled against the interpreter's null. See
        /// open-issues item 14, where this shape is the measurement that rejected inference.
        /// </summary>
        /// <remarks>
        /// The else branch is <c>null</c> rather than <c>0</c> so that the two branch types match: a
        /// ternary whose branches disagree is a pre-existing TernaryNode leak, unrelated to locals
        /// (<c>Number &gt; 1 ? 'a' : 0</c> leaks identically), and pinning it here would pin the leak
        /// rather than this.
        /// </remarks>
        [Test]
        public void AnAssignmentInAnUntakenBranchLeavesTheLocalNull()
        {
            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "(Number > 100 ? $a = 5 : null; $a == null)", new LocalStorageCases { Number = 4 })
                .ResultEqualsTo(true);

            TestCompiledVsInterpreted<LocalStorageCases, object>(
                "(Number > 1 ? $a = 5 : null; $a)", new LocalStorageCases { Number = 4 })
                .ResultEqualsTo(5);
        }

        [Test]
        public void LocalsAndVariablesAreSeparateNamespaces()
        {
            var variables = new Dictionary<string, object>();
            var value = Expression.Parse("($a = 'local'; #a = 'variable'; $a)")
                .GetValue(new LocalStorageCases(), variables);

            Assert.AreEqual("local", value);
            Assert.AreEqual("variable", variables["a"]);
            Assert.IsFalse(variables.ContainsKey("$a"));
        }

        // ----- typed requests

        [Test]
        public void ALocalSatisfiesATypedRequestThroughACast()
        {
            TestCompiledVsInterpreted<LocalStorageCases, int>(
                "($a = 41; $a as int + 1)", new LocalStorageCases())
                .ResultEqualsTo(42);

            TestCompiledVsInterpreted<LocalStorageCases, string>(
                "($s = 'ab'; $s as string + 'c')", new LocalStorageCases())
                .ResultEqualsTo("abc");
        }
    }
}

using System;

using NUnit.Framework;

using SpringExpressions;
using SpringExpressions.Expressions.Compiling.Expressions;
using SpringExpressions.Parser.antlr;

namespace SpringExpressionsTests.Expressions
{
    public class CastPositionCases
    {
        public int Age { get { return 45; } }
        public int List { get { return 100; } }
        public string Two(object a, object b) { return a + "|" + b; }
    }

    /// <summary>
    /// Pins the cast syntax: ONE keyword, BOTH positions, ONE type vocabulary. The suffix cast
    /// 'x as [type]' and the prefix cast 'as&lt;[type]&gt;(x)' build the same CastNode, and [type]
    /// in either position is the shared asTypeSlot rule: a structural type name (dotted
    /// identifier, generic arguments, empty array ranks) or the T(...) escape, whose 'name' slurp
    /// reaches everything ResolveType accepts (backtick arity, assembly-qualified names). The
    /// whole surface is carved from error space: 'as' followed by a bare identifier and an
    /// expression-initial 'as' were both syntax errors before, so no legal expression changed
    /// meaning. The one ambiguity - '&lt;' after a bare type name in suffix position: generics or
    /// comparison? - resolves by C#'s own rule: generics win exactly when a complete generic
    /// argument list parses. Design record: _Docs/cast-and-type-syntax.md sections 7 and 8.
    /// </summary>
    [TestFixture]
    public class CastBothPositionsTests : BaseCompiledTests
    {
        [OneTimeSetUp]
        public void RegisterShortGenericAlias()
        {
            // the backtick rows below use the SHORT form List`1[int], which - like T(List<string>)
            // - resolves only through the registry; the fully qualified spellings never need it
            SpringCore.TypeResolution.TypeRegistry.RegisterType(typeof(System.Collections.Generic.List<>));
        }

        // ---------------------------------------------------------------- suffix, bare types

        [Test]
        public void SuffixBareCastsPrimitivesLikeTheTypeFence()
        {
            TestCompiledVsInterpreted<int>("45.6 as int").ResultEqualsTo(45);
            TestCompiledVsInterpreted<long>("45.6 as long").ResultEqualsTo(45L);
            TestCompiledVsInterpreted<double>("45 as double").ResultEqualsTo(45d);
        }

        [Test]
        public void SuffixBareTakesDottedTypeNames()
        {
            TestCompiledVsInterpreted<int>("45.6 as System.Int32").ResultEqualsTo(45);

            // whitespace never reaches the parser, so a scattered spelling is the same type name
            TestCompiledVsInterpreted<int>("45.6 as System . Int32").ResultEqualsTo(45);
        }

        [Test]
        public void SuffixBareTakesGenericTypeNames()
        {
            TestCompiledVsInterpreted<object>("null as System.Collections.Generic.List<int>")
                .ResultEqualsTo(null);

            // nested generic arguments and the comma between them are structural, not slurped
            TestCompiledVsInterpreted<object>(
                "null as System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>")
                .ResultEqualsTo(null);
        }

        [Test]
        public void SuffixBareTakesArrayRanks()
        {
            TestCompiledVsInterpreted<object>("null as string[]").ResultEqualsTo(null);
            TestCompiledVsInterpreted<object>("null as int[,]").ResultEqualsTo(null);
            TestCompiledVsInterpreted<object>("null as string[][,]").ResultEqualsTo(null);
            TestCompiledVsInterpreted<object>("null as System.Collections.Generic.List<int>[]")
                .ResultEqualsTo(null);

            TestCompiledVsInterpreted<string[]>("new string[] {'a','b','c'} as string[]")
                .ResultEqualsTo(new[] { "a", "b", "c" });

            TestCompiledVsInterpreted<int>("(new string[] {'a','b','c'} as string[]).Length")
                .ResultEqualsTo(3);
        }

        /// <summary>
        /// A sized rank is an expression, not a type: only empty ranks belong to a type name.
        /// </summary>
        [Test]
        public void SuffixBareRefusesSizedArrayRanks()
        {
            Assert.Catch<RecognitionException>(() => Expression.Parse("45 as int[0]"));
        }

        // ------------------------------------------- suffix, the generics-vs-comparison ambiguity

        [Test]
        public void SuffixCastResultComparesWhenNoGenericClauseCanParse()
        {
            var ctx = new CastPositionCases();

            // literal after '<': the guess never starts, the '<' is a comparison
            TestCompiledVsInterpreted<CastPositionCases, bool>("Age as int < 46", ctx)
                .ResultEqualsTo(true);
            TestCompiledVsInterpreted<CastPositionCases, bool>("Age as int > 3", ctx)
                .ResultEqualsTo(true);

            // identifier after '<': the guess runs, fails at the missing '>', and backtracks -
            // the row a naive (predicate-free) grammar misparses into a hard syntax error
            TestCompiledVsInterpreted<CastPositionCases, bool>("Age as int < Age", ctx)
                .ResultEqualsTo(false);
        }

        /// <summary>
        /// 'Age as List &lt; 3' parses as '(Age as List) &lt; 3' - the comparison reading - so the
        /// failure is the unresolvable type name 'List' at the cast, not a syntax error. Had the
        /// generic guess swallowed the '&lt;', this would not parse at all.
        /// </summary>
        [Test]
        public void SuffixComparisonReadingSurvivesAnUnresolvableBareName()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<CastPositionCases, object>("Age as List < 3"));

            Assert.Throws<TypeLoadException>(
                () => InterpretGetter<CastPositionCases, object>("Age as List < 3")
                    .GetValue(new CastPositionCases()));
        }

        /// <summary>
        /// C#'s own disambiguation, both halves. In 'Two(Age as int &lt; Age, 3 &gt; Age)' the '3'
        /// fails the type-argument guess, so both arguments stay comparisons. In
        /// 'Two(Age as List &lt; Age, List &gt; Age)' a complete generic argument list parses, so
        /// generics win and the trailing operand dangles - exactly how C# reads F(G&lt;A,B&gt;(7)).
        /// Deliberate: do not "fix" the second half toward comparisons without a ruling.
        /// </summary>
        [Test]
        public void SuffixArgumentListsResolveTheAmbiguityLikeCSharp()
        {
            TestCompiledVsInterpreted<CastPositionCases, string>(
                "Two(Age as int < Age, 3 > Age)", new CastPositionCases())
                .ResultEqualsTo("False|False");

            Assert.Catch<RecognitionException>(
                () => Expression.Parse("Two(Age as List < Age, List > Age)"));
        }

        /// <summary>
        /// After committed generics the operand dangles; the shape was equally illegal under the
        /// comparison reading (chained relational operators never parsed), so nothing is lost.
        /// </summary>
        [Test]
        public void SuffixCommittedGenericsLeaveNoRoomForAChainedOperand()
        {
            Assert.Catch<RecognitionException>(() => Expression.Parse("Age as List < Age > 3"));
        }

        // ---------------------------------------------------------- suffix, precedence and limits

        [Test]
        public void SuffixBareBindsAtTheCastPrecedenceLevel()
        {
            var ctx = new CastPositionCases();

            TestCompiledVsInterpreted<CastPositionCases, int>("Age as int + 3", ctx)
                .ResultEqualsTo(48);
            TestCompiledVsInterpreted<CastPositionCases, int>("2 * Age as int", ctx)
                .ResultEqualsTo(90);
            TestCompiledVsInterpreted<CastPositionCases, double>("Age as int ^ 2", ctx)
                .ResultEqualsTo(2025d);
        }

        /// <summary>
        /// The single-cast rule is unchanged from the T(...) days: a second postfix cast needs
        /// parentheses.
        /// </summary>
        [Test]
        public void SuffixCastsStillDoNotChainWithoutParentheses()
        {
            Assert.Catch<RecognitionException>(() => Expression.Parse("1 as int as long"));

            TestCompiledVsInterpreted<long>("(1 as int) as long").ResultEqualsTo(1L);
        }

        /// <summary>
        /// The dotted-name loop is greedy, exactly like C#: in 'e as A.B.C' the whole dotted name
        /// is the type, so member access on a cast result needs parentheses. The type here is the
        /// unresolvable 'System.Int32.MaxValue' - refused compiled, TypeLoadException interpreted.
        /// </summary>
        [Test]
        public void SuffixBareDottedNamesAreGreedyLikeCSharp()
        {
            Assert.Throws<CompileErrorException>(
                () => CompileGetter<object>("45 as System.Int32.MaxValue"));

            Assert.Throws<TypeLoadException>(
                () => InterpretGetter<object>("45 as System.Int32.MaxValue").GetValue());
        }

        // ------------------------------------------------------------- suffix, the T(...) escape

        [Test]
        public void SuffixTypeFenceIsUntouchedBesideTheBareForm()
        {
            TestCompiledVsInterpreted<int>("45.6 as T(int)").ResultEqualsTo(45);
            TestCompiledVsInterpreted<string>("null as T(string)").ResultEqualsTo(null);
        }

        /// <summary>
        /// What only the escape can spell, suffix position: backtick arity and assembly-qualified
        /// names ride the 'name' slurp into ResolveType; the structural rule has no token for
        /// either.
        /// </summary>
        [Test]
        public void SuffixTypeFenceReachesBacktickAndAssemblyQualifiedNames()
        {
            TestCompiledVsInterpreted<object>("null as T(List`1[int])").ResultEqualsTo(null);

            TestCompiledVsInterpreted<string>("'x' as T(System.String, mscorlib)")
                .ResultEqualsTo("x");
        }

        // ----------------------------------------------------------------- prefix, bare types

        [Test]
        public void PrefixCastsAndComposes()
        {
            TestCompiledVsInterpreted<int>("as<int>(45.6)").ResultEqualsTo(45);
            TestCompiledVsInterpreted<int>("as<int>(5 + 3)").ResultEqualsTo(8);
            TestCompiledVsInterpreted<double>("as<double>(as<int>(45.9))").ResultEqualsTo(45d);

            TestCompiledVsInterpreted<CastPositionCases, long>("as<long>(Age)", new CastPositionCases())
                .ResultEqualsTo(45L);
        }

        [Test]
        public void PrefixTakesGenericsAndArrayRanks()
        {
            TestCompiledVsInterpreted<object>("as<System.Collections.Generic.List<int>>(null)")
                .ResultEqualsTo(null);
            TestCompiledVsInterpreted<object>(
                "as<System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>>(null)")
                .ResultEqualsTo(null);
            TestCompiledVsInterpreted<object>("as<string[]>(null)").ResultEqualsTo(null);
            TestCompiledVsInterpreted<object>("as<int[,]>(null)").ResultEqualsTo(null);
        }

        /// <summary>
        /// The prefix cast is a chain head, so members apply to its result with no parentheses -
        /// the ergonomic half the suffix form cannot have.
        /// </summary>
        [Test]
        public void PrefixResultChainsMembersWithoutParentheses()
        {
            TestCompiledVsInterpreted<int>("as<string[]>(new string[] {'a','b','c'}).Length")
                .ResultEqualsTo(3);

            TestCompiledVsInterpreted<string>("as<int>(45.9).ToString()").ResultEqualsTo("45");
        }

        [Test]
        public void PrefixWorksInOperandPositions()
        {
            TestCompiledVsInterpreted<bool>("3 < as<int>(5)").ResultEqualsTo(true);
            TestCompiledVsInterpreted<int>("1 + as<int>(2.9)").ResultEqualsTo(3);
        }

        [Test]
        public void PrefixAndSuffixComposeFreely()
        {
            TestCompiledVsInterpreted<long>("as<int>(45.6) as long").ResultEqualsTo(45L);

            TestCompiledVsInterpreted<CastPositionCases, long>(
                "as<long>(Age as int)", new CastPositionCases())
                .ResultEqualsTo(45L);
        }

        /// <summary>
        /// Unboxing null is a NullReferenceException on both backends, prefix spelling included -
        /// the same row CastAgreementTests pins for the suffix.
        /// </summary>
        [Test]
        public void PrefixNullUnboxingMatchesTheSuffixRuling()
        {
            Assert.Throws<NullReferenceException>(
                () => CompileGetter<object>("as<int>(null)").GetValue());

            Assert.Throws<NullReferenceException>(
                () => InterpretGetter<object>("as<int>(null)").GetValue());
        }

        // ------------------------------------------------------------- prefix, the T(...) escape

        /// <summary>
        /// The escape works inside the angle brackets: the slurp stops at the type's own ')',
        /// which never collides with the '&gt;' the prefix shape demands next.
        /// </summary>
        [Test]
        public void PrefixTypeFenceReachesBacktickAndAssemblyQualifiedNames()
        {
            TestCompiledVsInterpreted<int>("as<T(int)>(45.6)").ResultEqualsTo(45);

            TestCompiledVsInterpreted<object>("as<T(List`1[int])>(null)").ResultEqualsTo(null);

            TestCompiledVsInterpreted<string>("as<T(System.String, mscorlib)>('x')")
                .ResultEqualsTo("x");
        }

        // --------------------------------------------------------- the boundary between positions

        /// <summary>
        /// After a value, 'as' is the postfix operator and demands a type - the two positions
        /// meet in a clean syntax error, never a quiet misparse.
        /// </summary>
        [Test]
        public void PrefixShapeAfterAValueIsASyntaxError()
        {
            Assert.Catch<RecognitionException>(() => Expression.Parse("Age as<int>(5)"));
        }

        /// <summary>
        /// The prefix cast is deliberately chain-HEAD only: 'as' is not an identifier, so it can
        /// never look like a member, and mid-chain use fails at parse rather than meaning
        /// something surprising.
        /// </summary>
        [Test]
        public void PrefixShapeMidChainIsASyntaxError()
        {
            Assert.Catch<RecognitionException>(() => Expression.Parse("'abc'.as<int>(5)"));
        }

        [Test]
        public void ExpressionInitialAsWithoutThePrefixShapeIsASyntaxError()
        {
            Assert.Catch<RecognitionException>(() => Expression.Parse("as < 23"));
        }
    }
}

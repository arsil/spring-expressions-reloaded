using System.Collections.Generic;
using System.Linq.Expressions;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    public class CompilationContext
    {
        public CompilationContext(LExpression rootContextExpression, LExpression variablesExpression)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = rootContextExpression;
            VariablesExpression = variablesExpression;
            _constructedCollections = new HashSet<LExpression>();
            _localStorage = new Dictionary<string, ParameterExpression>();
            _localStorageOrder = new List<ParameterExpression>();
        }

        public CompilationContext CreateWithNewThisContext(LExpression thisExpression)
        {
            return new CompilationContext(
                RootContextExpression, thisExpression, VariablesExpression, _constructedCollections);
        }
        // todo: error: context expression != RootExpression    !!!!  !!!!! !!!!

        private CompilationContext(
            LExpression rootContextExpression,
            LExpression thisExpression,
            LExpression variablesExpression,
            HashSet<LExpression> constructedCollections)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = thisExpression;
            VariablesExpression = variablesExpression;

            // Shared, not copied: a union inside a projection compiles against a derived context, and the
            // root that Compiler finally inspects is the one it registered into.
            _constructedCollections = constructedCollections;

            // No local storage, and deliberately none inherited: every caller of this builds a
            // delegate with its own Compile() call and hands it in as a constant, so its tree is a
            // separate compilation unit and cannot reference a block variable declared in the outer
            // one.
            _localStorage = null;
            _localStorageOrder = null;
        }

        /// <summary>
        /// Records that <paramref name="expression"/> builds a new collection, rather than yielding one read
        /// out of the object graph.
        /// </summary>
        /// <remarks>
        /// Compiler needs to tell the two apart at the root: a collection the engine built may be reshaped
        /// to match what the interpreter would have produced, while one that was read is the caller's own
        /// object and has to be handed back untouched, reference identity and all.
        ///
        /// Registering the emitted expression is what keeps that knowledge out of the values. Marking the
        /// collections themselves - a HashSet subclass, say - would work too, but the marker type then
        /// travels with every value that leaves the engine: nested in another collection, passed to a method
        /// on the context, assigned to a property, stored in the caller's variables. Here nothing but a real
        /// List&lt;T&gt; or HashSet&lt;T&gt; is ever built, so there is nothing that can leak.
        ///
        /// The registry lives and dies with one compilation, so it costs nothing at evaluation time and is
        /// never shared between compilations or across threads.
        /// </remarks>
        public void MarkAsConstructedCollection(LExpression expression)
        {
            _constructedCollections.Add(expression);
        }

        /// <summary>
        /// Whether <paramref name="expression"/> was registered by
        /// <see cref="MarkAsConstructedCollection"/>.
        /// </summary>
        public bool IsConstructedCollection(LExpression expression)
        {
            return _constructedCollections.Contains(expression);
        }

        public void AddLocalVariable(string variableName, ParameterExpression variableExpression)
        {
            if (_localVariables == null)
                _localVariables = new Dictionary<string, ParameterExpression>();

            _localVariables.Add(variableName, variableExpression);
        }

        public bool TryGetLocalVariable(
            string variableName, out ParameterExpression variableExpression)
        {
            if (_localVariables == null)
            {
                variableExpression = null;
                return false;
            }

            return _localVariables.TryGetValue(variableName, out variableExpression);
        }

        /// <summary>
        /// The storage a free <c>$local</c> - one no enclosing lambda declares as a parameter - reads
        /// and writes: one block variable per name, declared on demand. False where this scope cannot
        /// host one.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The interpreter's twin is <c>EvaluationContext.LocalVariables</c>, a dictionary created the
        /// first time something assigns to a local and thrown away with the evaluation. A block
        /// variable says the same thing to the LINQ compiler: whoever wraps the emitted tree -
        /// Compiler for a whole expression, LambdaExpressionNode for a lambda body - declares them,
        /// so the storage lives exactly one invocation of the compiled delegate and two threads
        /// evaluating the same expression cannot see each other's locals.
        /// </p>
        /// <p>
        /// Every <c>$name</c> is a literal in the grammar, so the set of names is known while the
        /// tree is being emitted and there is nothing a dictionary would buy: an unassigned variable
        /// already defaults to null, which is what the interpreter answers for a key it does not
        /// hold, and a name is a slot rather than a hash lookup. The first version did hold one
        /// <c>Dictionary&lt;string, object&gt;</c> here, mirroring the interpreter's storage one for
        /// one; this is the same semantics with the allocation and the lookup removed.
        /// </p>
        /// <p>
        /// The variables are object-typed and that part is forced: an unassigned local reads as null,
        /// the interpreter's hashtable lets one be reassigned to a different type, and whether a
        /// local is assigned at all stops being statically decidable inside a branch. Giving a local
        /// a real type is a language change - a declaration both backends execute - and is
        /// <c>_Docs/open-issues.md</c> item 15, which this is the compiled half of.
        /// </p>
        /// <p>
        /// A projection or selection body has no such scope: it is compiled by its own
        /// <c>Compile()</c> call and passed into the emitted tree as a constant delegate, so a block
        /// variable of the enclosing compilation is simply not in scope there. Emitting one anyway
        /// produced an unbound-variable failure out of the LINQ compiler, which the absorbing wrapper
        /// then had to report as an internal defect.
        /// </p>
        /// </remarks>
        public bool TryGetLocalStorage(
            [NotNull] string variableName, out ParameterExpression storage)
        {
            if (_localStorage == null)
            {
                storage = null;
                return false;
            }

            if (!_localStorage.TryGetValue(variableName, out storage))
            {
                storage = LExpression.Variable(typeof(object), "local_" + variableName);
                _localStorage.Add(variableName, storage);
                _localStorageOrder.Add(storage);
            }

            return true;
        }

        /// <summary>
        /// Every local storage variable asked for, in the order it was first reached, or an empty
        /// list - the question whoever wraps the tree asks, so an expression using no locals declares
        /// nothing.
        /// </summary>
        [NotNull, ItemNotNull]
        public IList<ParameterExpression> DeclaredLocalStorage
        {
            get { return (IList<ParameterExpression>)_localStorageOrder ?? EmptyStorage; }
        }

        private static readonly ParameterExpression[] EmptyStorage = new ParameterExpression[0];

        public LExpression RootContextExpression { get; private set; }
        public LExpression ThisExpression { get; private set; }

        /// <summary>
        /// The caller-supplied variables dictionary, as a parameter of the compiled delegate.
        /// Only <see cref="VariableNode"/> reads it: #root and #this resolve to
        /// <see cref="RootContextExpression"/> / <see cref="ThisExpression"/> and $locals to
        /// <see cref="ParameterExpression"/>s, all at compile time. Compiled code therefore needs
        /// no <c>EvaluationContext</c> - that object exists for the interpreter, which mutates it.
        /// </summary>
        public LExpression VariablesExpression { get; private set; }

        public Dictionary<string, ParameterExpression> _localVariables;

        private readonly HashSet<LExpression> _constructedCollections;

        // Lookup and declaration order kept apart: a Dictionary does not promise an enumeration
        // order, and an emitted tree that varies between runs is harder to read than one that does
        // not.
        private readonly Dictionary<string, ParameterExpression> _localStorage;
        private readonly List<ParameterExpression> _localStorageOrder;
    }
}
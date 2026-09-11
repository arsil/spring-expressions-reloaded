using System.Collections.Generic;
using System.Linq.Expressions;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    public class CompilationContext
    {
        public CompilationContext(
            LExpression rootContextExpression,
            LExpression variablesExpression,
            [NotNull] SandboxPolicy sandboxPolicy)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = rootContextExpression;
            VariablesExpression = variablesExpression;
            SandboxPolicy = sandboxPolicy;
            _constructedCollections = new HashSet<LExpression>();
            _localStorage = new Dictionary<string, ParameterExpression>();
            _localStorageOrder = new List<ParameterExpression>();
        }

        public CompilationContext CreateWithNewThisContext(LExpression thisExpression)
        {
            return new CompilationContext(
                RootContextExpression, thisExpression, VariablesExpression, SandboxPolicy,
                _constructedCollections, _localStorage, _localStorageOrder);
        }
        // todo: error: context expression != RootExpression    !!!!  !!!!! !!!!

        private CompilationContext(
            LExpression rootContextExpression,
            LExpression thisExpression,
            LExpression variablesExpression,
            [NotNull] SandboxPolicy sandboxPolicy,
            HashSet<LExpression> constructedCollections,
            Dictionary<string, ParameterExpression> localStorage,
            List<ParameterExpression> localStorageOrder)
        {
            RootContextExpression = rootContextExpression;
            ThisExpression = thisExpression;
            VariablesExpression = variablesExpression;
            SandboxPolicy = sandboxPolicy;

            // Shared, not copied: a union inside a projection compiles against a derived context, and the
            // root that Compiler finally inspects is the one it registered into.
            _constructedCollections = constructedCollections;

            // Shared, like the registry above: a projection or selection body is part of the same
            // compilation now - its lambda is nested in the emitted tree rather than compiled on its
            // own and handed in as a constant - so a block variable declared in the outer scope is
            // reachable from inside the body and the outer lambda's closure carries it.
            //
            // This used to be null, and the reason was true when written: with the body compiled
            // separately, emitting a reference to an outer block variable produced an unbound-variable
            // failure out of the LINQ compiler. Nesting the body removed that, and with it the only
            // mechanical argument for making a body different from anywhere else.
            _localStorage = localStorage;
            _localStorageOrder = localStorageOrder;
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
        /// <b>A projection or selection body shares the enclosing scope</b>, so a local declared
        /// outside one is readable and writable inside it - <c>($s = ''; Words.!{$s = $s + #this};
        /// $s)</c> compiles. That was a refusal until 2026-09-11, and correctly so at the time: the
        /// body was compiled by its own <c>Compile()</c> call and handed into the tree as a constant
        /// delegate, so an outer block variable was genuinely not in scope and emitting a reference
        /// to one produced an unbound-variable failure. Nesting the body lambda removed the
        /// obstacle, and with it the only argument for treating a body differently from anywhere
        /// else. What remains inside a body is the ordinary object-typed-local story: a local's
        /// members need a cast, exactly as they do outside one.
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

        /// <summary>
        /// What this compilation is allowed to reach. Fixed when the expression was created and
        /// carried down here, never read from ambient state - see
        /// <c>_Docs/type-sandboxing.md</c> §4.3 for why a scope cannot work: member binds happen once
        /// while the tree is built, so a policy that varied per evaluation could only be honoured by
        /// recompiling.
        /// </summary>
        /// <remarks>
        /// Shared unchanged by <see cref="CreateWithNewThisContext"/>: a projection or selection body
        /// is a separate compilation unit but the same expression, so it is governed by the same
        /// policy.
        /// </remarks>
        [NotNull]
        public SandboxPolicy SandboxPolicy { get; private set; }

        public Dictionary<string, ParameterExpression> _localVariables;

        private readonly HashSet<LExpression> _constructedCollections;

        // Lookup and declaration order kept apart: a Dictionary does not promise an enumeration
        // order, and an emitted tree that varies between runs is harder to read than one that does
        // not.
        private readonly Dictionary<string, ParameterExpression> _localStorage;
        private readonly List<ParameterExpression> _localStorageOrder;
    }
}
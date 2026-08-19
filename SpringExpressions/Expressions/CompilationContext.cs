using System.Collections.Generic;
using System.Linq.Expressions;

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
    }
}
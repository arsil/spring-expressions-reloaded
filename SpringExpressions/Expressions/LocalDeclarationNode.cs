using System;
using System.Collections;
using System.Collections.Generic;

using JetBrains.Annotations;

using SpringCore.TypeConversion;
using SpringCore.TypeResolution;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// A typed local declaration - <c>int $x = 5</c>, or <c>int $x</c> with no initialiser - written as
    /// an element of a <c>(a; b; c)</c> sequence.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>Both backends execute the declaration, which is the whole reason a declared local can carry a
    /// type where an inferred one cannot.</b> The interpreter seeds
    /// <c>EvaluationContext.LocalVariables</c> with the converted value; the compiled path declares a
    /// block variable of the declared type. Neither is reproducing what the other does, so there is
    /// nothing to keep in step - the same construction that <c>PromoteNumericType</c>,
    /// <c>EnumEqualsName</c> and <c>CastOperations</c> already use.
    /// </p>
    /// <p>
    /// <b>A declared local has no scope of its own, and that is deliberate.</b> A free <c>$x</c> is
    /// expression-wide on both backends today - one flat <c>Hashtable</c> per evaluation interpreted,
    /// one block variable per name hoisted to the outermost block compiled - so
    /// <c>(($x = 5; $x); $x)</c> reads 5 in both. A declaration joins that namespace rather than
    /// introducing a second rule: giving declared names block scope would mean the compiled path got
    /// scoping free from LINQ while the interpreter had a scope chain written by hand, which is exactly
    /// the shape this fork's divergences come from. The cost is that a name cannot be shadowed.
    /// </p>
    /// <p>
    /// <b>A declared local is a typed sink, so it borrows assignment's conversion rule rather than
    /// inventing one.</b> The compiled path converts by
    /// <see cref="SpringExpressions.Util.ArrayElementConversions.TryConvertExpression"/> - identity, a
    /// retyped null, reference or boxing assignability, and the implicit numeric widenings - and
    /// refuses anything else; the interpreter converts by
    /// <see cref="TypeConversionUtils.ConvertValueIfNecessary"/>, which is what a property setter runs.
    /// Everything the compiled path accepts, the interpreter reaches the same value for; everything
    /// else is refused compiled and served by the interpreter alone, so no shape has two answers.
    /// </p>
    /// </remarks>
    public class LocalDeclarationNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public LocalDeclarationNode()
        {
        }

        /// <summary>
        /// Declares the local, gives it its initial value and evaluates to that value.
        /// </summary>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            var name = getText();
            var declaredType = ResolveDeclaredType(evalContext.SandboxPolicy);

            RefuseRedeclaration(evalContext, name);

            var initializer = GetInitializer();

            var value = initializer == null
                ? DefaultValueOf(declaredType)
                : GetValue(initializer, context, evalContext);

            value = TypeConversionUtils.ConvertValueIfNecessary(declaredType, value, name);

            if (evalContext.LocalVariables == null)
                evalContext.LocalVariables = new Hashtable();

            evalContext.LocalVariables[name] = value;

            if (evalContext.LocalDeclarations == null)
                evalContext.LocalDeclarations = new Dictionary<string, LocalDeclaration>();

            evalContext.LocalDeclarations[name] = new LocalDeclaration(declaredType, this);

            return value;
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var name = getText();

            Type declaredType;
            try
            {
                declaredType = ResolveDeclaredType(compilationContext.SandboxPolicy);
            }
            catch (TypeLoadException)
            {
                // ResolveType throws rather than returning null, and a name that does not resolve is
                // the caller's own mistake: refusing keeps it out of the absorber, which would report
                // their typo as our defect, and the interpreter raises it at evaluation.
                throw CannotCompile("the declared type name does not resolve");
            }

            // A name already in use is either a second declaration of it or a free local that was read
            // or written before the declaration ran. Both are the caller's mistake rather than a shape
            // with no compiled form, so this refuses and the interpreter raises the error at
            // evaluation - the standing paired shape. It also cannot do anything else: whatever emitted
            // earlier already holds the other variable.
            if (!compilationContext.TryDeclareLocalStorage(name, declaredType, this, out var storage))
            {
                throw CannotCompile(
                    $"local '${name}' is already in use here; a declaration must come before every use "
                    + "of the name, and a name may be declared only once");
            }

            var initializer = GetInitializer();

            // No initialiser: the block variable is already default(T), which is the value the
            // interpreter seeds, so reading it is both the declaration's effect and its value.
            if (initializer == null)
                return storage;

            var value = GetExpressionTreeIfPossible(initializer, contextExpression, compilationContext);

            // A collection this engine built is reshaped to what the declared type asks for, exactly as
            // it is at every other sink. One the caller owns is never registered and so arrives as the
            // very instance.
            value = compilationContext.NormalizeIfConstructed(value, declaredType);

            if (!SpringExpressions.Util.ArrayElementConversions.TryConvertExpression(
                    value, declaredType, out var converted))
            {
                throw CannotCompile(
                    $"no compiled conversion of a '{value.Type}' into '${name}', which is declared "
                    + $"'{declaredType}'; the interpreter converts values the emitted assignment cannot");
            }

            return BuildAssign(storage, converted);
        }

        /// <summary>
        /// The declared type's name is the first child - an <c>asTypeSlot</c>, so the same vocabulary
        /// the cast operator uses - and the initialiser, where one is written, the second.
        /// </summary>
        [CanBeNull]
        private BaseNode GetInitializer()
        {
            var typeNode = getFirstChild();
            return typeNode == null ? null : (BaseNode)typeNode.getNextSibling();
        }

        [NotNull]
        private Type ResolveDeclaredType([NotNull] SandboxPolicy sandboxPolicy)
        {
            if (_declaredType == null)
            {
                var typeNode = getFirstChild();
                if (typeNode == null)
                    throw new ArgumentException("A local declaration has no type.");

                // Gated exactly as a cast's type name is, and cached per node the way TypeNode and
                // CastNode cache theirs - the name is fixed by the expression text.
                _declaredType = TypeResolutionUtils.ResolveTypeForExpression(
                    typeNode.getText(), sandboxPolicy);
            }

            return _declaredType;
        }

        private Type _declaredType;

        /// <summary>
        /// The value a declaration with no initialiser starts at - <c>default(T)</c>, which is what the
        /// compiled path's block variable holds before anything assigns to it.
        /// </summary>
        [CanBeNull]
        private static object DefaultValueOf([NotNull] Type type)
        {
            return type.IsValueType && Nullable.GetUnderlyingType(type) == null
                ? Activator.CreateInstance(type)
                : null;
        }

        /// <summary>
        /// A name may be declared once, and not after it has been used.
        /// </summary>
        /// <remarks>
        /// Re-entering the <i>same</i> declaration is not a redeclaration: a projection body is
        /// evaluated per item, so <c>Ints.!{(int $x = #this; $x)}</c> runs this node once per item and
        /// must keep working. The test is therefore on which node declared the name, not on whether the
        /// name is present.
        /// </remarks>
        private void RefuseRedeclaration([NotNull] EvaluationContext evalContext, [NotNull] string name)
        {
            var declarations = evalContext.LocalDeclarations;

            if (declarations != null && declarations.TryGetValue(name, out var existing))
            {
                if (!ReferenceEquals(existing.DeclaringNode, this))
                {
                    throw new ArgumentException(
                        $"Local '${name}' is declared more than once in this expression.");
                }

                return;
            }

            if (evalContext.LocalVariables != null && evalContext.LocalVariables.Contains(name))
            {
                throw new ArgumentException(
                    $"Local '${name}' is used before it is declared.");
            }
        }
    }

    /// <summary>
    /// What a <see cref="LocalDeclarationNode"/> recorded about one name for the rest of an evaluation:
    /// the declared type, which every later assignment converts to, and the node that declared it, which
    /// is how re-entering one declaration is told from writing a second.
    /// </summary>
    internal sealed class LocalDeclaration
    {
        public LocalDeclaration([NotNull] Type type, [NotNull] object declaringNode)
        {
            Type = type;
            DeclaringNode = declaringNode;
        }

        [NotNull]
        public readonly Type Type;

        [NotNull]
        public readonly object DeclaringNode;
    }
}

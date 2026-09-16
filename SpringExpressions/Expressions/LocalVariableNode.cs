#region License

/*
 * Copyright © 2002-2011 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;

using SpringCore.TypeConversion;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed variable node.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class LocalVariableNode : BaseNode
    {
        //internal const string LOCAL_VARIABLES = "__locals";
     
        /// <summary>
        /// Create a new instance
        /// </summary>
        public LocalVariableNode()
        {
        }

                /// <summary>
        /// Returns value of the local variable represented by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            string varName = this.getText();
            IDictionary locals = evalContext.LocalVariables;
            if (locals != null)
            {
                return locals[varName];
            }
            return null;
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var variableName = getText();

            // A lambda parameter is bound by the call, and the enclosing lambda already declared it.
            if (compilationContext.TryGetLocalVariable(variableName, out var variableExpression))
                return variableExpression;

            // Anything else is a free local: storage the expression owns for the length of one
            // evaluation, and one object-typed block variable per name is all that takes. It used to
            // refuse here, which meant an expression could assign to a local through the interpreter
            // and never through the compiled backend - and, since the interpreter answers null for a
            // local nothing has assigned to, refusing the read of an undefined one was refusing a
            // shape that has a perfectly good answer. An unassigned block variable is null, which is
            // that answer without a line of code.
            if (!compilationContext.TryGetLocalStorage(variableName, out var storage))
                throw CannotCompile(LocalsOutOfScopeReason);

            return storage;
        }

        protected override LExpression GetExpressionTreeForSetterIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext,
            LExpression newValueExpression)
        {
            var variableName = getText();

            // The interpreter writes a lambda parameter into the argument dictionary the call swapped
            // in, which the compiled form has no equivalent of - its parameters are the delegate's
            // own. Rather than assign to the ParameterExpression and hope the two stay level, the
            // shape is refused and the interpreter serves it.
            if (compilationContext.TryGetLocalVariable(variableName, out var _))
                throw CannotCompile("a lambda parameter is bound by the call and cannot be assigned to");

            if (!compilationContext.TryGetLocalStorage(variableName, out var storage))
                throw CannotCompile(LocalsOutOfScopeReason);

            // Assign is an expression and yields the value assigned, which is what the interpreter's
            // Set does and what '($x = 5) + $x' being ten depends on - so nothing has to be wrapped
            // around it to produce a value.
            //
            // The value is boxed on the way into the object-typed slot: LINQ inserts no boxing of its
            // own, so without this '$x = 5' would refuse while '$x = 'five'' compiled - the kind of
            // split that made the same assignment behave differently for no reason a caller could
            // see.
            // A collection this engine built is reshaped on the way in, to the shape the interpreter
            // stores. The interpreter builds a List<object> and puts that in its slot; without this
            // the compiled path stored the typed list it had built, so '($xs = Ints.!{…}; $xs)'
            // answered List<int> against the interpreter's List<object>.
            //
            // It has to happen here rather than at the read: a slot is object-typed, so by the time
            // the value comes back out there is no static type left to reshape. A collection merely
            // *read* is not registered and so is stored untouched - the caller's own object, identity
            // and all.
            // A declared local is a typed sink: the value is reshaped to what the declaration asked for
            // and then converted by the same rule a property write uses, or the shape is refused and
            // the interpreter converts. An undeclared one is object-typed and takes the boxing below.
            if (storage.Type != typeof(object))
                return BuildAssign(storage, ConvertIntoDeclaredSlot(
                    compilationContext, newValueExpression, storage.Type, variableName));

            if (compilationContext.IsConstructedCollection(newValueExpression))
            {
                newValueExpression = Expressions.Compiler.NormalizeConstructedCollection(
                    newValueExpression, typeof(object));
            }

            return BuildAssign(storage, BoxIfValueType(newValueExpression));
        }

        /// <summary>
        /// A scope with no storage for locals at all. Every compilation now has one - a projection or
        /// selection body shares its enclosing scope since 2026-09-11 - so this is a guard against a
        /// future scope that does not, not a shape any expression reaches today.
        /// </summary>
        private const string LocalsOutOfScopeReason
            = "this scope has no storage for local variables";

        /// <summary>
        /// The value converted into a declared local's slot, or a refusal.
        /// </summary>
        /// <remarks>
        /// The rule is borrowed rather than invented - it is the array-initialiser conversion that
        /// <c>new T[] {…}</c>, <c>params</c> elements and a property write all run, so a declared local
        /// gains no conversion rule of its own. Everything it accepts, the interpreter's converter
        /// reaches the same value for; everything it refuses is served by the interpreter alone, which
        /// is what keeps the two backends from answering differently about a write.
        /// </remarks>
        [NotNull]
        private LExpression ConvertIntoDeclaredSlot(
            [NotNull] CompilationContext compilationContext,
            [NotNull] LExpression newValueExpression,
            [NotNull] Type declaredType,
            [NotNull] string variableName)
        {
            var value = compilationContext.NormalizeIfConstructed(newValueExpression, declaredType);

            if (SpringExpressions.Util.ArrayElementConversions.TryConvertExpression(
                    value, declaredType, out var converted))
            {
                return converted;
            }

            throw CannotCompile(
                $"no compiled assignment of a '{value.Type}' to '${variableName}', which is declared "
                + $"'{declaredType}'; the interpreter converts values the emitted assignment cannot");
        }

        private static LExpression BoxIfValueType([NotNull] LExpression expression)
        {
            return expression.Type.IsValueType
                ? LExpression.Convert(expression, typeof(object))
                : expression;
        }

        /// <summary>
        /// Sets value of the local variable represented by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <param name="newValue">New value for this node.</param>
        protected override object Set(object context, EvaluationContext evalContext, object newValue)
        {
            string varName = this.getText();
            IDictionary locals = evalContext.LocalVariables;
            if (locals == null)
            {
                locals = new Hashtable();
                evalContext.LocalVariables = locals;
            }
            // A declared local is a typed sink and converts on the way in, the way a property write
            // does; an undeclared one is untyped, so the value written is the value given.
            var declarations = evalContext.LocalDeclarations;
            if (declarations != null && declarations.TryGetValue(varName, out var declaration))
                newValue = TypeConversionUtils.ConvertValueIfNecessary(declaration.Type, newValue, varName);

            locals[varName] = newValue;

            return newValue;
        }
    }
}
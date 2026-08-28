using System;
using System.Collections.Generic;

using System.Linq.Expressions;

using JetBrains.Annotations;

using SpringExpressions.Expressions.Compiling.Expressions;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Expressions
{
    using static BaseNode;

    internal static class Compiler
    {
        /// <summary>
        /// Builds the lambda, reporting a body the delegate type cannot accept as a
        /// <see cref="CompileErrorException"/>.
        /// </summary>
        /// <remarks>
        /// LExpression.Lambda validates the body against the delegate's return type and throws
        /// ArgumentException when they do not match - a HashSet&lt;int&gt; body against a requested
        /// ISet&lt;object&gt; result, for instance, since ISet&lt;T&gt; is invariant and no conversion
        /// exists. ArgumentException is not this codebase's "cannot compile" signal, so
        /// WeaklyTypedExpression's catch never sees it and an expression the interpreter would evaluate
        /// perfectly well becomes a hard failure instead of falling back.
        /// </remarks>
        private static Expression<TDelegate> BuildLambda<TDelegate>(
            LExpression body, params ParameterExpression[] parameters)
        {
            try
            {
                return LExpression.Lambda<TDelegate>(body, parameters);
            }
            catch (ArgumentException ex)
            {
                throw new CompileErrorException(
                    $"cannot compile an expression of type '{body.Type}' as '{typeof(TDelegate)}': {ex.Message}");
            }
        }

        /// <summary>
        /// Normalizes a set result to HashSet&lt;object&gt; where the caller asked for nothing narrower.
        /// </summary>
        /// <remarks>
        /// The compiled path keeps a set's item type - a union of two int collections is a HashSet&lt;int&gt;
        /// - while the interpreter sees only boxed values and always builds a HashSet&lt;object&gt;. So the
        /// same expression had two different result types depending on which backend ran it, and the caller
        /// does not choose the backend: the weakly typed path compiles when it can and interprets when it
        /// cannot. Where nothing more specific than object was requested there is no reason to prefer the
        /// typed one, and agreeing with the interpreter is worth more, so the root value is reprojected.
        ///
        /// Only the root value: everything inside the tree keeps the item type it needs, which is what lets
        /// sum(), average(), max(), projections and selections over a union stay compiled. Reprojecting
        /// rather than converting because ISet&lt;T&gt; is invariant - there is no conversion from
        /// HashSet&lt;int&gt; to ISet&lt;object&gt; to emit, so this allocates a second set. That cost is paid
        /// once per evaluation, and only when the root result is a typed set.
        /// </remarks>
        private static LExpression NormalizeSetResult(LExpression body, Type resultType)
        {
            var itemType = CollectionOperandUtils.GetSetItemType(body.Type);

            // Already a set of object - what the interpreter builds - so there is nothing to reconcile.
            if (itemType == null || itemType == typeof(object))
            {
                return body;
            }

            // A requested type that a plain HashSet<T> satisfies: copy into one, keeping the item type.
            if (resultType != typeof(object)
                && resultType.IsAssignableFrom(typeof(HashSet<>).MakeGenericType(itemType)))
            {
                return LExpression.Call(ToTypedHashSetMethodInfo.MakeGenericMethod(itemType), body);
            }

            // Otherwise hand back what the interpreter would have built. If that does not satisfy the
            // request either - a set of some unrelated item type - BuildLambda refuses it as a compile
            // error rather than the ArgumentException LExpression.Lambda would raise.
            return LExpression.Call(ToHashSetOfObjectsMethodInfo, body);
        }

        /// <summary>
        /// The list counterpart of <see cref="NormalizeSetResult"/>.
        /// </summary>
        /// <remarks>
        /// Reached only for a list the expression built - see the caller - and only when its item type is
        /// something narrower than object, which is the case a literal of uniformly typed items produces and
        /// the interpreter cannot. A literal with no common item type is already a list of object and needs
        /// nothing done to it.
        /// </remarks>
        private static LExpression NormalizeListResult(LExpression body, Type resultType)
        {
            var itemType = CollectionOperandUtils.GetListItemType(body.Type);

            // Already a list of object - what the interpreter builds - so there is nothing to reconcile.
            if (itemType == null || itemType == typeof(object))
            {
                return body;
            }

            // Nothing narrower requested: hand back what the interpreter would have built.
            if (resultType != typeof(object)
                && resultType.IsAssignableFrom(typeof(List<>).MakeGenericType(itemType)))
            {
                return LExpression.Call(ToTypedListMethodInfo.MakeGenericMethod(itemType), body);
            }

            return LExpression.Call(ToListOfObjectsMethodInfo, body);
        }

        /// <summary>
        /// The dictionary counterpart of <see cref="NormalizeSetResult"/> and
        /// <see cref="NormalizeListResult"/>.
        /// </summary>
        /// <remarks>
        /// Reached only for a dictionary the expression built - see the caller - and only when its key or
        /// value type is something narrower than object, which is the case a map literal of uniformly
        /// typed entries produces and the interpreter cannot. A literal with mixed entry types is already
        /// a dictionary of object and needs nothing done to it.
        /// </remarks>
        private static LExpression NormalizeDictionaryResult(LExpression body, Type resultType)
        {
            if (!CollectionOperandUtils.TryGetDictionaryItemTypes(body.Type, out var keyType, out var valueType))
            {
                return body;
            }

            // Already a dictionary of object - what the interpreter builds - so there is nothing to
            // reconcile.
            if (keyType == typeof(object) && valueType == typeof(object))
            {
                return body;
            }

            // A requested type that a plain Dictionary<K,V> satisfies: copy into one, keeping the types.
            if (resultType != typeof(object)
                && resultType.IsAssignableFrom(typeof(Dictionary<,>).MakeGenericType(keyType, valueType)))
            {
                return LExpression.Call(
                    ToTypedDictionaryMethodInfo.MakeGenericMethod(keyType, valueType), body);
            }

            return LExpression.Call(ToDictionaryOfObjectsMethodInfo, body);
        }

        private static readonly System.Reflection.MethodInfo ToTypedDictionaryMethodInfo
            = typeof(CollectionOperandUtils).GetMethod(
                nameof(CollectionOperandUtils.ToTypedDictionary),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        private static readonly System.Reflection.MethodInfo ToDictionaryOfObjectsMethodInfo
            = typeof(CollectionOperandUtils).GetMethod(
                nameof(CollectionOperandUtils.ToDictionaryOfObjects),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        private static readonly System.Reflection.MethodInfo ToTypedListMethodInfo
            = typeof(CollectionOperandUtils).GetMethod(
                nameof(CollectionOperandUtils.ToTypedList),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        private static readonly System.Reflection.MethodInfo ToListOfObjectsMethodInfo
            = typeof(CollectionOperandUtils).GetMethod(
                nameof(CollectionOperandUtils.ToListOfObjects),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        private static readonly System.Reflection.MethodInfo ToHashSetOfObjectsMethodInfo
            = typeof(CollectionOperandUtils).GetMethod(
                nameof(CollectionOperandUtils.ToHashSetOfObjects),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        private static readonly System.Reflection.MethodInfo ToTypedHashSetMethodInfo
            = typeof(CollectionOperandUtils).GetMethod(
                nameof(CollectionOperandUtils.ToTypedHashSet),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Compiles a getter, or refuses. **Nothing else escapes.**
        /// </summary>
        /// <remarks>
        /// This and its two siblings are the whole compilation phase - three call sites in the entire
        /// library - which is why the guarantee lives here rather than in the fifty-one nodes that emit.
        /// A node that reports its refusal properly is passed through untouched; anything else is a
        /// defect in this library and is absorbed into a refusal, so a caller who did nothing wrong
        /// gets the interpreter instead of an exception nobody can act on. See
        /// <see cref="InternalCompilerErrorException"/>, and <c>_Docs/compilation-error-reporting.md</c>
        /// for the sweep that measured how much used to escape.
        /// </remarks>
        public static Func<TContext, IDictionary<string, object>, TResult> CompileGetter<TResult, TContext>(
            BaseNode expressionNode)
        {
            try
            {
                return CompileGetterCore<TResult, TContext>(expressionNode);
            }
            catch (Exception e) when (InternalCompilerErrorException.ShouldAbsorb(e))
            {
                throw new InternalCompilerErrorException(expressionNode, e);
            }
        }

        private static Func<TContext, IDictionary<string, object>, TResult> CompileGetterCore<TResult, TContext>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var variablesParam = LExpression.Parameter(typeof(IDictionary<string, object>), "variables");

            LExpression getRootContextExpression;
            // todo: error: czy to ma sens?????!!!!------------------------------------------------------------------------------
            //            if (context == null)
            //                getRootContextExpression = LExpression.Constant(null, typeof(TContext));
            //          else
            //            getRootContextExpression = LExpression.Convert(ctxParam, typeof(TContext));

            getRootContextExpression = ctxParam;


            // The root arrives as a typed delegate parameter, so the compiled tree never needed the
            // untyped EvaluationContext.RootContext; the only thing it did need was the variables
            // dictionary, which is now the second parameter. Nothing per-evaluation is cached on the
            // expression instance, which is what makes a compiled expression safe to share.
            var compilationContext = new CompilationContext(getRootContextExpression, variablesParam);

            var exp = GetExpressionTreeIfPossible(
                expressionNode,
                getRootContextExpression,
                compilationContext);

            // An expression whose body is void - an assignment, say - still has to produce a value when the
            // result type is object. Yielding null after it is what the weakly typed path always did.
            if (exp.Type == typeof(void) && typeof(TResult) == typeof(object))
            {
                exp = LExpression.Block(exp, LExpression.Constant(null, typeof(object)));
            }

            if (exp.Type.IsValueType)
            {
                var resultType = typeof(TResult);

                if (resultType == typeof(object))
                {
                    // boxing value types for TResult == object
                    exp = LExpression.Convert(exp, typeof(object));
                }
                else if (resultType != exp.Type && resultType.IsValueType)
                {
                    exp = LExpression.ConvertChecked(exp, resultType);
                }
            }

            // Only a collection this engine built may be reshaped. One that was read out of the object graph
            // - a property, a field, a method result - is the caller's own object and is handed back exactly
            // as it came, reference identity included, which is also what the interpreter does.
            if (compilationContext.IsConstructedCollection(exp))
            {
                exp = NormalizeSetResult(exp, typeof(TResult));
                exp = NormalizeListResult(exp, typeof(TResult));
                exp = NormalizeDictionaryResult(exp, typeof(TResult));
            }

            // An object-typed body carries no compile-time information, so a typed request over it is
            // decided at runtime - the same cast the interpreted getter performs on its result. Without
            // this, the compiled path refused shapes the interpreter satisfies, e.g. convert(decimal)
            // requested as List<decimal>: its emitted call is object-typed because the target type is
            // an argument value.
            if (exp.Type == typeof(object) && typeof(TResult) != typeof(object))
            {
                exp = LExpression.Convert(exp, typeof(TResult));
            }

            exp = DeclareLocalsIfUsed(exp, compilationContext);

            Expression<Func<TContext, IDictionary<string, object>, TResult>> lambda
                = BuildLambda<Func<TContext, IDictionary<string, object>, TResult>>(
                    exp, ctxParam, variablesParam);

            return lambda.Compile();
        }

        /// <summary>
        /// Declares the storage a free <c>$local</c> uses, if the emitted tree asked for any.
        /// </summary>
        /// <remarks>
        /// A block variable holding a fresh dictionary, assigned before the body runs - so the
        /// locals live exactly one invocation of the delegate, which is what the interpreter's
        /// per-evaluation EvaluationContext.LocalVariables gives it, and what keeps two threads
        /// evaluating one shared compiled expression out of each other's way.
        /// <p>
        /// Wrapping happens last, after every inspection of <paramref name="body"/>: the block takes
        /// its type from the body, so nothing above changes, while the constructed-collection
        /// registry is keyed on the body expression itself and would stop recognising it.
        /// </p>
        /// </remarks>
        [NotNull]
        internal static LExpression DeclareLocalsIfUsed(
            [NotNull] LExpression body, [NotNull] CompilationContext compilationContext)
        {
            var locals = compilationContext.DeclaredLocalsDictionary;

            if (locals == null)
                return body;

            return LExpression.Block(
                new[] { locals },
                LExpression.Assign(locals, LExpression.New(typeof(Dictionary<string, object>))),
                body);
        }

        /// <summary>Compiles a setter, or refuses - see <see cref="CompileGetter{TResult,TContext}"/>.</summary>
        public static Action<TContext, IDictionary<string, object>, TArgument> CompileSetter<TContext, TArgument>(
            BaseNode expressionNode)
        {
            try
            {
                return CompileSetterCore<TContext, TArgument>(expressionNode);
            }
            catch (Exception e) when (InternalCompilerErrorException.ShouldAbsorb(e))
            {
                throw new InternalCompilerErrorException(expressionNode, e);
            }
        }

        private static Action<TContext, IDictionary<string, object>, TArgument> CompileSetterCore<TContext, TArgument>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var newValueParam = LExpression.Parameter(typeof(TArgument), "newValue");

            var variablesParam = LExpression.Parameter(typeof(IDictionary<string, object>), "variables");

            LExpression getRootContextExpression;
            // todo: error: czy to ma sens?????!!!!------------------------------------------------------------------------------
            //            if (context == null)
            //                getRootContextExpression = LExpression.Constant(null, typeof(TContext));
            //          else
            //            getRootContextExpression = LExpression.Convert(ctxParam, typeof(TContext));

            getRootContextExpression = ctxParam;

            var compilationContext = new CompilationContext(getRootContextExpression, variablesParam);

            var exp = GetExpressionTreeForSetterIfPossible(
                expressionNode,
                getRootContextExpression,
                compilationContext,
                newValueParam);

            exp = DeclareLocalsIfUsed(exp, compilationContext);

               // todo: error; must compile!
            
               // todo: nodeType == Assign?
/*
            if (exp.Type != typeof(void))
            {
                var tree = ((SpringExpressions.Parser.antlr.collections.AST)expressionNode).ToStringTree();
                throw new InvalidOperationException($"Expression returns {exp.Type} instead of void! \n" + tree);
            }
*/
            Expression<Action<TContext, IDictionary<string, object>, TArgument>> lambda
                = BuildLambda<Action<TContext, IDictionary<string, object>, TArgument>>(
                    exp, ctxParam, variablesParam, newValueParam);

            return lambda.Compile();
        }

        /// <summary>Compiles a void expression, or refuses - see <see cref="CompileGetter{TResult,TContext}"/>.</summary>
        public static Action<TContext, IDictionary<string, object>> CompileExecuteWithVoidReturnType<TContext>(
            BaseNode expressionNode)
        {
            try
            {
                return CompileExecuteWithVoidReturnTypeCore<TContext>(expressionNode);
            }
            catch (Exception e) when (InternalCompilerErrorException.ShouldAbsorb(e))
            {
                throw new InternalCompilerErrorException(expressionNode, e);
            }
        }

        private static Action<TContext, IDictionary<string, object>> CompileExecuteWithVoidReturnTypeCore<TContext>(
            BaseNode expressionNode)
        {
            var ctxParam = LExpression.Parameter(typeof(TContext), "context");
            var variablesParam = LExpression.Parameter(typeof(IDictionary<string, object>), "variables");

            LExpression getRootContextExpression;
            // todo: error: czy to ma sens?????!!!!------------------------------------------------------------------------------
            //            if (context == null)
            //                getRootContextExpression = LExpression.Constant(null, typeof(TContext));
            //          else
            //            getRootContextExpression = LExpression.Convert(ctxParam, typeof(TContext));

            getRootContextExpression = ctxParam;

            var compilationContext = new CompilationContext(getRootContextExpression, variablesParam);

            var exp = GetExpressionTreeIfPossible(
                expressionNode,
                getRootContextExpression,
                compilationContext);

            // todo: error void or Assign or Block? and last of the block is void or assign?
            // todo: error   Or Call(?) Call return void... so it is void?
            var validExpression
                = exp.Type == typeof(void)
                || exp.NodeType == ExpressionType.Assign;

            // A refusal, not a defect: the shape may execute perfectly well through the interpreter,
            // which discards whatever value the expression produces. Reporting it as anything other
            // than CompileErrorException would escape the fallback and turn a shape that works into a
            // hard failure - 'Ints.sort()' and '#x = 5' both emit a Call and both land here.
            if (!validExpression)
               throw new CompileErrorException(
                   expressionNode,
                   $"a void expression must emit a void call or an assignment, and this emits "
                   + $"'{exp.NodeType}' returning '{exp.Type}'");

            exp = DeclareLocalsIfUsed(exp, compilationContext);

            Expression<Action<TContext, IDictionary<string, object>>> lambda
                = BuildLambda<Action<TContext, IDictionary<string, object>>>(
                    exp, ctxParam, variablesParam);

            return lambda.Compile();
        }
    }
}

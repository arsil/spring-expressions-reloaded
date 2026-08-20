using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using static SpringExpressions.BaseNode;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    abstract class BaseGetterExpression<TRoot, TResult> : BaseStronglyTypedExpression
    {
        protected BaseGetterExpression(
                BaseNode expressionNode,
                CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        {
            // todo: error handling!!!!
            if (_compileOptions.HasFlag(CompileOptions.CompileOnParse))
                _compiledExpression = Compiler.CompileGetter<TResult, TRoot>(_expressionNode);
        }

        protected TResult GetValueInternal(TRoot context, IDictionary<string, object> variables)
        {
            if (_compileOptions.HasFlag(CompileOptions.MustUseInterpreter))
            {
                // The interpreter mutates its context - SwitchThisContext in the projection and
                // selection nodes, SwitchLocalVariables in the lambda node - so it gets a fresh
                // one per evaluation. This is the slow path anyway.
                var value = _expressionNode.GetValueUsingInterpreter(
                    context, new EvaluationContext(context, variables));

                // A value that satisfies the request is cast, never copied - a read collection keeps
                // its reference identity. Only the object-typed collections the interpreter itself
                // builds are reprojected to a narrower request, mirroring what ToTypedList and
                // ToTypedHashSet do for the compiled root; anything else falls through to the cast
                // and its honest InvalidCastException.
                if (value is TResult || value == null)
                    return (TResult)value;

                if (value is List<object> && ListReprojection != null)
                    return ListReprojection(value);

                if (value is HashSet<object> && SetReprojection != null)
                    return SetReprojection(value);

                return (TResult)value;
            }

            // todo: error handling!!!!
            var compiled = _compiledExpression
                ?? (_compiledExpression = Compiler.CompileGetter<TResult, TRoot>(_expressionNode));

            // Root and variables are parameters of the compiled delegate, so nothing is shared
            // between concurrent evaluations and nothing is allocated per evaluation.
            return compiled(context, variables);
        }

        /// <summary>
        /// Copies an interpreter-built List&lt;object&gt; into the requested item type, or null when
        /// TResult is not a type a List&lt;T&gt; can satisfy. Built once per closed generic type - the
        /// CLR's instantiation is the cache.
        /// </summary>
        private static readonly Func<object, TResult> ListReprojection
            = BuildReprojection(typeof(List<>), nameof(CopyToTypedList));

        private static readonly Func<object, TResult> SetReprojection
            = BuildReprojection(typeof(HashSet<>), nameof(CopyToTypedSet));

        private static Func<object, TResult> BuildReprojection(Type containerDefinition, string copyMethodName)
        {
            var resultType = typeof(TResult);

            if (!resultType.IsGenericType)
                return null;

            var genericArguments = resultType.GetGenericArguments();
            if (genericArguments.Length != 1)
                return null;

            // A request for object-typed items is already satisfied by the value itself - the cast
            // above handles it - and reprojecting would gratuitously copy.
            var itemType = genericArguments[0];
            if (itemType == typeof(object))
                return null;

            if (!resultType.IsAssignableFrom(containerDefinition.MakeGenericType(itemType)))
                return null;

            var copyMethod = typeof(BaseGetterExpression<TRoot, TResult>)
                .GetMethod(copyMethodName, BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(itemType);

            return (Func<object, TResult>)Delegate.CreateDelegate(typeof(Func<object, TResult>), copyMethod);
        }

        private static List<T> CopyToTypedList<T>(object source)
        {
            var result = new List<T>();
            foreach (object item in (IEnumerable)source)
                result.Add((T)item);

            return result;
        }

        private static HashSet<T> CopyToTypedSet<T>(object source)
        {
            var result = new HashSet<T>();
            foreach (object item in (IEnumerable)source)
                result.Add((T)item);

            return result;
        }

        private Func<TRoot, IDictionary<string, object>, TResult> _compiledExpression;
    }

    class GetterExpression<TRoot, TResult>
        : BaseGetterExpression<TRoot, TResult>
        , IGetterExpression<TRoot, TResult>
    {
        public GetterExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public TResult GetValue(TRoot context, IDictionary<string, object> variables = null)
            => GetValueInternal(context, variables);
    }

    class GetterExpression<TResult>
        : BaseGetterExpression<object, TResult>
        , IGetterExpression<TResult>
    {
        public GetterExpression(BaseNode expressionNode, CompileOptions compileOptions)
            : base(expressionNode, compileOptions)
        { }

        public TResult GetValue(IDictionary<string, object> variables = null)
            => GetValueInternal(null, variables);
    }
}

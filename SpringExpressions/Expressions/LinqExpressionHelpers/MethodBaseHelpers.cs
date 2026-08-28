using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using JetBrains.Annotations;

using SpringExpressions.Expressions.Compiling.Expressions;
using SpringExpressions.Util;

using LExpression = System.Linq.Expressions.Expression;


namespace SpringExpressions.Expressions.LinqExpressionHelpers
{
    internal static class MethodBaseHelpers
    {
             // todo: error: wywalić do helpera osobnego!!!!!!!!!!!!!!!!!!!!!!!!!!!
        /// <summary>
        /// Checks, if the specified type is a nullable
        /// </summary>
        public static bool IsNullableType(Type type)
        {
            return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        /// <summary>
        /// Checks, if the specified type is a nullable
        /// </summary>
        public static bool IsNullableType(Type type, out Type itemType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                itemType = type.GetGenericArguments()[0];
                return true;
            }

            itemType = null;
            return false;
        }

        /// <summary>
        /// Checks, if the specified type is a nullable
        /// </summary>
        public static bool IsNullableType(Type type, ref int itemTypeCode)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                itemTypeCode = (int)Type.GetTypeCode(type.GetGenericArguments()[0]);
                return true;
            }

            return false;
        }


        public static bool IsGenericDictionary(Type type)
        {
            return
                type.EnumerateInterfaces().Where(@interface => @interface.IsGenericType)
                .Any(@interface => @interface.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        public static bool IsGenericEnumerable(Type type)
        {
            return 
                type.EnumerateInterfaces().Where(@interface => @interface.IsGenericType)
                .Any(@interface => @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        public static bool IsGenericEnumerable(Type type, out Type itemType)
        {
            Type enumerableType = type.EnumerateInterfaces()
                .Where(@interface => @interface.IsGenericType)
                .FirstOrDefault(@interface => @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            itemType = enumerableType?.GetGenericArguments()[0];
            return enumerableType != null;
        }


        public static bool IsGenericEnumerableOfItemType(Type type, Type itemType)
        {
            return type.EnumerateInterfaces()
                .Where(@interface => @interface.IsGenericType)
                .Any(@interface => @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    && @interface.GetGenericArguments()[0] == itemType);
        }


        public static bool IsGenericList(Type type)
        {
            return type.EnumerateInterfaces()
                .Where(@interface => @interface.IsGenericType)
                .Any(@interface => @interface.GetGenericTypeDefinition() == typeof(IList<>));
        }

        public static bool IsGenericSet(Type type)
        {
            return type.EnumerateInterfaces()
                .Where(@interface => @interface.IsGenericType)
                .Any(@interface => @interface.GetGenericTypeDefinition() == typeof(ISet<>));
        }

        private static IEnumerable<Type> EnumerateInterfaces(this Type type)
        {
            if (type.IsInterface)
                yield return type;

            foreach (var interfaceType in type.GetInterfaces())
                yield return interfaceType;
        }

        [CanBeNull]
        public static Tuple<MethodInfo, LExpression[]> GetMethodByArgumentValues(
            [NotNull, ItemNotNull] IEnumerable<MethodInfo> methods, [CanBeNull, ItemNotNull] LExpression[] arguments)
        {
            // No overload accepting the arguments' static types is "no method", not a crash: the caller
            // treats null as unresolved and reports the miss as a CompileErrorException.
            var result = GetMethodBaseByArgumentValues("method", methods, arguments);

            if (result == null)
                return null;

            return new Tuple<MethodInfo, LExpression[]>((MethodInfo)result.Item1, result.Item2);
        }

        [CanBeNull]
        public static Tuple<ConstructorInfo, LExpression[]> GetConstructorByArgumentValues(
            [NotNull, ItemNotNull] IEnumerable<ConstructorInfo> constructors, [CanBeNull, ItemNotNull] LExpression[] arguments)
        {
            var result = GetMethodBaseByArgumentValues("constructor", constructors, arguments);

            if (result == null)
                return null;

            return new Tuple<ConstructorInfo, LExpression[]>((ConstructorInfo)result.Item1, result.Item2);
        }

        [CanBeNull]
        private static Tuple<MethodBase, LExpression[]> GetMethodBaseByArgumentValues<T>(
            [NotNull] string baseMethodNameForExceptionText,
            [NotNull, ItemNotNull] IEnumerable<T> methods,
            [CanBeNull, ItemNotNull] LExpression[] arguments) where T : MethodBase
        {
            List<Tuple<MethodBase, LExpression[]>> matches = null;
            List<Type[]> matchParameterSets = null;
            List<Tuple<MethodBase, LExpression[]>> omittedOptionalsMatches = null;
            List<Tuple<MethodBase, LExpression[]>> expandedMatches = null;

            var allArguments = arguments ?? new LExpression[0];

            foreach (T m in methods)
            {
                ParameterInfo[] methodParameterInfoArray = m.GetParameters();
                bool isMatch = true;
                bool isExactMatch = true;
                bool isOmittedOptionalsMatch = false;
                bool isExpandedMatch = false;
                LExpression[] argumentsForCurrentMethod = allArguments;

                // The interpreter's twin of this decision is in ReflectionUtils: the arguments as
                // written first, then omitted defaults, then a built params array.
                //
                // Two catch clauses used to wrap this loop, for an InvalidCastException and an
                // InvalidOperationException the author could not place ("to dopisalem!!!! bo taki
                // wyjatek wyrzuca konstruowanie tablic ze zlymi typami"). They came from the params
                // packer building an array out of arguments it could not hold; the binder answers
                // that with NotApplicable instead of throwing, so nothing below raises them.
                switch (ArgumentBindingUtils.TryBind(
                    methodParameterInfoArray, allArguments, out var bound))
                {
                    case ArgumentBinding.Exact:
                        argumentsForCurrentMethod = bound;
                        break;
                    case ArgumentBinding.WithOmittedOptionals:
                        argumentsForCurrentMethod = bound;
                        isOmittedOptionalsMatch = true;
                        break;
                    case ArgumentBinding.Expanded:
                        argumentsForCurrentMethod = bound;
                        isExpandedMatch = true;
                        break;
                    default:
                        // Undecidable as well as NotApplicable: a candidate this backend cannot bind
                        // is simply not among the ones it may choose from, and the interpreter serves
                        // the call if nothing else binds.
                        continue;
                }

                if (methodParameterInfoArray.Length != argumentsForCurrentMethod.Length)
                {
                    isMatch = false;
                }
                else
                {
                    for (int i = 0; i < methodParameterInfoArray.Length; i++)
                    {
                        var currentMethodParameter = methodParameterInfoArray[i].ParameterType;
                        var currentArgument = argumentsForCurrentMethod[i];

                        // todo: error: zrobić test na zwracanie null obiektu z metody czy wywołanie kolejnej się nie rozjebie!!!!! ------------------------------------------------------  --
                        var currentArgumentIsConstNull
                            = currentArgument is ConstantExpression constExpr && constExpr.Value == null;

                        if (currentArgumentIsConstNull
                            && currentMethodParameter.IsValueType
                            && !IsNullableType(currentMethodParameter))
                        {
                            // null argument but method parameter does not accept nulls!
                            isMatch = false;
                            break;
                        }

                        if (!currentArgumentIsConstNull
                            && !currentMethodParameter.IsAssignableFrom(currentArgument.Type))
                        {
                            // not null argument but cast not possible (incompatible type)
                            isMatch = false;
                            break;
                        }

                        if (currentArgumentIsConstNull
                            || currentMethodParameter != currentArgument.Type)
                        {
                            isExactMatch = false;
                        }
                    }
                }

                if (isMatch)
                {
                    // The tiers of ArgumentBinding, in C#'s order: a candidate that took the
                    // arguments as written outranks one that had to fill a default, which outranks
                    // one that had to build a params array.
                    if (isExpandedMatch)
                    {
                        if (expandedMatches == null)
                        {
                            expandedMatches = new List<Tuple<MethodBase, LExpression[]>>();
                        }

                        expandedMatches.Add(new Tuple<MethodBase, LExpression[]>(m, argumentsForCurrentMethod));
                        continue;
                    }

                    if (isOmittedOptionalsMatch)
                    {
                        if (omittedOptionalsMatches == null)
                        {
                            omittedOptionalsMatches = new List<Tuple<MethodBase, LExpression[]>>();
                        }

                        omittedOptionalsMatches.Add(new Tuple<MethodBase, LExpression[]>(m, argumentsForCurrentMethod));
                        continue;
                    }

                    if (isExactMatch)
                    {
                        return new Tuple<MethodBase, LExpression[]>(m, argumentsForCurrentMethod);
                    }

                    if (matches == null)
                    {
                        matches = new List<Tuple<MethodBase, LExpression[]>>();
                        matchParameterSets = new List<Type[]>();
                    }

                    matches.Add(new Tuple<MethodBase, LExpression[]>(m, argumentsForCurrentMethod));
                    matchParameterSets.Add(Array.ConvertAll(methodParameterInfoArray, p => p.ParameterType));
                }
            }

            if (matches == null)
            {
                var lowerTier = omittedOptionalsMatches ?? expandedMatches;

                if (lowerTier == null)
                {
                    return null;
                }

                if (lowerTier.Count == 1)
                {
                    return lowerTier[0];
                }

                // Betterness ranks neither tier - the omitted-optionals candidates have parameter
                // lists of different lengths, and expanded matches were never ranked - except that C#
                // prefers the candidate declaring fewest parameters among the former. Anything still
                // tied keeps the legacy ambiguity, reported here as a compile refusal, which is what
                // the interpreter's twin reports at evaluation.
                if (omittedOptionalsMatches != null)
                {
                    Tuple<MethodBase, LExpression[]> fewest = null;
                    var tied = false;

                    foreach (var candidate in omittedOptionalsMatches)
                    {
                        var count = candidate.Item1.GetParameters().Length;

                        if (fewest == null || count < fewest.Item1.GetParameters().Length)
                        {
                            fewest = candidate;
                            tied = false;
                        }
                        else if (count == fewest.Item1.GetParameters().Length)
                        {
                            tied = true;
                        }
                    }

                    if (!tied)
                    {
                        return fewest;
                    }
                }

                throw new CompileErrorException(
                    $"Ambiguous match for {baseMethodNameForExceptionText} '{lowerTier[0].Item1.Name}' for " +
                    $"the specified number and static types of arguments.");
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            // Ties break by C#'s betterness now, the same rule the interpreter's scan applies - a
            // null literal against Show(object)/Show(string) picks the string overload on both
            // backends, as C# would. Only genuinely incomparable sets still refuse. A tie discovered
            // while the tree is being BUILT is a compile refusal, not a runtime error: this used to
            // throw AmbiguousMatchException, which escapes the weak path's catch
            // (CompileErrorException) and turned a shape the interpreter serves into a hard failure
            // at construction.
            var best = SpringUtil.TypeCheckingUtils.IndexOfUniqueBestParameterSet(matchParameterSets);
            if (best >= 0)
            {
                return matches[best];
            }

            throw new CompileErrorException(
                $"Ambiguous match for {baseMethodNameForExceptionText} '{matches[0].Item1.Name}' for " +
                $"the specified number and static types of arguments.");
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using JetBrains.Annotations;

using SpringExpressions.Expressions.Compiling.Expressions;

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
            var anyExpandedMatch = false;

            foreach (T m in methods)
            {
                ParameterInfo[] methodParameterInfoArray = m.GetParameters();
                bool isMatch = true;
                bool isExactMatch = true;
                bool isExpandedMatch = false;
                LExpression[] argumentsForCurrentMethod = arguments ?? new LExpression[0];

                try
                {
                    if (methodParameterInfoArray.Length > 0)
                    {
                        var lastMethodParameter
                            = methodParameterInfoArray[methodParameterInfoArray.Length - 1];

                        var lastParameterHasParamArrayAttribute
                            = lastMethodParameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;

                        if (lastParameterHasParamArrayAttribute
                            && arguments.Length >= methodParameterInfoArray.Length)
                        {
                            argumentsForCurrentMethod = ConvertArgumentsForVariableParamsMethod(
                                arguments,
                                methodParameterInfoArray.Length,
                                lastMethodParameter.ParameterType.GetElementType());
                            isExpandedMatch = true;
                        }
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
                }
                    // todo: error: dlaczego InvalidCastException         !!!!! ??????? -------------------- !!!!!!! ??????? ---------------------------------------
                catch (InvalidCastException)
                {
                    isMatch = false;
                }
                   // todo: error: to dopisałem!!!! bo taki wyjątek wyrzuca konstruowanie tablic ze złymi typami... ale czy to jest ok?
                catch (InvalidOperationException)
                {
                    isMatch = false;
                }

                if (isMatch)
                {
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
                    anyExpandedMatch = anyExpandedMatch || isExpandedMatch;
                }
            }

            if (matches == null)
            {
                return null;
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            // Ties break by C#'s betterness now, the same rule the interpreter's scan applies - a
            // null literal against Show(object)/Show(string) picks the string overload on both
            // backends, as C# would. Only genuinely incomparable sets - or params-expanded matches,
            // which betterness does not rank - still refuse. A tie discovered while the tree is being
            // BUILT is a compile refusal, not a runtime error: this used to throw
            // AmbiguousMatchException, which escapes the weak path's catch (CompileErrorException)
            // and turned a shape the interpreter serves into a hard failure at construction.
            if (!anyExpandedMatch)
            {
                var best = SpringUtil.TypeCheckingUtils.IndexOfUniqueBestParameterSet(matchParameterSets);
                if (best >= 0)
                {
                    return matches[best];
                }
            }

            throw new CompileErrorException(
                $"Ambiguous match for {baseMethodNameForExceptionText} '{matches[0].Item1.Name}' for " +
                $"the specified number and static types of arguments.");
        }

        /// <summary>
        /// Packages arguments into argument list containing parameter array as a last argument.
        /// </summary>
        public static LExpression[] ConvertArgumentsForVariableParamsMethod(
            LExpression[] arguments, 
            int variableParamsMethodArgumentCount, 
            Type variableParamsArrayItemType)
        {
            LExpression[] result = new LExpression[variableParamsMethodArgumentCount];
            int i = 0;

            // copy regular arguments
            while (i < variableParamsMethodArgumentCount - 1)
            {
                result[i] = arguments[i];
                i++;
            }

            // package param array into last argument
            var variableParameters = new List<LExpression>();


                      // todo: error: dupa blada bo tutaj jak typy nie pasują, to się wyjebie!--------------------------
                      // todo: error: nulls! type conversion!!! ---------------  -------------------------------------------------------------------------------

                 // todo: error: uspójnić kod budowania strongly-types list!!!!
            while (i < arguments.Length)
            {
                var currentArg = arguments[i++];


                if (currentArg is ConstantExpression constExpression
                    && constExpression.Value == null)
                {
                    currentArg = LExpression.Constant(null, variableParamsArrayItemType);
                }

                variableParameters.Add(currentArg);
            }

            result[result.Length - 1] = LExpression.NewArrayInit(variableParamsArrayItemType, variableParameters); ;

            

            return result;
        }

    }
}

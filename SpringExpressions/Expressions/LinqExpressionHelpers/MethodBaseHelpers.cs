using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

        public static Tuple<MethodInfo, LExpression[]> GetMethodByArgumentValues(
            IEnumerable<MethodInfo> methods, LExpression[] arguments)
        {
            var result = GetMethodBaseByArgumentValues("method", methods, arguments);
            return new Tuple<MethodInfo, LExpression[]>((MethodInfo)result.Item1, result.Item2);
        }

        private static Tuple<MethodBase, LExpression[]> GetMethodBaseByArgumentValues<T>(
            string baseMethodNameForExceptionText, 
            IEnumerable<T> methods,
            LExpression[] arguments) where T : MethodBase
        {
            Tuple<MethodBase, LExpression[]> match = null;
            int matchCount = 0;

            foreach (T m in methods)
            {
                ParameterInfo[] methodParameterInfoArray = m.GetParameters();
                bool isMatch = true;
                bool isExactMatch = true;
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

                    matchCount++;
                    if (matchCount == 1)
                    {
                        match = new Tuple<MethodBase, LExpression[]>(m, argumentsForCurrentMethod); 
                    }
                    else
                    {
                        throw new AmbiguousMatchException(
                            $"Ambiguous match for {baseMethodNameForExceptionText} '{m.Name}' for " +
                            $"the specified number and types of arguments.");
                    }
                }
            }

            return match;
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

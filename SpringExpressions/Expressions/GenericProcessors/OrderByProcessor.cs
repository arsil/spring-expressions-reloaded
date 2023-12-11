using System;
using System.Collections.Generic;
using System.Reflection;


namespace SpringExpressions.Expressions.GenericProcessors
{
    internal class OrderByProcessor : IGenericProcessor
    {
        public bool TryGetMethodArguments(
            Type collectionType, Type itemType, List<Type> argumentTypes, out MethodInfo methodInfo)
        {
            if (argumentTypes.Count == 2)
            {
                if (argumentTypes[1].IsGenericType && argumentTypes[1].GetGenericTypeDefinition() == typeof(Func<,,>))
                {
                    methodInfo = OrderByFuncGeneric.MakeGenericMethod(itemType);
                    return true;
                }
            }

            methodInfo = null;
            return false;
        }

        private static List<T> OrderByFunc<T>(IEnumerable<T> list, Func<T, T, int> comparision)
        {
            var result = new List<T>(list);
            result.Sort((a, b) => comparision(a, b));
            return result;
        }

        private static readonly MethodInfo OrderByFuncGeneric
            = typeof(OrderByProcessor).GetMethod(nameof(OrderByFunc), BindingFlags.Static | BindingFlags.NonPublic);
    }
}

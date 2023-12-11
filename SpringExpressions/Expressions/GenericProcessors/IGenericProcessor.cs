using System;
using System.Collections.Generic;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal interface IGenericProcessor
    {
        bool TryGetMethodArguments(
            Type collectionType, 
            Type itemType, 
            List<Type> argumentTypes, 
            out MethodInfo methodInfo);
    }
}
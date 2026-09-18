using System;
using System.Collections.Generic;
using System.Reflection;

namespace SpringExpressions.Expressions.GenericProcessors
{
    internal interface IGenericProcessor
    {
        /// <param name="node">
        /// The node being compiled, so that a processor rejecting an argument can refuse by name.
        /// A bad argument is the caller's mistake: it must be a CompileErrorException naming this node,
        /// never a raw throw out of the emit path, which BaseNode's absorber would report as our own
        /// defect with "please report it" attached.
        /// </param>
        bool TryGetMethodArguments(
            BaseNode node,
            Type collectionType, 
            Type itemType, 
            List<Type> argumentTypes, 
            out MethodInfo methodInfo);
    }
}
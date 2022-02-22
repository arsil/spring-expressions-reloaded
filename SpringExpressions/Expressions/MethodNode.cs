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
using System.Runtime.Serialization;
using SpringExpressions.Processors;
using SpringUtil;
using SpringReflection.Dynamic;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed method node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class MethodNode : NodeWithArguments
    {
        private const BindingFlags BINDING_FLAGS
            = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.IgnoreCase;

        private static readonly IDictionary collectionProcessorMap = new Hashtable();
        private static readonly IDictionary extensionMethodProcessorMap = new Hashtable();

        private bool initialized = false;
        private bool cachedIsParamArray = false;
        private Type paramArrayType;
        private int argumentCount;
        private SafeMethod cachedInstanceMethod;
        private int cachedInstanceMethodHash;

        /// <summary>
        /// Static constructor. Initializes a map of special collection processor methods.
        /// </summary>
        static MethodNode()
        {
            collectionProcessorMap.Add("count", new CountAggregator());
            collectionProcessorMap.Add("sum", new SumAggregator());
            collectionProcessorMap.Add("max", new MaxAggregator());
            collectionProcessorMap.Add("min", new MinAggregator());
            collectionProcessorMap.Add("average", new AverageAggregator());
            collectionProcessorMap.Add("sort", new SortProcessor());
            collectionProcessorMap.Add("orderBy", new OrderByProcessor());
            collectionProcessorMap.Add("distinct", new DistinctProcessor());
            collectionProcessorMap.Add("nonNull", new NonNullProcessor());
            collectionProcessorMap.Add("reverse", new ReverseProcessor());
            collectionProcessorMap.Add("convert", new ConversionProcessor());

            extensionMethodProcessorMap.Add("date", new DateConversionProcessor());
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public MethodNode()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected MethodNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Returns node's value for the given context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            string methodName = this.getText();
            object[] argValues = ResolveArguments(evalContext);
            ICollectionProcessor localCollectionProcessor = null;
            IMethodCallProcessor methodCallProcessor = null;

            // resolve method, if necessary
            lock (this)
            {
                // check if it is a collection and the methodname denotes a collection processor
                if ((context == null || context is ICollection))
                {
                    // predefined collection processor?
                    localCollectionProcessor = (ICollectionProcessor)collectionProcessorMap[methodName];

                    // user-defined collection processor?
                    if (localCollectionProcessor == null && evalContext.Variables != null)
                    {
                        object temp;
                        evalContext.Variables.TryGetValue(methodName, out temp);
                        localCollectionProcessor = temp as ICollectionProcessor;
                    }
                }

                // try extension methods
                methodCallProcessor = (IMethodCallProcessor)extensionMethodProcessorMap[methodName];
                {
                    // user-defined extension method processor?
                    if (methodCallProcessor == null && evalContext.Variables != null)
                    {
                        object temp;
                        evalContext.Variables.TryGetValue(methodName, out temp);
                        methodCallProcessor = temp as IMethodCallProcessor;
                    }
                }

                // try instance method
                if (context != null)
                {
                    // calculate checksum, if the cached method matches the current context
                    if (initialized)
                    {
                        int calculatedHash = CalculateMethodHash(context.GetType(), argValues);
                        initialized = (calculatedHash == cachedInstanceMethodHash);
                    }

                    if (!initialized)
                    {
                        Initialize(methodName, argValues, context);
                        initialized = true;
                    }
                }
            }

            if (localCollectionProcessor != null)
            {
                return localCollectionProcessor.Process((ICollection)context, argValues);
            }
            else if (methodCallProcessor != null)
            {
                return methodCallProcessor.Process(context, argValues);
            }
            else if (cachedInstanceMethod != null)
            {
                object[] paramValues = (cachedIsParamArray)
                                        ? ReflectionUtils.PackageParamArray(argValues, argumentCount, paramArrayType)
                                        : argValues;
                return cachedInstanceMethod.Invoke(context, paramValues);
            }
            else
            {
                throw new ArgumentException(string.Format("Method '{0}' with the specified number and types of arguments does not exist.", methodName));
            }
        }

        private int CalculateMethodHash(Type contextType, object[] argValues)
        {
            int hash = contextType.GetHashCode();
            for (int i = 0; i < argValues.Length; i++)
            {
                object arg = argValues[i];
                if (arg != null)
                    hash += s_primes[i] * arg.GetType().GetHashCode();
            }
            return hash;
        }

        private void Initialize(string methodName, object[] argValues, object context)
        {
            Type contextType = (context is Type ? context as Type : context.GetType());

            // check the context type first
            MethodInfo mi = GetBestMethod(contextType, methodName, BINDING_FLAGS, argValues);

            // if not found, probe the Type's type          
            if (mi == null)
            {
                mi = GetBestMethod(typeof(Type), methodName, BINDING_FLAGS, argValues);
            }

            if (mi == null)
            {
                return;
            }
            else
            {
                ParameterInfo[] parameters = mi.GetParameters();
                if (parameters.Length > 0)
                {
                    ParameterInfo lastParameter = parameters[parameters.Length - 1];
                    cachedIsParamArray = lastParameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;
                    if (cachedIsParamArray)
                    {
                        paramArrayType = lastParameter.ParameterType.GetElementType();
                        argumentCount = parameters.Length;
                    }
                }

                cachedInstanceMethod = new SafeMethod(mi);
                cachedInstanceMethodHash = CalculateMethodHash(contextType, argValues);
            }
        }

        /// <summary>
        /// Gets the best method given the name, argument values, for a given type.
        /// </summary>
        /// <param name="type">The type on which to search for the method.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="bindingFlags">The binding flags.</param>
        /// <param name="argValues">The arg values.</param>
        /// <returns>Best matching method or null if none found.</returns>
        public static MethodInfo GetBestMethod(Type type, string methodName, BindingFlags bindingFlags, object[] argValues)
        {
            MethodInfo mi = null;
            try
            {
                mi = type.GetMethod(methodName, bindingFlags | BindingFlags.FlattenHierarchy);
            }
            catch (AmbiguousMatchException)
            {

                IList<MethodInfo> overloads = GetCandidateMethods(type, methodName, bindingFlags, argValues.Length);
                if (overloads.Count > 0)
                {
                    mi = ReflectionUtils.GetMethodByArgumentValues(overloads, argValues);
                }
            }
            return mi;
        }



        private static IList<MethodInfo> GetCandidateMethods(Type type, string methodName, BindingFlags bindingFlags, int argCount)
        {
            MethodInfo[] methods = type.GetMethods(bindingFlags | BindingFlags.FlattenHierarchy);
            List<MethodInfo> matches = new List<MethodInfo>();

            foreach (MethodInfo method in methods)
            {
                if (method.Name == methodName)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == argCount)
                    {
                        matches.Add(method);
                    }
                    else if (parameters.Length > 0)
                    {
                        ParameterInfo lastParameter = parameters[parameters.Length - 1];
                        if (lastParameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
                        {
                            matches.Add(method);
                        }
                    }
                }
            }

            return matches;
        }

        // used to calculate signature hash while caring for arg positions
        private static readonly int[] s_primes =
            {
                17, 19, 23, 29
                , 31, 37, 41, 43, 47, 53, 59, 61, 67, 71
                , 73, 79, 83, 89, 97, 101, 103, 107, 109, 113
                , 127, 131, 137, 139, 149, 151, 157, 163, 167, 173
                , 179, 181, 191, 193, 197, 199, 211, 223, 227, 229
                , 233, 239, 241, 251, 257, 263, 269, 271, 277, 281
                , 283, 293, 307, 311, 313, 317, 331, 337, 347, 349
                , 353, 359, 367, 373, 379, 383, 389, 397, 401, 409
                , 419, 421, 431, 433, 439, 443, 449, 457, 461, 463
                , 467, 479, 487, 491, 499, 503, 509, 521, 523, 541
                , 547, 557, 563, 569, 571, 577, 587, 593, 599, 601
                , 607, 613, 617, 619, 631, 641, 643, 647, 653, 659
                , 661, 673, 677, 683, 691, 701, 709, 719, 727, 733
                , 739, 743, 751, 757, 761, 769, 773, 787, 797, 809
                , 811, 821, 823, 827, 829, 839, 853, 857, 859, 863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941
                , 947, 953, 967, 971, 977, 983, 991, 997, 1009, 1013, 1019, 1021, 1031, 1033, 1039, 1049, 1051, 1061, 1063, 1069
                , 1087, 1091, 1093, 1097, 1103, 1109, 1117, 1123, 1129, 1151, 1153, 1163, 1171, 1181, 1187, 1193, 1201, 1213, 1217, 1223
                , 1229, 1231, 1237, 1249, 1259, 1277, 1279, 1283, 1289, 1291, 1297, 1301, 1303, 1307, 1319, 1321, 1327, 1361, 1367, 1373
                , 1381, 1399, 1409, 1423, 1427, 1429, 1433, 1439, 1447, 1451, 1453, 1459, 1471, 1481, 1483, 1487, 1489, 1493, 1499, 1511
                , 1523, 1531, 1543, 1549, 1553, 1559, 1567, 1571, 1579, 1583, 1597, 1601, 1607, 1609, 1613, 1619, 1621, 1627, 1637, 1657
                , 1663, 1667, 1669, 1693, 1697, 1699, 1709, 1721, 1723, 1733, 1741, 1747, 1753, 1759, 1777, 1783, 1787, 1789, 1801, 1811
                , 1823, 1831, 1847, 1861, 1867, 1871, 1873, 1877, 1879, 1889, 1901, 1907, 1913, 1931, 1933, 1949, 1951, 1973, 1979, 1987
                , 1993, 1997, 1999, 2003, 2011, 2017, 2027, 2029, 2039, 2053, 2063, 2069, 2081, 2083, 2087, 2089, 2099, 2111, 2113, 2129
                , 2131, 2137, 2141, 2143, 2153, 2161, 2179, 2203, 2207, 2213, 2221, 2237, 2239, 2243, 2251, 2267, 2269, 2273, 2281, 2287
                , 2293, 2297, 2309, 2311, 2333, 2339, 2341, 2347, 2351, 2357, 2371, 2377, 2381, 2383, 2389, 2393, 2399, 2411, 2417, 2423
            };
    }
}

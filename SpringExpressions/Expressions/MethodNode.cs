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
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using SpringExpressions.Expressions.GenericProcessors;
using SpringExpressions.Processors;
using SpringUtil;
using SpringReflection.Dynamic;

using LExpression = System.Linq.Expressions.Expression;

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

	    protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression, LExpression evalContext)
	    {
// todo: byæ mo¿e trzeba to lockowaæ!
			string methodName = getText();

		    var instance = contextExpression;

			var node = this.getFirstChild();
			var arguments = new List<LExpression>();
		    var argumentsTypes = new List<Type>();

			while (node != null)
			{
				//if (node.getFirstChild() is LambdaExpressionNode)
				//{
				//	argList.Add((BaseNode)node.getFirstChild());
				//}
				//else if (node is NamedArgumentNode)
				//{
				//	namedArgs.Add(node.getText(), node);
				//}
				//else

				var arg = GetExpressionTreeIfPossible((BaseNode) node, contextExpression, evalContext);
				if (arg == null)
					return null;

				arguments.Add(arg);
				argumentsTypes.Add(arg.Type);

				node = node.getNextSibling();
			}

		    if (typeof(IEnumerable<decimal>).IsAssignableFrom(instance.Type))
		    {
				// this is a strongly typed collection <decimal>

				var decProcMethodInfo = typeof(DecimalProcessor).GetMethod(methodName);
				if (decProcMethodInfo != null)
			    {
				    var result = LExpression.Call(decProcMethodInfo, contextExpression);
				    return result;
			    }
		    }
			else if (typeof(IEnumerable<int>).IsAssignableFrom(instance.Type))
			{
				// this is a strongly typed collection <int>

				var decProcMethodInfo = typeof(IntProcessor).GetMethod(methodName);
				if (decProcMethodInfo != null)
				{
					var result = LExpression.Call(decProcMethodInfo, contextExpression);
					return result;
				}
			}

			// todo: obs³ugiwaæ inne typy?
			// todo: statyczne metody! -------------- statyczne metody! -----------------------------------------------------------------------------------------------
			   // todo: teoretycznie statyczne metody dzia³aj¹:)

		    MethodInfo methodInfo = null;


			var contextExpressionType = contextExpression.Type;

			if (contextExpressionType == typeof(Type)
				&& contextExpression.NodeType == ExpressionType.Constant)
			{
				// System.Type or underlaying type (e.g. Int32)
				contextExpressionType = (Type)((ConstantExpression)contextExpression).Value;
				instance = null;

				// try inner type (e.g. Int32)
				methodInfo = contextExpressionType.GetMethod(methodName,
					BINDING_FLAGS | BindingFlags.FlattenHierarchy,
					null, argumentsTypes.ToArray(), null);

				if (methodInfo == null)
				{
					// not found - going back to System.Type
					contextExpressionType = contextExpression.Type;
					instance = contextExpression;
				}

			}

			   // todo: statyczne metody! tej!
			   // todO: teoretycznie dzia³aj¹... 

			// todo: to co nie dzia³a, to null-owe parametry (np. ToString('dummy', null))...
			// todo: bo null jest u nas zawsze typu Object, a metoda oczekuje np. IFormatProvider!
			// todo: musielibyœmy typy weryfikowaæ i null-e zawsze castowaæ na poprawny typ!
			// todo: czyli wyszukiwaæ metod po liczbie paramametrów i odpowiednio odsiewaæ po
			// todo: znanych typach argumentow (nie-nullowych)!

		    if (methodInfo == null)
		    {
			    methodInfo
				    = contextExpressionType.GetMethod(
					    methodName,
					    BINDING_FLAGS | BindingFlags.FlattenHierarchy,
					    null,
					    argumentsTypes.ToArray(),
					    null);

		    }

		    if (methodInfo == null && methodName == "date")
		    {
				// common date() method...
			    if (arguments.Count == 1)
			    {
				    methodInfo = dateTimeParseMi;
			    }
			    else if (arguments.Count == 2)
			    {
				    methodInfo = dateTimeParseExactMi;
					arguments.Add(LExpression.Constant(
						CultureInfo.InvariantCulture, typeof(CultureInfo)));
			    }

			    // static method
			    instance = null;
		    }

		    if (methodInfo == null)
			    return null;

			return LExpression.Call(instance, methodInfo, arguments);
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
            };

	    private static MethodInfo dateTimeParseMi = typeof(DateTime)
			.GetMethod("Parse",
		    BINDING_FLAGS,
		    null,
		    new [] {typeof(string)},
		    null);

		private static MethodInfo dateTimeParseExactMi = typeof(DateTime)
			.GetMethod("ParseExact",
			BINDING_FLAGS,
			null,
			new[] { typeof(string), typeof(string), typeof(CultureInfo) },
			null);
	}
}

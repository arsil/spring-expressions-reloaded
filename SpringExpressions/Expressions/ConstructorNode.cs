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

using JetBrains.Annotations;

using SpringCore.TypeResolution;
using SpringExpressions.Expressions.Compiling.Expressions;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using SpringUtil;
using SpringReflection.Dynamic;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed method node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class ConstructorNode : NodeWithArguments
    {
        private SafeConstructor constructor;
        private IDictionary namedArgs;
        private bool isParamArray = false;
        private Type paramArrayType;
        private int argumentCount;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public ConstructorNode()
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public ConstructorNode(Type type)
            :base(type.FullName)
        {
        }

        
        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var arguments = new List<LExpression>();
            var argumentsTypes = new List<Type>();

            var node = getFirstChild();

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

                var arg = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);

                arguments.Add(arg);
                argumentsTypes.Add(arg.Type);

                node = node.getNextSibling();
            }

            Type objectType;
            try
            {
                objectType = GetObjectType(getText().Trim());
            }
            catch (TypeLoadException)
            {
                // ResolveType throws rather than returning null. While a tree is being built that is
                // a compile refusal - letting the TypeLoadException escape would blind the weak
                // path's fallback - and the interpreter then reports the unresolvable type name at
                // evaluation, as upstream always did.
                throw CannotCompile("the type name does not resolve");
            }

            if (objectType == null)
                throw CannotCompile("no compiled constructor matching these arguments");

            // The same tiers and the same overload gate as MethodNode - see ResolveMethod there. The
            // old exact-type GetConstructor ran the DefaultBinder, whose AmbiguousMatchException
            // escaped compilation and whose primitive widening the interpreter never had.
            var resolved = ResolveConstructor(objectType, arguments, argumentsTypes.ToArray());

            if (resolved == null)
                throw CannotCompile("no compiled constructor matching these arguments");

            var finalArguments = new List<LExpression>(resolved.Item2);
            MethodNode.ConvertParameters(resolved.Item1, finalArguments);

            return LExpression.New(resolved.Item1, finalArguments);
        }

        /// <summary>
        /// Constructor resolution for the compiled backend, mirroring MethodNode.ResolveMethod tier
        /// for tier: a single candidate goes straight to the conversion gate; several candidates
        /// require statically determinate arguments (the overload gate), then run the legacy
        /// assignability scan and the widening tier - the same rules the interpreter resolves by, so
        /// a constructor call that compiles can only pick the constructor the interpreter would pick.
        /// </summary>
        [CanBeNull]
        private static Tuple<ConstructorInfo, LExpression[]> ResolveConstructor(
            [NotNull] Type objectType,
            [NotNull, ItemNotNull] List<LExpression> arguments,
            [NotNull, ItemNotNull] Type[] argumentTypes)
        {
            var candidates = GetCandidateConstructors(objectType, argumentTypes.Length);

            if (candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
                return Tuple.Create(candidates[0], arguments.ToArray());

            for (var position = 0; position < arguments.Count; position++)
            {
                if (MethodNode.IsStaticallyDeterminate(arguments[position], candidates, position, arguments.Count))
                    continue;

                throw new CompileErrorException(
                    $"Overload choice for the constructor of '{objectType.Name}' depends on the "
                    + $"runtime type of an argument statically typed '{arguments[position].Type}'; "
                    + "there is no compiled form - the interpreter chooses from the runtime values. "
                    + "Add a cast to pick an overload.");
            }

            var scanned = MethodBaseHelpers.GetConstructorByArgumentValues(candidates, arguments.ToArray());
            if (scanned != null)
                return scanned;

            var widened = MethodNode.ResolveByWidening(candidates, argumentTypes, out var ambiguous);
            if (ambiguous)
            {
                throw new CompileErrorException(
                    $"Ambiguous match for the constructor of '{objectType.Name}': the arguments "
                    + "convert implicitly to more than one overload and neither is better - C# "
                    + "refuses this call too. Add a cast to pick one.");
            }

            return widened != null ? Tuple.Create(widened, arguments.ToArray()) : null;
        }

        /// <summary>
        /// Creates new instance of the type defined by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object[] argValues = ResolveArguments(evalContext);
            IDictionary namedArgValues = ResolveNamedArguments(evalContext);

            if (constructor == null)
            {
                lock(this)
                {
                    if (constructor == null)
                    {
                        constructor = InitializeNode(argValues, namedArgValues);
                    }
                }
            }

            object[] paramValues = (isParamArray ? ReflectionUtils.PackageParamArray(argValues, argumentCount, paramArrayType) : argValues);
            object instance = constructor.Invoke(paramValues);
            if (namedArgValues != null)
            {
                SetNamedArguments(instance, namedArgValues);
            }
            
            return instance;
        }

        /// <summary>
        /// Determines the type of object that should be instantiated.
        /// </summary>
        /// <param name="typeName">
        /// The type name to resolve.
        /// </param>
        /// <returns>
        /// The type of object that should be instantiated.
        /// </returns>
        /// <exception cref="TypeLoadException">
        /// If the type cannot be resolved.
        /// </exception>
        protected virtual Type GetObjectType(string typeName)
        {
            return TypeResolutionUtils.ResolveType(typeName);
        }

        /// <summary>
        /// Initializes this node by caching necessary constructor and property info.
        /// </summary>
        /// <param name="argValues"></param>
        /// <param name="namedArgValues"></param>
        private SafeConstructor InitializeNode(object[] argValues, IDictionary namedArgValues)
        {
            SafeConstructor ctor = null;
            Type objectType = GetObjectType(this.getText().Trim());
                
            // cache constructor info
            ConstructorInfo ci = GetBestConstructor(objectType, argValues);
            if (ci == null)
            {
                throw new ArgumentException(
                    String.Format("Constructor for the type [{0}] with a specified " +
                                  "number and types of arguments does not exist.",
                                  objectType.FullName));
            }
            else 
            {
                ParameterInfo[] parameters = ci.GetParameters();
                if (parameters.Length > 0)
                {
                    ParameterInfo lastParameter = parameters[parameters.Length - 1];
                    isParamArray = lastParameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;
                    if (isParamArray)
                    {
                        paramArrayType = lastParameter.ParameterType.GetElementType();
                        argumentCount = parameters.Length;
                    }
                }
                ctor = new SafeConstructor(ci);
            }
                
            // cache named args info
            if (namedArgValues != null)
            {
                namedArgs = new Hashtable(namedArgValues.Count);
                foreach (string name in namedArgValues.Keys)
                {
                    this.namedArgs[name] = Expression.ParseProperty(name);
                }
            }

            return ctor;
        }

        /// <summary>
        /// Sets the named arguments (properties).
        /// </summary>
        /// <param name="instance">Instance to set property values on.</param>
        /// <param name="namedArgValues">Argument (property) name to value mappings.</param>
        private void SetNamedArguments(object instance, IDictionary namedArgValues)
        {
            foreach (string name in namedArgValues.Keys)
            {
                IExpression property = (IExpression) namedArgs[name];
                property.SetValue(instance, namedArgValues[name]);
            }
        }

        [CanBeNull]
        private static ConstructorInfo GetBestConstructor([NotNull] Type type, [NotNull, ItemCanBeNull] object[] argValues)
        {
            IList<ConstructorInfo> candidates = GetCandidateConstructors(type, argValues.Length);
            if (candidates.Count > 0)
            {
                var ci = ReflectionUtils.GetConstructorByArgumentValues(candidates, argValues);

                // The widening tier, as for methods: the legacy scan above knows assignability but
                // not numeric widening, so new Thing(45) against Thing(long) found nothing here since
                // upstream - while the compiled path's DefaultBinder widened and succeeded, a
                // succeeds-versus-throws divergence. Legacy picks never change (this runs only on
                // "no match"), the invoker's argument converter performs the conversion, and a tie is
                // reported the way this resolver has always reported ties - at evaluation.
                if (ci == null)
                {
                    ci = MethodNode.ResolveByWidening(
                        candidates,
                        Array.ConvertAll(argValues, v => v?.GetType()),
                        out var ambiguous);

                    if (ambiguous)
                    {
                        throw new AmbiguousMatchException(
                            $"Ambiguous match for the constructor of '{type.Name}': the argument "
                            + "values convert implicitly to more than one overload and neither is better.");
                    }
                }

                return ci;
            }
            return null;
        }

        private static IList<ConstructorInfo> GetCandidateConstructors(Type type, int argCount)
        {
            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            List<ConstructorInfo> matches = new List<ConstructorInfo>();

            foreach (ConstructorInfo ctor in ctors)
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length == argCount)
                {
                    matches.Add(ctor);
                }
                else if (parameters.Length > 0)
                {
                    ParameterInfo lastParameter = parameters[parameters.Length - 1];
                    if (lastParameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
                    {
                        matches.Add(ctor);
                    }
                }
            }

            return matches;
        }

    }
}

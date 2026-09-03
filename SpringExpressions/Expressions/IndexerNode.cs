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

using SpringCore;
using SpringUtil;
using SpringReflection.Dynamic;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed indexer node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class IndexerNode : NodeWithArguments
    {
        private const BindingFlags BINDING_FLAGS
            = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.IgnoreCase;

        private SafeProperty indexer;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public IndexerNode()
        {
        }

        
        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            if (TryGetArguments(contextExpression, compilationContext, 
                    out var arguments, out var argumentsTypes))
            {
                throw CannotCompile("no compiled indexer for this container and index type");
            }

               // TODO: error: może pobranie arraya? tylko trzeba przetestować, czy nie stracimy typu!.. .bo jak przez object, to syf!
	        if (contextExpression.Type.IsArray)
	        {
		        try
		        {
			        return LExpression.ArrayIndex(
				        contextExpression,
				        arguments);
		        }
		        catch (ArgumentException)
		        {
			        // A wrong index count or a non-int index throws while the tree is being built -
			        // a compile-time event the fallback cannot see. The interpreter reports its own
			        // InvalidPropertyException at evaluation, as upstream always did.
			        throw CannotCompile("the array index count or type does not match");
		        }
	        }

            // A key a generic dictionary does not hold reads as nothing, not as an exception - see
            // TryCreateGenericDictionaryRead. Before the accessor resolution below, which would emit
            // get_Item and take Dictionary's own throw.
            var dictionaryRead = TryCreateGenericDictionaryRead(contextExpression, arguments);
            if (dictionaryRead != null)
                return dictionaryRead;

	        var indexerPropertyName = GetIndexerPropertyName(contextExpression.Type);

            // An indexer's accessor is an ordinary method, so the compiled resolution is
            // MethodNode's - the same tiers, the same overload gate and the same betterness that
            // methods and constructors resolve by. The old exact-type GetMethod ran the
            // DefaultBinder, whose AmbiguousMatchException escaped compilation.
            var resolved =
                TryExactAccessorForNullLiterals(
                    contextExpression.Type, "get_" + indexerPropertyName, arguments, argumentsTypes)
                ?? MethodNode.ResolveMethod(
                    this,
                    contextExpression.Type,
                    "get_" + indexerPropertyName,
                    arguments,
                    argumentsTypes.ToArray());

            if (resolved == null)
                throw CannotCompile("no compiled indexer for this container and index type");

            var finalArguments = new List<LExpression>(resolved.Item2);
            MethodNode.ConvertParameters(this, resolved.Item1, finalArguments);

            return LExpression.Call(contextExpression, resolved.Item1, finalArguments);
        }

        /// <summary>
        /// A read from a generic dictionary, answering nothing for a key it does not hold, or null when
        /// the shape does not qualify.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>The interpreter has always answered null for a missing key, and this is what it takes for
        /// the compiled path to say the same thing.</b> Not a decision of the interpreter's, either:
        /// <c>Get</c> dispatches on <c>context is IDictionary</c> - the <i>non-generic</i> interface,
        /// which a <c>Dictionary&lt;K, V&gt;</c> also implements - and that indexer is
        /// <c>object this[object]</c>, which returns null for a missing key where
        /// <c>IDictionary&lt;K, V&gt;</c>'s throws. So it read every dictionary through the pre-generics
        /// interface and got <c>Hashtable</c> behaviour for free, while the compiled path emitted
        /// <c>get_Item</c> and took <see cref="KeyNotFoundException"/>. The last row of the evaluation
        /// sweep.
        /// </p>
        /// <p>
        /// <b>The result is <c>V?</c> where <c>V</c> cannot hold null</b>, which is the cost and it was
        /// measured rather than assumed. An <c>int?</c> behaves as an <c>int</c> everywhere it matters:
        /// arithmetic, comparison, equality and member access all keep their compiled forms and still
        /// answer <c>Int32</c>, and an <c>int?</c> argument still binds to an <c>int</c> parameter (this
        /// engine is more permissive than C#, which needs the cast). What is lost is one thing - a
        /// <i>non-nullable typed request</i> over the read, <c>ParseGetter&lt;Root, int&gt;("Map['a']")</c>,
        /// which the nullable-request ruling refuses, so it is interpreted instead. The escapes are that
        /// ruling's own: ask for <c>int?</c>, or write <c>Map['a'] as int</c>.
        /// </p>
        /// <p>
        /// <b>What deliberately does not come here.</b> A non-generic <c>Hashtable</c> has no
        /// <c>IDictionary&lt;K, V&gt;</c>, so it keeps the accessor path - and needs nothing, since its
        /// own indexer already answers null. A null index literal keeps the legacy exact-match quirk
        /// (<see cref="TryExactAccessorForNullLiterals"/>) rather than being handed to
        /// <c>TryGetValue</c>, which would throw <see cref="ArgumentNullException"/> for a
        /// <c>Dictionary</c>. And the setter is untouched: writing a key is not this question.
        /// </p>
        /// </remarks>
        [CanBeNull]
        private static LExpression TryCreateGenericDictionaryRead(
            [NotNull] LExpression contextExpression, [NotNull] List<LExpression> arguments)
        {
            if (arguments.Count != 1)
                return null;

            var key = arguments[0];

            // the null-index quirk stays with the accessor path
            if (key is System.Linq.Expressions.ConstantExpression constant && constant.Value == null)
                return null;

            if (!CollectionOperandUtils.TryGetGenericDictionaryTypes(
                    contextExpression.Type, out var keyType, out var valueType))
            {
                return null;
            }

            if (!keyType.IsAssignableFrom(key.Type))
                return null;

            var tryGetValue = typeof(IDictionary<,>)
                .MakeGenericType(keyType, valueType)
                .GetMethod("TryGetValue");

            if (tryGetValue == null)
                return null;

            // V? where V is a non-nullable value type, V itself where it can already hold nothing
            var resultType =
                valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null
                    ? typeof(Nullable<>).MakeGenericType(valueType)
                    : valueType;

            var found = LExpression.Variable(valueType, "found");

            return LExpression.Block(
                new[] { found },
                LExpression.Condition(
                    LExpression.Call(contextExpression, tryGetValue, key, found),
                    resultType == valueType
                        ? (LExpression)found
                        : LExpression.Convert(found, resultType),
                    LExpression.Constant(null, resultType)));
        }

        protected override LExpression GetExpressionTreeForSetterIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext,
            LExpression newValueExpression)
        {
            if (TryGetArguments(contextExpression, compilationContext,
                    out var arguments, out var argumentsTypes))
            {
                throw CannotCompile("no compiled indexer for this container and index type");
            }

                // TODO: error: ??? nie rozumiem komentarza:) może pobranie arraya? tylko trzeba przetestować, czy nie stracimy typu!.. .bo jak przez object, to syf!
            if (contextExpression.Type.IsArray)
            {
                try
                {
                    return BuildAssign(
                        LExpression.ArrayIndex(contextExpression, arguments),
                        newValueExpression);
                }
                catch (ArgumentException)
                {
                    // Same compile-time event as the getter's array branch: a wrong index count or a
                    // non-int index must refuse, not leak.
                    throw CannotCompile("the array index count or type does not match");
                }
            }

            var indexerPropertyName = GetIndexerPropertyName(contextExpression.Type);

            arguments.Add(newValueExpression);
            argumentsTypes.Add(newValueExpression.Type);

            // The set accessor resolves like the get accessor - MethodNode's tiers and gate - with
            // the new value taking part in the signature as its last argument.
            var resolved =
                TryExactAccessorForNullLiterals(
                    contextExpression.Type, "set_" + indexerPropertyName, arguments, argumentsTypes)
                ?? MethodNode.ResolveMethod(
                    this,
                    contextExpression.Type,
                    "set_" + indexerPropertyName,
                    arguments,
                    argumentsTypes.ToArray());

            if (resolved == null)
                throw CannotCompile("no compiled indexer for this container and index type");

            var finalArguments = new List<LExpression>(resolved.Item2);
            MethodNode.ConvertParameters(this, resolved.Item1, finalArguments);

            return LExpression.Call(contextExpression, resolved.Item1, finalArguments);
        }

        /// <summary>
        /// The interpreter's indexer lookup has a tier the method lookup does not: an exact
        /// GetProperty over the value types with nulls mapped to typeof(object)
        /// (ReflectionUtils.GetTypes). A null index therefore picks this[object] there - not the
        /// betterness winner the candidate scan would choose - so when null literals are present and
        /// every other argument's static type is its exact runtime type, this backend replays that
        /// tier first, or the two would disagree on '[null]'. Legacy behaviour preserved verbatim;
        /// anything this pre-pass does not match falls through to the shared tiers.
        /// </summary>
        [CanBeNull]
        private static Tuple<MethodInfo, LExpression[]> TryExactAccessorForNullLiterals(
            [NotNull] Type contextType,
            [NotNull] string accessorName,
            [NotNull, ItemNotNull] List<LExpression> arguments,
            [NotNull, ItemNotNull] List<Type> argumentsTypes)
        {
            var anyNullLiteral = false;
            foreach (var argument in arguments)
            {
                if (argument is System.Linq.Expressions.ConstantExpression constant && constant.Value == null)
                    anyNullLiteral = true;
            }

            if (!anyNullLiteral)
                return null;

            foreach (var candidate in MethodNode.GetCompiledCandidateMethods(contextType, accessorName, arguments.Count))
            {
                var parameters = candidate.GetParameters();
                if (parameters.Length != arguments.Count)
                    continue;

                var exact = true;
                for (var i = 0; i < parameters.Length && exact; i++)
                    exact = parameters[i].ParameterType == argumentsTypes[i];

                if (exact)
                    return Tuple.Create(candidate, arguments.ToArray());
            }

            return null;
        }

        private bool TryGetArguments(
            LExpression contextExpression, 
            CompilationContext compilationContext, 
            out List<LExpression> arguments,
            out List<Type> argumentsTypes)
        {
            arguments = new List<LExpression>();
            argumentsTypes = new List<Type>();

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
                if (arg == null)
                    return true;

                arguments.Add(arg);
                argumentsTypes.Add(arg.Type);

                node = node.getNextSibling();
            }

            return false;
        }



        /// <summary>
        /// Returns node's value for the given context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            if (context == null)
            {
                throw new NullValueInNestedPathException("Cannot retrieve the value of the indexer because the context for its resolution is null.");
            }

            try
            {
                if (context is Array)
                {
                    return GetArrayValue( (Array) context, evalContext );
                }
                else if (context is IList)
                {
                    return GetListValue( (IList) context, evalContext );
                }
                else if (context is IDictionary)
                {
                    return GetDictionaryValue( (IDictionary) context, evalContext );
                }
                else if (context is string)
                {
                    return GetCharacter( (string) context, evalContext );
                }
                else
                {
                    return GetGenericIndexer( context, evalContext );
                }
            }
            catch (TargetInvocationException e)
            {
                throw new InvalidPropertyException(evalContext.RootContextType, this.ToString(), "Getter for indexer threw an exception.", e);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Illegal attempt to get value for the indexer.",e );
            }
            catch (IndexOutOfRangeException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Index out of range.",e );
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Argument out of range.",e );
            }
            catch (InvalidCastException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Invalid index type.",e );
            }
            catch (ArgumentException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Invalid argument.",e );
            }
        }

        /// <summary>
        /// Sets node's value for the given context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <param name="newValue">New value for this node.</param>
        protected override void Set(object context, EvaluationContext evalContext, object newValue)
        {
            if (context == null)
            {
                throw new NullValueInNestedPathException("Cannot set the value of the indexer because the context for its resolution is null.");
            }
            
            try
            {
                if (context is Array)
                {
                    SetArrayValue( (Array) context, evalContext,newValue );
                }
                else if (context is IList)
                {
                    SetListValue( (IList) context, evalContext,newValue );
                }
                else if (context is IDictionary)
                {
                    SetDictionaryValue( (IDictionary) context, evalContext,newValue );
                }
                else
                {
                    SetGenericIndexer( context, evalContext,newValue );
                }
            }
            catch (TargetInvocationException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Setter for indexer threw an exception.",e );
            }
            catch (UnauthorizedAccessException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Illegal attempt to set value for the indexer.",e );
            }
            catch (IndexOutOfRangeException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Index out of range.",e );
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Argument out of range.",e );
            }
            catch (InvalidCastException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Invalid index type.",e );
            }
            catch (ArgumentException e)
            {
                throw new InvalidPropertyException( evalContext.RootContextType,this.ToString(),"Invalid argument.",e );
            }
            
        }

        /// <summary>
        /// Utility method that is needed by ObjectWrapper and AbstractAutowireCapableObjectFactory.
        /// </summary>
        /// <param name="context">Context to resolve property against.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <returns>PropertyInfo for this node.</returns>
        internal PropertyInfo GetPropertyInfo(object context, IDictionary<string, object> variables)
        {
            lock (this)
            {
                EvaluationContext evalContext = new EvaluationContext(context, variables);
                InitializeIndexerProperty(context, evalContext);

                return indexer.PropertyInfo;
            }
        }

        private object GetArrayValue(Array array, EvaluationContext evalContext)
        {
            int argCount = array.Rank;
            AssertArgumentCount(argCount);

            Int32[] indices = new Int32[argCount];
            for (int i = 0; i < argCount; i++)
            {
                indices[i] = (Int32) ResolveArgument(i, evalContext);
            }
            return array.GetValue(indices);
        }

        private object GetListValue(IList list, EvaluationContext evalContext)
        {
            AssertArgumentCount(1);
            return list[(int) ResolveArgument(0, evalContext)];
        }

        private object GetDictionaryValue(IDictionary dictionary, EvaluationContext evalContext)
        {
            AssertArgumentCount(1);
            return dictionary[ResolveArgument( 0,evalContext )];
        }

        private object GetCharacter(string character, EvaluationContext evalContext)
        {
            AssertArgumentCount(1);
            return character[(int)ResolveArgument( 0,evalContext )];
        }

        private object GetGenericIndexer(object context, EvaluationContext evalContext)
        {
            object[] indices = InitializeIndexerProperty( context, evalContext );
            return indexer.GetValue(context, indices);
        }

        private void SetArrayValue(Array array, EvaluationContext evalContext,object newValue)
        {
            int argCount = array.Rank;
            AssertArgumentCount(argCount);

            Int32[] indices = new Int32[argCount];
            for (int i = 0; i < argCount; i++)
            {
                indices[i] = (Int32) ResolveArgument(i, evalContext);
            }
            array.SetValue(newValue, indices);
        }

        private void SetListValue(IList list, EvaluationContext evalContext,object newValue)
        {
            AssertArgumentCount(1);
            list[(int) ResolveArgument(0, evalContext)] = newValue;
        }

        private void SetDictionaryValue(IDictionary dictionary, EvaluationContext evalContext,object newValue)
        {
            AssertArgumentCount(1);
            dictionary[ResolveArgument( 0,evalContext )] = newValue;
        }

        private void SetGenericIndexer(object context, EvaluationContext evalContext,object newValue)
        {
            object[] indices = InitializeIndexerProperty( context, evalContext );
            indexer.SetValue( context, newValue, indices );
        }

        private object[] InitializeIndexerProperty(object context, EvaluationContext evalContext)
        {
            object[] indices = ResolveArguments( evalContext );

            if (indexer == null)
            {
                lock (this)
                {
                    if (indexer == null)
                    {
                        Type contextType = context.GetType();
                        var indexerProperty = GetIndexerPropertyInfo(contextType, indices);

                        indexer = new SafeProperty(indexerProperty);
                    }
                }
            }

            return indices;
        }

        [NotNull]
        private static PropertyInfo GetIndexerPropertyInfo([NotNull] Type contextType, [NotNull, ItemCanBeNull] object[] indices)
        {
            var defaultMember = GetIndexerPropertyName(contextType);

            // The legacy lookup first, preserved verbatim: whatever it resolved before, it still
            // resolves.
            PropertyInfo indexerProperty = contextType.GetProperty(defaultMember,
                BINDING_FLAGS,
                null,
                null,
                ReflectionUtils.GetTypes(indices),
                null);

            if (indexerProperty != null)
                return indexerProperty;

            // Then the same tiers methods and constructors resolve by: the assignability scan with
            // its betterness tie-break, and the widening tier where it finds nothing - so an
            // indexer taking long serves an int index on this backend exactly as it does compiled,
            // the invoker's argument converter performing the conversion.
            var candidateProperties = new List<PropertyInfo>();
            var candidateGetters = new List<MethodInfo>();

            foreach (var property in contextType.GetProperties(BINDING_FLAGS | BindingFlags.FlattenHierarchy))
            {
                if (!string.Equals(property.Name, defaultMember, StringComparison.OrdinalIgnoreCase))
                    continue;

                var getter = property.GetGetMethod(true);
                if (getter == null || getter.GetParameters().Length != indices.Length)
                    continue;

                candidateProperties.Add(property);
                candidateGetters.Add(getter);
            }

            if (candidateGetters.Count > 0)
            {
                var mi = ReflectionUtils.GetMethodByArgumentValues(candidateGetters, indices);

                if (mi == null)
                {
                    mi = MethodNode.ResolveByWidening(
                        candidateGetters,
                        Array.ConvertAll(indices, v => v?.GetType()),
                        out var ambiguous);

                    if (ambiguous)
                    {
                        throw new AmbiguousMatchException(
                            $"Ambiguous match for the indexer of '{contextType.Name}': the index "
                            + "values convert implicitly to more than one overload and neither is better.");
                    }
                }

                if (mi != null)
                    return candidateProperties[candidateGetters.IndexOf(mi)];
            }

            throw new ArgumentException(
                "Indexer property with specified number and types of arguments does not exist.");
        }

        private static string GetIndexerPropertyName(Type contextType)
        {
            string defaultMember = "Item";
            object[] atts = contextType.GetCustomAttributes(typeof(DefaultMemberAttribute), true);
            if (atts.Length > 0)
            {
                defaultMember = ((DefaultMemberAttribute) atts[0]).MemberName;
            }
            return defaultMember;
        }
    }
}
#region License

/*
 * Copyright 2002-2010 the original author or authors.
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

#region Imports

using System;
using System.Collections;
using System.Collections.Generic;

using System.ComponentModel;
using SpringUtil;
using System.Reflection;

#endregion

namespace SpringCore.TypeConversion
{
    /// <summary>
    /// Utility methods that are used to convert objects from one type into another.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class TypeConversionUtils
    {
        /// <summary>
        /// Convert the value to the required <see cref="System.Type"/> (if necessary from a string).
        /// </summary>
        /// <param name="newValue">The proposed change value.</param>
        /// <param name="requiredType">
        /// The <see cref="System.Type"/> we must convert to.
        /// </param>
        /// <param name="propertyName">Property name, used for error reporting purposes...</param>
        /// <exception cref="Spring.Objects.ObjectsException">
        /// If there is an internal error.
        /// </exception>
        /// <returns>The new value, possibly the result of type conversion.</returns>
        public static object ConvertValueIfNecessary(Type requiredType, object newValue, string propertyName)
        {
            if (newValue != null)
            {
                // if it is assignable, return the value right away
                if (IsAssignableFrom(newValue, requiredType))
                {
                    return newValue;
                }

                // A type's own implicit conversion operator comes before any conversion this class
                // performs - the ordering _Docs/open-issues.md item 12 ruled for operators, applied to
                // conversion. Without it a caller's type with `implicit operator decimal` could not be
                // assigned to a decimal member at all: this converter knows IConvertible and
                // TypeConverter, and has never heard of an operator.
                //
                // Measured across four custom types and fifteen targets, exactly two targets already
                // had an answer here: `object`, which the assignability test above has already taken,
                // and `string`, which every type reaches through ToString(). Everything else threw, so
                // consulting the operator first is *additive* everywhere except string - error space
                // becoming answers, with no existing answer to change.
                //
                // string is therefore excluded, deliberately. Honouring `implicit operator string`
                // would turn ToString()'s answer into the operator's, which is what C# does and is
                // very likely the better answer - but it is a change to inherited behaviour rather
                // than a new capability, and it wants deciding on its own rather than arriving as a
                // side effect of this one.
                if (requiredType != typeof(string)
                    && SpringUtil.TypeCheckingUtils.TryGetImplicitConversion(
                        newValue.GetType(), requiredType, out var implicitConversion))
                {
                    var converted = implicitConversion.Invoke(null, new[] { newValue });

                    // The operator may land short of the target - Money -> decimal for a double
                    // parameter - and C# allows that second, built-in step. Recursing rather than
                    // widening here keeps one rule for it.
                    return implicitConversion.ReturnType == requiredType
                        ? converted
                        : ConvertValueIfNecessary(requiredType, converted, propertyName);
                }

                // if required type is an array, convert all the elements
                if (requiredType != null && requiredType.IsArray)
                {
                    // convert individual elements to array elements
                    Type componentType = requiredType.GetElementType();
                    if (TryGetElements(newValue, out var arrayElements))
                    {
                        return ToArrayWithTypeConversion(componentType, arrayElements, propertyName);
                    }
                    else if (newValue is string)
                    {
                        if (requiredType.Equals(typeof(char[])))
                        {
                            return ((string)newValue).ToCharArray();
                        }
                        else
                        {
                            string[] elements = StringUtils.CommaDelimitedListToStringArray((string)newValue);
                            return ToArrayWithTypeConversion(componentType, elements, propertyName);
                        }
                    }
                    else if (!newValue.GetType().IsArray)
                    {
                        // A plain value: convert it to an array with a single component.
                        Array result = Array.CreateInstance(componentType, 1);
                        object val = ConvertValueIfNecessary(componentType, newValue, propertyName);
                        result.SetValue(val, 0);
                        return result;
                    }
                }
                // if required type is some ISet<T>, convert all the elements
                if (requiredType != null && requiredType.IsGenericType && TypeImplementsGenericInterface(requiredType, typeof(SpringCollections.Generic.ISet<>)))
                {
                    // convert individual elements to array elements
                    Type componentType = requiredType.GetGenericArguments()[0];
                    if (TryGetElements(newValue, out var elements))
                    {
                        return ToTypedCollectionWithTypeConversion(typeof(SpringCollections.Generic.HashedSet<>), componentType, elements, propertyName);
                    }
                }

                // if required type is a BCL ISet<T>, convert all the elements
                //
                // The branch above knows only the *vendored* set, which is all this library had when
                // it was written - so a consumer whose property is a HashSet<T> or an ISet<T> could be
                // assigned nothing but an already-built set: a list, an array, an ArrayList or a list
                // literal all threw InvalidCastException, having fallen through to the IEnumerable<T>
                // branch below and been built into a List<T>. Measured, all four.
                //
                // Assigning to a *vendored* set property has always worked from any of those, and
                // still does - the branch above is untouched. This is the same capability for the set
                // type the framework actually has, and it matters more since the fork's collection
                // operators began returning BCL sets. Before ISet<T> (.NET 4.0) there was nothing to
                // write here, which is why upstream did not.
                if (requiredType != null && requiredType.IsGenericType
                    && TypeImplementsGenericInterface(requiredType, typeof(ISet<>)))
                {
                    Type componentType = requiredType.GetGenericArguments()[0];
                    if (TryGetElements(newValue, out var setElements))
                    {
                        return ToTypedCollectionWithTypeConversion(
                            typeof(HashSet<>), componentType, setElements, propertyName);
                    }
                }

                // if required type is some IList<T>, convert all the elements
                if (requiredType != null && requiredType.IsGenericType && TypeImplementsGenericInterface(requiredType, typeof(IList<>)))
                {
                    // convert individual elements to array elements
                    Type componentType = requiredType.GetGenericArguments()[0];
                    if (TryGetElements(newValue, out var elements))
                    {
                        return ToTypedCollectionWithTypeConversion(typeof(List<>), componentType, elements, propertyName);
                    }
                }

                // if required type is some IDictionary<K,V>, convert all the elements
                if (requiredType != null && requiredType.IsGenericType && TypeImplementsGenericInterface(requiredType, typeof(IDictionary<,>)))
                {
                    Type[] typeParameters = requiredType.GetGenericArguments();
                    Type keyType = typeParameters[0];
                    Type valueType = typeParameters[1];
                    if (newValue is IDictionary)
                    {
                        IDictionary elements = (IDictionary)newValue;
                        Type targetCollectionType = typeof(Dictionary<,>);
                        Type collectionType = targetCollectionType.MakeGenericType(new Type[] { keyType, valueType });
                        object typedCollection = Activator.CreateInstance(collectionType);

                        MethodInfo addMethod = collectionType.GetMethod("Add", new Type[] { keyType, valueType });
                        int i = 0;
                        foreach (DictionaryEntry entry in elements)
                        {
                            string propertyExpr = BuildIndexedPropertyName(propertyName, i);
                            object key = ConvertValueIfNecessary(keyType, entry.Key, propertyExpr + ".Key");
                            object value = ConvertValueIfNecessary(valueType, entry.Value, propertyExpr + ".Value");
                            addMethod.Invoke(typedCollection, new object[] { key, value });
                            i++;
                        }
                        return typedCollection;
                    }
                }

                // if required type is some IEnumerable<T>, convert all the elements
                if (requiredType != null && requiredType.IsGenericType && TypeImplementsGenericInterface(requiredType, typeof(IEnumerable<>)))
                {
                    // convert individual elements to array elements
                    Type componentType = requiredType.GetGenericArguments()[0];
                    if (TryGetElements(newValue, out var elements))
                    {
                        return ToTypedCollectionWithTypeConversion(typeof(List<>), componentType, elements, propertyName);
                    }
                }

                // try to convert using type converter
                try
                {
                    TypeConverter typeConverter = TypeConverterRegistry.GetConverter(requiredType);
                    if (typeConverter != null && typeConverter.CanConvertFrom(newValue.GetType()))
                    {
                        try
                        {
                            newValue = typeConverter.ConvertFrom(newValue);
                        }
                        catch
                        {
                            if (newValue is string)
                            {
                                newValue = typeConverter.ConvertFromInvariantString((string)newValue);
                            }
                        }
                    }
                    else
                    {
                        typeConverter = TypeConverterRegistry.GetConverter(newValue.GetType());
                        if (typeConverter != null && typeConverter.CanConvertTo(requiredType))
                        {
                            newValue = typeConverter.ConvertTo(newValue, requiredType);
                        }
                        else
                        {
                            // look if it's an enum
                            if (requiredType != null
                                && requiredType.IsEnum
                                && (!(newValue is float)
                                    && (!(newValue is double))))
                            {
                                // convert numeric value into enum's underlying type
                                Type numericType = Enum.GetUnderlyingType(requiredType);
                                newValue = Convert.ChangeType(newValue, numericType);

                                if (Enum.IsDefined(requiredType, newValue))
                                {
                                    newValue = Enum.ToObject(requiredType, newValue);
                                }
                                else
                                {
                                    throw new TypeMismatchException(
                                        CreatePropertyChangeEventArgs(propertyName, null, newValue), requiredType);
                                }
                            }
                            else if (newValue is IConvertible)
                            {
                                // last resort - try ChangeType
                                newValue = Convert.ChangeType(newValue, requiredType);
                            }
                            else
                            {
                                throw new TypeMismatchException(
                                    CreatePropertyChangeEventArgs(propertyName, null, newValue), requiredType);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new TypeMismatchException(
                        CreatePropertyChangeEventArgs(propertyName, null, newValue), requiredType, ex);
                }
                if (newValue == null && (requiredType == null || !Type.GetType("System.Nullable`1").Equals(requiredType.GetGenericTypeDefinition())))
                {
                    throw new TypeMismatchException(
                        CreatePropertyChangeEventArgs(propertyName, null, newValue), requiredType);
                }
            }
            return newValue;
        }

        /// <summary>
        /// The items of <paramref name="value"/> when it is a collection of them, materialised so the
        /// builders below can count them.
        /// </summary>
        /// <remarks>
        /// <b>A BCL <c>HashSet&lt;T&gt;</c> is not a non-generic <see cref="ICollection"/></b>, and
        /// every branch here used to test for exactly that - so a set reaching this converter matched
        /// none of them. Measured before the fix: assigning a set to an <b>array</b> property fell
        /// through to the single-value case below and produced a <b>one-element array holding the
        /// set's <c>ToString()</c></b>, silently; assigning one to a <c>List&lt;T&gt;</c> property
        /// threw. It became reachable when the fork's collection operators began returning BCL sets
        /// instead of the vendored <c>HybridSet</c>, which <i>is</i> a non-generic
        /// <see cref="ICollection"/>.
        /// <p>
        /// The same blind spot, one layer up, is what made every collection processor refuse a
        /// <c>HashSet&lt;T&gt;</c> - see <c>ICollectionProcessor.Process</c>, widened from
        /// <see cref="ICollection"/> to <see cref="IEnumerable"/> for the identical reason. This is
        /// that ruling applied where the values are converted rather than processed.
        /// </p>
        /// <p>
        /// <b>A string is deliberately not a collection here.</b> It is enumerable, but the array
        /// branch has its own handling for one - a comma-delimited list, or <c>char[]</c> - and that
        /// has to keep winning.
        /// </p>
        /// </remarks>
        private static bool TryGetElements(object value, out ICollection elements)
        {
            elements = value as ICollection;

            if (elements != null)
                return true;

            if (value is string || !(value is IEnumerable enumerable))
                return false;

            var items = new List<object>();

            foreach (var item in enumerable)
                items.Add(item);

            elements = items;
            return true;
        }

        private static object ToArrayWithTypeConversion(Type componentType, ICollection elements, string propertyName)
        {
            Array destination = Array.CreateInstance(componentType, elements.Count);
            int i = 0;
            foreach (object element in elements)
            {
                object value = ConvertValueIfNecessary(componentType, element, BuildIndexedPropertyName(propertyName, i));
                destination.SetValue(value, i);
                i++;
            }
            return destination;
        }

        private static object ToTypedCollectionWithTypeConversion(Type targetCollectionType, Type componentType, ICollection elements, string propertyName)
        {
            if (!TypeImplementsGenericInterface(targetCollectionType, typeof(ICollection<>)))
            {
                throw new ArgumentException("argument must be a type that derives from ICollection<T>", "targetCollectionType");
            }


            Type collectionType = targetCollectionType.MakeGenericType(new Type[] { componentType });

            object typedCollection = Activator.CreateInstance(collectionType);

            int i = 0;
            foreach (object element in elements)
            {
                object value = ConvertValueIfNecessary(componentType, element, BuildIndexedPropertyName(propertyName, i));
                collectionType.GetMethod("Add").Invoke(typedCollection, new object[] { value });
                i++;
            }
            return typedCollection;
        }

        private static string BuildIndexedPropertyName(string propertyName, int index)
        {
            return (propertyName != null ?
                    propertyName + "[" + index + "]" :
                    null);
        }

        private static bool IsAssignableFrom(object newValue, Type requiredType)
        {
            if (newValue is MarshalByRefObject)
            {
                //TODO see what type of type checking can be done.  May need to 
                //preserve information when proxy was created by SaoServiceExporter.
                return true;
            }
            if (requiredType == null)
            {
                return false;
            }
            return requiredType.IsAssignableFrom(newValue.GetType());
        }

        /// <summary>
        /// Utility method to create a property change event.
        /// </summary>
        /// <param name="fullPropertyName">
        /// The full name of the property that has changed.
        /// </param>
        /// <param name="oldValue">The property old value</param>
        /// <param name="newValue">The property new value</param>
        /// <returns>
        /// A new <see cref="SpringCore.PropertyChangeEventArgs"/>.
        /// </returns>
        private static PropertyChangeEventArgs CreatePropertyChangeEventArgs(string fullPropertyName, object oldValue,
                                                                             object newValue)
        {
            return new PropertyChangeEventArgs(fullPropertyName, oldValue, newValue);
        }

        /// <summary>
        /// Determines if a Type implements a specific generic interface.
        /// </summary>
        /// <param name="candidateType">Candidate <see lang="Type"/> to evaluate.</param>
        /// <param name="matchingInterface">The <see lang="interface"/> to test for in the Candidate <see lang="Type"/>.</param>
        /// <returns><see lang="true" /> if a match, else <see lang="false"/></returns>
        private static bool TypeImplementsGenericInterface(Type candidateType, Type matchingInterface)
        {
            if (!matchingInterface.IsInterface)
            {
                throw new ArgumentException("matchingInterface Type must be an Interface Type", "matchingInterface");
            }

            if (candidateType.IsInterface && IsMatchingGenericInterface(candidateType, matchingInterface))
            {
                return true;
            }

            bool match = false;
            Type[] implementedInterfaces = candidateType.GetInterfaces();
            foreach (Type interfaceType in implementedInterfaces)
            {
                if (IsMatchingGenericInterface(interfaceType, matchingInterface))
                {
                    match = true;
                    break;
                }
            }

            return match;
        }

        private static bool IsMatchingGenericInterface(Type candidateInterfaceType, Type matchingGenericInterface)
        {
            return candidateInterfaceType.IsGenericType && candidateInterfaceType.GetGenericTypeDefinition() == matchingGenericInterface;
        }
    }
}

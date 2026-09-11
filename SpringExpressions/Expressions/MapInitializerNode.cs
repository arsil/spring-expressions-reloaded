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

using JetBrains.Annotations;

using SpringExpressions.Parser.antlr.collections;
using SpringUtil;

using LExpression = System.Linq.Expressions.Expression;


namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed map initializer node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class MapInitializerNode : BaseNode
    {
        /// <summary>
        /// Creates a new instance of <see cref="MapInitializerNode"/>.
        /// </summary>
        public MapInitializerNode()
        {}

                  protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
             CompilationContext compilationContext)
         {
             var node = getFirstChild();
             Type commonKeyType = null;
             Type commonValueType = null;
             List<LExpression> dictionaryEntries = new List<LExpression>();

             while (node != null)
             {
                 var item = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, compilationContext);
                 dictionaryEntries.Add(item);

                 if (!item.Type.IsGenericType
                     || item.Type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
                     throw CannotCompile("no compiled form for this map initializer");

                 // Keys and values unify independently, each to the entries' shared type or to object,
                 // so uniform keys survive mixed values into a Dictionary<K, object>, and the mirror
                 // case likewise - unifying whole pair types would collapse both components on any
                 // mismatch in either.
                 var entryTypes = item.Type.GetGenericArguments();

                 // A map literal keeps its entry types on both backends, or it is not compiled at all -
                 // the list literal's rule, asked of each component. The interpreter works the key and
                 // value types out from the entries' RUNTIME types (see Get), which reaches the same
                 // answer as the unification below exactly when neither component's static type can be
                 // narrowed by the value it holds. Where it can, the compiled path stands aside and the
                 // interpreter is the only backend that runs, so there is nothing to disagree with.
                 //
                 // Unlike the list literal, a null entry component is NOT exempted, and it cannot be:
                 // by the time an entry is a KeyValuePair<object, T> the difference between "a null was
                 // written here" and "this component is genuinely object-typed" is gone. Exempting
                 // object would therefore let '#{Anything : 1}' through, where the compiled path says
                 // object and the interpreter says int. So '#{null : 1}' is declined too - a
                 // conservative loss of one shape the two would in fact have agreed on, and the
                 // interpreter serves it.
                 foreach (var componentType in entryTypes)
                 {
                     if (TypeCheckingUtils.RuntimeCanNarrow(componentType))
                     {
                         throw CannotCompile(
                             $"an entry component is statically typed '{componentType}', which the "
                             + "value it holds can narrow at runtime, so the interpreter would infer "
                             + "different entry types for this map literal");
                     }
                 }

                 commonKeyType = commonKeyType == null || commonKeyType == entryTypes[0]
                     ? entryTypes[0]
                     : typeof(object);
                 commonValueType = commonValueType == null || commonValueType == entryTypes[1]
                     ? entryTypes[1]
                     : typeof(object);

                 node = node.getNextSibling();
             }

             if (commonKeyType == null)
                 throw CannotCompile("no compiled form for this map initializer");

             if (commonKeyType != typeof(object) || commonValueType != typeof(object))
             {
                 // strongly typed dictionary

                 var commonType = typeof(KeyValuePair<,>).MakeGenericType(commonKeyType, commonValueType);

                 // An entry whose pair type is narrower than the unified one is widened to it; each
                 // component's conversion is identity or boxing, nothing else.
                 for (var i = 0; i < dictionaryEntries.Count; i++)
                 {
                     if (dictionaryEntries[i].Type == commonType)
                         continue;

                     var entryTypes = dictionaryEntries[i].Type.GetGenericArguments();
                     var convertMi = GetType().GetMethod("ConvertEntry").MakeGenericMethod(
                         entryTypes[0], entryTypes[1], commonKeyType, commonValueType);

                     dictionaryEntries[i] = LExpression.Call(convertMi, dictionaryEntries[i]);
                 }

                 // todo: null check!
                 var mi = GetType().GetMethod("CreateStronglyTypedDictionary")
                     .MakeGenericMethod(commonKeyType, commonValueType);

                 // The dictionary this builds is the engine's own, so Compiler may reshape the root to
                 // the Dictionary<object, object> the interpreter produces; a dictionary merely read is
                 // the caller's and keeps its identity, and the registry is what tells the two apart.
                 var literal = LExpression.Call(mi,
                     LExpression.NewArrayInit(commonType, dictionaryEntries));

                 // Deliberately NOT registered - see ListInitializerNode. Registration exists so the
                 // boundary can reshape into the shape the interpreter would have built; here the
                 // interpreter builds this very shape, and reshaping would throw away the entry types
                 // both backends agreed on.
                 return literal;
             }
             else
             {
                 for (var i = 0; i < dictionaryEntries.Count; i++)
                 {
                     var mi = GetType().GetMethod("ToOldDictionaryEntry").MakeGenericMethod(dictionaryEntries[i].Type.GetGenericArguments());
                     dictionaryEntries[i] = LExpression.Call(mi, dictionaryEntries[i]);
                 }

                 var mi2 = GetType().GetMethod("CreateWeaklyTypedDictionary");

                 // Already the shape the interpreter builds, so there is nothing for the boundary to
                 // reconcile - registered all the same, like the object-typed list literal.
                 var literal = LExpression.Call(mi2,
                     LExpression.NewArrayInit(typeof(DictionaryEntry), dictionaryEntries));

                 // Deliberately NOT registered - see ListInitializerNode. Registration exists so the
                 // boundary can reshape into the shape the interpreter would have built; here the
                 // interpreter builds this very shape, and reshaping would throw away the entry types
                 // both backends agreed on.
                 return literal;
             }

            throw CannotCompile("no compiled form for this map initializer");
         }

         /// <summary>
         /// Creates new instance of the map defined by this node.
         /// </summary>
         /// <param name="context">Context to evaluate expressions against.</param>
         /// <param name="evalContext">Current expression evaluation context.</param>
         /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            // The entry types come from the entries' runtime types, keys and values unified
            // independently - the interpreter's version of what the compiled path does on static
            // types, and the compiled path declines the literal whenever the two could differ. A map
            // literal always has at least one entry, so unlike a computed collection there is never
            // nothing to work from.
            //
            // Dictionary<object, object> where a component has no single type - which is also what a
            // null in that position produces, since a null carries no type of its own.
            var keys = new List<object>();
            var values = new List<object>();
            Type commonKeyType = null;
            Type commonValueType = null;

            AST entryNode = this.getFirstChild();
            while (entryNode != null)
            {
                DictionaryEntry entry = (DictionaryEntry) GetValue(((MapEntryNode)entryNode), evalContext.RootContext, evalContext );

                keys.Add(entry.Key);
                values.Add(entry.Value);

                UnifyWith(entry.Key, ref commonKeyType);
                UnifyWith(entry.Value, ref commonValueType);

                entryNode = entryNode.getNextSibling();
            }

            IDictionary entries = commonKeyType == null || commonValueType == null
                ? new Dictionary<object, object>()
                : (IDictionary)Activator.CreateInstance(
                    typeof(Dictionary<,>).MakeGenericType(commonKeyType, commonValueType));

            for (var i = 0; i < keys.Count; i++)
                entries[keys[i]] = values[i];

            return entries;
        }

        /// <summary>
        /// Folds one component's runtime type into the type the whole column shares, or into object.
        /// </summary>
        /// <remarks>
        /// A null contributes nothing, exactly as it does compiled: the type comes from the other
        /// entries. A column of nothing but nulls leaves <paramref name="commonType"/> null, and the
        /// caller reads that as object - which is what the compiled path builds for it too.
        /// </remarks>
        private static void UnifyWith([CanBeNull] object value, [CanBeNull] ref Type commonType)
        {
            if (value == null)
                return;

            var valueType = value.GetType();

            if (commonType == null)
                commonType = valueType;
            else if (commonType != valueType)
                commonType = typeof(object);
        }

           // todo: koniecznie to zrobić w jakimś helperze!!!
         public static Dictionary<T, K> CreateStronglyTypedDictionary<T, K>(
            IEnumerable<KeyValuePair<T, K>> values)
         {
             var result = new Dictionary<T, K>();
             foreach (var kvp in values)
                result[kvp.Key] = kvp.Value;

             return result;
         }

         public static DictionaryEntry ToOldDictionaryEntry<T, K>(
             KeyValuePair<T, K> kvp) => new DictionaryEntry(kvp.Key, kvp.Value);

         public static KeyValuePair<TKeyTo, TValueTo> ConvertEntry<TKey, TValue, TKeyTo, TValueTo>(
             KeyValuePair<TKey, TValue> entry)
         {
             return new KeyValuePair<TKeyTo, TValueTo>(
                 (TKeyTo)(object)entry.Key, (TValueTo)(object)entry.Value);
         }

         public static Dictionary<object, object> CreateWeaklyTypedDictionary(
             IEnumerable<DictionaryEntry> values)
         {
             var result = new Dictionary<object, object>();
             foreach (var kvp in values)
                 result[kvp.Key] = kvp.Value;

             return result;
         }

/*
         // jak to skonwertować... żeby utworzyć dibionary>?... fuck!! dla drama!!!!
         private static IDictionary CreateWeaklyTypedDictionary(IEnumerable<KeyValuePair<,> dupa>)
         {
         }*/
    }
}

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
using SpringExpressions.Parser.antlr.collections;

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

                 compilationContext.MarkAsConstructedCollection(literal);
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

                 compilationContext.MarkAsConstructedCollection(literal);
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
            // Dictionary<object, object>, not Hashtable. The interpreter sees boxed keys and values and
            // has no types to work from, so object is all it can offer; the compiled path keeps the
            // entry types where they are uniform and the root is reprojected to match at the boundary.
            IDictionary entries = new Dictionary<object, object>();
            AST entryNode = this.getFirstChild();
            while (entryNode != null)
            {
                DictionaryEntry entry = (DictionaryEntry) GetValue(((MapEntryNode)entryNode), evalContext.RootContext, evalContext );
                entries[entry.Key] = entry.Value;
                entryNode = entryNode.getNextSibling();
            }

            return entries;
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

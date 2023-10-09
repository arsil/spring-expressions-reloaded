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
using System.Runtime.Serialization;
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;


namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed map initializer node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class MapInitializerNode : BaseNode
    {
        /// <summary>
        /// Creates a new instance of <see cref="MapInitializerNode"/>.
        /// </summary>
        public MapInitializerNode()
        {}

         /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected MapInitializerNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

         protected override LExpression GetExpressionTreeIfPossible(
             LExpression contextExpression,
             LExpression evalContext)
         {
             var node = getFirstChild();
             Type commonType = null;
             List<LExpression> dictionaryEntries = new List<LExpression>();

             while (node != null)
             {
                 var item = GetExpressionTreeIfPossible((BaseNode)node, contextExpression, evalContext);
                 dictionaryEntries.Add(item);

                 var dupa = item.Type.GetGenericTypeDefinition();
                 if (dupa != typeof(KeyValuePair<,>))
                     return null;

                 if (commonType == null)
                     commonType = item.Type;
                 else if (item.Type != commonType)
                     commonType = typeof(KeyValuePair<object, object>);

                 node = node.getNextSibling();
             }

             if (commonType == null)
                 return null;

             if (commonType != typeof(KeyValuePair<object, object>))
             {
                 // strongly typed dictionary

                 var kvpGenericArguments = commonType.GetGenericArguments();
/*                 var constructorArgType
                     = typeof(IEnumerable<>).MakeGenericType(
                         typeof(KeyValuePair<,>).MakeGenericType(kvpGenericArguments));
                 var dictionaryType = typeof(Dictionary<,>).MakeGenericType(kvpGenericArguments);
                 var constructor = dictionaryType.GetConstructor(new[] { constructorArgType });
*/
                 // todo: null check!
                 var mi = GetType().GetMethod("CreateStronglyTypedDictionary").MakeGenericMethod(kvpGenericArguments);
                 return LExpression.Call(mi,
                     LExpression.NewArrayInit(commonType, dictionaryEntries));
             }
             else
             {
                 for (var i = 0; i < dictionaryEntries.Count; i++)
                 {
                     var mi = GetType().GetMethod("ToOldDictionaryEntry").MakeGenericMethod(dictionaryEntries[i].Type.GetGenericArguments());
                     dictionaryEntries[i] = LExpression.Call(mi, dictionaryEntries[i]);
                 }

                 var mi2 = GetType().GetMethod("CreateWeaklyTypedDictionary");

                return LExpression.Call(mi2,
                     LExpression.NewArrayInit(typeof(DictionaryEntry), dictionaryEntries));
             }

            return null;
         }

         /// <summary>
         /// Creates new instance of the map defined by this node.
         /// </summary>
         /// <param name="context">Context to evaluate expressions against.</param>
         /// <param name="evalContext">Current expression evaluation context.</param>
         /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            IDictionary entries = new Hashtable();
            AST entryNode = this.getFirstChild();
            while (entryNode != null)
            {
                DictionaryEntry entry = (DictionaryEntry) GetValue(((MapEntryNode)entryNode), evalContext.RootContext, evalContext );
                entries[entry.Key] = entry.Value;
                entryNode = entryNode.getNextSibling();
            }

            return entries;
        }

           // todo: koniecznie to zrobiæ w jakimœ helperze!!!
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

         public static Hashtable CreateWeaklyTypedDictionary(
             IEnumerable<DictionaryEntry> values)
         {
             var result = new Hashtable();
             foreach (var kvp in values)
                 result[kvp.Key] = kvp.Value;

             return result;
         }

/*
         // jak to skonwertowaæ... ¿eby utworzyæ dibionary>?... fuck!! dla drama!!!!
         private static IDictionary CreateWeaklyTypedDictionary(IEnumerable<KeyValuePair<,> dupa>)
         {
         }*/
    }
}

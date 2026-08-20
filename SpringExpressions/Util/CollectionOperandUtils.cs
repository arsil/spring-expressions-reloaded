using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using SpringCollections;

namespace SpringUtil
{
    /// <summary>
    /// Builds the working sets that the '+', '*' and '-' operators use when applied to collections.
    /// </summary>
    /// <remarks>
    /// The result is a <see cref="HashSet{T}"/> of object rather than a SpringCollections.HybridSet. The
    /// interpreter sees boxed values and has no item type to work from, hence object; the point of the
    /// change is that the compiled path already produced a HashSet for some of the same expressions, so
    /// the two backends disagreed on the result type. Both now yield BCL collections, and the vendored
    /// pre-generics set is no longer part of any operator's result.
    /// </remarks>
    internal static class CollectionOperandUtils
    {
        /// <summary>
        /// Whether <paramref name="value"/> is a set of any kind. A null value is not.
        /// </summary>
        /// <remarks>
        /// The operators hold their operands as object and any of them may be null, so the null test lives
        /// here rather than at each call site.
        /// </remarks>
        internal static bool IsAnySet([CanBeNull] object value)
        {
            return value != null && IsAnySet(value.GetType());
        }

        /// <summary>
        /// Whether <paramref name="type"/> is a set of any kind: the vendored non-generic
        /// SpringCollections.ISet, or any <see cref="ISet{T}"/> whatever its item type.
        /// </summary>
        /// <remarks>
        /// The generic half goes through <see cref="GetSetItemType"/>, which needs reflection, because
        /// <see cref="ISet{T}"/> is invariant - it takes T in input positions, so it is not declared
        /// covariant and there is no HashSet&lt;int&gt; to ISet&lt;object&gt; conversion. A plain
        /// "is ISet&lt;object&gt;" would therefore match only sets whose item type is exactly object: enough
        /// for what the interpreter itself produces, but not for a HashSet&lt;int&gt; handed in by a caller,
        /// which is easy to come by now that the operators return HashSets.
        /// </remarks>
        internal static bool IsAnySet([NotNull] Type type)
        {
            return typeof(ISet).IsAssignableFrom(type) || GetSetItemType(type) != null;
        }

        /// <summary>
        /// The item type of a generic set, or null if <paramref name="type"/> is not one.
        /// </summary>
        /// <remarks>
        /// Reflection rather than an assignability test, for the same reason: <see cref="ISet{T}"/> is
        /// invariant, so ISet&lt;object&gt; is not assignable from HashSet&lt;int&gt; - nor from
        /// HashSet&lt;string&gt;, since covariance would not apply to a set even if it were declared.
        /// </remarks>
        [CanBeNull]
        internal static Type GetSetItemType([NotNull] Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ISet<>))
            {
                return type.GetGenericArguments()[0];
            }

            var setInterface = type.GetInterfaces().FirstOrDefault(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>));

            return setInterface == null ? null : setInterface.GetGenericArguments()[0];
        }

        /// <summary>
        /// The item type of a <see cref="List{T}"/>, or of anything deriving from one, or null.
        /// </summary>
        /// <remarks>
        /// Walks the base chain rather than testing the generic definition for equality, so a caller's own
        /// List subclass counts too. Nodes that only want to call an inherited member such as ToArray or the
        /// indexer should ask this rather than compare definitions, which is needlessly strict.
        /// </remarks>
        [CanBeNull]
        internal static Type GetListItemType([NotNull] Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return current.GetGenericArguments()[0];
                }
            }

            return null;
        }

        /// <summary>
        /// A plain <see cref="List{T}"/> holding the same elements in the same order.
        /// </summary>
        /// <remarks>
        /// Used where a list the engine built is reshaped at the boundary to the item type the caller asked
        /// for. CompilationContext.MarkAsConstructedCollection is how "the engine built it" is known.
        /// </remarks>
        [NotNull]
        internal static List<T> ToTypedList<T>([NotNull] IEnumerable<T> elements)
        {
            return new List<T>(elements);
        }

        /// <summary>
        /// The elements of <paramref name="elements"/> as objects, in order and keeping duplicates.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="ToHashSetOfObjects"/> this must not deduplicate: it reprojects a list, where
        /// order and repeats are part of the value.
        /// </remarks>
        [NotNull]
        internal static List<object> ToListOfObjects([NotNull] IEnumerable elements)
        {
            var list = new List<object>();

            foreach (var element in elements)
                list.Add(element);

            return list;
        }

        /// <summary>
        /// A plain <see cref="HashSet{T}"/> holding the same elements.
        /// </summary>
        /// <remarks>
        /// Used where a set the engine built is reshaped at the boundary to the item type the caller asked
        /// for. CompilationContext.MarkAsConstructedCollection is how "the engine built it" is known.
        /// </remarks>
        [NotNull]
        internal static HashSet<T> ToTypedHashSet<T>([NotNull] IEnumerable<T> elements)
        {
            return new HashSet<T>(elements);
        }

        /// <summary>
        /// The distinct elements of <paramref name="elements"/>.
        /// </summary>
        [NotNull]
        internal static HashSet<object> ToHashSetOfObjects([NotNull] IEnumerable elements)
        {
            var set = new HashSet<object>();

            foreach (var element in elements)
                set.Add(element);

            return set;
        }

        /// <summary>
        /// The distinct keys of <paramref name="dictionary"/>.
        /// </summary>
        /// <remarks>
        /// Named apart from <see cref="ToHashSetOfObjects"/> rather than overloading it:
        /// <see cref="IDictionary"/> is itself an <see cref="IEnumerable"/>, so an overload pair would
        /// silently pick the enumerable one - and enumerate DictionaryEntry values instead of keys -
        /// whenever the static type at the call site happened to be the wider one.
        /// </remarks>
        [NotNull]
        internal static HashSet<object> KeysToHashSetOfObjects([NotNull] IDictionary dictionary)
        {
            return ToHashSetOfObjects(dictionary.Keys);
        }

        /// <summary>
        /// The key and value types of a <see cref="Dictionary{TKey,TValue}"/>, or of anything deriving
        /// from one; false if <paramref name="type"/> is neither.
        /// </summary>
        /// <remarks>
        /// Walks the base chain rather than testing the generic definition for equality, so a caller's
        /// own Dictionary subclass counts too - the same reasoning as <see cref="GetListItemType"/>.
        /// </remarks>
        internal static bool TryGetDictionaryItemTypes(
            [NotNull] Type type, out Type keyType, out Type valueType)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var arguments = current.GetGenericArguments();
                    keyType = arguments[0];
                    valueType = arguments[1];
                    return true;
                }
            }

            keyType = null;
            valueType = null;
            return false;
        }

        /// <summary>
        /// A plain <see cref="Dictionary{TKey,TValue}"/> holding the same entries.
        /// </summary>
        /// <remarks>
        /// Used where a dictionary the engine built is reshaped at the boundary to the key and value
        /// types the caller asked for. CompilationContext.MarkAsConstructedCollection is how "the engine
        /// built it" is known.
        /// </remarks>
        [NotNull]
        internal static Dictionary<TKey, TValue> ToTypedDictionary<TKey, TValue>(
            [NotNull] IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            var result = new Dictionary<TKey, TValue>();

            foreach (var entry in entries)
                result[entry.Key] = entry.Value;

            return result;
        }

        /// <summary>
        /// The entries of <paramref name="dictionary"/> with keys and values as objects.
        /// </summary>
        [NotNull]
        internal static Dictionary<object, object> ToDictionaryOfObjects([NotNull] IDictionary dictionary)
        {
            var result = new Dictionary<object, object>(dictionary.Count);

            foreach (DictionaryEntry entry in dictionary)
                result[entry.Key] = entry.Value;

            return result;
        }
    }
}

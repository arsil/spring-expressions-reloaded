using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using SpringCollections;

using LExpression = System.Linq.Expressions.Expression;

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
        /// The item type a source actually enumerates as - the T of the <see cref="IEnumerable{T}"/> it
        /// implements, or an array's element type - or null when it enumerates only untyped.
        /// </summary>
        /// <remarks>
        /// The projection and selection emitters used to take <c>GetGenericArguments()[0]</c> as the item
        /// type, which is right for a List&lt;T&gt; and wrong for anything whose first generic argument is
        /// not what it yields: a Dictionary&lt;K, V&gt; enumerates as KeyValuePair&lt;K, V&gt;, not as K, and
        /// a caller's own Cache&lt;TKey, TValue&gt; : IEnumerable&lt;TValue&gt; yields the value type. Asking
        /// the implemented interface answers all of them, and is what those emitters build their item
        /// parameter and their generic helper call from.
        /// <p>
        /// Ambiguity is refused rather than guessed: a type implementing IEnumerable&lt;T&gt; more than once
        /// (IEnumerable&lt;int&gt; and IEnumerable&lt;string&gt; both) has no single item type, so the emitters
        /// see null and refuse the shape - the interpreter, which reads runtime values, still serves it.
        /// </p>
        /// </remarks>
        [CanBeNull]
        internal static Type GetEnumerableItemType([NotNull] Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            var itemTypes = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(i => i.GetGenericArguments()[0])
                .Distinct()
                .ToList();

            return itemTypes.Count == 1 ? itemTypes[0] : null;
        }

        /// <summary>
        /// The number of items in <paramref name="source"/> without enumerating it, where the source can
        /// answer that. False means the caller has to count by walking.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The processors take <see cref="IEnumerable"/>, which has no Count, so this is where the O(1)
        /// answer comes back. Two interfaces are needed and neither is sufficient alone - measured:
        /// a <c>HashSet&lt;int&gt;</c> is an <c>ICollection&lt;int&gt;</c> but <b>not</b> a non-generic
        /// <see cref="ICollection"/>, while a <c>Queue&lt;int&gt;</c> is the non-generic one but
        /// <b>not</b> <c>ICollection&lt;int&gt;</c>. Testing only one of them leaves the other walking.
        /// </p>
        /// <p>
        /// The test is on the <i>runtime</i> type, which is the point: a property declared
        /// <c>IReadOnlyList&lt;int&gt;</c> holding a <c>List&lt;int&gt;</c> answers in O(1) here, where a
        /// static test on the declared type would walk it. That was the measured cost of the old
        /// <see cref="Expressions.GenericProcessors.CountProcessor"/>, which decided statically.
        /// </p>
        /// <p>
        /// The generic read is a compiled delegate cached per runtime type, the pattern
        /// <c>CastOperations</c> and <c>EqualityUtils</c> already use, so the reflection happens once per
        /// type rather than once per evaluation. Ambiguity is refused rather than guessed, as
        /// <see cref="GetEnumerableItemType"/> refuses it: a type implementing
        /// <c>ICollection&lt;T&gt;</c> more than once is counted by walking.
        /// </p>
        /// <p>
        /// <c>IReadOnlyCollection&lt;T&gt;</c> is deliberately not tested: it does not exist on net40,
        /// which is a live target here, and nothing reachable needs it - every BCL collection that
        /// implements it also implements one of the two interfaces above, so the only type it would add
        /// is one written to be read-only and nothing else.
        /// </p>
        /// </remarks>
        internal static bool TryGetCount([NotNull] IEnumerable source, out int count)
        {
            if (source is ICollection collection)
            {
                count = collection.Count;
                return true;
            }

            var counter = Counters.GetOrAdd(source.GetType(), CreateCounter);
            if (counter != null)
            {
                count = counter(source);
                return true;
            }

            count = 0;
            return false;
        }

        /// <summary>
        /// A delegate reading the Count of the single <c>ICollection&lt;T&gt;</c> <paramref name="type"/>
        /// implements, or null when there is not exactly one.
        /// </summary>
        [CanBeNull]
        private static Func<object, int> CreateCounter([NotNull] Type type)
        {
            var matches = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                .ToList();

            if (matches.Count != 1)
                return null;

            var parameter = LExpression.Parameter(typeof(object), "source");
            var read = LExpression.Property(
                LExpression.Convert(parameter, matches[0]),
                matches[0].GetProperty("Count"));

            return LExpression.Lambda<Func<object, int>>(read, parameter).Compile();
        }

        private static readonly ConcurrentDictionary<Type, Func<object, int>> Counters
            = new ConcurrentDictionary<Type, Func<object, int>>();

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
        /// <p>
        /// A null in gives a null out, for the reason given on <see cref="ToListOfObjects"/>: a processor
        /// answers null for a null source and the reshaping runs over whatever it produced.
        /// </p>
        /// </remarks>
        [CanBeNull]
        [ContractAnnotation("null=>null;notnull=>notnull")]
        internal static List<T> ToTypedList<T>([CanBeNull] IEnumerable<T> elements)
        {
            return elements == null ? null : new List<T>(elements);
        }

        /// <summary>
        /// The elements of <paramref name="elements"/> as objects, in order and keeping duplicates.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="ToHashSetOfObjects"/> this must not deduplicate: it reprojects a list, where
        /// order and repeats are part of the value.
        /// </remarks>
        /// <remarks>
        /// A null in gives a null out, and that is load-bearing rather than defensive: a collection
        /// processor answers null for a null source, and the root reshaping runs over whatever the
        /// processor produced. Dereferencing here turned the guarded answer back into a
        /// <see cref="NullReferenceException"/> - caught by the evaluation sweep, which saw the
        /// exception type change rather than the divergence disappear.
        /// </remarks>
        [CanBeNull]
        [ContractAnnotation("null=>null;notnull=>notnull")]
        internal static List<object> ToListOfObjects([CanBeNull] IEnumerable elements)
        {
            if (elements == null)
                return null;

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
        /// <summary>
        /// The key and value types of the single <c>IDictionary&lt;K, V&gt;</c> <paramref name="type"/>
        /// implements, or false when there is not exactly one.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The <i>interface</i> rather than the concrete <c>Dictionary&lt;,&gt;</c>
        /// (<see cref="TryGetDictionaryItemTypes"/> answers that), so a property declared
        /// <c>IDictionary&lt;string, int&gt;</c> is recognised too - which is what the compiled indexer
        /// needs, since it works from the declared type.
        /// </p>
        /// <p>
        /// Ambiguity is refused rather than guessed, as <see cref="GetEnumerableItemType"/> refuses it:
        /// a type implementing the interface twice has no single key type, and the interpreter - which
        /// reads runtime values - still serves those.
        /// </p>
        /// </remarks>
        internal static bool TryGetGenericDictionaryTypes(
            [NotNull] Type type, out Type keyType, out Type valueType)
        {
            var candidates = (type.IsInterface && type.IsGenericType
                              && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                                  ? new[] { type }
                                  : type.GetInterfaces()
                                        .Where(i => i.IsGenericType
                                                    && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                                        .ToArray());

            if (candidates.Length == 1)
            {
                var arguments = candidates[0].GetGenericArguments();
                keyType = arguments[0];
                valueType = arguments[1];
                return true;
            }

            keyType = null;
            valueType = null;
            return false;
        }

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

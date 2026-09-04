using System;
using System.Collections.Generic;

using JetBrains.Annotations;

using SpringUtil;

namespace SpringExpressions
{
    /// <summary>
    /// Builds a <see cref="SandboxPolicy"/> from an existing one. Start with
    /// <see cref="SandboxPolicy.NewBasedOn"/> and finish with <see cref="Build"/>.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Meant to be used once at startup and the result shared, because a policy's verdict cache is per
    /// instance: §5's "the check costs nothing" holds while a few long-lived policies do the work, and
    /// a caller deriving a fresh policy per expression would recompute every verdict. The ceremony of
    /// a builder is mildly helpful there - it does not read like something to do at a call site.
    /// </p>
    /// <p>
    /// The builder is mutable and the policy it produces is not, which is the whole arrangement: the
    /// hazard <c>_Docs/type-sandboxing.md</c> §4.3 guards against is a <i>policy</i> adjusted after
    /// being handed to three expressions. A builder lives for a few lines and yields one immutable
    /// policy, so the mutability never leaves startup.
    /// </p>
    /// <p>
    /// <b>Not built yet:</b> <c>TrustAssemblyOf</c>, which is §5.2's other axis and waits on the
    /// trusted-assembly ruling; and the closure check in <see cref="Build"/>, which needs a catalog to
    /// check against. Both are stage 4. This much exists because the member gate is untestable without
    /// some way to construct a catalogued policy - with an empty catalog every member is denied, so
    /// the interesting branch would ship unexercised.
    /// </p>
    /// </remarks>
    public sealed class SandboxPolicyBuilder
    {
        internal SandboxPolicyBuilder([NotNull] IDictionary<Type, HashSet<string>> catalog)
        {
            _catalog = new Dictionary<Type, HashSet<string>>();

            foreach (var entry in catalog)
                _catalog.Add(entry.Key, new HashSet<string>(entry.Value, MemberNameComparer));
        }

        /// <summary>
        /// Permits <paramref name="memberNames"/> on <typeparamref name="T"/>, adding to whatever the
        /// base policy already allowed there.
        /// </summary>
        /// <remarks>
        /// Sugar for the <see cref="Allow(Type, string[])"/> overload, and available only where
        /// <typeparamref name="T"/> can be a type argument at all: <c>Math</c>, <c>Environment</c> and
        /// <c>Convert</c> are static classes, and CS0718 forbids a static type as a type argument. The
        /// <see cref="Type"/> overload is therefore the primary form rather than a fallback - it is
        /// also the only one that can take a type known only at run time.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder Allow<T>([NotNull] params string[] memberNames)
        {
            return Allow(typeof(T), memberNames);
        }

        /// <summary>
        /// Permits <paramref name="memberNames"/> on <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The type to catalogue. An open generic definition for a generic type.</param>
        /// <param name="memberNames">
        /// Properties, fields and methods alike - the gate is keyed by name and cannot tell them
        /// apart, so there is deliberately no <c>AllowProperty</c>/<c>AllowMethod</c> split. Write
        /// them with <c>nameof</c>: a rename or a typo then breaks the build, which a bare string
        /// cannot do. Passing none catalogues the type with no members of its own, which makes it
        /// reachable and its inherited entries usable.
        /// </param>
        [NotNull]
        public SandboxPolicyBuilder Allow([NotNull] Type type, [NotNull] params string[] memberNames)
        {
            AssertUtils.ArgumentNotNull(type, "type");
            AssertUtils.ArgumentNotNull(memberNames, "memberNames");

            HashSet<string> entry;
            if (!_catalog.TryGetValue(type, out entry))
            {
                entry = new HashSet<string>(MemberNameComparer);
                _catalog.Add(type, entry);
            }

            foreach (var memberName in memberNames)
            {
                AssertUtils.ArgumentNotNull(memberName, "memberNames");
                entry.Add(memberName);
            }

            return this;
        }

        /// <summary>
        /// The finished policy. The builder may be reused afterwards without affecting it.
        /// </summary>
        [NotNull]
        public SandboxPolicy Build()
        {
            // Stage 4 adds §5.3's closure report here - "Uri.Segments returns String[], which nothing
            // catalogues, so callers get an inert object". Having exactly one place for it is the
            // structural reason this is a builder rather than a fluent method on the policy.
            var snapshot = new Dictionary<Type, HashSet<string>>();

            foreach (var entry in _catalog)
                snapshot.Add(entry.Key, new HashSet<string>(entry.Value, MemberNameComparer));

            return SandboxPolicy.WithCatalog(snapshot);
        }

        /// <summary>
        /// Case-insensitive, because this engine's member binding is: <c>Upper('x')</c>,
        /// <c>upper('x')</c> and <c>UPPER('x')</c> all resolve the same method. A case-sensitive
        /// catalog would deny a spelling the binder accepts, which is a gate disagreeing with the
        /// thing it gates. Whether the language should be case-insensitive at all is
        /// <c>_Docs/open-issues.md</c> item 11, and this comparer moves with that if it is ever ruled.
        /// </summary>
        [NotNull]
        private static readonly StringComparer MemberNameComparer = StringComparer.OrdinalIgnoreCase;

        [NotNull]
        private readonly Dictionary<Type, HashSet<string>> _catalog;
    }
}

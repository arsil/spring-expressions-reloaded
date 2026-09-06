using System;
using System.Collections.Generic;
using System.Reflection;

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
    /// <b>The verbs mirror §6.1's three levels.</b> Most types are safe whole and want
    /// <see cref="AllowAllMembersOf{T}()"/>; a few need picking apart, and which member direction is
    /// shorter differs per type - for <c>System.Type</c> an allow-list is six names, for
    /// <c>System.Environment</c> a reject-list is. <see cref="Forbid{T}()"/> is load-bearing rather
    /// than decorative since §5.2: a type nobody ruled on is <i>trusted</i> when an expression reaches
    /// it, so keeping a reachable type out has to be said.
    /// </p>
    /// </remarks>
    public sealed class SandboxPolicyBuilder
    {
        internal SandboxPolicyBuilder(
            [NotNull] IDictionary<Type, SandboxCatalogEntry> catalog,
            [CanBeNull] ISet<Assembly> allowedAssemblies,
            [CanBeNull] ISet<Assembly> forbiddenAssemblies)
        {
            _catalog = new Dictionary<Type, SandboxCatalogEntry>();

            foreach (var entry in catalog)
                _catalog.Add(entry.Key, new SandboxCatalogEntry(entry.Value));

            _allowedAssemblies = allowedAssemblies == null
                ? new HashSet<Assembly>()
                : new HashSet<Assembly>(allowedAssemblies);

            _forbiddenAssemblies = forbiddenAssemblies == null
                ? new HashSet<Assembly>()
                : new HashSet<Assembly>(forbiddenAssemblies);
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
        /// nameable and its inherited entries usable.
        /// </param>
        [NotNull]
        public SandboxPolicyBuilder Allow([NotNull] Type type, [NotNull] params string[] memberNames)
        {
            AssertUtils.ArgumentNotNull(memberNames, "memberNames");

            var entry = EntryFor(type);

            foreach (var memberName in memberNames)
            {
                AssertUtils.ArgumentNotNull(memberName, "memberNames");
                entry.Allow(memberName);
            }

            return this;
        }

        /// <summary>Every member of <typeparamref name="T"/>, inherited ones included.</summary>
        [NotNull]
        public SandboxPolicyBuilder AllowAllMembersOf<T>()
        {
            return AllowAllMembersOf(typeof(T));
        }

        /// <summary>
        /// Every member of <paramref name="type"/>, inherited ones included.
        /// </summary>
        /// <remarks>
        /// <b>This is a bet, and worth placing knowingly:</b> a type allowed whole gains whatever a
        /// future framework version adds to it, which is the forbid-list's failure mode scoped down to
        /// the types you deliberately opened. Take it for the pure ones - <c>DateTime</c>,
        /// <c>TimeSpan</c>, the numerics, <c>Math</c> - and not for a type with settable statics.
        /// <p>
        /// It also includes inherited members, so this permits <c>GetType()</c>. That is safe because
        /// <c>System.Type</c> is curated and <c>Assembly</c> is not on its list - the chain stops one
        /// link later than it looks. See §6.2, and the condition that goes with it: a type may be
        /// allowed whole only if every type its members can hand back is itself catalogued or curated,
        /// which is what <see cref="DescribeImplicitTrust"/> reports on.
        /// </p>
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder AllowAllMembersOf([NotNull] Type type)
        {
            EntryFor(type).AllMembers = true;
            return this;
        }

        /// <summary>
        /// Refuses <paramref name="memberNames"/> on <paramref name="type"/>, whatever else permits
        /// them - the reject-list direction, for a type where listing what is unsafe is shorter.
        /// </summary>
        /// <remarks>
        /// A rejection beats an allowance, including one inherited from a base type's entry, so
        /// this means what it says.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder Except([NotNull] Type type, [NotNull] params string[] memberNames)
        {
            AssertUtils.ArgumentNotNull(memberNames, "memberNames");

            var entry = EntryFor(type);

            foreach (var memberName in memberNames)
            {
                AssertUtils.ArgumentNotNull(memberName, "memberNames");
                entry.Reject(memberName);
            }

            return this;
        }

        /// <summary>The generic form of <see cref="Except(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder Except<T>([NotNull] params string[] memberNames)
        {
            return Except(typeof(T), memberNames);
        }

        /// <summary>
        /// Nothing at all: <paramref name="type"/> may not be named and may not be reached.
        /// </summary>
        /// <remarks>
        /// Load-bearing since §5.2. Under a pure allow-list this was a no-op - not catalogued already
        /// meant denied - but now a type nobody ruled on is trusted when an expression <i>arrives at</i>
        /// one, so this is the only way to keep a reachable type out.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder Forbid([NotNull] Type type)
        {
            EntryFor(type).Forbidden = true;
            return this;
        }

        /// <summary>The generic form of <see cref="Forbid(Type)"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder Forbid<T>()
        {
            return Forbid(typeof(T));
        }

        /// <summary>
        /// Every type in the assembly that declares <typeparamref name="T"/> becomes nameable and
        /// unrestricted.
        /// </summary>
        /// <remarks>
        /// Narrower in usefulness than it was before §5.2, which made *reaching* a type trusted by
        /// default: what this adds is that those types may also be <b>named</b> - <c>T(X)</c>,
        /// <c>new X()</c>, a cast - which the fallback never grants.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder AllowAssemblyOf<T>()
        {
            return AllowAssembly(typeof(T).Assembly);
        }

        /// <summary>Every type in <paramref name="assembly"/> becomes nameable and unrestricted.</summary>
        [NotNull]
        public SandboxPolicyBuilder AllowAssembly([NotNull] Assembly assembly)
        {
            AssertUtils.ArgumentNotNull(assembly, "assembly");

            _allowedAssemblies.Add(assembly);
            _forbiddenAssemblies.Remove(assembly);

            return this;
        }

        /// <summary>
        /// No type in the assembly that declares <typeparamref name="T"/> may be named or reached.
        /// </summary>
        /// <remarks>
        /// The direction §5.2 made useful: "nothing from this package, however I reach it". Forbidding
        /// wins over every allowance, including a per-type entry, because a coarse refusal that a fine
        /// permission could undo would be no refusal at all.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder ForbidAssemblyOf<T>()
        {
            return ForbidAssembly(typeof(T).Assembly);
        }

        /// <summary>No type in <paramref name="assembly"/> may be named or reached.</summary>
        [NotNull]
        public SandboxPolicyBuilder ForbidAssembly([NotNull] Assembly assembly)
        {
            AssertUtils.ArgumentNotNull(assembly, "assembly");

            _forbiddenAssemblies.Add(assembly);
            _allowedAssemblies.Remove(assembly);

            return this;
        }

        /// <summary>
        /// The finished policy. The builder may be reused afterwards without affecting it.
        /// </summary>
        [NotNull]
        public SandboxPolicy Build()
        {
            var snapshot = new Dictionary<Type, SandboxCatalogEntry>();

            foreach (var entry in _catalog)
                snapshot.Add(entry.Key, new SandboxCatalogEntry(entry.Value));

            return SandboxPolicy.WithCatalog(
                snapshot,
                new HashSet<Assembly>(_allowedAssemblies),
                new HashSet<Assembly>(_forbiddenAssemblies));
        }

        /// <summary>
        /// What this catalog implicitly trusts: for every member it permits, the type that member
        /// hands back, where the catalog has no entry for that type.
        /// </summary>
        /// <remarks>
        /// <b>§5.3's closure rule, and §5.2 changed what it means.</b> It used to report incompleteness
        /// - permit <c>Environment.OSVersion</c> without cataloguing <c>OperatingSystem</c> and the
        /// caller gets an object every member of which is denied. Since an uncatalogued type an
        /// expression <i>arrives at</i> is trusted, the same gap is now an implicit <b>grant</b>: the
        /// returned object is fully usable and nobody said so. So this is the report to read before
        /// shipping a catalog, and the reason <c>Build()</c> is where it belongs (§4.5).
        /// <p>
        /// It reports rather than throws, because most rows are fine - <c>DateTime.Year</c> hands back
        /// an <c>int</c> and nobody minds. What it is for is finding the row that hands back something
        /// with a settable static on it, which is how <c>CultureInfo</c> was found (§5.3).
        /// </p>
        /// </remarks>
        [NotNull, ItemNotNull]
        public IList<string> DescribeImplicitTrust()
        {
            var policy = Build();
            var gaps = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var catalogued in _catalog)
            {
                if (catalogued.Value.Forbidden)
                    continue;

                foreach (var member in catalogued.Key.GetMembers(
                             BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (!policy.PermitsForReport(catalogued.Key, member.Name))
                        continue;

                    var handedBack = ResultTypeOf(member);
                    if (handedBack == null || handedBack == typeof(void))
                        continue;

                    // A generic parameter is not a type anyone can catalogue - List<>.Find returns
                    // "T", Dictionary<,>.Item returns "TValue" - so reporting them is noise, and it
                    // was a sixth of the first run's rows. What a *constructed* List<int> hands back
                    // is judged when the expression reaches it, like anything else.
                    if (handedBack.IsGenericParameter
                        || (handedBack.HasElementType && handedBack.GetElementType().IsGenericParameter))
                    {
                        continue;
                    }

                    if (policy.KnowsForReport(handedBack))
                        continue;

                    var row = catalogued.Key.Name + "." + member.Name + " hands back "
                              + (handedBack.FullName ?? handedBack.Name) + ", which nothing catalogues";

                    if (seen.Add(row))
                        gaps.Add(row);
                }
            }

            gaps.Sort(StringComparer.Ordinal);

            return gaps;
        }

        [CanBeNull]
        private static Type ResultTypeOf([NotNull] MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property != null)
                return property.PropertyType;

            var field = member as FieldInfo;
            if (field != null)
                return field.FieldType;

            var method = member as MethodInfo;

            return method?.ReturnType;
        }

        [NotNull]
        private SandboxCatalogEntry EntryFor([NotNull] Type type)
        {
            AssertUtils.ArgumentNotNull(type, "type");

            SandboxCatalogEntry entry;
            if (!_catalog.TryGetValue(type, out entry))
            {
                entry = new SandboxCatalogEntry();
                _catalog.Add(type, entry);
            }

            return entry;
        }

        [NotNull]
        private readonly Dictionary<Type, SandboxCatalogEntry> _catalog;

        [NotNull]
        private readonly HashSet<Assembly> _allowedAssemblies;

        [NotNull]
        private readonly HashSet<Assembly> _forbiddenAssemblies;
    }
}

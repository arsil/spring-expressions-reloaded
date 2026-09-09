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
        /// <summary>
        /// A builder over an empty catalog - nothing permitted, nothing forbidden.
        /// </summary>
        /// <remarks>
        /// What <see cref="SandboxPolicy.Restricted"/>'s own catalog is authored with, so the shipped
        /// catalog goes through the same verbs a consumer uses. It cannot use
        /// <see cref="SandboxPolicy.NewBasedOn"/> for the obvious reason: it <i>is</i> the policy that
        /// call would start from.
        /// <p>
        /// This is the "start from nothing" base §4.5 predicted would be needed once
        /// <c>Restricted</c> stopped meaning "deny everything". It is internal for now - a consumer
        /// wanting to build from scratch rather than from <c>Restricted</c> has no public way to, and
        /// that gap is recorded rather than filled, because nobody has asked for it.
        /// </p>
        /// </remarks>
        [NotNull]
        internal static SandboxPolicyBuilder StartingFromNothing()
        {
            return new SandboxPolicyBuilder(
                new Dictionary<Type, SandboxCatalogEntry>(), null, null);
        }

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
        /// Properties, fields and methods alike - whichever kind bears the name is permitted. Write
        /// them with <c>nameof</c>: a rename or a typo then breaks the build, which a bare string
        /// cannot do. Passing none catalogues the type with no members of its own, which makes it
        /// nameable and its inherited entries usable.
        /// <p>
        /// Where a name must be permitted for one kind only, or in one direction only, the six verbs
        /// below say so - and say it in their names, which is the point of them.
        /// </p>
        /// </param>
        [NotNull]
        public SandboxPolicyBuilder Allow([NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return AllowFor(type, null, MemberAccess.Both, memberNames);
        }

        /// <summary>
        /// Permits <b>calling</b> these methods of <paramref name="type"/>, and says nothing about a
        /// property or field of the same name.
        /// </summary>
        /// <remarks>
        /// A method has one mode of use, so there is no <c>AllowMethodRead</c> and no
        /// <c>AllowMethodWrite</c> - which is the whole reason the catalog keys on the member's kind.
        /// The first cut of the access axis keyed on the name alone and mapped invoking onto
        /// <i>reading</i>, which made <c>AllowRead&lt;T&gt;("Exit")</c> permit calling <c>Exit</c> and
        /// - worse - made <c>ExceptWrite("Danger")</c> refuse nothing while reading as a refusal. Both
        /// measured. With the kind in the key those sentences cannot be written.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder AllowMethod([NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return AllowFor(type, MemberKind.Method, MemberAccess.Both, memberNames);
        }

        /// <summary>The generic form of <see cref="AllowMethod(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder AllowMethod<T>([NotNull] params string[] memberNames)
        {
            return AllowMethod(typeof(T), memberNames);
        }

        /// <summary>
        /// Permits reading <b>and</b> writing these properties or fields, and says nothing about a
        /// method of the same name.
        /// </summary>
        /// <remarks>
        /// Named <c>PropertyOrField</c> rather than <c>Property</c> because it means both, and
        /// splitting them would put back the trap this vocabulary exists to remove one level down: an
        /// <c>AllowProperty</c> applied to a field would be a silent no-op. It is also the name this
        /// codebase already uses for the concept - <c>PropertyOrFieldNode</c> is the node that gates it.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder AllowPropertyOrField(
            [NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return AllowFor(type, MemberKind.PropertyOrField, MemberAccess.Both, memberNames);
        }

        /// <summary>The generic form of <see cref="AllowPropertyOrField(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder AllowPropertyOrField<T>([NotNull] params string[] memberNames)
        {
            return AllowPropertyOrField(typeof(T), memberNames);
        }

        /// <summary>
        /// These properties or fields may be <b>read</b> and not written.
        /// </summary>
        /// <remarks>
        /// The reason the access axis exists: refusing a member outright to stop its setter is
        /// collateral damage. <c>CultureInfo.CurrentCulture</c> is the worked example - reading the
        /// culture is what a report wants, installing one changes every subsequent format in the
        /// process.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder AllowPropertyOrFieldRead(
            [NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return AllowFor(type, MemberKind.PropertyOrField, MemberAccess.Read, memberNames);
        }

        /// <summary>The generic form of <see cref="AllowPropertyOrFieldRead(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder AllowPropertyOrFieldRead<T>([NotNull] params string[] memberNames)
        {
            return AllowPropertyOrFieldRead(typeof(T), memberNames);
        }

        /// <summary>
        /// These properties or fields may be <b>written</b> and not read.
        /// </summary>
        /// <remarks>
        /// The symmetric half, and the rarer want - a field a script may set but not inspect. Coherent
        /// rather than decorative: <c>Secret = 'x'</c> is permitted while <c>Secret</c> is denied, and
        /// <c>Secret = Secret + 'x'</c> is correctly denied on its read half.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder AllowPropertyOrFieldWrite(
            [NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return AllowFor(type, MemberKind.PropertyOrField, MemberAccess.Write, memberNames);
        }

        /// <summary>The generic form of <see cref="AllowPropertyOrFieldWrite(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder AllowPropertyOrFieldWrite<T>([NotNull] params string[] memberNames)
        {
            return AllowPropertyOrFieldWrite(typeof(T), memberNames);
        }

        /// <summary>
        /// <paramref name="kind"/> null means both kinds - what the undirected <c>Allow</c> means.
        /// </summary>
        [NotNull]
        private SandboxPolicyBuilder AllowFor(
            [NotNull] Type type,
            MemberKind? kind,
            MemberAccess access,
            [NotNull] params string[] memberNames)
        {
            AssertUtils.ArgumentNotNull(memberNames, "memberNames");

            var entry = EntryFor(type);

            foreach (var memberName in memberNames)
            {
                AssertUtils.ArgumentNotNull(memberName, "memberNames");

                if (kind == null)
                    entry.AllowEitherKind(memberName);
                else
                    entry.Allow(memberName, kind.Value, access);
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
            return ExceptFor(type, null, MemberAccess.Both, memberNames);
        }

        /// <summary>The generic form of <see cref="Except(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder Except<T>([NotNull] params string[] memberNames)
        {
            return Except(typeof(T), memberNames);
        }

        /// <summary>
        /// Refuses <b>calling</b> these methods, leaving a property or field of the same name alone.
        /// </summary>
        /// <remarks>
        /// This is the verb the first cut of the access axis was missing, and its absence was the
        /// sharpest edge in it: someone writing
        /// <c>AllowAllMembersOf&lt;Environment&gt;().ExceptWrite(nameof(Environment.Exit))</c> got a
        /// line that read as a refusal and refused nothing, because a method has no write to refuse.
        /// <c>ExceptMethod</c> is what that sentence wanted, and the directional verbs now say
        /// <c>PropertyOrField</c> in their names so the mistake reads wrong where it is typed.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder ExceptMethod(
            [NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return ExceptFor(type, MemberKind.Method, MemberAccess.Both, memberNames);
        }

        /// <summary>The generic form of <see cref="ExceptMethod(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder ExceptMethod<T>([NotNull] params string[] memberNames)
        {
            return ExceptMethod(typeof(T), memberNames);
        }

        /// <summary>
        /// Refuses reading <b>and</b> writing these properties or fields, leaving a method of the same
        /// name alone.
        /// </summary>
        [NotNull]
        public SandboxPolicyBuilder ExceptPropertyOrField(
            [NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return ExceptFor(type, MemberKind.PropertyOrField, MemberAccess.Both, memberNames);
        }

        /// <summary>The generic form of <see cref="ExceptPropertyOrField(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder ExceptPropertyOrField<T>([NotNull] params string[] memberNames)
        {
            return ExceptPropertyOrField(typeof(T), memberNames);
        }

        /// <summary>
        /// Refuses <b>writing</b> these properties or fields while leaving them readable.
        /// </summary>
        /// <remarks>
        /// The most useful of the directional verbs, because it is what a whole-allowed type needs, and
        /// the shape the built-in <c>CultureInfo</c> entry uses:
        /// <c>AllowAllMembersOf&lt;CultureInfo&gt;().ExceptPropertyOrFieldWrite&lt;CultureInfo&gt;(nameof(CultureInfo.CurrentCulture))</c>
        /// reads the culture and refuses installing one, where the catalog previously had to refuse the
        /// member outright and lose the reader with it.
        /// </remarks>
        [NotNull]
        public SandboxPolicyBuilder ExceptPropertyOrFieldWrite(
            [NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return ExceptFor(type, MemberKind.PropertyOrField, MemberAccess.Write, memberNames);
        }

        /// <summary>The generic form of <see cref="ExceptPropertyOrFieldWrite(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder ExceptPropertyOrFieldWrite<T>(
            [NotNull] params string[] memberNames)
        {
            return ExceptPropertyOrFieldWrite(typeof(T), memberNames);
        }

        /// <summary>
        /// Refuses <b>reading</b> these properties or fields while leaving them writable.
        /// </summary>
        [NotNull]
        public SandboxPolicyBuilder ExceptPropertyOrFieldRead(
            [NotNull] Type type, [NotNull] params string[] memberNames)
        {
            return ExceptFor(type, MemberKind.PropertyOrField, MemberAccess.Read, memberNames);
        }

        /// <summary>The generic form of <see cref="ExceptPropertyOrFieldRead(Type, string[])"/>.</summary>
        [NotNull]
        public SandboxPolicyBuilder ExceptPropertyOrFieldRead<T>(
            [NotNull] params string[] memberNames)
        {
            return ExceptPropertyOrFieldRead(typeof(T), memberNames);
        }

        /// <summary>
        /// <paramref name="kind"/> null means both kinds - what the undirected <c>Except</c> means.
        /// </summary>
        [NotNull]
        private SandboxPolicyBuilder ExceptFor(
            [NotNull] Type type,
            MemberKind? kind,
            MemberAccess access,
            [NotNull] params string[] memberNames)
        {
            AssertUtils.ArgumentNotNull(memberNames, "memberNames");

            var entry = EntryFor(type);

            foreach (var memberName in memberNames)
            {
                AssertUtils.ArgumentNotNull(memberName, "memberNames");

                if (kind == null)
                    entry.RejectEitherKind(memberName);
                else
                    entry.Reject(memberName, kind.Value, access);
            }

            return this;
        }

        /// <summary>
        /// Nothing at all: <paramref name="type"/> may not be named and may not be reached.
        /// </summary>
        /// <remarks>
        /// Load-bearing since §5.2. Under a pure allow-list this was a no-op - not catalogued already
        /// meant denied - but now a type nobody ruled on is trusted when an expression <i>arrives at</i>
        /// one, so this is the only way to keep a reachable type out.
        /// <p>
        /// <b><see cref="object"/> is refused, because forbidding it cannot mean anything</b> - and it
        /// is refused rather than documented because it would otherwise fail <i>silently</i>, which is
        /// the worst thing a security API can do. The inheritance walk deliberately skips
        /// <c>object</c>'s entry (see <c>SandboxPolicy.InheritsARefusal</c>: without that skip it would
        /// terminate at <c>object</c> for every class alive and inherited refusals would never fire at
        /// all), so a ban on <c>object</c> is invisible to it. Measured before this guard existed:
        /// <c>Restricted</c> plus <c>Forbid&lt;object&gt;()</c> denied nothing whatever - an
        /// uncatalogued model's members, <c>Name.Length</c>, <c>T(System.Math).Max(1, 2)</c> all still
        /// worked.
        /// </p>
        /// <p>
        /// <b>It is exactly one special case, not a category.</b> <c>object</c> is the only type the
        /// walk skips, so it is the only no-op: <c>Forbid&lt;ValueType&gt;()</c> genuinely works, an
        /// uncatalogued struct's base chain reaching that entry and being denied.
        /// </p>
        /// <p>
        /// <b>Thrown from here rather than deferred to <see cref="Build"/></b>, unlike §5.3's closure
        /// rule, which is a property of the whole catalog and has nowhere earlier to live. The
        /// offending argument is right here in the call, so the stack should point at the line somebody
        /// typed.
        /// </p>
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="type"/> is <see cref="object"/>. What the caller was probably reaching for -
        /// a pure allow-list, where nothing uncatalogued is reachable at all - is not offered, and the
        /// message says so rather than leaving them to guess.
        /// </exception>
        [NotNull]
        public SandboxPolicyBuilder Forbid([NotNull] Type type)
        {
            AssertUtils.ArgumentNotNull(type, "type");

            if (type == typeof(object))
            {
                throw new ArgumentException(
                    "Forbid(typeof(object)) cannot mean anything, so it is refused rather than "
                    + "silently doing nothing: whether an uncatalogued type is reachable is decided by "
                    + "how the expression reached it, and object's entry is deliberately skipped when a "
                    + "refusal is inherited. A pure allow-list, in which nothing uncatalogued is "
                    + "reachable at all, is not currently offered.",
                    "type");
            }

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

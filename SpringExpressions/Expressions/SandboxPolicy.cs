using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;

using SpringUtil;

namespace SpringExpressions
{
    /// <summary>
    /// What an expression is allowed to reach: which framework types it may name, and which of their
    /// members it may touch.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>A type-name allow-list is not a sandbox</b>, which is the finding the whole design rests on:
    /// from <c>int</c> you reach <c>Assembly</c>, and <c>'abc'.GetType().Assembly</c> reaches it without
    /// naming a type at all. So the boundary is <i>which members are reachable</i>, with type names as
    /// one input to it - and a policy is consulted at two gates, one for type names
    /// (<c>TypeResolutionUtils.ResolveTypeForExpression</c>) and one for members
    /// (<see cref="RequirePermittedMember"/>), both on both backends. Indexing is deliberately ungated
    /// - <c>_Docs/type-sandboxing.md</c> §4.2 measures why the two attempts at it were unimplementable.
    /// </p>
    /// <p>
    /// <b>A policy is immutable, and belongs to one expression, fixed when that expression is created.</b>
    /// It is deliberately not an ambient scope: a compiled expression binds its members once while the
    /// tree is built, so a scope active at evaluation could only influence it by recompiling per policy;
    /// and the interpreter memoises its accessor per node, so a <c>using</c> block would govern the
    /// first evaluation and silently not the rest - worse than none, because it would look like it
    /// worked. It is also a parameter of its own rather than part of a bundled options object, so that a
    /// disabled sandbox cannot travel invisibly into a call site that only meant to change how
    /// compilation fails. See §4.3.
    /// </p>
    /// <p>
    /// <b>The default is on since 2026-09-06, for both API layers</b> - the inherited weakly typed
    /// surface included, not only the strongly typed one. A per-overload default would put the
    /// permissive setting exactly where the risk is highest, make "is this sandboxed?" an invisible
    /// property of the call site, and keep the frozen suite green by construction - so the largest
    /// breaking change on the backlog would become the one change that suite cannot see. See §3.5. The
    /// measurement that could have forced a split was taken and did not: a consumer's migration is one
    /// policy at startup, and no test in either suite needed anything else (§8.9).
    /// </p>
    /// <p>
    /// <b>A consumer defines a policy with <see cref="SandboxPolicyBuilder"/></b>, obtained from
    /// <see cref="NewBasedOn"/> - which is also how the built-in
    /// <see cref="Restricted"/> catalog is authored, so the one catalog that ships is expressible by
    /// the verbs a consumer has. A fluent <c>Allowing(...)</c> on the policy itself was rejected: it
    /// reads as mutation of the instance it is called on and carries the discarded-result trap, and it
    /// leaves nowhere to check §5.3's closure rule, since every intermediate would already be a
    /// complete policy. <c>Build()</c> is that one place. See §4.5.
    /// </p>
    /// <p>
    /// Members are named by <b>string</b>, with <c>nameof</c> as the intended spelling, and an entry is
    /// keyed on the name <i>and the member's kind</i> - <see cref="MemberKind.PropertyOrField"/> or
    /// <see cref="MemberKind.Method"/> - because only the first of those has a direction to read or
    /// write. Keying on the name alone made two of the four directional verbs meaningless on methods,
    /// one of them a refusal that refused nothing; see <see cref="MemberKind"/> for the measurement.
    /// <p>
    /// A mock-library-style <c>Allow&lt;Uri&gt;(u =&gt; u.Host)</c> was rejected: it promises overload
    /// granularity this gate cannot keep, and it does not compile at all for a static class -
    /// <c>Math</c>, <c>Environment</c> and <c>Convert</c> are static, and CS0718 forbids a static type
    /// as a type argument. That is also why <c>Allow(Type, params string[])</c> is the primary form
    /// and the generic one is sugar.
    /// </p>
    /// </p>
    /// <p>
    /// <see cref="SpringCore.TypeResolution.TypeRegistry"/> does not remove the need for that builder,
    /// which was the open question: a registry grant is all-or-nothing and process-global, so needing
    /// one member of <see cref="System.Type"/> would mean granting all of it - handing over
    /// <c>Assembly</c>, and with it <c>Assembly.Load</c>, which reopens everything the sandbox exists
    /// to close.
    /// </p>
    /// <p>
    /// <b>Policies are meant to be few and long-lived</b>, because the verdict cache below is per
    /// instance: §5's "the check costs nothing" holds while the built-in singletons do the work, and a
    /// caller who derived a fresh policy per expression would recompute every verdict.
    /// </p>
    /// </remarks>
    public sealed class SandboxPolicy
    {
        // Declaration order matters: a static field initialiser runs in textual order, so the two
        // singletons must exist before _default can be pointed at one of them.

        private static readonly SandboxPolicy AllowEverythingPolicy = new SandboxPolicy(null, null, null);

        private static readonly SandboxPolicy RestrictedPolicy = BuildRestrictedPolicy();

        // Stage 5, 2026-09-06: the sandbox is on by default. Measured before it was taken - flip,
        // run both suites, revert - and what that dry run found is _Docs/type-sandboxing.md §8.9.
        // A consumer whom this breaks writes one line at startup: either
        // SandboxPolicy.Default = SandboxPolicy.DangerouslyAllowEverything to opt out entirely, or a
        // policy of their own naming the types their expressions construct and reach statically.
        private static SandboxPolicy _default = RestrictedPolicy;

        /// <summary>
        /// The policy an expression gets when the call that created it did not name one.
        /// </summary>
        /// <remarks>
        /// A process-wide setting, meant to be set once at startup, and the only place a policy can be
        /// stated for <see cref="ExpressionEvaluator"/> - which parses internally and so has nowhere to
        /// take a policy argument. That is why it must be settable rather than a constant.
        /// <p>
        /// Swapping it affects <b>expressions created after the swap</b> and nothing else: each
        /// expression captures a policy instance when it is created, and a policy is immutable, so
        /// nothing already parsed can change its mind. Assignment of a reference is atomic, so a swap
        /// concurrent with a parse yields one policy or the other and never a torn state - but a swap
        /// mid-run is still a startup-shaped operation used at the wrong time, and reads as one.
        /// </p>
        /// </remarks>
        [NotNull]
        public static SandboxPolicy Default
        {
            get { return _default; }
            set
            {
                AssertUtils.ArgumentNotNull(value, "value");
                _default = value;
            }
        }

        /// <summary>
        /// The sandbox off: every type and every member is reachable, which is exactly the behaviour
        /// this library has always had.
        /// </summary>
        /// <remarks>
        /// Spelled so that it cannot be typed by accident or skimmed past in review, and so that a
        /// reviewer can grep for every deliberate escape. Turning the sandbox off widens what is
        /// reachable and changes nothing else - the same objects, of the same types, which is the
        /// property that ruled out substituting curated proxy types (§3.2).
        /// </remarks>
        [NotNull]
        public static SandboxPolicy DangerouslyAllowEverything
        {
            get { return AllowEverythingPolicy; }
        }

        /// <summary>
        /// The sandbox on, against the built-in catalog.
        /// </summary>
        /// <remarks>
        /// <b>What <see cref="Default"/> is, unless an application replaced it.</b> The catalog was
        /// curated last, from what the two suites and <c>SandboxCorpusTests</c> reject once the gates
        /// were live - the red list was its specification, which is why it is measured rather than
        /// imagined (§8.2).
        /// <p>
        /// It permits the types an expression may <i>name</i> - the primitives, <c>DateTime</c>,
        /// <c>Math</c>, the collection types the language builds, any enum - curates
        /// <see cref="System.Type"/> and <c>CultureInfo</c>, and forbids the reflection and loader
        /// family outright. Everything an expression merely <i>reaches</i> is trusted unless forbidden,
        /// which is §5.2 and is what keeps a consumer's own model out of the catalog entirely.
        /// </p>
        /// <p>
        /// A consumer who needs more starts here: <c>NewBasedOn(Restricted)</c>. Starting from
        /// <i>nothing</i> is internal-only for now - see
        /// <c>SandboxPolicyBuilder.StartingFromNothing</c>, which is what builds this.
        /// </p>
        /// </remarks>
        [NotNull]
        public static SandboxPolicy Restricted
        {
            get { return RestrictedPolicy; }
        }

        /// <summary>
        /// What this policy has decided about <paramref name="type"/> - computed once per type and
        /// cached, so that a member check costs one dictionary hit and one set hit, both on a cold path
        /// anyway (§5).
        /// </summary>
        internal TypeVerdict VerdictFor([NotNull] Type type)
        {
            AssertUtils.ArgumentNotNull(type, "type");

            if (_catalog == null)
                return TypeVerdict.Unrestricted(null);

            // GetOrAdd may run its factory more than once under contention. That is fine here and only
            // here: Compute is a pure function of the type and the loser is a discarded struct. Where
            // the loser would have been a duplicate *notification* - the evaluation-decision observer -
            // this codebase deliberately uses TryGetValue/TryAdd instead.
            return _verdicts.GetOrAdd(type, Compute);
        }

        /// <summary>
        /// Starts a builder from this policy's catalog. The result of <c>Build()</c> is a new policy;
        /// nothing here is ever modified.
        /// </summary>
        /// <remarks>
        /// A builder rather than a fluent <c>Allowing(...)</c> on the policy itself, for two reasons
        /// that differ in kind: a method on the policy reads as mutation of the instance it is called
        /// on and carries the discarded-result trap, and - the structural one - <c>Build()</c> is the
        /// only place §5.3's closure rule can ever be checked, because with a fluent derive every
        /// intermediate is already a complete policy. See <c>_Docs/type-sandboxing.md</c> §4.5.
        /// <p>
        /// Refused for <see cref="DangerouslyAllowEverything"/>, which has no catalog to build on: a
        /// policy that permits everything cannot be narrowed by adding permissions, and silently
        /// treating it as empty would turn "start from the sandbox being off" into "start from
        /// everything denied".
        /// </p>
        /// </remarks>
        [NotNull]
        public static SandboxPolicyBuilder NewBasedOn([NotNull] SandboxPolicy policy)
        {
            AssertUtils.ArgumentNotNull(policy, "policy");

            if (policy._catalog == null)
            {
                throw new ArgumentException(
                    "Cannot build on SandboxPolicy.DangerouslyAllowEverything: it has no catalog, and "
                    + "adding permissions to a policy that already permits everything is meaningless.",
                    "policy");
            }

            return new SandboxPolicyBuilder(
                policy._catalog, policy._allowedAssemblies, policy._forbiddenAssemblies);
        }

        private SandboxPolicy(
            [CanBeNull] IDictionary<Type, SandboxCatalogEntry> catalog,
            [CanBeNull] ISet<Assembly> allowedAssemblies,
            [CanBeNull] ISet<Assembly> forbiddenAssemblies)
        {
            _catalog = catalog;
            _catalogByFullName = catalog == null
                ? EmptyNameIndex
                : IndexByFullName(catalog);
            _allowedAssemblies = allowedAssemblies;
            _forbiddenAssemblies = forbiddenAssemblies;

            _ambiguousDeclaredTypes = ComputeAmbiguousDeclaredTypes(catalog);
            _ambiguousDeclaredTypeNames = NamesOf(_ambiguousDeclaredTypes);
        }

        /// <summary>
        /// The same set by <see cref="Type.FullName"/>, for the case where the runtime holds two
        /// <see cref="Type"/> objects for one type - see <see cref="TryGetEntryByName"/>, which needs
        /// the identical fallback for the identical reason.
        /// </summary>
        [NotNull]
        private static ISet<string> NamesOf([NotNull] ISet<Type> types)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var type in types)
            {
                if (type.FullName != null)
                    names.Add(type.FullName);
            }

            return names;
        }

        /// <summary>The policy a <see cref="SandboxPolicyBuilder"/> produces.</summary>
        [NotNull]
        internal static SandboxPolicy WithCatalog(
            [NotNull] IDictionary<Type, SandboxCatalogEntry> catalog,
            [CanBeNull] ISet<Assembly> allowedAssemblies,
            [CanBeNull] ISet<Assembly> forbiddenAssemblies)
        {
            return new SandboxPolicy(catalog, allowedAssemblies, forbiddenAssemblies);
        }

        /// <summary>
        /// Throws if this policy does not permit <paramref name="type"/>. The gate every expression
        /// type name goes through.
        /// </summary>
        /// <remarks>
        /// The exception names the part that was actually denied rather than the composite that
        /// contained it, so <c>T(List&lt;Process&gt;)</c> reports <c>Process</c>.
        /// </remarks>
        internal void RequirePermittedType([NotNull] Type type)
        {
            AssertUtils.ArgumentNotNull(type, "type");

            // Unknown is denied here and trusted by the member gate - §5.2. Naming is unbounded, so
            // this half is an allow-list.
            if (IsNameable(type))
                return;

            throw new SandboxViolationException(FirstDeniedPart(type));
        }

        /// <summary>
        /// Whether an expression may <i>name</i> this type: it and every part of it must be reachable.
        /// </summary>
        /// <remarks>
        /// <b>The part-by-part check belongs here and not in the verdict</b>, and putting it in the
        /// verdict was a mistake a failing test caught. Naming <c>List&lt;Process&gt;</c> must be
        /// refused, because a name is how an expression reaches a type in the first place - and
        /// <c>GenericTypeResolver</c> resolves each argument through the ungated entry point, so this
        /// is the only place they can be judged. But <i>using a member</i> on a value whose type
        /// happens to be generic is a different question: <c>Totals['net']</c> on a
        /// <c>Dictionary&lt;string, int&gt;</c> would have required <c>string</c> and <c>int</c> to be
        /// catalogued before the dictionary could be indexed at all, which is not what the catalog
        /// means.
        /// <p>
        /// <b>An array is not a thing the catalog rules on - it is a construction over an element
        /// type</b>, so it is asked about its element and never about itself. That ordering is
        /// load-bearing and it was wrong until 2026-09-06: the verdict test came first, an array type
        /// is never in the catalog, and <see cref="SandboxVerdict.Unknown"/> returned false one line
        /// before the decomposition could run. So <c>string[]</c> was refused while <c>string</c> was
        /// allowed whole, and the message blamed <c>string</c> - a type the policy permits. The same
        /// unreachable-branch mistake hid behind the array special case <c>Compute</c> used to carry,
        /// whose removal note claims this method "already decomposes them"; it did not get the chance.
        /// By-ref and pointer types read the same way.
        /// </p>
        /// <p>
        /// A constructed generic is the other half and is <i>not</i> the same case: there the
        /// definition itself is a catalog row - <c>List&lt;&gt;</c> is listed, <c>Nullable&lt;&gt;</c>
        /// had to be added - so its own verdict is asked first and the arguments after.
        /// </p>
        /// </remarks>
        private bool IsNameable([NotNull] Type type)
        {
            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsNameable(type.GetElementType());

            var verdict = VerdictFor(type).Verdict;

            if (verdict == SandboxVerdict.Denied || verdict == SandboxVerdict.Unknown)
                return false;

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    if (!IsNameable(argument))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Throws if this policy does not permit <paramref name="memberName"/> on
        /// <paramref name="receiverType"/>. The gate every member an expression reaches goes through.
        /// </summary>
        /// <remarks>
        /// <b>The receiver's type decides, not the member's declaring type.</b> A member declared on a
        /// base type is permitted when that base type is catalogued too - the verdict unions the
        /// entries up the chain - so <c>GetType</c> is listed once, on <c>System.Object</c>, rather
        /// than repeated on every entry. Reachability still comes from the type's <i>own</i> entry, so
        /// cataloguing <c>System.Object</c> does not make every type reachable.
        /// <p>
        /// This is the half that closes <c>'abc'.GetType().Assembly</c>, which names no type at all
        /// and so never meets the type gate: <c>System.Type</c> is catalogued with descriptive members
        /// only, and <c>Assembly</c> is not among them.
        /// </p>
        /// </remarks>
        internal void RequirePermittedMember(
            [NotNull] Type receiverType,
            [NotNull] string memberName,
            MemberKind kind,
            MemberAccess access)
        {
            if (PermitsMember(receiverType, memberName, kind, access))
                return;

            throw new SandboxViolationException(receiverType, memberName);
        }

        /// <summary>
        /// Whether <paramref name="memberName"/> may be used on <paramref name="receiverType"/> for
        /// <paramref name="access"/>, answering rather than throwing.
        /// </summary>
        /// <remarks>
        /// The interpreted property and field path asks this <b>once per node</b>, for both directions,
        /// and keeps the two answers as booleans - <c>Get</c> and <c>Set</c> then test a field rather
        /// than repeating the lookup. That is what keeps §5's promise while letting the two directions
        /// differ: the decision moves to the point of use, the lookup does not.
        /// </remarks>
        internal bool PermitsMember(
            [NotNull] Type receiverType, [NotNull] string memberName, MemberKind kind, MemberAccess access)
        {
            AssertUtils.ArgumentNotNull(receiverType, "receiverType");

            var verdict = VerdictFor(receiverType);

            // Unknown means nobody ruled, and for a type the expression *arrived at* that is
            // trust - §5.2. Reaching is already bounded by what the engineer exposed (§2), unlike
            // naming. Forbid<T>() is what keeps a reachable type out, which is why Denied and
            // Unknown are separate verdicts.
            return verdict.Verdict == SandboxVerdict.Unknown || verdict.Allows(memberName, kind, access);
        }

        // Indexing has no gate of its own, and that took three attempts to get right - see
        // _Docs/type-sandboxing.md §4.2. In short: an "Item" member rule is not implementable,
        // because the two backends resolve nothing in common for xs[0]; and a "the container's type
        // must be reachable" rule is not implementable either, because compiled sees the static type
        // and the interpreter the runtime one, so a container declared IDictionary<,> or object was
        // denied compiled and served interpreted. What governs an indexing operation is the member
        // that produced the container.

        /// <summary>
        /// Whether this policy permits <paramref name="memberName"/> on <paramref name="type"/>,
        /// answering without throwing. For <see cref="SandboxPolicyBuilder.DescribeImplicitTrust"/>,
        /// which is a report rather than a gate.
        /// </summary>
        internal bool PermitsForReport([NotNull] Type type, [NotNull] string memberName)
        {
            return PermitsMember(type, memberName, MemberKind.PropertyOrField, MemberAccess.Read)
                   || PermitsMember(type, memberName, MemberKind.Method, MemberAccess.Read);
        }

        /// <summary>
        /// Whether the catalog has ruled on <paramref name="type"/> at all. False means an expression
        /// that <i>reaches</i> one is trusted with it and nobody said so - which is what
        /// <see cref="SandboxPolicyBuilder.DescribeImplicitTrust"/> exists to surface.
        /// </summary>
        internal bool KnowsForReport([NotNull] Type type)
        {
            return VerdictFor(type).Verdict != SandboxVerdict.Unknown;
        }

        /// <summary>
        /// Throws when <paramref name="collectionType"/> holds items of a type the catalog explicitly
        /// forbids. The gate a collection processor goes through.
        /// </summary>
        /// <remarks>
        /// <b>Only an explicit <c>Forbid&lt;T&gt;()</c> stops a processor</b>, never merely an
        /// uncatalogued item type - which §5.2 answers with trust, and which is the overwhelmingly
        /// common case: an engineer's own model classes are uncatalogued and their collections must
        /// keep sorting. So this asks about <see cref="SandboxVerdict.Denied"/> alone, where the
        /// member gate also acts on <see cref="SandboxVerdict.Catalogued"/>.
        /// <p>
        /// It exists because a processor reaches members the expression never names - <c>CompareTo</c>
        /// for <c>sort()</c>, <c>Equals</c> and <c>GetHashCode</c> for <c>distinct()</c>, an implicit
        /// numeric conversion for <c>sum()</c> - so the member gate cannot see them. It applies to
        /// every processor including <c>count()</c>, which touches nothing: "count() works but sort()
        /// does not" is a distinction nobody can predict.
        /// </p>
        /// </remarks>
        internal void RequireItemTypeIsNotForbidden([NotNull] Type collectionType)
        {
            AssertUtils.ArgumentNotNull(collectionType, "collectionType");

            var itemType = CollectionOperandUtils.GetEnumerableItemType(collectionType);

            if (itemType == null || VerdictFor(itemType).Verdict != SandboxVerdict.Denied)
                return;

            throw new SandboxViolationException(itemType);
        }

        private TypeVerdict Compute([NotNull] Type type)
        {
            // An assembly-level rule first, since it is the coarsest thing anyone can say. Forbidding
            // wins: it is the verb §5.2 made useful, because "trusted unless ruled" means keeping a
            // reachable package out has to be said explicitly.
            if (_forbiddenAssemblies != null && _forbiddenAssemblies.Contains(type.Assembly))
                return TypeVerdict.Denied;

            SandboxCatalogEntry ownEntry;
            var hasOwnEntry = TryGetEntry(type, out ownEntry);

            if (hasOwnEntry && ownEntry.Forbidden)
                return TypeVerdict.Denied;

            // Forbidding runs down the tree, and until 2026-09-07 it did not: Forbid<Stream>() denied a
            // property declared Stream and permitted one declared MemoryStream, because a type nobody
            // had ruled on answered Unknown - which the member gate reads as trust - before any ancestor
            // was looked at. Measured on both backends, and it made every Forbid row narrower than it
            // reads: Forbid<Assembly>() did not cover RuntimeAssembly, Forbid<Delegate>() covered no
            // actual delegate type.
            //
            // Before the allowed-assembly branch, on the same ordering that puts the own-entry refusal
            // there: an explicit refusal beats a blanket allowance.
            if (!hasOwnEntry && InheritsARefusal(type))
                return TypeVerdict.Denied;

            if (_allowedAssemblies != null && _allowedAssemblies.Contains(type.Assembly))
                return TypeVerdict.Unrestricted(hasOwnEntry ? ownEntry.RejectedMembers : null);

            if (!hasOwnEntry)
            {
                // An enum is data: its members are its own named constants plus what System.Enum
                // gives every one of them - ToString, CompareTo, HasFlag, GetTypeCode - and none of
                // that reaches anything. So every enum is nameable, whoever declared it, rather than
                // being catalogued one at a time; T(RegexOptions).IgnoreCase was the single largest
                // cause of red when the default was first flipped, and DayOfWeek as a cast target sat
                // beside it. Cataloguing framework enums by hand would have missed the consumer's own
                // in exactly the same way.
                //
                // Placed *after* the two forbidding checks on purpose, so Forbid<SomeEnum>() and a
                // forbidden assembly still win - an explicit refusal beats a blanket rule, which is
                // the same ordering §8.8 ruled for collection processors.
                if (type.IsEnum)
                    return TypeVerdict.Unrestricted(null);

                // Nobody ruled. The gates answer that differently - denied when the expression named
                // the type, trusted when it arrived at one (§5.2).
                //
                // There used to be an array special case here, giving an uncatalogued array a
                // Catalogued-with-no-members verdict on the grounds that its reachability follows its
                // element type. That predates §5.2 and the positive corpus caught it: `Tags.Length`
                // on a string[] was denied, because "catalogued with no members" denies everything
                // while "nobody ruled" trusts what was reached. Arrays need nothing special now -
                // IsNameable still decomposes them for the *naming* question, which is the only place
                // an element type has to be judged.
                return TypeVerdict.Unknown;
            }

            var rejected = CollectRejected(type, ownEntry);

            // Whole-type allowance short-circuits the member union: there is nothing to collect when
            // every name is permitted anyway.
            if (hasOwnEntry && ownEntry.AllMembers)
                return TypeVerdict.Unrestricted(rejected);

            // Reachability is the type's own entry; the member list is the union up the chain, so an
            // inherited member is listed once where it is declared instead of on every entry that
            // wants it. Computed here, so a member check stays one set lookup (§5).
            var members = SandboxCatalogEntry.NewMap();

            if (hasOwnEntry)
                Union(members, ownEntry.AllowedMembers);

            foreach (var ancestor in Ancestors(type))
            {
                SandboxCatalogEntry entry;
                if (!TryGetEntry(ancestor, out entry))
                    continue;

                // A base type allowed whole lends every one of its members, which is how one
                // AllowAllMembersOf<object>() would hand ToString and Equals to everything below it.
                if (entry.AllMembers)
                    return TypeVerdict.Unrestricted(rejected);

                Union(members, entry.AllowedMembers);
            }

            return TypeVerdict.Catalogued(members, rejected);
        }

        /// <summary>
        /// Whether <paramref name="type"/> inherits a refusal: the nearest ancestor anyone wrote an
        /// entry for forbids it.
        /// </summary>
        /// <remarks>
        /// <b>The nearest entry decides, and <see cref="object"/>'s entry is not one of them.</b> Both
        /// halves were found by measurement rather than by design, and each fixes the other's failure:
        /// <p>
        /// <i>Nearest, not any.</i> A first cut scanned every ancestor and fired on any forbidden one,
        /// which made <c>System.RuntimeType</c> - the class every <c>typeof</c> and <c>GetType()</c>
        /// actually hands you - come out <see cref="SandboxVerdict.Denied"/>, because its chain runs
        /// <c>Type</c> (allowed, with an entry of its own) then <c>MemberInfo</c> (forbidden). So
        /// <c>Type</c> was reachable while its own runtime class was not. Unobservable at the time,
        /// since the nodes gate the <c>Type</c> a value <i>represents</i> rather than its runtime
        /// class - but a boundary that incoherent is waiting for the first path that asks differently.
        /// </p>
        /// <p>
        /// <i>Ignoring <see cref="object"/>.</i> The catalog gives <c>object</c> an entry, for the four
        /// members every type inherits - so "stop at the nearest entry" would stop at <c>object</c> for
        /// every class alive, and inheritance would never fire at all. It is skipped here for that
        /// reason, and it is skipped in exactly the opposite direction by
        /// <see cref="ReasonTheDeclaredTypeIsAmbiguous"/>, which <i>must</i> include it. The two walks
        /// answer different questions - <i>is this type banned?</i> against <i>could a value declared
        /// this way be a banned thing?</i> - so they treat the root of the hierarchy oppositely.
        /// </p>
        /// <p>
        /// <b>Interfaces are consulted only when the base chain named nobody, and only to forbid.</b>
        /// "Nearest" has no meaning across interfaces, which are a set rather than a chain, so an
        /// allowing interface entry cannot stop an inherited refusal the way a base class's can.
        /// </p>
        /// <p>
        /// <b>Entries only, never an ancestor's assembly.</b> Every class descends from
        /// <see cref="object"/>, so consulting ancestors' assemblies would make any
        /// <c>ForbidAssembly</c> naming the core library deny every type in the process.
        /// </p>
        /// </remarks>
        private bool InheritsARefusal([NotNull] Type type)
        {
            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType == typeof(object))
                    continue;

                SandboxCatalogEntry entry;

                if (TryGetEntry(baseType, out entry))
                    return entry.Forbidden;
            }

            foreach (var implemented in type.GetInterfaces())
            {
                SandboxCatalogEntry entry;

                if (TryGetEntry(implemented, out entry) && entry.Forbidden)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Why a member access on a receiver of this <i>declared</i> type has no compiled form, or null
        /// when it has one. The compiled path's half of the question a static type cannot answer.
        /// </summary>
        /// <remarks>
        /// <b>This is not a denial and must never be reported as one.</b> It says the declared type
        /// does not settle whether the access is permitted, so the shape has no compiled form and the
        /// interpreter - which is looking at the value - decides. The caller sees a
        /// <c>CompileErrorException</c>, falls back, and gets either a clean answer or a proper
        /// <see cref="SandboxViolationException"/> from the backend that can tell.
        /// <p>
        /// <b>Why it exists.</b> The compiled gate sees the declared type and the interpreter the
        /// runtime one, so a property declared <c>Stream</c> holding a forbidden <c>FileStream</c> was
        /// permitted compiled and denied interpreted - <b>with the compiled path as the permissive
        /// side</b>, which is the wrong way round for a boundary, since which backend runs is not the
        /// caller's choice. This is the house answer to a question static types cannot settle: refuse
        /// rather than guess, exactly as the overload gate does.
        /// </p>
        /// <p>
        /// <b>Measured cost, before it was built: two corpus expressions and zero tests.</b> Declining
        /// <i>every</i> method call on an <c>object</c>-declared receiver - the worst case, with no
        /// policy condition at all - left both suites fully green and took the corpus from 1,856
        /// compiled expressions to 1,854. The fear that <c>object</c> would decompile broadly was
        /// wrong, and wrong for a reason worth keeping: the set is matched against the receiver's
        /// <b>declared</b> type, not against what a value inherits, and a receiver actually declared
        /// <c>object</c> can only bind the four members <c>object</c> itself declares anyway.
        /// </p>
        /// <p>
        /// <b>Known limit: a forbidden <i>assembly</i> is not covered.</b> Its types cannot be
        /// enumerated, and the honest alternative - treating every non-sealed declared type as
        /// ambiguous whenever any assembly is forbidden - would decompile the world. Recorded rather
        /// than papered over; see <c>_Docs/type-sandboxing.md</c> §5.4.
        /// </p>
        /// </remarks>
        [CanBeNull]
        internal string ReasonTheDeclaredTypeIsAmbiguous([NotNull] Type declaredType)
        {
            AssertUtils.ArgumentNotNull(declaredType, "declaredType");

            if (_ambiguousDeclaredTypes.Count == 0)
                return null;

            if (!_ambiguousDeclaredTypes.Contains(declaredType)
                && !(declaredType.FullName != null
                     && _ambiguousDeclaredTypeNames.Contains(declaredType.FullName)))
            {
                return null;
            }

            return "a receiver declared [" + declaredType
                   + "] could hold a value of a type this sandbox forbids, which only the runtime "
                   + "value settles - so this access has no compiled form and is interpreted";
        }

        /// <summary>
        /// Every declared type through which a forbidden value could arrive: the base types and
        /// interfaces of each forbidden entry, walked once when the policy is built.
        /// </summary>
        /// <remarks>
        /// <b>Walked upward from the ban list, never downward from the loaded types</b>, and that is
        /// the whole reason this is affordable. Going the other way - scanning every loaded type for
        /// descendants of a forbidden one - would mark the universal interfaces (<c>IDisposable</c>
        /// arrives with the first stream, <c>ICloneable</c> and <c>ISerializable</c> with the first
        /// delegate) and decompile broadly; worse, it could never be complete, because assemblies load
        /// lazily and a policy built at startup cannot see a subclass whose assembly loads later. A
        /// boundary whose shape depends on load order is worse than the gap it closes.
        /// <p>
        /// <b>What that costs in precision, stated rather than hidden.</b> The walk is exact for the
        /// case it is built for - forbidding a leaf - because to resolve a member on the declared type
        /// at all, that declared type must lie in the forbidden type's own ancestry, which is
        /// precisely what was walked. It leaks in the mirror case: forbid a <i>base</i>, and a subclass
        /// that adds an unrelated interface can arrive through a property declared as that interface.
        /// Nothing reachable from the ban list predicts it, and closing it would mean treating every
        /// interface-declared receiver as ambiguous.
        /// </p>
        /// <p>
        /// A type that is itself forbidden is removed at the end: it is denied outright by both gates
        /// and never reaches this question.
        /// </p>
        /// </remarks>
        [NotNull]
        private static ISet<Type> ComputeAmbiguousDeclaredTypes(
            [CanBeNull] IDictionary<Type, SandboxCatalogEntry> catalog)
        {
            var ambiguous = new HashSet<Type>();

            if (catalog == null)
                return ambiguous;

            foreach (var pair in catalog)
            {
                if (!pair.Value.Forbidden)
                    continue;

                // object is included here and excluded from InheritsARefusal, deliberately - see the
                // two-walks note on that method.
                for (var baseType = pair.Key.BaseType; baseType != null; baseType = baseType.BaseType)
                    ambiguous.Add(baseType);

                foreach (var implemented in pair.Key.GetInterfaces())
                    ambiguous.Add(implemented);
            }

            foreach (var pair in catalog)
            {
                if (pair.Value.Forbidden)
                    ambiguous.Remove(pair.Key);
            }

            return ambiguous;
        }

        /// <summary>
        /// Every rejection that applies to <paramref name="type"/> - its own and its ancestors' - so
        /// that <c>.Except(...)</c> on a base type is not silently undone by a derived entry.
        /// </summary>
        [CanBeNull]
        private Dictionary<MemberKey, MemberAccess> CollectRejected(
            [NotNull] Type type, [CanBeNull] SandboxCatalogEntry ownEntry)
        {
            Dictionary<MemberKey, MemberAccess> rejected = null;

            if (ownEntry != null && ownEntry.RejectedMembers != null)
            {
                rejected = SandboxCatalogEntry.NewMap();
                Union(rejected, ownEntry.RejectedMembers);
            }

            foreach (var ancestor in Ancestors(type))
            {
                SandboxCatalogEntry entry;
                if (!TryGetEntry(ancestor, out entry) || entry.RejectedMembers == null)
                    continue;

                if (rejected == null)
                    rejected = SandboxCatalogEntry.NewMap();

                Union(rejected, entry.RejectedMembers);
            }

            return rejected;
        }

        /// <summary>
        /// Adds <paramref name="source"/> into <paramref name="target"/>, OR-ing the directions where
        /// both mention a name.
        /// </summary>
        /// <remarks>
        /// OR rather than replace, in both the allowed and the rejected union: a base type permitting
        /// a member for reading and a derived one permitting the same name for writing add up to both,
        /// which is what a reader of two catalog entries expects. The same applies to rejections, and
        /// there it is load-bearing - a base type's <c>ExceptWrite</c> must not be undone by a derived
        /// entry that only mentions reading.
        /// </remarks>
        private static void Union(
            [NotNull] Dictionary<MemberKey, MemberAccess> target,
            [CanBeNull] Dictionary<MemberKey, MemberAccess> source)
        {
            if (source == null)
                return;

            foreach (var pair in source)
            {
                MemberAccess existing;
                target[pair.Key] = target.TryGetValue(pair.Key, out existing)
                    ? existing | pair.Value
                    : pair.Value;
            }
        }

        /// <summary>Base types then interfaces, which is the order a reader expects to see them in.</summary>
        [NotNull]
        private static IEnumerable<Type> Ancestors([NotNull] Type type)
        {
            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
                yield return baseType;

            foreach (var implemented in type.GetInterfaces())
                yield return implemented;
        }

        /// <summary>
        /// The catalog entry for one type. A constructed generic falls back to its open definition,
        /// because that is the form the catalog holds - <c>List&lt;&gt;</c>, not <c>List&lt;int&gt;</c>.
        /// </summary>
        private bool TryGetEntry([NotNull] Type type, out SandboxCatalogEntry entry)
        {
            if (_catalog.TryGetValue(type, out entry) || TryGetEntryByName(type, out entry))
                return true;

            if (!type.IsGenericType || type.IsGenericTypeDefinition)
                return false;

            var definition = type.GetGenericTypeDefinition();

            return _catalog.TryGetValue(definition, out entry)
                   || TryGetEntryByName(definition, out entry);
        }

        /// <summary>
        /// The same entry, found by full type name, for the case where the runtime holds <b>two</b>
        /// <see cref="Type"/> objects for one type and reference identity therefore misses.
        /// </summary>
        /// <remarks>
        /// <b>Measured, and it is not hypothetical.</b> On netcoreapp2.1,
        /// <c>typeof(System.Collections.Hashtable)</c> is
        /// <c>…, System.Runtime.Extensions, Version=4.2.1.0</c> while the name
        /// <c>"System.Collections.Hashtable"</c> resolves to <c>…, System.Private.CoreLib,
        /// Version=4.0.0.0</c> - and <c>Equals</c> between the two is <b>false</b>. So the catalog
        /// entry, added with <c>typeof</c>, was invisible to the type the expression actually named,
        /// and <c>T(System.Collections.Hashtable)</c> was denied on that framework alone while working
        /// on the other four. Exactly one catalogued type is affected there and none anywhere else
        /// (measured over all 29 entries × netcoreapp2.1, net10.0, net472), but a boundary that
        /// depends on which framework is running is the class of bug this fork exists to remove.
        /// <p>
        /// <b>A name collision drops the name rather than guessing.</b> Two types with the same
        /// <see cref="Type.FullName"/> in different assemblies are indistinguishable here, so the
        /// index refuses to hold either - falling back to <see cref="SandboxVerdict.Unknown"/>, which
        /// denies a name and trusts a reachable value, instead of lending one type another's entry.
        /// </p>
        /// <p>
        /// This runs once per type per policy, behind the verdict cache (§5.1), so the steady-state
        /// cost is unchanged: reference identity is still the first and usually only lookup.
        /// </p>
        /// </remarks>
        private bool TryGetEntryByName([NotNull] Type type, out SandboxCatalogEntry entry)
        {
            entry = null;

            var fullName = type.FullName;

            return fullName != null && _catalogByFullName.TryGetValue(fullName, out entry);
        }

        /// <summary>
        /// <see cref="_catalog"/> indexed by <see cref="Type.FullName"/>, with every colliding name
        /// left out. See <see cref="TryGetEntryByName"/> for why it exists.
        /// </summary>
        [NotNull]
        private static IDictionary<string, SandboxCatalogEntry> IndexByFullName(
            [NotNull] IDictionary<Type, SandboxCatalogEntry> catalog)
        {
            var index = new Dictionary<string, SandboxCatalogEntry>(StringComparer.Ordinal);
            var colliding = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in catalog)
            {
                var fullName = pair.Key.FullName;

                if (fullName == null)
                    continue;

                if (index.ContainsKey(fullName))
                {
                    colliding.Add(fullName);
                    continue;
                }

                index.Add(fullName, pair.Value);
            }

            foreach (var name in colliding)
                index.Remove(name);

            return index;
        }

        /// <summary>
        /// The innermost part of <paramref name="type"/> that this policy denies, for the message.
        /// Only ever walked on the failure path.
        /// </summary>
        /// <remarks>
        /// The array branch is unconditional and is right that way <i>because</i>
        /// <see cref="IsNameable"/> asks an array only about its element: an array can now be denied
        /// for one reason only, so recursing into the element always names the real culprit. While
        /// that was not true the message accused permitted types - <c>T(System.DateTime[], mscorlib)</c>
        /// reported that <c>System.DateTime</c> was not permitted, which the same policy allows whole.
        /// The generic branch has always been conditional and stays so, since there the definition
        /// itself can be the denied part.
        /// </remarks>
        [NotNull]
        private Type FirstDeniedPart([NotNull] Type type)
        {
            if (type.IsArray || type.IsByRef || type.IsPointer)
                return FirstDeniedPart(type.GetElementType());

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    if (!IsNameable(argument))
                        return FirstDeniedPart(argument);
                }
            }

            return type;
        }

        /// <summary>
        /// <see cref="Restricted"/> - the built-in catalog, data rather than code, curated from
        /// measurement.
        /// </summary>
        /// <remarks>
        /// <b>Authored through the builder verbs, and that is not tidiness.</b> It poked
        /// <see cref="SandboxCatalogEntry"/> directly until 2026-09-07, which meant the one catalog
        /// that ships was not expressible by the verbs a consumer has, the verbs were unexercised by
        /// the only catalog we author, and any future check in <c>Build()</c> - §5.3's closure rule,
        /// say - would not have applied to the catalog most worth checking.
        /// <p>
        /// <b>Every row names the member's kind</b>, and the kinds were classified by reflection rather
        /// than by eye, because a wrong one denies a member silently. A property row saying
        /// <i>read</i> also cannot be written if a later framework version adds a setter to it, which
        /// is the same forward-looking argument that makes an allow-list safer than a reject-list here.
        /// </p>
        /// <p>
        /// Member names are matched case-insensitively - <see cref="MemberKey"/> does that itself -
        /// because this engine's member binding is case-insensitive and a case-sensitive catalog would
        /// deny a spelling the binder accepts. See <see cref="TypeVerdict.Allows"/>.
        /// </p>
        /// <p>
        /// And every addition drags its return types in with it (§5.3's closure rule): permitting
        /// <c>Environment.OSVersion</c> is pointless unless <c>OperatingSystem</c> is catalogued too,
        /// or the caller is handed an object every member of which is denied. That rule is a budget -
        /// a reach that goes too far announces itself as a chain of additions rather than one line.
        /// </p>
        /// </remarks>
        [NotNull]
        private static SandboxPolicy BuildRestrictedPolicy()
        {
            var builder = SandboxPolicyBuilder.StartingFromNothing();

            // §5.2 made this mandatory rather than merely advisable, and the negative corpus found
            // out: a type nobody has ruled on is *trusted* when an expression arrives at one, and
            // every object alive can produce a System.Type through GetType(). Without this entry
            // 'abc'.GetType().Assembly walks straight through - the escape the whole design exists to
            // close, reopened by the fallback that makes the rest of it usable.
            //
            // Descriptive members only, per §3: what the type is called, not what can be done with
            // it. No Assembly, no Module, no GetMethod*/GetProperty*/GetConstructor*/InvokeMember.
            // Classified by reflection rather than by eye - 14 read-only properties and 6 methods,
            // measured. Saying which is which costs nothing and makes the entry state its intent: not
            // one of these properties is settable, and nothing here may be written even if a future
            // framework adds a setter to one of them.
            builder.AllowPropertyOrFieldRead(
                typeof(Type),
                "Name", "FullName", "Namespace", "AssemblyQualifiedName",
                "IsEnum", "IsArray", "IsValueType", "IsClass", "IsInterface",
                "IsAbstract", "IsSealed", "IsPrimitive", "IsGenericType",
                "BaseType");

            builder.AllowMethod(
                typeof(Type),
                "IsSubclassOf", "IsInstanceOfType", "IsAssignableFrom",
                "ToString", "Equals", "GetHashCode");

            // System.Object must be catalogued - `T(System.Object)` and `x is T(object)` are ordinary
            // expressions - and it is the one type that must NOT be allowed whole, however harmless
            // its four members look. Compute unions the entries up the ancestor chain and returns
            // Unrestricted the moment an ancestor allows everything, so AllowAllMembersOf here would
            // hand every *catalogued* type an unrestricted verdict - System.Type included, and with it
            // Assembly and Assembly.Load. One line would have undone the whole design.
            //
            // Listing the four by name costs nothing, because they are what every type inherits and a
            // type allowed whole already has them. GetType belongs here rather than being repeated on
            // every entry, which is what the receiver-unions-its-ancestors rule is for.
            // All four are methods - object declares no properties at all.
            builder.AllowMethod(typeof(object), "ToString", "Equals", "GetHashCode", "GetType");

            // Defence in depth: these are reachable only through members System.Type no longer
            // permits, so nothing should get to them - but a forbidden type costs one dictionary
            // entry and removes any dependence on that reasoning staying true.
            foreach (var forbidden in new[]
                     {
                         typeof(System.Reflection.Assembly),
                         typeof(System.Reflection.Module),
                         typeof(System.Reflection.MemberInfo),
                         typeof(System.Reflection.MethodBase),
                         typeof(System.Reflection.ConstructorInfo),
                         typeof(System.Reflection.MethodInfo),
                         typeof(System.Reflection.PropertyInfo),
                         typeof(System.Reflection.FieldInfo),
                         typeof(AppDomain),
                         typeof(Activator),
                         typeof(GC),
                         typeof(Delegate)
                     })
            {
                builder.Forbid(forbidden);
            }

            // The effect types. §5.3's rule 1 - "never catalogue a type whose purpose is an effect" -
            // protected both routes when it was written, and protects only *naming* since §5.2 ruled
            // that a type an expression merely reaches is trusted. So a FileStream a model handed back
            // was usable: measured, myOrder.Log.WriteByte(65) wrote and Current.ProcessName read.
            // Forbidding is what keeps them out of the reached route as well.
            //
            // **Only a type a model can hand back needs a row.** A static-only type - System.IO.File,
            // Directory, Path - is reachable in exactly one way, by naming it, and naming an
            // uncatalogued type is already denied. There are no instances of it for a model to expose,
            // so a forbid row would buy nothing. That is what keeps this list short, and it is the
            // question to ask before adding to it.
            //
            // **No closure rule applies here**, unlike an allowance: forbidding a type says nothing
            // about what its members return, because none of them can be reached. §5.3's rule 3 is a
            // budget on permitting, not on refusing. (An earlier note in this work claimed 1a-ii would
            // need "closing the catalog over what they return" - that was the allow-side rule applied
            // to the wrong direction.)
            //
            // **Bases, not leaves**, since forbidding runs down the tree (§5.4): Stream covers
            // FileStream and NetworkStream, TextWriter covers StreamWriter, FileSystemInfo covers
            // FileInfo and DirectoryInfo. A consumer who wants one subtree member back writes an entry
            // for it - Forbid<Stream>() with Allow<MemoryStream>(...) is the pinned shape - and the
            // default here deliberately does not do that for them.
            //
            // **This is a breaking change for a consumer whose model exposes one of these**, and it is
            // the intended one: the sandbox was never protecting them, and now it does.
            foreach (var effect in new[]
                     {
                         // Filesystem and network writing, and everything derived from them.
                         typeof(System.IO.Stream),
                         typeof(System.IO.TextWriter),
                         typeof(System.IO.TextReader),

                         // FileInfo and DirectoryInfo - names, sizes and Delete().
                         typeof(System.IO.FileSystemInfo),

                         typeof(System.Diagnostics.Process),
                         typeof(System.Threading.Thread),
                         typeof(System.Net.Sockets.Socket)
                     })
            {
                builder.Forbid(effect);
            }

            // System.Environment, curated rather than forbidden - which is what §5.3 uses it as the
            // worked example of, and §6.3 always specified. It shipped forbidden outright, so
            // T(System.Environment).NewLine was denied along with everything else; neither suite
            // noticed, because nothing uses it and the one test that names the type registers it to
            // typeof(int) first.
            //
            // Audited rather than guessed: 25 public static properties and 9 method names on net10.0.
            // The allow-list below is 11 entries where a reject-list would be 23 - which corrects
            // §6.1's guess that this type wanted the reject direction - and the allow direction is the
            // safer one here for a second reason: the framework keeps *adding* to Environment
            // (CpuUsage, ProcessPath, IsPrivilegedProcess, TickCount64 are all recent), and a
            // reject-list would silently admit whatever the next version brings.
            //
            // Names absent on older frameworks cost nothing: the gate is keyed by name, so a member
            // that does not exist is never asked about, and one list serves all five targets.
            //
            // Every one of them is a readable property, so the verb says so: nothing here may be
            // written and nothing here may be called.
            builder.AllowPropertyOrFieldRead(
                typeof(Environment),
                "NewLine", "ProcessorCount", "Is64BitProcess", "Is64BitOperatingSystem",
                "OSVersion", "Version", "TickCount", "TickCount64", "SystemPageSize",
                "HasShutdownStarted", "IsPrivilegedProcess");

            // Required by §5.3's closure rule: Environment.OSVersion returns one of these, and a
            // permitted member whose return type is uncatalogued hands the caller an inert object.
            // The "am I on Linux or Windows?" case.
            builder.AllowAllMembersOf(typeof(OperatingSystem));

            // The nameable half, driven row by row by the positive corpus (§6.3, §8.1 stage 4).
            //
            // Two things to know before adding to it. Nothing may be *named* unless it is catalogued,
            // which is why an entry is needed at all for `new DateTime(…)` or `T(Math)`. And
            // cataloguing a type to make it nameable also switches its *members* from trusted to
            // governed - `string` had to be catalogued for `new System.String[] {…}`, and that alone
            // put `Customer.Name.Length` under the catalog's rule. So a type catalogued for naming
            // almost always wants AllMembers unless there is a reason to curate it.
            //
            // AllMembers is a bet, and §6.2 states it: the type gains whatever a future framework
            // version adds. Taken here for the pure ones - values, maths, text - and refused for
            // CultureInfo, which has a settable static that changes the whole process (§5.3).
            foreach (var whole in new[]
                     {
                         typeof(bool), typeof(char), typeof(string),
                         typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
                         typeof(int), typeof(uint), typeof(long), typeof(ulong),
                         typeof(float), typeof(double), typeof(decimal),
                         typeof(DateTime), typeof(TimeSpan), typeof(DateTimeOffset), typeof(Guid),
                         typeof(Math), typeof(Convert), typeof(Version), typeof(Array),
                         typeof(System.Text.StringBuilder),
                         typeof(System.Globalization.Calendar),
                         typeof(System.Globalization.GregorianCalendar),
                         typeof(System.Globalization.NumberFormatInfo),
                         typeof(System.Globalization.DateTimeFormatInfo),
                         typeof(IFormatProvider),
                         typeof(List<>), typeof(Dictionary<,>), typeof(HashSet<>),
                         typeof(System.Collections.ArrayList), typeof(System.Collections.Hashtable),

                         // The interfaces the language itself hands out, and the wrapper it hands out
                         // as often. `Ints is T(IList<int>)` and `T(int?)` are ordinary expressions,
                         // and both were refused: only the concrete classes had been catalogued, so a
                         // collection named by its interface was denied while the same collection
                         // named by its class was allowed. Generic and non-generic spellings are
                         // listed as pairs deliberately - catalogueing one of a pair is the
                         // GetCultures/CreateSpecificCulture inconsistency §8.8 already recorded once.
                         typeof(Nullable<>),
                         typeof(System.Collections.IEnumerable),
                         typeof(System.Collections.ICollection),
                         typeof(System.Collections.IList),
                         typeof(System.Collections.IDictionary),
                         typeof(IEnumerable<>), typeof(ICollection<>), typeof(IList<>),
                         typeof(IDictionary<,>), typeof(ISet<>), typeof(KeyValuePair<,>),

                         // A parser with no effects, and §4.5's own worked example of a curated entry
                         // - which is why it reads oddly as a whole one. Every member of Uri is
                         // descriptive: it takes a string apart and hands back strings, ints and its
                         // own enums. Nothing on it opens, connects or loads.
                         typeof(Uri),

                         // @[Serializable]. An attribute type is reached by name like any other, so it
                         // needs a row; this is the only framework attribute either suite names.
                         // Whether attribute types should be nameable *as a class* - the way enums now
                         // are - is deliberately left to the naming ruling, since an attribute is an
                         // arbitrary type where an enum is data.
                         typeof(SerializableAttribute)
                     })
            {
                builder.AllowAllMembersOf(whole);
            }

            // CultureInfo is the one formatting type that cannot be allowed whole, and §5.3 has the
            // measurement: setting CurrentCulture through an expression changed every subsequent
            // ToString('C') in the process. Reading it is fine and genuinely wanted; writing it is an
            // effect on unrelated code. CreateSpecificCulture is refused with it, because the culture
            // it hands back is writable - the two are only dangerous together.
            var culture = typeof(System.Globalization.CultureInfo);

            builder.AllowAllMembersOf(culture);

            // CurrentCulture and CurrentUICulture are readable and not settable, which is what §6.3
            // asked for and what refusing them outright could not express until the access axis
            // existed. Reading the culture is the ordinary thing an expression wants; installing one
            // changes every subsequent format in the process (§5.3's measurement).
            builder.ExceptPropertyOrFieldWrite(
                culture, "CurrentCulture", "CurrentUICulture");

            // The thread defaults are settable statics with the same effect one scope out, and are
            // refused in both directions rather than made read-only: nobody reads them to render a
            // report, so there is no reader to preserve.
            builder.ExceptPropertyOrField(
                culture, "DefaultThreadCurrentCulture", "DefaultThreadCurrentUICulture");

            // These three are methods, and saying so is the point of the verb - the version of this
            // list that predated MemberKind refused them "in both directions", which for a method
            // means nothing in particular.
            //
            // GetCultures was found by DescribeImplicitTrust rather than by review, and it is an
            // inconsistency rather than a hole: it hands back *writable* CultureInfo instances,
            // exactly as CreateSpecificCulture does, and that is why the latter was rejected. Neither
            // can do harm while the CurrentCulture setter is refused - there is nowhere to install a
            // mutated culture - so this is defence in depth on both, or on neither. Rejecting the pair
            // costs one rarely-wanted member and removes the need to keep that reasoning true.
            builder.ExceptMethod(
                culture, "CreateSpecificCulture", "ClearCachedData", "GetCultures");

            return builder.Build();
        }

        /// <summary>Null means "allow everything"; a table means "only what is in it".</summary>
        [CanBeNull]
        private readonly IDictionary<Type, SandboxCatalogEntry> _catalog;

        /// <summary>
        /// <see cref="_catalog"/> keyed by <see cref="Type.FullName"/>, consulted only when reference
        /// identity misses. See <see cref="TryGetEntryByName"/>.
        /// </summary>
        [NotNull]
        private readonly IDictionary<string, SandboxCatalogEntry> _catalogByFullName;

        [NotNull]
        private static readonly IDictionary<string, SandboxCatalogEntry> EmptyNameIndex =
            new Dictionary<string, SandboxCatalogEntry>(StringComparer.Ordinal);

        /// <summary>
        /// Declared types through which a forbidden value could arrive - see
        /// <see cref="ComputeAmbiguousDeclaredTypes"/>. Empty whenever nothing is forbidden, which is
        /// what makes <see cref="ReasonTheDeclaredTypeIsAmbiguous"/> free for a policy that bans
        /// nothing.
        /// </summary>
        [NotNull]
        private readonly ISet<Type> _ambiguousDeclaredTypes;

        [NotNull]
        private readonly ISet<string> _ambiguousDeclaredTypeNames;

        /// <summary>Assemblies every type of which is unrestricted, or null.</summary>
        [CanBeNull]
        private readonly ISet<Assembly> _allowedAssemblies;

        /// <summary>Assemblies no type of which may be named or reached, or null.</summary>
        [CanBeNull]
        private readonly ISet<Assembly> _forbiddenAssemblies;

        /// <summary>
        /// Keyed on <see cref="Type"/>, so the lookup is reference equality - the fastest key available.
        /// </summary>
        [NotNull]
        private readonly ConcurrentDictionary<Type, TypeVerdict> _verdicts =
            new ConcurrentDictionary<Type, TypeVerdict>();
    }
}

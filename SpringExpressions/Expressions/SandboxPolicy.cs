using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
    /// one input to it - and a policy is consulted at two gates, one for type names and one for members.
    /// Neither gate exists yet; see <c>_Docs/type-sandboxing.md</c> §8 for the order of work.
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
    /// <b>The default is on, for both API layers</b> - the inherited weakly typed surface included, not
    /// only the strongly typed one. A per-overload default would put the permissive setting exactly
    /// where the risk is highest, make "is this sandboxed?" an invisible property of the call site, and
    /// keep the frozen suite green by construction - so the largest breaking change on the backlog would
    /// become the one change that suite cannot see. See §3.5; a split remains the fallback that stage
    /// 4's measurement may force, and is not the design.
    /// </p>
    /// <p>
    /// <b>Not yet in force.</b> <see cref="Default"/> is <see cref="DangerouslyAllowEverything"/> until
    /// the gates and the catalog are built, so nothing about today's behaviour has changed. Flipping it
    /// is the last step (§8.1, stage 5).
    /// </p>
    /// <p>
    /// <b>The constructor is private while <see cref="Default"/>'s setter is public, and that
    /// asymmetry is deliberate <i>for now</i>.</b> The value space is currently the two built-ins
    /// below, which is all the setter needs to do its job - its purpose is the one-line escape,
    /// <c>SandboxPolicy.Default = SandboxPolicy.DangerouslyAllowEverything</c>. How a consumer defines
    /// a policy of their own is <b>ruled and not built</b>: a builder,
    /// <c>SandboxPolicy.NewBasedOn(...).Allow&lt;Uri&gt;(nameof(Uri.Host)).TrustAssemblyOf&lt;Order&gt;().Build()</c>,
    /// arriving at stage 4. A fluent <c>Allowing(...)</c> on the policy itself was rejected - it reads
    /// as mutation of the instance it is called on and carries the discarded-result trap, and more
    /// importantly it leaves nowhere to check §5.3's closure rule, since every intermediate would
    /// already be a complete policy. <c>Build()</c> is that one place. See §4.5.
    /// </p>
    /// <p>
    /// Members are named by <b>string</b>, with <c>nameof</c> as the intended spelling - properties,
    /// fields and methods in one list, because the gate is keyed by name and cannot tell them apart
    /// (<see cref="TypeVerdict.Allows"/> is the whole check). A mock-library-style
    /// <c>Allow&lt;Uri&gt;(u =&gt; u.Host)</c> was rejected: it promises overload granularity this
    /// gate cannot keep, and it does not compile at all for a static class - <c>Math</c>,
    /// <c>Environment</c> and <c>Convert</c> are static, and CS0718 forbids a static type as a type
    /// argument. That is also why <c>Allow(Type, params string[])</c> is the primary form and the
    /// generic one is sugar.
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

        private static readonly SandboxPolicy AllowEverythingPolicy = new SandboxPolicy(null);

        private static readonly SandboxPolicy RestrictedPolicy = new SandboxPolicy(BuildCatalog());

        private static SandboxPolicy _default = AllowEverythingPolicy;

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
        /// <b>The catalog is empty at this stage</b>, so this policy currently denies every type. That
        /// is deliberate: both gates are built against a stub, and the catalog is curated last, from
        /// what the two test suites reject once the gates are live - the red list is its specification.
        /// See §8.2.
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
                return TypeVerdict.Unrestricted;

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

            return new SandboxPolicyBuilder(policy._catalog);
        }

        private SandboxPolicy([CanBeNull] IDictionary<Type, HashSet<string>> catalog)
        {
            _catalog = catalog;
        }

        /// <summary>The policy a <see cref="SandboxPolicyBuilder"/> produces.</summary>
        [NotNull]
        internal static SandboxPolicy WithCatalog([NotNull] IDictionary<Type, HashSet<string>> catalog)
        {
            return new SandboxPolicy(catalog);
        }

        /// <summary>
        /// Throws if this policy does not permit <paramref name="type"/>. The gate every expression
        /// type name goes through.
        /// </summary>
        /// <remarks>
        /// The exception names the part that was actually denied rather than the composite that
        /// contained it, so <c>T(List&lt;Process&gt;)</c> reports <c>Process</c>.
        /// </remarks>
        internal void DemandTypeIsPermitted([NotNull] Type type)
        {
            AssertUtils.ArgumentNotNull(type, "type");

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
        /// </remarks>
        private bool IsNameable([NotNull] Type type)
        {
            if (VerdictFor(type).Verdict == SandboxVerdict.Denied)
                return false;

            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsNameable(type.GetElementType());

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
        internal void DemandMemberIsPermitted([NotNull] Type receiverType, [NotNull] string memberName)
        {
            AssertUtils.ArgumentNotNull(receiverType, "receiverType");

            if (VerdictFor(receiverType).Allows(memberName))
                return;

            throw new SandboxViolationException(receiverType, memberName);
        }

        // Indexing has no gate of its own, and that took three attempts to get right - see
        // _Docs/type-sandboxing.md §4.2. In short: an "Item" member rule is not implementable,
        // because the two backends resolve nothing in common for xs[0]; and a "the container's type
        // must be reachable" rule is not implementable either, because compiled sees the static type
        // and the interpreter the runtime one, so a container declared IDictionary<,> or object was
        // denied compiled and served interpreted. What governs an indexing operation is the member
        // that produced the container.

        private TypeVerdict Compute([NotNull] Type type)
        {
            // Still owed here, and it belongs to the catalog stage rather than to this one: the
            // trusted-assembly test of §5.2, which is what makes the caller's own types unrestricted
            // without any configuration. Until it exists, only catalogued types are reachable.
            HashSet<string> ownEntry;
            var hasOwnEntry = TryGetEntry(type, out ownEntry);

            // An array is reachable when its element type is, so nobody has to catalogue int[],
            // string[] and every other instantiation by hand. Its *members* still come from the
            // union below, where System.Array's entry is the useful one: one Allow(typeof(Array),
            // nameof(Array.Length)) covers every array rather than one entry per element type.
            var reachableAsComposite =
                (type.IsArray || type.IsByRef || type.IsPointer)
                && VerdictFor(type.GetElementType()).Verdict != SandboxVerdict.Denied;

            if (!hasOwnEntry && !reachableAsComposite)
                return TypeVerdict.Denied;

            if (!hasOwnEntry)
                ownEntry = EmptyEntry;

            // Reachability is the type's own entry; the member list is the union up the chain, so an
            // inherited member is listed once where it is declared instead of on every entry that
            // wants it. Computed here, so a member check stays one set lookup (§5).
            var members = new HashSet<string>(ownEntry, StringComparer.OrdinalIgnoreCase);

            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                HashSet<string> baseEntry;
                if (TryGetEntry(baseType, out baseEntry))
                    members.UnionWith(baseEntry);
            }

            foreach (var implemented in type.GetInterfaces())
            {
                HashSet<string> interfaceEntry;
                if (TryGetEntry(implemented, out interfaceEntry))
                    members.UnionWith(interfaceEntry);
            }

            return TypeVerdict.Catalogued(members);
        }

        /// <summary>
        /// The catalog entry for one type. A constructed generic falls back to its open definition,
        /// because that is the form the catalog holds - <c>List&lt;&gt;</c>, not <c>List&lt;int&gt;</c>.
        /// </summary>
        private bool TryGetEntry([NotNull] Type type, out HashSet<string> entry)
        {
            if (_catalog.TryGetValue(type, out entry))
                return true;

            return type.IsGenericType
                   && !type.IsGenericTypeDefinition
                   && _catalog.TryGetValue(type.GetGenericTypeDefinition(), out entry);
        }

        /// <summary>
        /// The innermost part of <paramref name="type"/> that this policy denies, for the message.
        /// Only ever walked on the failure path.
        /// </summary>
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
        /// The built-in catalog - data, not code, and empty until stage 4 curates it from measurement.
        /// </summary>
        /// <remarks>
        /// Every member set added here must be built with <see cref="StringComparer.OrdinalIgnoreCase"/>,
        /// because this engine's member binding is case-insensitive and a case-sensitive catalog would
        /// deny a spelling the binder accepts. See <see cref="TypeVerdict.Allows"/>.
        /// <p>
        /// And every addition drags its return types in with it (§5.3's closure rule): permitting
        /// <c>Environment.OSVersion</c> is pointless unless <c>OperatingSystem</c> is catalogued too,
        /// or the caller is handed an object every member of which is denied. That rule is a budget -
        /// a reach that goes too far announces itself as a chain of additions rather than one line.
        /// </p>
        /// </remarks>
        [NotNull]
        private static IDictionary<Type, HashSet<string>> BuildCatalog()
        {
            return new Dictionary<Type, HashSet<string>>();
        }

        /// <summary>Null means "allow everything"; a table means "only what is in it".</summary>
        [CanBeNull]
        private readonly IDictionary<Type, HashSet<string>> _catalog;

        /// <summary>
        /// Keyed on <see cref="Type"/>, so the lookup is reference equality - the fastest key available.
        /// </summary>
        [NotNull]
        private static readonly HashSet<string> EmptyEntry
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [NotNull]
        private readonly ConcurrentDictionary<Type, TypeVerdict> _verdicts =
            new ConcurrentDictionary<Type, TypeVerdict>();
    }
}

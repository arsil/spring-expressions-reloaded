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

        private SandboxPolicy([CanBeNull] IDictionary<Type, HashSet<string>> catalog)
        {
            _catalog = catalog;
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

            if (VerdictFor(type).Verdict != SandboxVerdict.Denied)
                return;

            throw new SandboxViolationException(FirstDeniedPart(type));
        }

        private TypeVerdict Compute([NotNull] Type type)
        {
            // A composite type is reachable only if every part of it is, and this is the only place
            // the parts can be judged. Measured: GenericTypeResolver resolves the generic definition,
            // each type argument and an array's item type through the *ungated*
            // TypeResolutionUtils.ResolveType (GenericTypeResolver.cs:62, :69, :82), so a gated entry
            // point does not reach them - T(List<Process>) would otherwise pass on List<>'s catalog
            // entry alone. §4.1 assumed a gated call covered generic arguments; it does not, which is
            // exactly what §9 asked to be pinned rather than taken on trust.
            if (type.IsArray || type.IsByRef || type.IsPointer)
            {
                // Reachability follows the element type. The member question - is Length allowed on
                // int[]? - is stage 3's, and an empty list is the fail-closed answer until it rules.
                return VerdictFor(type.GetElementType()).Verdict == SandboxVerdict.Denied
                    ? TypeVerdict.Denied
                    : TypeVerdict.Catalogued(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    if (VerdictFor(argument).Verdict == SandboxVerdict.Denied)
                        return TypeVerdict.Denied;
                }

                // The catalog holds open definitions - List<>, Dictionary<,> - so List<int> is
                // judged by List<>'s entry once its arguments have passed.
                return VerdictFor(type.GetGenericTypeDefinition());
            }

            // Still owed here, and it belongs to the catalog stage rather than to this one: the
            // trusted-assembly test of §5.2, which is what makes the caller's own types unrestricted
            // without any configuration. Until it exists, only catalogued types are reachable.
            HashSet<string> allowedMembers;

            return _catalog.TryGetValue(type, out allowedMembers)
                ? TypeVerdict.Catalogued(allowedMembers)
                : TypeVerdict.Denied;
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
                    if (VerdictFor(argument).Verdict == SandboxVerdict.Denied)
                        return FirstDeniedPart(argument);
                }

                var definition = type.GetGenericTypeDefinition();

                return VerdictFor(definition).Verdict == SandboxVerdict.Denied ? definition : type;
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
        private readonly ConcurrentDictionary<Type, TypeVerdict> _verdicts =
            new ConcurrentDictionary<Type, TypeVerdict>();
    }
}

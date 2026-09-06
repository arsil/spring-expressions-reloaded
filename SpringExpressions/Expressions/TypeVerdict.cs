using System;
using System.Collections.Generic;

using JetBrains.Annotations;

namespace SpringExpressions
{
    /// <summary>
    /// What a <see cref="SandboxPolicy"/> has decided about one type.
    /// </summary>
    internal enum SandboxVerdict
    {
        /// <summary>
        /// The catalog has no entry for this type, and the two gates answer that differently - which
        /// is the whole of <c>_Docs/type-sandboxing.md</c> §5.2. A type an expression <b>names</b>
        /// is denied, because naming is unbounded: <c>TypeResolver</c> will <c>Assembly.Load</c>
        /// something that was never in the process. A type an expression <b>arrives at</b>, as the
        /// receiver of a member access, is trusted: reaching is bounded by what the engineer chose to
        /// expose, which is §2's principle.
        /// <p>
        /// So this is not "denied" and it is not "allowed" - it is "nobody ruled", and only the gate
        /// knows which question is being asked.
        /// </p>
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Every member is reachable, save any the entry rejects. Either the type was catalogued
        /// whole, or its assembly was.
        /// </summary>
        Unrestricted = 1,

        /// <summary>
        /// Reachable, but only the members the catalog lists. This is the interesting case:
        /// <c>System.Environment</c> declares <c>MachineName</c> and <c>Exit</c> alike, and
        /// <c>CultureInfo</c> declares <c>InvariantCulture</c> beside a <c>CurrentCulture</c> setter
        /// that changes the whole process (§5.3).
        /// </summary>
        Catalogued = 2,

        /// <summary>
        /// Explicitly forbidden - <c>Forbid&lt;T&gt;()</c>, or its assembly was. Denied to both gates,
        /// named or reached.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="Unknown"/> since §5.2, and that distinction is what makes
        /// <c>Forbid</c> load-bearing rather than decorative: under the old pure allow-list, not
        /// catalogued already meant denied everywhere, so forbidding said nothing. Now it is the only
        /// way to keep a type out that an expression can <i>reach</i>.
        /// </remarks>
        Denied = 3
    }

    /// <summary>
    /// A <see cref="SandboxVerdict"/> together with the member lists it needs, computed once per type
    /// per policy and cached - see <c>_Docs/type-sandboxing.md</c> §5.1.
    /// </summary>
    internal readonly struct TypeVerdict
    {
        /// <summary>Nobody ruled on this type. The gate decides what that means (§5.2).</summary>
        public static readonly TypeVerdict Unknown =
            new TypeVerdict(SandboxVerdict.Unknown, null, null);

        /// <summary>Explicitly forbidden, to both gates.</summary>
        public static readonly TypeVerdict Denied =
            new TypeVerdict(SandboxVerdict.Denied, null, null);

        /// <summary>
        /// Every member, except <paramref name="rejectedMembers"/> where that is given - the
        /// reject-list direction, for a type where listing what is unsafe is shorter than listing what
        /// is not.
        /// </summary>
        public static TypeVerdict Unrestricted([CanBeNull] HashSet<string> rejectedMembers)
        {
            return new TypeVerdict(SandboxVerdict.Unrestricted, null, rejectedMembers);
        }

        /// <summary>
        /// Only <paramref name="allowedMembers"/>, less anything also rejected. The allow-list
        /// direction, for a type where most of the surface is the thing being defended against.
        /// </summary>
        public static TypeVerdict Catalogued(
            [NotNull] HashSet<string> allowedMembers, [CanBeNull] HashSet<string> rejectedMembers)
        {
            if (allowedMembers == null)
                throw new ArgumentNullException(nameof(allowedMembers));

            return new TypeVerdict(SandboxVerdict.Catalogued, allowedMembers, rejectedMembers);
        }

        public SandboxVerdict Verdict { get; }

        /// <summary>
        /// Whether this member may be used, for a type the catalog has ruled on. <b>False for
        /// <see cref="SandboxVerdict.Unknown"/></b> - a caller holding an unknown verdict must decide
        /// by route rather than ask this, which is why the gates and not the verdict own §5.2.
        /// </summary>
        /// <remarks>
        /// The catalog builds these sets with <see cref="StringComparer.OrdinalIgnoreCase"/>, because
        /// this engine's member binding is case-insensitive: <c>Upper('x')</c>, <c>upper('x')</c> and
        /// <c>UPPER('x')</c> all resolve the same method. A case-sensitive catalog would deny a
        /// spelling the binder accepts, which is a gate disagreeing with the thing it gates. Whether
        /// the language should be case-insensitive at all is <c>_Docs/open-issues.md</c> item 11.
        /// <p>
        /// A rejection wins over an allowance, so <c>.Except(...)</c> means what it says whether the
        /// name arrived from this type's own entry or from a base type's.
        /// </p>
        /// </remarks>
        public bool Allows([CanBeNull] string memberName)
        {
            if (memberName == null)
                return false;

            if (_rejected != null && _rejected.Contains(memberName))
                return false;

            switch (Verdict)
            {
                case SandboxVerdict.Unrestricted:
                    return true;

                case SandboxVerdict.Catalogued:
                    return _allowed.Contains(memberName);

                default:
                    return false;
            }
        }

        private TypeVerdict(
            SandboxVerdict verdict,
            [CanBeNull] HashSet<string> allowed,
            [CanBeNull] HashSet<string> rejected)
        {
            Verdict = verdict;
            _allowed = allowed;
            _rejected = rejected;
        }

        /// <summary>Null for every verdict but <see cref="SandboxVerdict.Catalogued"/>.</summary>
        [CanBeNull]
        private readonly HashSet<string> _allowed;

        /// <summary>Null unless the entry rejected something.</summary>
        [CanBeNull]
        private readonly HashSet<string> _rejected;
    }
}

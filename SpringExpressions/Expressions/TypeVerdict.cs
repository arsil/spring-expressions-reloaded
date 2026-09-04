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
        /// The type and all of its members are reachable. Either the sandbox is off, or this is one of
        /// the caller's own types rather than a framework one.
        /// </summary>
        Unrestricted = 0,

        /// <summary>
        /// The type is reachable, but only the members the catalog lists. This is the interesting case:
        /// <c>System.Environment</c> declares <c>MachineName</c> and <c>Exit</c> alike, so a per-type
        /// allow-list on its own would hand over both.
        /// </summary>
        Catalogued = 1,

        /// <summary>The type is not reachable at all, by name or by navigation.</summary>
        Denied = 2
    }

    /// <summary>
    /// A <see cref="SandboxVerdict"/> together with the member list it needs, computed once per type per
    /// policy and cached - see <c>_Docs/type-sandboxing.md</c> §5.1.
    /// </summary>
    internal readonly struct TypeVerdict
    {
        /// <summary>Everything on this type is reachable.</summary>
        public static readonly TypeVerdict Unrestricted =
            new TypeVerdict(SandboxVerdict.Unrestricted, null);

        /// <summary>The type is not reachable.</summary>
        public static readonly TypeVerdict Denied =
            new TypeVerdict(SandboxVerdict.Denied, null);

        /// <summary>
        /// The type is reachable and <paramref name="allowedMembers"/> is what may be used on it. The
        /// set carries its own comparer, which is what decides the case question in
        /// <see cref="Allows"/>.
        /// </summary>
        public static TypeVerdict Catalogued([NotNull] HashSet<string> allowedMembers)
        {
            if (allowedMembers == null)
                throw new ArgumentNullException(nameof(allowedMembers));

            return new TypeVerdict(SandboxVerdict.Catalogued, allowedMembers);
        }

        public SandboxVerdict Verdict { get; }

        /// <summary>
        /// True when this member may be used. Always true for <see cref="SandboxVerdict.Unrestricted"/>
        /// and always false for <see cref="SandboxVerdict.Denied"/>, so a caller holding a verdict needs
        /// no second branch of its own.
        /// </summary>
        /// <remarks>
        /// The catalog builds these sets with <see cref="StringComparer.OrdinalIgnoreCase"/>, because
        /// this engine's member binding is case-insensitive: <c>Upper('x')</c>, <c>upper('x')</c> and
        /// <c>UPPER('x')</c> all resolve the same method, inherited from <c>Type.GetMethod</c>'s
        /// <c>IgnoreCase</c> lookup and preserved by the candidate scan. A case-sensitive catalog would
        /// deny a spelling the binder accepts, which is a gate disagreeing with the thing it gates.
        /// Whether the language should be case-insensitive at all is <c>_Docs/open-issues.md</c> item
        /// 11 - a question nobody has ruled; if it is ever ruled the other way, the comparer moves with
        /// it.
        /// </remarks>
        public bool Allows([CanBeNull] string memberName)
        {
            switch (Verdict)
            {
                case SandboxVerdict.Unrestricted:
                    return true;

                case SandboxVerdict.Catalogued:
                    return memberName != null && _allowedMembers.Contains(memberName);

                default:
                    return false;
            }
        }

        private TypeVerdict(SandboxVerdict verdict, [CanBeNull] HashSet<string> allowedMembers)
        {
            Verdict = verdict;
            _allowedMembers = allowedMembers;
        }

        /// <summary>Null for every verdict but <see cref="SandboxVerdict.Catalogued"/>.</summary>
        [CanBeNull]
        private readonly HashSet<string> _allowedMembers;
    }
}

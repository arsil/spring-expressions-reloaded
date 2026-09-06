using System;
using System.Collections.Generic;

using JetBrains.Annotations;

namespace SpringExpressions
{
    /// <summary>
    /// What a catalog says about one type: forbidden, whole, or a member list in either direction.
    /// </summary>
    /// <remarks>
    /// Mutable while a <see cref="SandboxPolicyBuilder"/> is running and copied into an immutable
    /// policy by <c>Build()</c>. See <c>_Docs/type-sandboxing.md</c> §6.1 for why both member
    /// directions exist: for <c>System.Type</c> an allow-list is six names and a reject-list would be
    /// dozens; for <c>System.Environment</c> it is the other way round.
    /// </remarks>
    internal sealed class SandboxCatalogEntry
    {
        internal SandboxCatalogEntry()
        {
        }

        internal SandboxCatalogEntry([NotNull] SandboxCatalogEntry other)
        {
            Forbidden = other.Forbidden;
            AllMembers = other.AllMembers;
            _allowed = Copy(other._allowed);
            _rejected = Copy(other._rejected);
        }

        /// <summary><c>Forbid&lt;T&gt;()</c> - denied to both gates, named or reached.</summary>
        internal bool Forbidden { get; set; }

        /// <summary><c>AllowAllMembersOf&lt;T&gt;()</c> - every member, less any rejected.</summary>
        internal bool AllMembers { get; set; }

        internal void Allow([NotNull] string memberName)
        {
            if (_allowed == null)
                _allowed = NewSet();

            _allowed.Add(memberName);
        }

        internal void Reject([NotNull] string memberName)
        {
            if (_rejected == null)
                _rejected = NewSet();

            _rejected.Add(memberName);
        }

        /// <summary>The allowed names, or null when this entry allows every member.</summary>
        [CanBeNull]
        internal HashSet<string> AllowedMembers
        {
            get { return _allowed; }
        }

        [CanBeNull]
        internal HashSet<string> RejectedMembers
        {
            get { return _rejected; }
        }

        /// <summary>
        /// True when the entry says nothing about members at all - <c>Allow(typeof(X))</c> with no
        /// names. That still catalogues the type, which is what makes it reachable and its inherited
        /// entries usable; it just contributes no members of its own.
        /// </summary>
        internal bool HasNoMemberOpinion
        {
            get { return !AllMembers && _allowed == null; }
        }

        /// <summary>
        /// Case-insensitive, because this engine's member binding is - see
        /// <see cref="TypeVerdict.Allows"/>.
        /// </summary>
        [NotNull]
        internal static HashSet<string> NewSet()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        [CanBeNull]
        private static HashSet<string> Copy([CanBeNull] HashSet<string> source)
        {
            return source == null ? null : new HashSet<string>(source, StringComparer.OrdinalIgnoreCase);
        }

        [CanBeNull]
        private HashSet<string> _allowed;

        [CanBeNull]
        private HashSet<string> _rejected;
    }
}

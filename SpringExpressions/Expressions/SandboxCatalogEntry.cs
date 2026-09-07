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

        /// <summary>
        /// Permits <paramref name="memberName"/> of one kind for <paramref name="access"/>, adding to
        /// whatever this entry already permits for it.
        /// </summary>
        /// <remarks>
        /// Flags are OR-ed rather than replaced, so
        /// <c>AllowPropertyOrFieldRead(n)</c> followed by <c>AllowPropertyOrFieldWrite(n)</c> is the
        /// same as <c>AllowPropertyOrField(n)</c> - two builder calls describing one member must not
        /// have an order-dependent result.
        /// <p>
        /// A method entry is recorded with <see cref="MemberAccess.Both"/> whatever is passed:
        /// invoking is invoking, and a direction on a method is what <see cref="MemberKind"/> exists
        /// to make unsayable. The verbs cannot pass anything else, so this is belt and braces.
        /// </p>
        /// <p>
        /// <b>Neither parameter has a default, deliberately.</b> A caller who forgot the kind would
        /// silently get a property entry, and one who forgot the access would get
        /// <see cref="MemberAccess.Both"/> - the <i>widest</i> permission, chosen by omission. That is
        /// the same shape as a node reaching for <c>SandboxPolicy.Default</c> instead of the policy it
        /// was handed: a hole waiting for somebody to wire it up. Every call site states both.
        /// </p>
        /// </remarks>
        internal void Allow(
            [NotNull] string memberName,
            MemberKind kind,
            MemberAccess access)
        {
            if (_allowed == null)
                _allowed = NewMap();

            Add(_allowed, memberName, kind, access);
        }

        /// <summary>Permits the name for <b>both</b> kinds - what <c>Allow</c>/<c>Except</c> mean.</summary>
        internal void AllowEitherKind([NotNull] string memberName)
        {
            Allow(memberName, MemberKind.PropertyOrField, MemberAccess.Both);
            Allow(memberName, MemberKind.Method, MemberAccess.Both);
        }

        internal void Reject(
            [NotNull] string memberName,
            MemberKind kind,
            MemberAccess access)
        {
            if (_rejected == null)
                _rejected = NewMap();

            Add(_rejected, memberName, kind, access);
        }

        internal void RejectEitherKind([NotNull] string memberName)
        {
            Reject(memberName, MemberKind.PropertyOrField, MemberAccess.Both);
            Reject(memberName, MemberKind.Method, MemberAccess.Both);
        }

        private static void Add(
            [NotNull] Dictionary<MemberKey, MemberAccess> map,
            [NotNull] string memberName,
            MemberKind kind,
            MemberAccess access)
        {
            if (kind == MemberKind.Method)
                access = MemberAccess.Both;

            var key = new MemberKey(kind, memberName);

            MemberAccess existing;
            map[key] = map.TryGetValue(key, out existing) ? existing | access : access;
        }

        /// <summary>The allowed names and their directions, or null when this entry allows every member.</summary>
        [CanBeNull]
        internal Dictionary<MemberKey, MemberAccess> AllowedMembers
        {
            get { return _allowed; }
        }

        [CanBeNull]
        internal Dictionary<MemberKey, MemberAccess> RejectedMembers
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
        internal static Dictionary<MemberKey, MemberAccess> NewMap()
        {
            // MemberKey compares its name with OrdinalIgnoreCase itself, so the dictionary needs no
            // comparer of its own.
            return new Dictionary<MemberKey, MemberAccess>();
        }

        [CanBeNull]
        private static Dictionary<MemberKey, MemberAccess> Copy(
            [CanBeNull] Dictionary<MemberKey, MemberAccess> source)
        {
            if (source == null)
                return null;

            var copy = NewMap();
            foreach (var pair in source)
                copy.Add(pair.Key, pair.Value);

            return copy;
        }

        [CanBeNull]
        private Dictionary<MemberKey, MemberAccess> _allowed;

        [CanBeNull]
        private Dictionary<MemberKey, MemberAccess> _rejected;
    }
}

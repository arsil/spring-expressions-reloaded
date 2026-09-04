using System;

using JetBrains.Annotations;

using SpringUtil;

namespace SpringExpressions
{
    /// <summary>
    /// The expression tried to reach a type or a member that its <see cref="SandboxPolicy"/> does not
    /// permit.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>It derives from <see cref="Exception"/>, and nothing in this library catches it.</b> That is
    /// deliberate, and it is the one failure that sits outside the compile-failure convention
    /// (<c>_Docs/compile-failure-convention.md</c>). A <c>CompileErrorException</c> is a <i>routing</i>
    /// signal here rather than a failure signal: it means "this shape has no compiled form, let the
    /// interpreter do it", and three places act on it - <c>GetterExpressions</c>,
    /// <c>SetterExpressions</c> and <c>VoidExpressions</c> - under
    /// <see cref="EvaluationMode.CompileOrInterpret"/>, which is the default at every entry point. A
    /// denial reported that way would therefore be converted into an instruction to interpret: the
    /// caller would get a working expression back and no error at all, and whether they ever learned
    /// would depend on the interpreter's own gate firing later. "You are not allowed" and "I cannot
    /// compile this, use the other backend" must not be the same signal, because the correct response
    /// to the second is exactly the wrong response to the first.
    /// </p>
    /// <p>
    /// Both backends throw this, with the same message, and always <i>before</i> the denied type or
    /// member is touched. <b>Which moment that is follows the backend, not the expression</b>: a
    /// compiled expression denies while its delegate is built - out of <c>ParseGetter</c> on the
    /// strongly typed path, out of the first <c>GetValue&lt;T&gt;</c> on the weakly typed one - while an
    /// interpreted expression denies at the first evaluation that reaches the node. The guarantee is
    /// "it never runs", not "you always find out at parse". See <c>_Docs/type-sandboxing.md</c> §3.3
    /// and §3.4.
    /// </p>
    /// </remarks>
    public sealed class SandboxViolationException : Exception
    {
        /// <summary>The expression named a type the sandbox does not permit.</summary>
        public SandboxViolationException([NotNull] Type deniedType)
            : base(DescribeType(deniedType))
        {
            DeniedType = deniedType;
        }

        /// <summary>
        /// The expression reached a member the sandbox does not permit, on a type that it does.
        /// </summary>
        public SandboxViolationException([NotNull] Type declaringType, [NotNull] string deniedMember)
            : base(DescribeMember(declaringType, deniedMember))
        {
            DeniedType = declaringType;
            DeniedMember = deniedMember;
        }

        /// <summary>
        /// The type that was denied, or - when <see cref="DeniedMember"/> is set - the type declaring
        /// the denied member.
        /// </summary>
        [NotNull]
        public Type DeniedType { get; }

        /// <summary>
        /// The denied member's name, or null when the type itself was denied rather than one of its
        /// members.
        /// </summary>
        [CanBeNull]
        public string DeniedMember { get; }

        [NotNull]
        private static string DescribeType([NotNull] Type deniedType)
        {
            return "The sandbox does not permit the type '" + Describe(deniedType) + "'.";
        }

        [NotNull]
        private static string DescribeMember([NotNull] Type declaringType, [NotNull] string deniedMember)
        {
            AssertUtils.ArgumentNotNull(deniedMember, "deniedMember");

            return "The sandbox does not permit the member '" + deniedMember
                   + "' on type '" + Describe(declaringType) + "'.";
        }

        [NotNull]
        private static string Describe([NotNull] Type type)
        {
            AssertUtils.ArgumentNotNull(type, "type");

            // A constructed generic or an array has no FullName in some corner cases; the display name
            // is what a reader needs, and both backends must produce the same string.
            return type.FullName ?? type.Name;
        }
    }
}

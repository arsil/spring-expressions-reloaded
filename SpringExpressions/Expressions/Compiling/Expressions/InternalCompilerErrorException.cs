using System;
using System.Runtime.InteropServices;
using System.Threading;

using JetBrains.Annotations;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    /// <summary>
    /// A defect in this library, met while building an expression tree, reported as a compile refusal
    /// so that it cannot break a caller who did nothing wrong.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The compiled backend is allowed to say "I have no compiled form for this shape" - that is
    /// <see cref="CompileErrorException"/> and it is routine. What it is not allowed to do is fail in
    /// some other way: an <c>ArgumentException</c> out of a LINQ factory, a null <c>MethodInfo</c>
    /// dereferenced, an off-by-one in candidate gathering. Those escape the weakly typed path's
    /// fallback, which catches only <see cref="CompileErrorException"/>, and turn a shape the
    /// interpreter evaluates perfectly into a hard failure - in every mode, including the default one.
    /// Sixteen such escapes have been found and fixed one at a time; this type ends the class instead.
    /// </p>
    /// <p>
    /// <b>Internal on purpose.</b> Deriving from <see cref="CompileErrorException"/> means the existing
    /// fallback catches it with no change, and a caller sees the exception type they already handle.
    /// Nothing about our defects enters the public vocabulary - an earlier design added a third
    /// <c>InterpretationReason</c> value for this and was rejected for exactly that reason: an enum
    /// value is a contract, and what it would document is "sometimes this library is broken".
    /// </p>
    /// <p>
    /// <b>It does not hide defects from us.</b> The original failure travels as
    /// <see cref="Exception.InnerException"/>, and NUnit's <c>Assert.Throws&lt;T&gt;</c> demands an
    /// exact type match - so the suite's <c>Assert.Throws&lt;CompileErrorException&gt;</c> sites go red
    /// on a wrapped defect exactly as they do today on a raw <c>ArgumentException</c>, and every test
    /// that expected compilation to succeed fails outright.
    /// </p>
    /// </remarks>
    internal class InternalCompilerErrorException : CompileErrorException
    {
        public InternalCompilerErrorException([CanBeNull] BaseNode node, [NotNull] Exception cause)
            : base(node, BuildReason(cause), cause)
        {
        }

        /// <summary>
        /// Whether a failure met during compilation is one of ours to absorb.
        /// </summary>
        /// <remarks>
        /// Three exclusions. A <see cref="CompileErrorException"/> is already the right answer and is
        /// left alone - wrapping it would bury the node name a refusal carries. A fatal exception
        /// is rethrown: converting one would report "no compiled form for this shape" for a machine
        /// that is out of memory, which is worse than useless.
        /// <p>
        /// And a <see cref="SandboxViolationException"/> must escape untouched, which is the exclusion
        /// most easily forgotten because it looks like every other emit-time failure. Absorbing it
        /// would turn it into an <see cref="InternalCompilerErrorException"/> - a
        /// <see cref="CompileErrorException"/> - so the weakly typed fallback would catch it and build
        /// an interpreter instead, telling the caller nothing and blaming us for a defect while it did
        /// so. The interpreter's own gate would deny again a moment later, but the caller would have
        /// been told "the compiler is broken, please report it" rather than "you may not reach that
        /// type". See <c>_Docs/type-sandboxing.md</c> §3.3: a denial is deliberately outside the
        /// compile-failure convention, and this is where that has to be enforced.
        /// </p>
        /// <p>
        /// Used from an exception filter rather than a catch body, so the stack is not unwound for the
        /// failures this returns false for.
        /// </p>
        /// </remarks>
        public static bool ShouldAbsorb([CanBeNull] Exception exception)
        {
            return exception != null
                && !(exception is CompileErrorException)
                && !(exception is SandboxViolationException)
                && !IsFatal(exception);
        }

        /// <summary>
        /// Failures that mean the process is in trouble, not that an expression could not be compiled.
        /// </summary>
        /// <remarks>
        /// Most of this list is documentation rather than working code, and that is worth knowing
        /// before anyone trusts it: <see cref="StackOverflowException"/> cannot be caught at all on
        /// .NET, and an <see cref="AccessViolationException"/> or <see cref="SEHException"/> is not
        /// delivered to a managed handler by default. <see cref="ThreadAbortException"/> is rethrown
        /// automatically at the end of any catch block regardless, and is never raised outside .NET
        /// Framework. <see cref="OutOfMemoryException"/> is the one this predicate genuinely acts on.
        /// The rest are named so that a reader knows they were considered.
        /// </remarks>
        public static bool IsFatal([CanBeNull] Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is ThreadAbortException
                || exception is AccessViolationException
                || exception is SEHException;
        }

        [NotNull]
        private static string BuildReason([NotNull] Exception cause)
        {
            return "internal compiler error - " + cause.GetType().Name + ": " + cause.Message
                + ". The interpreter evaluates this expression instead; please report it.";
        }
    }
}

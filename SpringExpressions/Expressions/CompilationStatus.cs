using JetBrains.Annotations;

namespace SpringExpressions
{
    /// <summary>
    /// What an expression turned out to be - as opposed to <see cref="EvaluationMode"/>, which is what a
    /// caller asked for.
    /// </summary>
    /// <remarks>
    /// Past tense on purpose. A single vocabulary serving both roles would have one word meaning both
    /// "compile this" and "it is compiled", which reads as a description where it is an instruction.
    /// </remarks>
    public enum EvaluationKind
    {
        /// <summary>A compiled delegate evaluates this expression.</summary>
        Compiled = 0,

        /// <summary>The tree-walking interpreter evaluates this expression.</summary>
        Interpreted = 1
    }

    /// <summary>
    /// Which half of an expression a decision is about. A weakly typed expression decides separately for
    /// reads and writes against the same declared context type, so naming the context type alone does
    /// not identify a decision.
    /// </summary>
    public enum EvaluationOperation
    {
        Get = 0,
        Set = 1
    }

    /// <summary>
    /// Why an expression is interpreted. Only two reasons exist, and they mean different things to
    /// whoever is asking: one is the caller's own choice, the other is the engine's limit.
    /// </summary>
    /// <remarks>
    /// A third value, <c>NoCompiledPathExists</c>, was carried through several drafts of
    /// <c>_Docs/compilation-options-and-status.md</c> to describe the weakly typed setter - the one thing
    /// that could never compile. Routing weak writes through the compiler removed that case, leaving the
    /// value with no producer, so it is not here. A vocabulary entry nothing can report is the fault that
    /// whole document exists to repair.
    /// </remarks>
    public enum InterpretationReason
    {
        /// <summary>The caller asked for <see cref="EvaluationMode.MustInterpret"/>.</summary>
        Requested = 0,

        /// <summary>
        /// The shape has no compiled form. <see cref="CompilationStatus.RefusalMessage"/> names the node
        /// that refused and why.
        /// </summary>
        CompilationRefused = 1
    }

    /// <summary>
    /// What an expression became: compiled, or interpreted and for which of the two possible reasons.
    /// </summary>
    /// <remarks>
    /// An enum alone would not do. "Interpreted" says nothing about whether it is the expression's fault
    /// or the engine's, and the difference is the whole point of asking - which is why
    /// <see cref="RefusalMessage"/> carries the refusal verbatim, node name included.
    /// </remarks>
    public sealed class CompilationStatus
    {
        [NotNull]
        public static CompilationStatus Compiled { get; } = new CompilationStatus(EvaluationKind.Compiled, null, null);

        [NotNull]
        public static CompilationStatus InterpretedByRequest { get; }
            = new CompilationStatus(EvaluationKind.Interpreted, InterpretationReason.Requested, null);

        [NotNull]
        public static CompilationStatus InterpretedAfterRefusal([NotNull] string refusalMessage)
            => new CompilationStatus(
                EvaluationKind.Interpreted, InterpretationReason.CompilationRefused, refusalMessage);

        private CompilationStatus(
            EvaluationKind kind, InterpretationReason? reason, string refusalMessage)
        {
            Kind = kind;
            Reason = reason;
            RefusalMessage = refusalMessage;
        }

        /// <summary>Whether a compiled delegate or the interpreter evaluates this expression.</summary>
        public EvaluationKind Kind { get; }

        /// <summary>Null when <see cref="Kind"/> is <see cref="EvaluationKind.Compiled"/>.</summary>
        public InterpretationReason? Reason { get; }

        /// <summary>
        /// The refusal, verbatim - "Cannot compile SelectionFirstNode '^{': …" - or null unless
        /// <see cref="Reason"/> is <see cref="InterpretationReason.CompilationRefused"/>.
        /// </summary>
        [CanBeNull]
        public string RefusalMessage { get; }

        public override string ToString()
            => Kind == EvaluationKind.Compiled
                ? "Compiled"
                : RefusalMessage == null
                    ? "Interpreted (Requested)"
                    : $"Interpreted (CompilationRefused): {RefusalMessage}";
    }
}

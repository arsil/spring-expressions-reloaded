using System;

using JetBrains.Annotations;

namespace SpringExpressions
{
    /// <summary>
    /// One decision a weakly typed expression made: for these declared types, and for reading or for
    /// writing, it either compiled or it did not.
    /// </summary>
    /// <remarks>
    /// This is the weakly typed counterpart of <see cref="CompilationStatus"/>, and it is pushed rather
    /// than pulled. A strongly typed expression decides inside the caller's own constructor call, so it
    /// can be asked; a weakly typed one decides per declared type, on first use with that type, inside
    /// code the asker did not write and possibly on another thread - there is no moment at which a query
    /// could be complete. So an observer is handed to <see cref="Expression.Parse"/> and told when each
    /// decision happens. See <c>_Docs/compilation-options-and-status.md</c> §4.3.
    /// <p>
    /// It carries neither the expression nor its text. The observer was registered for <i>this</i>
    /// expression at <i>this</i> <c>Parse</c> call, so both are already in the caller's hand - and
    /// carrying the expression would keep every observed one alive in any handler that accumulates
    /// decisions.
    /// </p>
    /// </remarks>
    public sealed class EvaluationDecision
    {
        internal EvaluationDecision(
            [NotNull] Type contextType,
            [CanBeNull] Type valueType,
            EvaluationOperation operation,
            [NotNull] CompilationStatus status)
        {
            ContextType = contextType;
            ValueType = valueType;
            Operation = operation;
            Kind = status.Kind;
            Reason = status.Reason;
            RefusalMessage = status.RefusalMessage;
        }

        /// <summary>
        /// The context type the call site declared - <c>TContext</c> of <c>GetValue</c> or
        /// <c>SetValue</c> - which is what the expression was compiled against, never the runtime type
        /// of the root.
        /// </summary>
        [NotNull]
        public Type ContextType { get; }

        /// <summary>
        /// For a write, the value type the call site declared; null for a read.
        /// </summary>
        /// <remarks>
        /// A write is compiled against both halves, so both are part of the decision and of its
        /// identity: <c>SetValue(order, 45)</c> and <c>SetValue(order, "45")</c> into the same
        /// <c>int</c> member are two decisions against one context type, and they can differ - the first
        /// compiles, the second has no compiled form and falls back. Naming only the context type would
        /// report the two as one repeated fact and leave a reader unable to tell which call produced
        /// which answer.
        /// </remarks>
        [CanBeNull]
        public Type ValueType { get; }

        /// <summary>Whether this decision is about reading the expression or writing through it.</summary>
        public EvaluationOperation Operation { get; }

        /// <summary>Whether a compiled delegate or the interpreter serves that combination.</summary>
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
        {
            var types = ValueType == null
                ? ContextType.ToString()
                : ContextType + " = " + ValueType;

            var outcome = Kind == EvaluationKind.Compiled
                ? "Compiled"
                : RefusalMessage == null
                    ? "Interpreted (Requested)"
                    : "Interpreted (CompilationRefused): " + RefusalMessage;

            return Operation + " " + types + ": " + outcome;
        }
    }
}

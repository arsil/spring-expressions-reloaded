namespace SpringExpressions
{
    /// <summary>
    /// What a caller asks for when an expression is parsed: what should happen if the compiled backend
    /// has no form for the shape.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The values are imperatives on purpose. A caller *requests* a mode; what an expression *became* is
    /// a different question with a different vocabulary, reported separately - a single word meaning both
    /// "compile this" and "it is compiled" reads as a description where it is in fact an instruction.
    /// </p>
    /// <p>
    /// The mode is fixed when the expression is created and never changes afterwards. A compiled
    /// expression bakes its member binds in, so a mode that could be varied per evaluation would either
    /// do nothing or force recompilation; and an expression whose meaning depended on ambient state at
    /// the moment of a call would be unpredictable to reason about. See
    /// <c>_Docs/compilation-options-and-status.md</c>.
    /// </p>
    /// <p>
    /// This replaces the former <c>CompileOptions</c>, a [Flags] enum of five values of which two were
    /// ever read - and whose combinations could not be honoured: "MustCompile | MustUseInterpreter" was
    /// legal to write and meant "interpret".
    /// </p>
    /// </remarks>
    public enum EvaluationMode
    {
        /// <summary>
        /// Compile if possible, interpret if not. The default on every path: an expression that works,
        /// even slowly, beats one that refuses.
        /// </summary>
        CompileOrInterpret = 0,

        /// <summary>
        /// Compile, and throw <see cref="Expressions.Compiling.Expressions.CompileErrorException"/> if
        /// the shape has no compiled form. For a caller who would rather be told than run interpreted.
        /// </summary>
        MustCompile = 1,

        /// <summary>
        /// Never compile. The interpreter accepts a strict superset of what the compiled backend does,
        /// so this always works, and it is how a caller asks for the interpreter's semantics
        /// deliberately rather than by accident.
        /// </summary>
        MustInterpret = 2
    }
}

using System;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
    /// <summary>
    /// Thrown when an expression cannot be turned into a compiled expression tree.
    /// </summary>
    /// <remarks>
    /// This reports a limitation of the compiled evaluation path, not a malformed expression: the same
    /// expression may well evaluate correctly through the interpreter. It is the single signal for
    /// "not compilable", raised both by nodes that implement no compiled path at all and by nodes that
    /// implement one but met operands or a shape they cannot handle.
    /// <para>
    /// Public so that callers can distinguish "this expression is not compilable" from a defect in the
    /// library, and can choose to fall back to interpreting instead of failing.
    /// </para>
    /// </remarks>
    public class CompileErrorException : Exception
    {
        public CompileErrorException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates an exception naming the node that could not be compiled and why.
        /// </summary>
        /// <param name="node">The node that could not produce an expression tree.</param>
        /// <param name="reason">
        /// What prevented compilation, phrased to complete the sentence "cannot compile X: ...".
        /// </param>
        public CompileErrorException(BaseNode node, string reason)
            : base(BuildMessage(node, reason))
        {
            NodeType = node == null ? null : node.GetType();
            Reason = reason;
        }

        /// <summary>The node type that could not be compiled, when known.</summary>
        public Type NodeType { get; private set; }

        /// <summary>What prevented compilation, without the node name prefix.</summary>
        public string Reason { get; private set; }

        private static string BuildMessage(BaseNode node, string reason)
        {
            var nodeName = node == null ? "expression" : node.GetType().Name;
            var text = node == null ? null : node.getText();

            return string.IsNullOrEmpty(text)
                ? "Cannot compile " + nodeName + ": " + reason
                : "Cannot compile " + nodeName + " '" + text + "': " + reason;
        }
    }
}

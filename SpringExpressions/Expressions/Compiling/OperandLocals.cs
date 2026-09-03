using System;
using System.Collections.Generic;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;
using LParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace SpringExpressions.Expressions.Compiling
{
    /// <summary>
    /// Emits a tree that uses each operand exactly once, by putting the operands in block variables
    /// where an emitted tree would otherwise mention them more than once.
    /// </summary>
    /// <remarks>
    /// <p>
    /// An emitted operand is an <i>expression</i>, not a value: writing it into two places in the tree
    /// means evaluating it twice at run time, and writing it into one branch of a conditional means not
    /// evaluating it at all when the other branch is taken. Both are invisible to every other test in
    /// this suite - the answer is identical, so only a caller's side effects can tell - which is why
    /// four instances of it shipped and each was found by hand or by
    /// <c>OperandReadsNeverDivergeTests</c>.
    /// </p>
    /// <p>
    /// The interpreter reads the left operand and then the right, once each, whatever the operator does
    /// with them afterwards. That is the order these blocks assign in, so the two backends agree on
    /// what a caller's own code does as well as on the answer.
    /// </p>
    /// <p>
    /// <b>Short-circuiting is unaffected and must stay that way.</b> This is for an operand a node has
    /// decided to use; an operator that deliberately does not evaluate its right operand - <c>and</c>,
    /// <c>or</c>, a conditional's untaken branch - never reaches here, because it does not build a tree
    /// mentioning that operand at all.
    /// </p>
    /// </remarks>
    internal static class OperandLocals
    {
        /// <summary>
        /// Builds a tree over one operand, hoisting it into a block variable unless it is safe to
        /// mention twice.
        /// </summary>
        [NotNull]
        public static LExpression UseOnce(
            [NotNull] LExpression operand,
            [NotNull] Func<LExpression, LExpression> build)
        {
            var locals = new List<LParameterExpression>();
            var prologue = new List<LExpression>();

            var body = build(Hoist(operand, locals, prologue));

            return Wrap(locals, prologue, body);
        }

        /// <summary>
        /// Builds a tree over two operands, hoisting each into a block variable unless it is safe to
        /// mention twice. The left operand is assigned first.
        /// </summary>
        [NotNull]
        public static LExpression UseOnce(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] Func<LExpression, LExpression, LExpression> build)
        {
            var locals = new List<LParameterExpression>();
            var prologue = new List<LExpression>();

            // Assigned in this order deliberately: it is the order the interpreter's GetLeftValue and
            // GetRightValue run in, and a caller with side effects in both operands can see it.
            var hoistedLeft = Hoist(left, locals, prologue);
            var hoistedRight = Hoist(right, locals, prologue);

            var body = build(hoistedLeft, hoistedRight);

            return Wrap(locals, prologue, body);
        }

        /// <summary>
        /// <see cref="UseOnce(LExpression, LExpression, Func{LExpression, LExpression, LExpression})"/>
        /// for a builder that may refuse the operands it is given, reporting the refusal as a false
        /// return rather than as an exception.
        /// </summary>
        /// <remarks>
        /// The hoisting has to wrap the refusal too: a builder that answers null must leave no block
        /// behind, or the caller would be handed a tree that assigns two locals and then does nothing
        /// with them.
        /// </remarks>
        [ContractAnnotation("=>true,result:notnull;=>false,result:null")]
        public static bool TryUseOnce(
            [NotNull] LExpression left,
            [NotNull] LExpression right,
            [NotNull] Func<LExpression, LExpression, LExpression> build,
            [CanBeNull] out LExpression result)
        {
            var locals = new List<LParameterExpression>();
            var prologue = new List<LExpression>();

            var hoistedLeft = Hoist(left, locals, prologue);
            var hoistedRight = Hoist(right, locals, prologue);

            var body = build(hoistedLeft, hoistedRight);

            result = body == null ? null : Wrap(locals, prologue, body);

            return result != null;
        }

        /// <summary>
        /// A local for the operand, or the operand itself when evaluating it twice cannot be observed
        /// and cannot cost anything.
        /// </summary>
        /// <remarks>
        /// Only a constant and a parameter qualify. A parameter read is a local slot read, and a
        /// constant is already computed - neither can run a caller's code. Everything else is hoisted,
        /// including a plain property access: a property getter is a method call, and this library has
        /// no way to know whether one has side effects. Erring towards a needless local is a slot in the
        /// emitted delegate; erring the other way is the defect this class exists to remove.
        /// </remarks>
        [NotNull]
        private static LExpression Hoist(
            [NotNull] LExpression operand,
            [NotNull] List<LParameterExpression> locals,
            [NotNull] List<LExpression> prologue)
        {
            if (operand is System.Linq.Expressions.ConstantExpression
                || operand is LParameterExpression)
            {
                return operand;
            }

            var local = LExpression.Variable(operand.Type, "operand" + locals.Count);

            locals.Add(local);
            prologue.Add(LExpression.Assign(local, operand));

            return local;
        }

        /// <summary>
        /// The block, or the body alone when nothing needed hoisting - so an expression over constants
        /// and parameters emits exactly what it did before this class existed.
        /// </summary>
        [NotNull]
        private static LExpression Wrap(
            [NotNull] List<LParameterExpression> locals,
            [NotNull] List<LExpression> prologue,
            [NotNull] LExpression body)
        {
            if (locals.Count == 0)
                return body;

            prologue.Add(body);

            return LExpression.Block(locals, prologue);
        }
    }
}

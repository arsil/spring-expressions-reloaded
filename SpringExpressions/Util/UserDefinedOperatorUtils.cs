using System;
using System.Collections.Concurrent;
using System.Reflection;

using JetBrains.Annotations;

namespace SpringExpressions.Util
{
    /// <summary>
    /// Finds the operator a type declares for itself - <c>TimeSpan + TimeSpan</c>, a caller's own
    /// <c>Vector + Vector</c>, <c>DateTime + TimeSpan</c>.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The engine had no notion of these at all. A type participated in arithmetic only by converting
    /// to a built-in number (<see cref="TypeCheckingUtils.TryGetImplicitRealConversion"/>), so a
    /// decimal-like struct worked and anything else - <c>TimeSpan</c> included - was refused compiled
    /// and threw interpreted. Measured with a caller's own struct declaring <c>operator +</c>: refused
    /// on both backends, exactly as <c>TimeSpan</c> was.
    /// </p>
    /// <p>
    /// <b>The lookup runs before the conversion path, which is C#'s order</b> and matters for a type
    /// that has both. Such a type used to erase itself: with an implicit conversion to decimal *and*
    /// its own <c>operator +</c>, <c>a + b</c> answered a <c>decimal</c> rather than the type, on both
    /// backends, silently. Nothing in either suite declared such a type, which is why it was never
    /// caught. The types that <i>are</i> pinned - <c>MoneyLike</c>, <c>SpeedLike</c> - declare a
    /// conversion and no operators, so this lookup finds nothing for them and their behaviour is
    /// untouched by construction.
    /// </p>
    /// <p>
    /// <b>Deliberately narrow: exact operand types only.</b> C#'s operator resolution is a chapter of
    /// the specification - candidate sets from both operand types, lifted forms, user-defined
    /// conversions applied to operands, better-conversion tie-breaking. None of that is here. If a
    /// declared operator's parameters do not match the operand types exactly, this finds nothing and
    /// the caller refuses, on both backends. A refusal is a shape the interpreter serves or rejects
    /// identically, so this narrowness costs no agreement between the two.
    /// </p>
    /// <p>
    /// <b>Both backends run this same lookup.</b> The compiled path must pass the resolved
    /// <see cref="MethodInfo"/> to <c>LExpression.Add(left, right, method)</c> rather than let
    /// <c>LExpression.Add(left, right)</c> resolve for itself: LINQ applies its own, more permissive
    /// rules, and the two would drift apart exactly where this engine works hardest not to.
    /// </p>
    /// </remarks>
    public static class UserDefinedOperatorUtils
    {
        /// <summary>
        /// The operator method a pair of operand types declares between them, or null.
        /// </summary>
        /// <param name="operatorMethodName">The CLR name - <c>op_Addition</c>, <c>op_LessThan</c>.</param>
        [CanBeNull]
        public static MethodInfo FindBinary(
            [NotNull] string operatorMethodName, [NotNull] Type leftType, [NotNull] Type rightType)
        {
            var key = new OperatorKey(operatorMethodName, leftType, rightType);

            return BinaryCache.GetOrAdd(key, k => ResolveBinary(k.Name, k.Left, k.Right));
        }

        /// <summary>
        /// The unary operator a type declares for itself - <c>op_UnaryNegation</c> - or null.
        /// </summary>
        [CanBeNull]
        public static MethodInfo FindUnary(
            [NotNull] string operatorMethodName, [NotNull] Type operandType)
        {
            var key = new OperatorKey(operatorMethodName, operandType, operandType);

            return UnaryCache.GetOrAdd(key, k => ResolveUnary(k.Name, k.Left));
        }

        /// <summary>
        /// Which of C#'s two complement operators a type declares for itself.
        /// </summary>
        public enum NotOperator
        {
            /// <summary>Neither, so <c>!</c> falls through to its built-in roles.</summary>
            None,

            /// <summary><c>op_LogicalNot</c> only - what C# spells <c>!</c>.</summary>
            LogicalNot,

            /// <summary><c>op_OnesComplement</c> only - what C# spells <c>~</c>.</summary>
            OnesComplement,

            /// <summary>Both, which one spelling cannot tell apart.</summary>
            Both
        }

        /// <summary>
        /// The complement operator a type declares for itself, for the single <c>!</c> that serves both
        /// of C#'s roles.
        /// </summary>
        /// <remarks>
        /// <p>
        /// This language has no <c>~</c>: <c>!</c> is logical negation for a boolean and bitwise
        /// complement for an integer or enum, inherited and deliberate, the same dual role
        /// <c>and</c>/<c>or</c>/<c>xor</c> carry. For a built-in operand the role is read from the
        /// operand's type. A custom type gives no such signal, so it is read from **which operator the
        /// type declares** - a type declaring only <c>op_OnesComplement</c> means C#'s <c>~</c>, and one
        /// declaring only <c>op_LogicalNot</c> means C#'s <c>!</c>.
        /// </p>
        /// <p>
        /// <b>A type declaring both is refused</b>, because the two answer differently and the
        /// expression cannot say which it wants: measured on a struct declaring both, C# gives
        /// <c>~x</c> = -6 and <c>!x</c> = -5 for the same operand. Picking one silently would make
        /// <c>!x</c> mean whichever the engine happened to prefer. Refusing is the standing answer for
        /// an illegal expression - the compile phase refuses, the interpreter raises the error at
        /// evaluation.
        /// </p>
        /// <p>
        /// No built-in type reaches this. Measured: <c>bool</c>, <c>int</c>, <c>long</c>,
        /// <c>decimal</c> and <c>double</c> declare neither operator - their complements are intrinsic -
        /// and C# forbids an enum from declaring operators at all. So the callers may consult this after
        /// their built-in roles rather than before, which keeps the common <c>!someBoolean</c> free of
        /// the lookup.
        /// </p>
        /// </remarks>
        public static NotOperator FindNot([NotNull] Type operandType)
        {
            var hasLogicalNot = FindUnary("op_LogicalNot", operandType) != null;
            var hasOnesComplement = FindUnary("op_OnesComplement", operandType) != null;

            if (hasLogicalNot && hasOnesComplement)
                return NotOperator.Both;

            if (hasLogicalNot)
                return NotOperator.LogicalNot;

            return hasOnesComplement ? NotOperator.OnesComplement : NotOperator.None;
        }

        /// <summary>
        /// Whether the built-in numeric rules already own this pair, in which case no operator lookup
        /// happens at all.
        /// </summary>
        /// <remarks>
        /// <c>decimal</c> declares <c>op_Addition(decimal, decimal)</c>, so without this the lookup
        /// would start intercepting ordinary decimal arithmetic and quietly take it off the promotion
        /// path. The promotion rules keep the whole built-in numeric space; this helper exists for
        /// everything else.
        /// </remarks>
        public static bool IsOwnedByNumericPromotion([NotNull] Type leftType, [NotNull] Type rightType)
        {
            return IsBuiltInNumeric(leftType) && IsBuiltInNumeric(rightType);
        }

        private static bool IsBuiltInNumeric([NotNull] Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            switch (Type.GetTypeCode(underlying))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;

                default:
                    return false;
            }
        }

        [CanBeNull]
        private static MethodInfo ResolveBinary(string name, Type leftType, Type rightType)
        {
            return FindOn(leftType, name, leftType, rightType)
                ?? (rightType == leftType ? null : FindOn(rightType, name, leftType, rightType));
        }

        [CanBeNull]
        private static MethodInfo ResolveUnary(string name, Type operandType)
        {
            var candidate = operandType.GetMethod(
                name, BindingFlags.Public | BindingFlags.Static, null, new[] { operandType }, null);

            if (candidate == null || candidate.ReturnType == typeof(void))
                return null;

            // Exact, for the reason given in FindOn.
            return candidate.GetParameters()[0].ParameterType == operandType ? candidate : null;
        }

        [CanBeNull]
        private static MethodInfo FindOn(Type declaringType, string name, Type leftType, Type rightType)
        {
            var candidate = declaringType.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { leftType, rightType },
                null);

            if (candidate == null || candidate.ReturnType == typeof(void))
                return null;

            // GetMethod with a null binder uses Type.DefaultBinder, which *widens*: asking for
            // (TimeSpan, int) hands back op_Division(TimeSpan, double). The parameters are then checked
            // here, because a widened match is not the exact match this lookup promises - and letting
            // one through produced an emitter failure, "the operands for operator 'Divide' do not match
            // the parameters of method 'op_Division'", caught by CompilationNeverLeaksTests.
            //
            // Widening the operands instead is a coherent design and C#'s own, but it is a larger one:
            // the interpreter would have to widen identically or the backends drift, and reflection's
            // Invoke widens arguments silently, which is exactly the kind of accidental agreement this
            // engine does not build on. Exact only, and 'Span / 45.0' is the spelling that works.
            var parameters = candidate.GetParameters();

            return parameters[0].ParameterType == leftType && parameters[1].ParameterType == rightType
                ? candidate
                : null;
        }

        private readonly struct OperatorKey : IEquatable<OperatorKey>
        {
            public OperatorKey(string name, Type left, Type right)
            {
                Name = name;
                Left = left;
                Right = right;
            }

            public readonly string Name;
            public readonly Type Left;
            public readonly Type Right;

            public bool Equals(OperatorKey other)
                => Name == other.Name && Left == other.Left && Right == other.Right;

            public override bool Equals(object obj) => obj is OperatorKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Name.GetHashCode() * 397) ^ Left.GetHashCode()) * 397 ^ Right.GetHashCode();
                }
            }
        }

        private static readonly ConcurrentDictionary<OperatorKey, MethodInfo> BinaryCache
            = new ConcurrentDictionary<OperatorKey, MethodInfo>();

        private static readonly ConcurrentDictionary<OperatorKey, MethodInfo> UnaryCache
            = new ConcurrentDictionary<OperatorKey, MethodInfo>();
    }
}

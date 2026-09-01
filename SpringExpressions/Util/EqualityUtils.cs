using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Reflection;
using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    internal static class EqualityUtils
    {
        [MustUseReturnValue]
        public static bool EqualsForObjectsOfTheSameType(object t1, object t2)
        {
            return Methods.GetOrAdd(t1.GetType(), CreateMethod)(t1, t2);
        }

        [MustUseReturnValue]
        public static bool NotEqualsForObjectsOfTheSameType(object t1, object t2)
        {
            return !Methods.GetOrAdd(t1.GetType(), CreateMethod)(t1, t2);
        }


        /// <summary>
        /// Whether an enum value equals the member the string names - the rule for comparing an enum to
        /// a string, and the single implementation both backends run so that they cannot drift.
        /// </summary>
        /// <remarks>
        /// The string must be a member <em>name</em>. Enum.Parse also accepts a numeric literal, which
        /// made "FooType.One == '0'" answer true - an accident of the parser rather than a decision, and
        /// ruled out: a numeric string is an ArgumentException now, like any other string that does not
        /// name a member. Names are matched case-sensitively, as Enum.Parse always did here. A
        /// comma-separated list of names stays legal, since that is how a [Flags] combination is
        /// written; only the numeric form is gone.
        /// <p>
        /// Used by == and by != alike, which is the point: OpEqual had this rule and OpNotEqual did not,
        /// so "Type == 'One'" answered true while "Type != 'One'" threw. The author's note on the branch
        /// said as much - "to nie ma sensu (te enumy)... bo not eq tego nie robi".
        /// </p>
        /// </remarks>
        [MustUseReturnValue]
        public static bool EnumEqualsName([NotNull] object enumValue, [CanBeNull] string name)
        {
            // A null is not a member name, so it equals no enum value - which is what the interpreter
            // already answered, though by a different route: its 'rightValue is string' pattern does
            // not match a null, so the pair fell past this rule entirely and ended up false. The
            // compiled path emits the call unconditionally and reached name.Split below, so
            // 'Name == Colour' with a null Name was a NullReferenceException compiled against False
            // interpreted. Answering here rather than guarding at the emit site keeps the rule in the
            // one place both backends read it from.
            if (name == null)
                return false;

            var enumType = enumValue.GetType();

            foreach (var part in name.Split(','))
            {
                if (Array.IndexOf(Enum.GetNames(enumType), part.Trim()) < 0)
                {
                    throw new ArgumentException(
                        $"'{name}' does not name a member of the enum [{enumType.FullName}]; comparing an "
                        + "enum to a string compares it to a member name.");
                }
            }

            return enumValue.Equals(Enum.Parse(enumType, name));
        }

        private static bool EqualsUsingEqualityComparer<T>(object t1, object t2)
        {
            return EqualityComparer<T>.Default.Equals((T)t1, (T)t2);
        }

        private static readonly MethodInfo MiEqualsUsingEqualityComparer = typeof(EqualityUtils)
            .GetMethod(nameof(EqualsUsingEqualityComparer), BindingFlags.Static | BindingFlags.NonPublic);


        /// <summary>
        /// The comparison one type uses for itself: its own <c>op_Equality</c> where it declares one,
        /// and <see cref="EqualityComparer{T}"/> otherwise. Built once per type and cached.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The engine already honoured the operator for numerics, <c>string</c> and <c>DateTime</c> -
        /// three hand-written special cases in <c>EqualityHelper</c>, all routed to
        /// <c>LExpression.Equal</c>, which resolves a declared <c>op_Equality</c>. Every other
        /// same-typed pair fell to <c>EqualityComparer</c>, so a type's own operator was never called:
        /// <c>Guid</c>, <c>TimeSpan</c>, <c>DateTimeOffset</c>, <c>Uri</c>, <c>Version</c> and anything
        /// a caller wrote. This is that gap closed with one rule rather than a fourth special case -
        /// the author's own <c>// todo: error: equatable&lt;&gt;</c> sat on the line where it was
        /// missing.
        /// </p>
        /// <p>
        /// Measured before the change: of the BCL types in play, the operator and the comparer agree
        /// everywhere except <c>double</c>/<c>float</c>, where <c>NaN == NaN</c> is false and
        /// <c>NaN.Equals(NaN)</c> is true. Those are excluded here by
        /// <see cref="UserDefinedOperatorUtils.IsOwnedByNumericPromotion"/>, so this change cannot
        /// move them; the NaN divergence is its own item and is fixed on its own terms.
        /// </p>
        /// <p>
        /// <c>!=</c> is not looked up. It stays the negation of <c>==</c>, which is this engine's
        /// standing rule (the enum-name ruling insisted on it), so a type declaring an
        /// <c>op_Inequality</c> that is not its <c>op_Equality</c> negated is deliberately not
        /// honoured - two operators that disagree have no coherent reading here.
        /// </p>
        /// </remarks>
        private static Func<object, object, bool> CreateMethod(Type itemType)
        {
            var userDefined = FindEqualityOperator(itemType);

            if (userDefined != null)
                return CompileOperatorCall(itemType, userDefined);

            var genericMethod = MiEqualsUsingEqualityComparer.MakeGenericMethod(itemType);
            return (Func<object, object, bool>)Delegate
                .CreateDelegate(typeof(Func<object, object, bool>), genericMethod);
        }

        [CanBeNull]
        private static MethodInfo FindEqualityOperator([NotNull] Type itemType)
        {
            if (UserDefinedOperatorUtils.IsOwnedByNumericPromotion(itemType, itemType))
                return null;

            var method = UserDefinedOperatorUtils.FindBinary("op_Equality", itemType, itemType);

            return method != null && method.ReturnType == typeof(bool) ? method : null;
        }

        /// <summary>
        /// A compiled call rather than <c>MethodInfo.Invoke</c>, the pattern CastOperations and
        /// NumericBinaryOperations already use: built once per type, so the operator costs no more per
        /// evaluation than the comparer it replaces.
        /// </summary>
        private static Func<object, object, bool> CompileOperatorCall(
            [NotNull] Type itemType, [NotNull] MethodInfo method)
        {
            var left = LExpression.Parameter(typeof(object), "left");
            var right = LExpression.Parameter(typeof(object), "right");

            var call = LExpression.Call(
                method,
                LExpression.Convert(left, itemType),
                LExpression.Convert(right, itemType));

            return LExpression.Lambda<Func<object, object, bool>>(call, left, right).Compile();
        }

        /// <summary>
        /// A warm start for the types most expressions use, nothing more - every entry here is one
        /// <see cref="CreateMethod"/> would produce anyway. <c>string</c>, <c>DateTime</c> and
        /// <c>TimeSpan</c> were seeded too and are not any more: they declare <c>op_Equality</c>, so
        /// seeding them would keep the interpreter on the comparer while the compiled path used the
        /// operator - the same answer either way, measured, but two rules where one will do.
        /// </summary>
        static EqualityUtils()
        {
            AddMethodForType<int>();
            AddMethodForType<decimal>();
            AddMethodForType<long>();

            // double and float compare by IEEE 754, not by EqualityComparer. The comparer answers
            // Equals, for which NaN equals itself - so 'Nan == Nan' was true here and false compiled,
            // and 'Nan != Nan' the other way round. IEEE, which is what == means for a real number and
            // what the compiled path has always emitted, says a NaN equals nothing including itself.
            // A boxed 'double?' holding a value reports typeof(double), so these two entries cover the
            // nullable spellings as well.
            Methods[typeof(double)] = (t1, t2) => (double)t1 == (double)t2;
            Methods[typeof(float)] = (t1, t2) => (float)t1 == (float)t2;
            AddMethodForType<ulong>();
            AddMethodForType<uint>();
            AddMethodForType<short>();
            AddMethodForType<ushort>();
            AddMethodForType<byte>();
            AddMethodForType<sbyte>();
            AddMethodForType<char>();
            AddMethodForType<bool>();

            AddMethodForType<int?>();
            AddMethodForType<decimal?>();
            AddMethodForType<double?>();
            AddMethodForType<float?>();
            AddMethodForType<long?>();
            AddMethodForType<DateTime?>();
            AddMethodForType<TimeSpan?>();
            AddMethodForType<ulong?>();
            AddMethodForType<uint?>();
            AddMethodForType<short?>();
            AddMethodForType<ushort?>();
            AddMethodForType<byte?>();
            AddMethodForType<sbyte?>();
            AddMethodForType<char?>();
            AddMethodForType<bool?>();
        }

        private static void AddMethodForType<T>()
        {
            Methods[typeof(T)] = EqualsUsingEqualityComparer<T>;
        }
        private static readonly ConcurrentDictionary<Type, Func<object, object, bool>>
            Methods = new ConcurrentDictionary<Type, Func<object, object, bool>>();
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Reflection;
using JetBrains.Annotations;

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
        public static bool EnumEqualsName([NotNull] object enumValue, [NotNull] string name)
        {
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


        private static Func<object, object, bool> CreateMethod(Type itemType)
        {
            var genericMethod = MiEqualsUsingEqualityComparer.MakeGenericMethod(itemType);
            return (Func<object, object, bool>)Delegate
                .CreateDelegate(typeof(Func<object, object, bool>), genericMethod);
        }

        static EqualityUtils()
        {
            AddMethodForType<int>();
            AddMethodForType<decimal>();
            AddMethodForType<double>();
            AddMethodForType<float>();
            AddMethodForType<long>();
            AddMethodForType<DateTime>();
            AddMethodForType<TimeSpan>();
            AddMethodForType<string>();
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

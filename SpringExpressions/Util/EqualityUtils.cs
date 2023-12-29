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

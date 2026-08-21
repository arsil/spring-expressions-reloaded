#region License

/*
 * Copyright 2002-2010 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

using SpringExpressions.Util;

#endregion

namespace SpringUtil
{
    /// <summary>
    /// Utility class containing helper methods for object comparison.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    static class CompareUtils
    {
        /// <summary>Compares two objects.</summary>
        /// <param name="first">First object.</param>
        /// <param name="second">Second object.</param>
        /// <returns>
        /// 0, if objects are equal; 
        /// less than zero, if the first object is smaller than the second one;
        /// greater than zero, if the first object is greater than the second one.</returns>
        public static int Compare(object first, object second)
        {
            // anything is greater than null, unless both operands are null
            if (first == null)
            {
                return (second == null ? 0 : -1);
            }

            if (second == null)
            {
                return 1;
            }

            // Custom real-valued types convert through their implicit operator before anything else:
            // the coercion below only knows TypeCodes and TypeConverters, and the same-type path would
            // demand an IComparable the custom type need not have.
            first = NumberUtils.ToBuiltInRealIfPossible(first);
            second = NumberUtils.ToBuiltInRealIfPossible(second);

            var firstArgType = first.GetType();
            var secondArgType = second.GetType();

            if (firstArgType != secondArgType)
            {
                // Mixed numeric types compare under the same binary numeric promotion the
                // arithmetic operations and the compiled comparison run on, so the backends agree
                // by construction, and pairs the promotion refuses (int against ulong) refuse here
                // too, exactly as they do in arithmetic. The legacy highest-TypeCode coercion is
                // gone: it disagreed with the compiled comparison on signed/unsigned mixes
                // (-1 < uint compared true compiled but threw OverflowException here).
                if (TypeCheckingUtils.IsNumber(first) && TypeCheckingUtils.IsNumber(second))
                {
                    return NumericBinaryOperations.Compare(first, second);
                }

                throw new ArgumentException("Cannot compare instances of ["
                    + firstArgType.FullName
                    + "] and ["
                    + secondArgType.FullName
                    + "] because they cannot be coerced to the same type.");
            }

            // here types must be equal
              // todo: error: GetOrAdd Throws????1111-----------------------------------------------------------------------------
            var method = Methods.GetOrAdd(firstArgType, CreateMethod);
            return method(first, second);

            /*
            if (first is IComparable comparable)
            {
                return comparable.CompareTo(second);
            }

            throw new ArgumentException("Cannot compare instances of the type ["
                + firstArgType.FullName
                + "] because it doesn't implement IComparable");
            */
        }


        private static int CompareSameTypes<T>(object first, object second)
        {
            return Comparer<T>.Default.Compare((T)first, (T)second);
        }

        static CompareUtils()
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
        { Methods[typeof(T)] = CompareSameTypes<T>; }

        private static readonly MethodInfo MiCompareSameTypes = typeof(CompareUtils)
            .GetMethod(nameof(CompareSameTypes), BindingFlags.Static | BindingFlags.NonPublic);

        private static Func<object, object, int> CreateMethod(Type itemType)
        {
            var genericMethod = MiCompareSameTypes.MakeGenericMethod(itemType);
            return (Func<object, object, int>)Delegate
                .CreateDelegate(typeof(Func<object, object, int>), genericMethod);
        }


        private static readonly ConcurrentDictionary<Type, Func<object, object, int>> Methods
            = new ConcurrentDictionary<Type, Func<object, object, int>>();

    }
}

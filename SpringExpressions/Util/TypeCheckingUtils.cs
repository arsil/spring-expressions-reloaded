#region License

/*
 * Copyright © 2002-2011 the original author or authors.
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

using System;
using System.ComponentModel;
using System.Reflection;

namespace SpringUtil
{
    /// <summary>
    /// Answers questions about types - declared <see cref="Type"/>s for the compiled backend, and the
    /// runtime types of boxed values for the interpreter. <see cref="NumberUtils"/> computes with
    /// values; nothing here computes anything.
    /// </summary>
    public static class TypeCheckingUtils
    {
        /// <summary>
        /// Determines whether the supplied <paramref name="number"/> is an integer.
        /// </summary>
        public static bool IsInteger(object number)
        {
            return (number is Int32 || number is Int64 || number is UInt32 || number is UInt64
                || number is Int16 || number is UInt16 || number is Byte || number is SByte);
        }

        /// <summary>
        /// Determines whether the supplied <paramref name="number"/> is of numeric type - a built-in
        /// number, a type implicitly convertible to a real one, or a type whose TypeConverter reaches
        /// decimal.
        /// </summary>
        public static bool IsNumber(object number)
        {
            var isNumber = (IsInteger(number) || IsNativeDecimal(number));
            if (!isNumber && number != null)
                isNumber = IsRealType(number.GetType())
                    || TypeDescriptor.GetConverter(number).CanConvertTo(typeof(Decimal));

            return isNumber;
        }

        /// <summary>
        /// Determines whether the supplied <paramref name="number"/> is a real number.
        /// </summary>
        private static bool IsNativeDecimal(object number)
        {
            return (number is Single || number is Double || number is Decimal);
        }

        /// <summary>
        /// Determines whether the supplied <paramref name="type"/> is an integer type.
        /// </summary>
        public static bool IsInteger(Type type)
        {
            return type == typeof(Int32) || type == typeof(Int64) || type == typeof(UInt32) || type == typeof(UInt64)
                || type == typeof(Int16) || type == typeof(UInt16) || type == typeof(Byte) || type == typeof(SByte);
        }

        /// <summary>
        /// float, double or decimal, nullable or not - or any type that converts implicitly to one of
        /// them. The catalog of real-valued types is open: a caller's own numeric struct with an
        /// implicit conversion to decimal is as real-valued as decimal itself, and converting it into
        /// an integral target would hit the same round-versus-truncate split.
        /// </summary>
        public static bool IsRealType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            return IsBuiltInRealType(type) || TryGetImplicitRealConversion(type, out _);
        }

        /// <summary>
        /// The implicit operator converting <paramref name="type"/> to a built-in real type, preferring
        /// decimal over double over float when the type offers more than one. A built-in real needs no
        /// conversion and yields false.
        /// </summary>
        public static bool TryGetImplicitRealConversion(Type type, out MethodInfo conversion)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            conversion = null;
            var bestRank = 0;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "op_Implicit")
                    continue;

                // The operator must convert FROM this type: conversion operators live on the type in
                // both directions, and decimal itself declares op_Implicit(int) and friends.
                var parameters = method.GetParameters();
                if (parameters.Length != 1
                    || (Nullable.GetUnderlyingType(parameters[0].ParameterType) ?? parameters[0].ParameterType) != type)
                    continue;

                var returnType = Nullable.GetUnderlyingType(method.ReturnType) ?? method.ReturnType;
                var rank = returnType == typeof(decimal) ? 3
                    : returnType == typeof(double) ? 2
                    : returnType == typeof(float) ? 1
                    : 0;

                if (rank > bestRank)
                {
                    bestRank = rank;
                    conversion = method;
                }
            }

            return conversion != null;
        }

        private static bool IsBuiltInRealType(Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        /// <summary>
        /// An integral type, char or enum, nullable or not - the targets a real-to-integral conversion
        /// would have to round or truncate into.
        /// </summary>
        public static bool IsIntegralKind(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type.IsEnum || type == typeof(char))
                return true;

            var code = (int)Type.GetTypeCode(type);
            return code >= (int)TypeCode.SByte && code <= (int)TypeCode.UInt64;
        }
    }
}

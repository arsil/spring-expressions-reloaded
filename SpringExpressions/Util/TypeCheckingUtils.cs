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
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

using JetBrains.Annotations;

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

        /// <summary>
        /// C#'s implicit numeric conversion table (spec §10.2.3), transcribed verbatim - the "CSharp"
        /// in the name marks the closed rulebook: built-in numeric types only, always widening, never
        /// lossy toward integrals; enums and nullables are not numeric conversions and answer false.
        /// Both backends' overload resolution widens by exactly this table, which is what keeps a
        /// widened call answering alike compiled and interpreted.
        ///
        /// Deliberately NOT here, though the engine converts them elsewhere:
        /// - double/float to decimal. C# has no implicit conversion in either direction; the fork's
        ///   decimal-meets-real cell is an ARITHMETIC ruling (PromoteNumericType) and an argument
        ///   CONVERSION for already-resolved methods (ConvertParameters), never a resolution rule.
        ///   Admitting it here would flip the betterness math - double would beat decimal, so
        ///   DblOrDec(45) would resolve to the double overload instead of failing like C#'s CS0121 -
        ///   silently re-deciding the ruled tie behavior.
        /// - custom real-valued types' op_Implicit. That is the widening tier's business:
        ///   HasImplicitWideningConversion layers it on top of this table.
        /// </summary>
        public static bool IsCSharpImplicitNumericConversion([CanBeNull] Type from, [CanBeNull] Type to)
        {
            if (from == null || to == null || from.IsEnum || to.IsEnum)
                return false;

            var target = Type.GetTypeCode(to);
            if (target == TypeCode.Object)
                return false;

            switch (Type.GetTypeCode(from))
            {
                case TypeCode.SByte:
                    return target == TypeCode.Int16 || target == TypeCode.Int32 || target == TypeCode.Int64
                        || target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.Byte:
                    return target == TypeCode.Int16 || target == TypeCode.UInt16
                        || target == TypeCode.Int32 || target == TypeCode.UInt32
                        || target == TypeCode.Int64 || target == TypeCode.UInt64
                        || target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.Int16:
                    return target == TypeCode.Int32 || target == TypeCode.Int64
                        || target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.UInt16:
                    return target == TypeCode.Int32 || target == TypeCode.UInt32
                        || target == TypeCode.Int64 || target == TypeCode.UInt64
                        || target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.Int32:
                    return target == TypeCode.Int64
                        || target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.UInt32:
                    return target == TypeCode.Int64 || target == TypeCode.UInt64
                        || target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.Char:
                    return target == TypeCode.UInt16
                        || target == TypeCode.Int32 || target == TypeCode.UInt32
                        || target == TypeCode.Int64 || target == TypeCode.UInt64
                        || target == TypeCode.Single || target == TypeCode.Double || target == TypeCode.Decimal;
                case TypeCode.Single:
                    return target == TypeCode.Double;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The widening tier's applicability question: does <paramref name="from"/> reach
        /// <paramref name="to"/> through C#'s implicit numeric conversions - going through a custom
        /// real's own implicit operator first when <paramref name="from"/> declares one? Identity and
        /// reference assignability are deliberately NOT included: those are the legacy tier's
        /// business, and the widening tier only runs where the legacy tier found nothing. Nullables
        /// answer false - the interpreter never sees a boxed Nullable, and a lifted conversion is not
        /// something Convert.ChangeType could perform at invoke time.
        /// </summary>
        public static bool HasImplicitWideningConversion([CanBeNull] Type from, [CanBeNull] Type to)
        {
            if (from == null || to == null
                || Nullable.GetUnderlyingType(from) != null
                || Nullable.GetUnderlyingType(to) != null)
                return false;

            if (IsCSharpImplicitNumericConversion(from, to))
                return true;

            if (TryGetImplicitRealConversion(from, out var conversion))
            {
                var target = conversion.ReturnType;
                return target == to || IsCSharpImplicitNumericConversion(target, to);
            }

            return false;
        }

        /// <summary>
        /// C#'s better-conversion-target rule: <paramref name="first"/> beats
        /// <paramref name="second"/> when an implicit conversion runs first-to-second but not back -
        /// by reference or boxing assignability (Derived beats object, string beats object) or by the
        /// implicit numeric table (long beats double, from an int). Where neither direction converts -
        /// double against decimal, string against IFormatProvider - neither target is better, and the
        /// call is ambiguous exactly where C# says CS0121.
        /// </summary>
        public static bool IsBetterConversionTarget([NotNull] Type first, [NotNull] Type second)
        {
            return ConvertsImplicitly(first, second) && !ConvertsImplicitly(second, first);
        }

        private static bool ConvertsImplicitly([NotNull] Type from, [NotNull] Type to)
        {
            return to.IsAssignableFrom(from) || IsCSharpImplicitNumericConversion(from, to);
        }

        /// <summary>
        /// From same-arity parameter-type lists that are all applicable to the same arguments, the
        /// index of the unique best per C#'s betterness - each position at least as good, at least one
        /// strictly better, against every rival - or -1 when the race ties, which callers surface as
        /// an ambiguity.
        /// </summary>
        public static int IndexOfUniqueBestParameterSet([NotNull, ItemNotNull] IList<Type[]> candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var beatsEveryRival = true;

                for (var j = 0; j < candidates.Count && beatsEveryRival; j++)
                {
                    if (j != i && !IsBetterParameterSet(candidates[i], candidates[j]))
                        beatsEveryRival = false;
                }

                if (beatsEveryRival)
                    return i;
            }

            return -1;
        }

        private static bool IsBetterParameterSet([NotNull] Type[] first, [NotNull] Type[] second)
        {
            var strictlyBetter = false;

            for (var i = 0; i < first.Length; i++)
            {
                if (first[i] == second[i])
                    continue;

                if (!IsBetterConversionTarget(first[i], second[i]))
                    return false;

                strictlyBetter = true;
            }

            return strictlyBetter;
        }
    }
}

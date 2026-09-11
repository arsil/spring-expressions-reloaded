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
        /// number, a type implicitly convertible to one, or a type whose TypeConverter reaches
        /// decimal.
        /// </summary>
        public static bool IsNumber(object number)
        {
            var isNumber = (IsInteger(number) || IsNativeDecimal(number));
            if (!isNumber && number != null)
                isNumber = IsNumericType(number.GetType())
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
        /// A built-in number - integral or real, nullable or not - or any type that converts
        /// implicitly to one. This is what the arithmetic, comparison and unary operators mean by
        /// "number", and it is deliberately wider than <see cref="IsRealType"/>: a caller's own struct
        /// with <c>implicit operator int</c> is as much a number as one with
        /// <c>implicit operator decimal</c>, and it computes with the semantics of the built-in it
        /// converts to - so integer division truncates.
        /// </summary>
        /// <remarks>
        /// <b><c>char</c> and <c>bool</c> are absent on purpose.</b> A <c>char</c> is not a number in
        /// this language - <c>Letter + 1</c> throws on both backends - so a type converting to one is
        /// not a number either, and admitting it would make the two disagree.
        /// </remarks>
        public static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            return IsBuiltInRealType(type)
                || IsInteger(type)
                || TryGetImplicitNumericConversion(type, out _);
        }

        /// <summary>
        /// The implicit operator converting <paramref name="type"/> to a built-in real type, preferring
        /// decimal over double over float when the type offers more than one. A built-in real needs no
        /// conversion and yields false.
        /// </summary>
        /// <remarks>
        /// Real-only, and that narrowness is now deliberate rather than incidental: this answers
        /// <see cref="IsRealType"/>, whose one job is the round-versus-truncate question that refuses a
        /// real argument against an integral parameter. A type with <c>implicit operator int</c> is a
        /// number (<see cref="IsNumericType"/>) and is <i>not</i> real - answering true here would make
        /// <c>TakesInt(counter)</c> refuse a conversion that loses nothing.
        /// </remarks>
        public static bool TryGetImplicitRealConversion(Type type, out MethodInfo conversion)
        {
            return TryGetImplicitConversionToBuiltInNumber(
                type, integralTargetsCount: false, conversion: out conversion);
        }

        /// <summary>
        /// The implicit operator converting <paramref name="type"/> to any built-in number, preferring
        /// the target that loses least: decimal over double over float, and every real over every
        /// integral. A built-in number needs no conversion and yields false.
        /// </summary>
        /// <remarks>
        /// <b>The rank is consulted before the other operand is</b>, which is where this parts company
        /// with C#. C# builds a candidate list (<c>int+int</c>, <c>double+double</c>, …) and lets the
        /// pair of operands choose; this normalizes each operand on its own and then promotes. The two
        /// answer alike unless a type declares <i>several</i> numeric conversions - a type converting
        /// to both <c>int</c> and <c>double</c> divides as a double here and as an int in C#. Matching
        /// C# would mean porting its betterness rules over user-defined conversions into both backends,
        /// which is the same chapter of the specification <c>UserDefinedOperatorUtils</c> declines.
        /// <p>
        /// Ranking the reals above the integrals is what makes this purely additive: every shape it
        /// newly admits refused on both backends before, and no operand that already normalized to a
        /// real changes target.
        /// </p>
        /// </remarks>
        public static bool TryGetImplicitNumericConversion(Type type, out MethodInfo conversion)
        {
            return TryGetImplicitConversionToBuiltInNumber(
                type, integralTargetsCount: true, conversion: out conversion);
        }

        private static bool TryGetImplicitConversionToBuiltInNumber(
            Type type, bool integralTargetsCount, out MethodInfo conversion)
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
                var rank = RankOfNumericTarget(returnType, integralTargetsCount);

                if (rank > bestRank)
                {
                    bestRank = rank;
                    conversion = method;
                }
            }

            return conversion != null;
        }

        /// <summary>
        /// How good a conversion target a built-in number is, higher being better: the reals in order
        /// of what they keep, then the integrals in order of range. Anything else - <c>char</c>,
        /// <c>bool</c>, a reference type - is not a numeric target at all.
        /// </summary>
        private static int RankOfNumericTarget(Type target, bool integralTargetsCount)
        {
            if (target == typeof(decimal)) return 11;
            if (target == typeof(double)) return 10;
            if (target == typeof(float)) return 9;

            if (!integralTargetsCount)
                return 0;

            if (target == typeof(long)) return 8;
            if (target == typeof(ulong)) return 7;
            if (target == typeof(int)) return 6;
            if (target == typeof(uint)) return 5;
            if (target == typeof(short)) return 4;
            if (target == typeof(ushort)) return 3;
            if (target == typeof(sbyte)) return 2;
            if (target == typeof(byte)) return 1;

            return 0;
        }

        /// <summary>
        /// The user-defined implicit conversion operator taking <paramref name="from"/> to
        /// <paramref name="to"/>, declared on either type, or false.
        /// </summary>
        /// <remarks>
        /// <b>The general form of <see cref="TryGetImplicitRealConversion"/>, which is real-only.</b>
        /// That narrowness was a measured defect rather than a choice: a type with
        /// <c>implicit operator decimal</c> bound to a <c>decimal</c> parameter and joined in
        /// arithmetic, while one with <c>implicit operator int</c> did neither - and worse,
        /// <c>TakesInt(counter)</c> answered <c>7</c> compiled and threw <c>InvalidCastException</c>
        /// interpreted, because the emitter resolves an operator LINQ can see and the interpreter's
        /// converter only knew about reals.
        /// <p>
        /// <b>C# looks on both types</b>, so this does too: the source declares
        /// <c>op_Implicit(Money) -&gt; decimal</c>, while a target might declare
        /// <c>op_Implicit(int) -&gt; BigInteger</c>. An operator on the source must convert
        /// <i>from</i> it - conversion operators live on a type in both directions, and <c>decimal</c>
        /// itself declares <c>op_Implicit(int)</c>.
        /// </p>
        /// <p>
        /// <b>Two steps are allowed, as in C#:</b> an operator landing on a type that then widens
        /// implicitly to the target counts, so a <c>Money</c> reaches a <c>double</c> parameter via
        /// <c>decimal</c>. The operator is returned; the caller emits or invokes the widening itself.
        /// </p>
        /// <p>
        /// <b>Deliberately not implemented:</b> C#'s full user-defined conversion resolution - the
        /// candidate set from both types, the most-encompassing source and target types, lifted forms
        /// over nullables. Exact operand types only, which is the same limit
        /// <c>UserDefinedOperatorUtils</c> takes for operators and for the same reason.
        /// </p>
        /// </remarks>
        public static bool TryGetImplicitConversion(
            [CanBeNull] Type from, [CanBeNull] Type to, out MethodInfo conversion)
        {
            conversion = null;

            if (from == null || to == null || from == to)
                return false;

            return TryFindImplicitConversion(from, from, to, out conversion)
                   || TryFindImplicitConversion(to, from, to, out conversion);
        }

        private static bool TryFindImplicitConversion(
            [NotNull] Type declaringType, [NotNull] Type from, [NotNull] Type to,
            out MethodInfo conversion)
        {
            conversion = null;

            foreach (var method in declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "op_Implicit")
                    continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != from)
                    continue;

                if (method.ReturnType == to)
                {
                    conversion = method;
                    return true;
                }

                // The two-step case: the operator lands somewhere that widens to the target on its
                // own. Keep looking for an exact match rather than returning at once - an exact
                // operator is always the better answer.
                if (conversion == null && IsCSharpImplicitNumericConversion(method.ReturnType, to))
                    conversion = method;
            }

            return conversion != null;
        }

        private static bool IsBuiltInRealType(Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        /// <summary>
        /// Whether a value of static type <paramref name="type"/> can turn out to be something more
        /// specific at runtime.
        /// </summary>
        /// <remarks>
        /// <p>
        /// This is what decides whether the interpreter, which only ever sees runtime types, can reach
        /// the same answer as the compiled path, which only ever sees static ones. It is asked of every
        /// element of a collection <i>literal</i>: where nothing can narrow, both backends compute the
        /// same item type and the literal keeps it; where something can, the compiled path declines the
        /// literal so that only the interpreter runs and there is nothing to disagree with.
        /// </p>
        /// <p>
        /// Measured over the shapes a literal holds, rather than reasoned about:
        /// </p>
        /// <list type="bullet">
        /// <item><c>int</c>, <c>DateTime</c>, an enum - a non-nullable value type is exactly itself when
        /// boxed.</item>
        /// <item><c>string</c>, or any sealed class - nothing can derive from it.</item>
        /// <item><c>object</c>, or any non-sealed class or interface - the value may be a subtype, and
        /// the interpreter would name that subtype. A collection is one of these, which is why a
        /// literal holding a collection has no compiled form.</item>
        /// <item><c>int?</c> - boxing a nullable yields the underlying type or a null reference, never a
        /// <c>Nullable&lt;int&gt;</c>, so the two can never agree.</item>
        /// <item>An array is covariant only over reference element types: an <c>int[]</c> is always an
        /// <c>int[]</c>, while an <c>object[]</c> may be a <c>string[]</c>.</item>
        /// </list>
        /// <p>
        /// A null literal is not asked about by the callers at all - it contributes no runtime type, so
        /// both backends take the item type from the other elements and already agree.
        /// </p>
        /// </remarks>
        public static bool RuntimeCanNarrow([NotNull] Type type)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return elementType == null
                       || !elementType.IsValueType
                       || Nullable.GetUnderlyingType(elementType) != null;
            }

            if (type.IsValueType)
                return Nullable.GetUnderlyingType(type) != null;

            return !type.IsSealed;
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

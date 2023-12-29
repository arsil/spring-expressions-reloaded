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

#region Imports

using SpringExpressions.Util;
using System;
using System.ComponentModel;

#endregion

namespace SpringUtil
{
    /// <summary>
    /// Various utility methods relating to numbers.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Mainly for internal use within the framework.
    /// </p>
    /// </remarks>
    /// <author>Aleksandar Seovic</author>
    sealed class NumberUtils
    {
        /// <summary>
        /// Determines whether the supplied <paramref name="number"/> is an integer.
        /// </summary>
        /// <param name="number">The object to check.</param>
        /// <returns>
        /// <see lang="true"/> if the supplied <paramref name="number"/> is an integer.
        /// </returns>
        public static bool IsInteger(object number)
        {
			return (number is Int32 || number is Int64 || number is UInt32 || number is UInt64 
				|| number is Int16 || number is UInt16 || number is Byte || number is SByte);
        }

		/// <summary>
		/// Determines whether the supplied <paramref name="number"/> is an integer.
		/// </summary>
		/// <param name="number">The object to check.</param>
		/// <returns>
		/// <see lang="true"/> if the supplied <paramref name="number"/> is an integer.
		/// </returns>
		public static bool IsInteger(Type number)
		{
			return (number == typeof(Int32))|| number == typeof(Int64) || number == typeof(UInt32) || number == typeof(UInt64)
				|| number == typeof(Int16) || number == typeof(UInt16) || number == typeof(Byte) || number == typeof(SByte);
		}

		/// <summary>
		/// Determines whether the supplied <paramref name="number"/> is of numeric type.
		/// </summary>
		/// <param name="number">The object to check.</param>
		/// <returns>
		/// 	<c>true</c> if the specified object is of numeric type; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsNumber(object number)
        {
            var isNumber = (IsInteger(number) || IsNativeDecimal(number));
            if (!isNumber && number != null)
                isNumber = TypeDescriptor.GetConverter(number).CanConvertTo(typeof(Decimal));

            return isNumber;
        }

        /// <summary>
        /// Is the supplied <paramref name="number"/> equal to zero (0)?
        /// </summary>
        /// <param name="number">The number to check.</param>
        /// <returns>
        /// <see lang="true"/> id the supplied <paramref name="number"/> is equal to zero (0).
        /// </returns>
        public static bool IsZero(object number)
        {
            if (number is Int32)
                return ((Int32)number) == 0;
			else if (number is Decimal)
				return ((Decimal)number) == 0m;
            else if (number is Int64)
                return ((Int64)number) == 0;
            else if (number is UInt32)
                return ((UInt32)number) == 0;
            else if (number is UInt64)
                return (Convert.ToDecimal(number) == 0);
			else if (number is Int16)
				return ((Int16)number) == 0;
			else if (number is UInt16)
				return ((UInt16)number) == 0;
            else if (number is Byte)
                return ((Byte)number) == 0;
            else if (number is SByte)
                return ((SByte)number) == 0;
            else if (number is Single)
                return ((Single)number) == 0f;
            else if (number is Double)
                return ((Double)number) == 0d;
            return false;
        }

        /// <summary>
        /// Negates the supplied <paramref name="number"/>.
        /// </summary>
        /// <param name="number">The number to negate.</param>
        /// <returns>The supplied <paramref name="number"/> negated.</returns>
        /// <exception cref="System.ArgumentException">
        /// If the supplied <paramref name="number"/> is not a supported numeric type.
        /// </exception>
        public static object Negate(object number)
        {
            switch (number)
            {
                case Int32 int32Value:
                    return -int32Value;
                case Decimal decimalValue:
                    return -decimalValue;
                case Int64 int64Value:
                    return -int64Value;
                case Int16 int16Value:
                    return -int16Value;
                case UInt16 uint16Value:
                    return -uint16Value;
                case UInt32 uint32Value:
                    return -uint32Value;
                
                case ulong _:
                    throw new ArgumentException("Operator '-' cannot be applied to operand of type 'ulong'");

                case Byte byteValue:
                    return -byteValue;
                case SByte sbyteValue:
                    return -sbyteValue;
                case Single floatValue:
                    return -floatValue;
                case Double doubleValue:
                    return -doubleValue;
            }

            if (number != null)
            {
                var converter = TypeDescriptor.GetConverter(number);
                if (converter.CanConvertTo(typeof (Decimal)))
                {
                    var value = converter.ConvertTo(number, typeof (Decimal));
                    if (value != null)
                        return -((Decimal) value);
                }
            }
            throw new ArgumentException(string.Format("'{0}' is not one of the supported numeric types.", number));
        }

        public static object UnaryPlus(object number)
        {
            switch (number)
            {
                case Int32 int32Value:
                    return +int32Value;
                case Decimal decimalValue:
                    return +decimalValue;
                case Int64 int64Value:
                    return +int64Value;
                case Int16 int16Value:
                    return +int16Value;
                case UInt16 uint16Value:
                    return +uint16Value;
                case UInt32 uint32Value:
                    return +uint32Value;
                case ulong ulongValue:
                    return +ulongValue;
                case Byte byteValue:
                    return +byteValue;
                case SByte sbyteValue:
                    return +sbyteValue;
                case Single floatValue:
                    return +floatValue;
                case Double doubleValue:
                    return +doubleValue;
            }

            if (number != null)
            {
                var converter = TypeDescriptor.GetConverter(number);
                if (converter.CanConvertTo(typeof(Decimal)))
                {
                    var value = converter.ConvertTo(number, typeof(Decimal));
                    if (value != null)
                        return -((Decimal)value);
                }
            }
            throw new ArgumentException(string.Format("'{0}' is not one of the supported numeric types.", number));
        }


        /// <summary>
        /// Returns the bitwise not (~) of the supplied <paramref name="number"/>.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The value of ~<paramref name="number"/>.</returns>
        /// <exception cref="System.ArgumentException">
        /// If the supplied <paramref name="number"/> is not a supported numeric type.
        /// </exception>
        public static object BitwiseNot(object number)
        {
            if (number is bool)
                return !((bool)number);
            else if (number is Int32)
                return ~((Int32)number);
            else if (number is Int16)
                return ~((Int16)number);
            else if (number is Int64)
                return ~((Int64)number);
            else if (number is UInt16)
                return ~((UInt16)number);
            else if (number is UInt32)
                return ~((UInt32)number);
            else if (number is UInt64)
                return ~((UInt64)number);
            else if (number is Byte)
                return ~((Byte)number);
            else if (number is SByte)
                return ~((SByte)number);
            else
            {
                throw new ArgumentException(string.Format("'{0}' is not one of the supported integer types.", number));
            }
        }

        /// <summary>
        /// Bitwise ANDs (&amp;) the specified integral values.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        /// <exception cref="System.ArgumentException">
        /// If one of the supplied arguments is not a supported integral types.
        /// </exception>
        public static object BitwiseAnd(object m, object n)
        {
            return NumericBinaryOperations.And(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is bool)
                return (bool)m & (bool)n;
            else if (n is Int32)
                return (Int32)m & (Int32)n;
            else if (n is Int16)
                return (Int16)m & (Int16)n;
            else if (n is Int64)
                return (Int64)m & (Int64)n;
            else if (n is UInt16)
                return (UInt16)m & (UInt16)n;
            else if (n is UInt32)
                return (UInt32)m & (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m & (UInt64)n;
            else if (n is Byte)
                return (Byte)m & (Byte)n;
            else if (n is SByte)
                return (SByte)m & (SByte)n;
            else
            {
                throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported integral types.", m, n));
            }
            */
        }

        /// <summary>
        /// Bitwise ORs (|) the specified integral values.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        /// <exception cref="System.ArgumentException">
        /// If one of the supplied arguments is not a supported integral types.
        /// </exception>
        public static object BitwiseOr(object m, object n)
        {
            return NumericBinaryOperations.Or(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is bool)
                return (bool)m | (bool)n;
            else if (n is Int32)
                return (Int32)m | (Int32)n;
            else if (n is Int16)
                return (Int16)m | (Int16)n;
            else if (n is Int64)
                return (Int64)m | (Int64)n;
            else if (n is UInt16)
                return (UInt16)m | (UInt16)n;
            else if (n is UInt32)
                return (UInt32)m | (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m | (UInt64)n;
            else if (n is Byte)
                return (Byte)m | (Byte)n;
            else if (n is SByte)
            {
                if (SystemUtils.MonoRuntime)
                {
                    SByte x = (sbyte) n;
                    SByte y = (sbyte) m;
                    int result = (int) x | (int) y;
                    return SByte.Parse(result.ToString());
                }
                return (SByte) ((SByte) m | (SByte) n);
            }
            throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported integral types.", m, n));
            */
        }

        /// <summary>
        /// Bitwise XORs (^) the specified integral values.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        /// <exception cref="System.ArgumentException">
        /// If one of the supplied arguments is not a supported integral types.
        /// </exception>
        public static object BitwiseXor(object m, object n)
        {
            return NumericBinaryOperations.Xor(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is bool)
                return (bool)m ^ (bool)n;
            else if (n is Int32)
                return (Int32)m ^ (Int32)n;
            else if (n is Int16)
                return (Int16)m ^ (Int16)n;
            else if (n is Int64)
                return (Int64)m ^ (Int64)n;
            else if (n is UInt16)
                return (UInt16)m ^ (UInt16)n;
            else if (n is UInt32)
                return (UInt32)m ^ (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m ^ (UInt64)n;
            else if (n is Byte)
                return (Byte)m ^ (Byte)n;
            else if (n is SByte)
                return (SByte)m ^ (SByte)n;
            else
            {
                throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported integral types.", m, n));
            }
            */
        }

        /// <summary>
        /// Adds the specified numbers.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        public static object Add(object m, object n)
        {
            return NumericBinaryOperations.Add(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is Int32)
                return (Int32)m + (Int32)n;
			else if (n is Decimal)
				return (Decimal)m + (Decimal)n;
            else if (n is Int64)
                return (Int64)m + (Int64)n;
            else if (n is UInt32)
                return (UInt32)m + (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m + (UInt64)n;
			else if (n is Int16)
				return (Int16)m + (Int16)n;
			else if (n is UInt16)
				return (UInt16)m + (UInt16)n;
            else if (n is Byte)
                return (Byte)m + (Byte)n;
            else if (n is SByte)
                return (SByte)m + (SByte)n;
            else if (n is Single)
                return (Single)m + (Single)n;
            else if (n is Double)
                return (Double)m + (Double)n;
            else
            {
                throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported numeric types.", m, n));
            }*/
        }
/*
	    public static bool AddIfPossible(object m, object n, out object result)
	    {
		    var mConv = m as IConvertible;
		    var nConv = n as IConvertible;

		    if (mConv != null && nConv != null)
		    {
			    var mTc = mConv.GetTypeCode();
				var nTc = nConv.GetTypeCode();

				if (mTc != nTc)
					throw new Exception("NotImplemented");

			    switch (nTc)
			    {
					case TypeCode.Int32:
					    result = (Int32) m + (Int32) n;
					    return true;
			    }

			}

		    result = null;
		    return false;
	    }
*/
	    /// <summary>
        /// Subtracts the specified numbers.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        public static object Subtract(object m, object n)
        {
            return NumericBinaryOperations.Sub(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is Int32)
                return (Int32)m - (Int32)n;
			else if (n is Decimal)
				return (Decimal)m - (Decimal)n;
            else if (n is Int64)
                return (Int64)m - (Int64)n;
            else if (n is UInt32)
                return (UInt32)m - (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m - (UInt64)n;
			else if (n is Int16)
				return (Int16)m - (Int16)n;
			else if (n is UInt16)
				return (UInt16)m - (UInt16)n;
            else if (n is Byte)
                return (Byte)m - (Byte)n;
            else if (n is SByte)
                return (SByte)m - (SByte)n;
            else if (n is Single)
                return (Single)m - (Single)n;
            else if (n is Double)
                return (Double)m - (Double)n;
            else
            {
                throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported numeric types.", m, n));
            }
            */
        }

        /// <summary>
        /// Multiplies the specified numbers.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        public static object Multiply(object m, object n)
        {
            return NumericBinaryOperations.Mul(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is Int32)
                return (Int32)m * (Int32)n;
			else if (n is Decimal)
				return (Decimal)m * (Decimal)n;
            else if (n is Int64)
                return (Int64)m * (Int64)n;
            else if (n is UInt32)
                return (UInt32)m * (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m * (UInt64)n;
			else if (n is Int16)
				return (Int16)m * (Int16)n;
			else if (n is UInt16)
				return (UInt16)m * (UInt16)n;
            else if (n is Byte)
                return (Byte)m * (Byte)n;
            else if (n is SByte)
                return (SByte)m * (SByte)n;
            else if (n is Single)
                return (Single)m * (Single)n;
            else if (n is Double)
                return (Double)m * (Double)n;
            else
            {
                throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported numeric types.", m, n));
            }
            */
        }

        /// <summary>
        /// Divides the specified numbers.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        public static object Divide(object m, object n)
        {
            return NumericBinaryOperations.Div(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is Int32)
                return (Int32)m / (Int32)n;
			else if (n is Decimal)
				return (Decimal)m / (Decimal)n;
            else if (n is Int64)
                return (Int64)m / (Int64)n;
            else if (n is UInt32)
                return (UInt32)m / (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m / (UInt64)n;
			else if (n is Int16)
				return (Int16)m / (Int16)n;
			else if (n is UInt16)
				return (UInt16)m / (UInt16)n;
            else if (n is Byte)
                return (Byte)m / (Byte)n;
            else if (n is SByte)
                return (SByte)m / (SByte)n;
            else if (n is Single)
                return (Single)m / (Single)n;
            else if (n is Double)
                return (Double)m / (Double)n;
            else
            {
                throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported numeric types.", m, n));
            }
            */
        }

        /// <summary>
        /// Calculates remainder for the specified numbers.
        /// </summary>
        /// <param name="m">The first number (dividend).</param>
        /// <param name="n">The second number (divisor).</param>
        public static object Modulus(object m, object n)
        {
            return NumericBinaryOperations.Mod(m, n);
            /*
            CoerceTypes(ref m, ref n);

            if (n is Int32)
                return (Int32)m % (Int32)n;
			else if (n is Decimal)
				return (Decimal)m % (Decimal)n;
            else if (n is Int64)
                return (Int64)m % (Int64)n;
            else if (n is UInt32)
                return (UInt32)m % (UInt32)n;
            else if (n is UInt64)
                return (UInt64)m % (UInt64)n;
			else if (n is Int16)
				return (Int16)m % (Int16)n;
			else if (n is UInt16)
				return (UInt16)m % (UInt16)n;
            else if (n is Byte)
                return (Byte)m % (Byte)n;
            else if (n is SByte)
                return (SByte)m % (SByte)n;
            else if (n is Single)
                return (Single)m % (Single)n;
            else if (n is Double)
                return (Double)m % (Double)n;
            else
            {
                throw new ArgumentException(string.Format("'{0}' and/or '{1}' are not one of the supported numeric types.", m, n));
            }*/
        }

        /// <summary>
        /// Raises first number to the power of the second one.
        /// </summary>
        /// <param name="m">The first number.</param>
        /// <param name="n">The second number.</param>
        public static object Power(object m, object n)
        {
            return Math.Pow(Convert.ToDouble(m), Convert.ToDouble(n));
        }

        /// <summary>
        /// Coerces the types so they can be compared.
        /// </summary>
        /// <param name="m">The right.</param>
        /// <param name="n">The left.</param>
        public static void CoerceTypes(ref object m, ref object n)
        {
            TypeCode leftTypeCode = Convert.GetTypeCode(m);
            TypeCode rightTypeCode = Convert.GetTypeCode(n);

            if (leftTypeCode == TypeCode.Object && m != null)
            {
                var mConverter = TypeDescriptor.GetConverter(m);
                m = mConverter.ConvertTo(m, typeof(decimal));
                leftTypeCode = TypeCode.Decimal;
            }

            if (rightTypeCode == TypeCode.Object && n != null)
            {
                var nConverter = TypeDescriptor.GetConverter(n);
                n = nConverter.ConvertTo(n, typeof(decimal));
				rightTypeCode = TypeCode.Decimal;
			}


            if (leftTypeCode > rightTypeCode)
            {
                n = Convert.ChangeType(n, leftTypeCode);

            }
            else if (leftTypeCode < rightTypeCode)
            {
                m = Convert.ChangeType(m, rightTypeCode);
            }
        }

		/// <summary>
		/// Determines whether the supplied <paramref name="number"/> is a decimal number.
		/// </summary>
		/// <param name="number">The object to check.</param>
		/// <returns>
		/// <see lang="true"/> if the supplied <paramref name="number"/> is a decimal number.
		/// </returns>
		private static bool IsNativeDecimal(object number)
		{
			return (number is Single || number is Double || number is Decimal);
		}

		#region Constructor (s) / Destructor

		// CLOVER:OFF

		/// <summary>
		/// Creates a new instance of the <see cref="SpringUtil.NumberUtils"/> class.
		/// </summary>
		/// <remarks>
		/// <p>
		/// This is a utility class, and as such exposes no public constructors.
		/// </p>
		/// </remarks>
		private NumberUtils()
        {
        }

        // CLOVER:ON

        #endregion
    }
}
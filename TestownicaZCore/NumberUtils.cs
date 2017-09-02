#region License

/*
 * Copyright � 2002-2011 the original author or authors.
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
using System.Collections.Generic;
using System.ComponentModel;
using Core.Types;

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
		/// Determines whether the supplied <paramref name="number"/> is of numeric type.
		/// </summary>
		/// <param name="number">The object to check.</param>
		/// <returns>
		/// 	<c>true</c> if the specified object is of numeric type; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsNumber(object number)
		{
			var isNumber = (IsInteger(number) || IsDecimal(number));
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
				return ((Int32) number) == 0;
			else if (number is Decimal)
				return ((Decimal) number) == 0m;
			else if (number is Int64)
				return ((Int64) number) == 0;
			else if (number is UInt32)
				return ((UInt32) number) == 0;
			else if (number is UInt64)
				return (Convert.ToDecimal(number) == 0);
			else if (number is Int16)
				return ((Int16) number) == 0;
			else if (number is UInt16)
				return ((UInt16) number) == 0;
			else if (number is Byte)
				return ((Byte) number) == 0;
			else if (number is SByte)
				return ((SByte) number) == 0;
			else if (number is Single)
				return ((Single) number) == 0f;
			else if (number is Double)
				return ((Double) number) == 0d;
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
			if (number is Int32)
				return -((Int32) number);
			else if (number is Decimal)
				return -((Decimal) number);
			else if (number is Int64)
				return -((Int64) number);
			else if (number is Int16)
				return -((Int16) number);
			else if (number is UInt16)
				return -((Int32) number);
			else if (number is UInt32)
				return -((Int64) number);
			else if (number is UInt64)
				return -(Convert.ToDecimal(number));
			else if (number is Byte)
				return -((Int16) number);
			else if (number is SByte)
				return -((Int16) number);
			else if (number is Single)
				return -((Single) number);
			else if (number is Double)
				return -((Double) number);
			else
			{
				if (number != null)
				{
					var converter = TypeDescriptor.GetConverter(number);
					if (converter.CanConvertTo(typeof(Decimal)))
					{
						var value = converter.ConvertTo(number, typeof(Decimal));
						if (value != null)
							return -((Decimal) value);
					}
				}
				throw new ArgumentException(string.Format("'{0}' is not one of the supported numeric types.",
					number));
			}
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
				return !((bool) number);
			else if (number is Int32)
				return ~((Int32) number);
			else if (number is Int16)
				return ~((Int16) number);
			else if (number is Int64)
				return ~((Int64) number);
			else if (number is UInt16)
				return ~((UInt16) number);
			else if (number is UInt32)
				return ~((UInt32) number);
			else if (number is UInt64)
				return ~((UInt64) number);
			else if (number is Byte)
				return ~((Byte) number);
			else if (number is SByte)
				return ~((SByte) number);
			else
			{
				throw new ArgumentException(string.Format("'{0}' is not one of the supported integer types.",
					number));
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
			CoerceTypes(ref m, ref n);

			if (n is bool)
				return (bool) m & (bool) n;
			else if (n is Int32)
				return (Int32) m & (Int32) n;
			else if (n is Int16)
				return (Int16) m & (Int16) n;
			else if (n is Int64)
				return (Int64) m & (Int64) n;
			else if (n is UInt16)
				return (UInt16) m & (UInt16) n;
			else if (n is UInt32)
				return (UInt32) m & (UInt32) n;
			else if (n is UInt64)
				return (UInt64) m & (UInt64) n;
			else if (n is Byte)
				return (Byte) m & (Byte) n;
			else if (n is SByte)
				return (SByte) m & (SByte) n;
			else
			{
				throw new ArgumentException(
					string.Format("'{0}' and/or '{1}' are not one of the supported integral types.", m, n));
			}
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
			CoerceTypes(ref m, ref n);

			if (n is bool)
				return (bool) m ^ (bool) n;
			else if (n is Int32)
				return (Int32) m ^ (Int32) n;
			else if (n is Int16)
				return (Int16) m ^ (Int16) n;
			else if (n is Int64)
				return (Int64) m ^ (Int64) n;
			else if (n is UInt16)
				return (UInt16) m ^ (UInt16) n;
			else if (n is UInt32)
				return (UInt32) m ^ (UInt32) n;
			else if (n is UInt64)
				return (UInt64) m ^ (UInt64) n;
			else if (n is Byte)
				return (Byte) m ^ (Byte) n;
			else if (n is SByte)
				return (SByte) m ^ (SByte) n;
			else
			{
				throw new ArgumentException(
					string.Format("'{0}' and/or '{1}' are not one of the supported integral types.", m, n));
			}
		}

		/// <summary>
		/// Adds the specified numbers.
		/// </summary>
		/// <param name="m">The first number.</param>
		/// <param name="n">The second number.</param>
		public static object Add(object m, object n)
		{
//			CoerceTypes(ref m, ref n);

			if (n is Int32)
				return (Int32) m + (Int32) n;
			else if (n is Decimal)
				return (Decimal) m + (Decimal) n;
			else if (n is Int64)
				return (Int64) m + (Int64) n;
			else if (n is UInt32)
				return (UInt32) m + (UInt32) n;
			else if (n is UInt64)
				return (UInt64) m + (UInt64) n;
			else if (n is Int16)
				return (Int16) m + (Int16) n;
			else if (n is UInt16)
				return (UInt16) m + (UInt16) n;
			else if (n is Byte)
				return (Byte) m + (Byte) n;
			else if (n is SByte)
				return (SByte) m + (SByte) n;
			else if (n is Single)
				return (Single) m + (Single) n;
			else if (n is Double)
				return (Double) m + (Double) n;
			else
			{
				throw new ArgumentException(
					string.Format("'{0}' and/or '{1}' are not one of the supported numeric types.", m, n));
			}
		}

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

		// todo: konwersje do Decimala i podobnych?

		public static object AddIfPossible2(object m, object n)
		{
			if (m is int)
				return MathHelperIntType.Add((int) m, n);
			
			if (m is decimal)
				throw new NotImplementedException();

			if (m is long)
				return MathHelperLongType.Add((long) m, n);

			if (m is double)
				return MathHelperDoubleType.Add((double)m, n);





			// todo: check if convertible to decimal?
			// todo: czy to si� op�aca? mo�e dla pewnych typ�w lepiej z haszmapy bra� operatory? b�dzie wolniej. ale bez przesady

			// ulongi te� s� rzadk u�ywane chyba....
			if (m is uint)
				throw new NotImplementedException();
			if (m is ulong)
				throw new NotImplementedException();


			if (m is float)
				throw new NotImplementedException();

			// to jest totalny plankton?
			if (m is short)
				throw new NotImplementedException();
			if (m is ushort)
				throw new NotImplementedException();
			if (m is byte)
				throw new NotImplementedException();
			if (m is sbyte)
				return MathHelperSByteType.Add((sbyte)m, n);

			return null;
		}


		// wolne... 3 x wolniejsze....
		public static object AddIfPossible5(object m, object n)
		{
			var kvp = new Kulas(m.GetType(), n.GetType());
			Func<object, object, object> func;

			//lock (ops)
				if (ops.TryGetValue(kvp, out func))
					return func(m, n);

			return null;
		}

		public static object AddIfPossible6(object m, object n)
		{
			var convM = m as IConvertible;
			var convN = n as IConvertible;
			if (convM == null || convN == null)
				return null;

			var mTypeCode = (int) convM.GetTypeCode();
			var nTypeCode = (int) convN.GetTypeCode();

			if (mTypeCode < 16 && nTypeCode < 16)
				return AddOperationsHelper.AdditionsSingleArray[(mTypeCode << 4) + nTypeCode](m, n);
				//				return AddOperationsHelper.Additions[mTypeCode][nTypeCode](m, n);


				return null;
		}

		public static object AddIfPossible7(object m, object n)
		{
			var convM = m as IConvertible;
			var convN = n as IConvertible;
			if (convM == null || convN == null)
				return null;

			var mTypeCode = (int)convM.GetTypeCode();
			var nTypeCode = (int)convN.GetTypeCode();

			if (mTypeCode < 16 && nTypeCode < 16)
				return AddOperationsHelper.Additions[mTypeCode][nTypeCode](m, n);


			return null;
		}

		private static Dictionary<Kulas, Func<object, object, object>> ops =
			new Dictionary<Kulas, Func<object, object, object>>()
			{
				{ new Kulas(typeof(int), typeof(int)),
					(a, b) => (int) a + (int) b},
				{ new Kulas(typeof(int), typeof(sbyte)),
					(a, b) => (int) a + (sbyte) b}
			};

	// todo: jak wykry� SZYBKO, �e dany typ jest konwertowalny do Decimala? -----------------------------------------------------

		private class Kulas
		{
			public Kulas(Type t1, Type t2)
			{
				this.t1 = t1;
				this.t2 = t2;
			}

			public override bool Equals(object other)
			{
				return t1 == ((Kulas)other).t1 && t2 == ((Kulas)other).t2;
//				return Equals(t1, ((Kulas)other).t1) && Equals(t2, ((Kulas)other).t2);
			}



			public override int GetHashCode()
			{
				unchecked
				{
					return (t1.GetHashCode() * 397) ^ t2.GetHashCode();
				}
			}

			private readonly Type t1, t2;
		}


		/// <summary>
		/// Subtracts the specified numbers.
		/// </summary>
		/// <param name="m">The first number.</param>
		/// <param name="n">The second number.</param>
		public static object Subtract(object m, object n)
		{
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
		}

		/// <summary>
		/// Multiplies the specified numbers.
		/// </summary>
		/// <param name="m">The first number.</param>
		/// <param name="n">The second number.</param>
		public static object Multiply(object m, object n)
		{
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
		}

		/// <summary>
		/// Divides the specified numbers.
		/// </summary>
		/// <param name="m">The first number.</param>
		/// <param name="n">The second number.</param>
		public static object Divide(object m, object n)
		{
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
		}

		/// <summary>
		/// Calculates remainder for the specified numbers.
		/// </summary>
		/// <param name="m">The first number (dividend).</param>
		/// <param name="n">The second number (divisor).</param>
		public static object Modulus(object m, object n)
		{
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
			}
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
				m = mConverter.ConvertTo(m, typeof(Decimal));
				leftTypeCode = Convert.GetTypeCode(m);
			}

			if (rightTypeCode == TypeCode.Object && n != null)
			{
				var nConverter = TypeDescriptor.GetConverter(n);
				n = nConverter.ConvertTo(n, typeof(Decimal));
				rightTypeCode = Convert.GetTypeCode(n);
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
		private static bool IsDecimal(object number)
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
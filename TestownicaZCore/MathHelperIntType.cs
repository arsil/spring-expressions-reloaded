using System;
using System.Collections.Generic;


namespace SpringUtil
{
	// kolejność: int, decimal, long, double.....
	class MathHelperIntType
	{
		public static object Add(int l, object r)
		{
			if (r is int)
				return l + (int) r;
			if (r is decimal)
				return l + (decimal)r;
			if (r is long)
				return l + (long)r;
			if (r is double)
				return l + (double)r;

			// todo: check if convertible to decimal?
			// todo: czy to się opłaca? może dla pewnych typów lepiej z haszmapy brać operatory? będzie wolniej. ale bez przesady


			if (r is uint)
				return (uint)l + (uint)r;
			if (r is ulong)
				return (ulong)l + (ulong)r;
			if (r is float)
				return l + (float)r;
			// to jest totalny plankton?
			if (r is short)
				return l+ (short)r;
			if (r is ushort)
				return l + (ushort)r;
			if (r is byte)
				return l + (byte)r;
			if (r is sbyte)
				return l + (sbyte)r;



			return null;
		}
	}

	class MathHelperLongType
	{
		public static object Add(long l, object r)
		{
			if (r is int)
				return l + (int) r;
			if (r is decimal)
				return l + (decimal) r;
			if (r is long)
				return l + (long) r;
			if (r is double)
				return l + (double)r;


			// todo: check if convertible to decimal?
			// todo: czy to się opłaca? może dla pewnych typów lepiej z haszmapy brać operatory? będzie wolniej. ale bez przesady

			// ulongi też są rzadk używane chyba....
			if (r is uint)
				return (uint) l + (uint) r;
			if (r is ulong)
				return (ulong) l + (ulong) r;


			if (r is float)
				return l + (float) r;

			// to jest totalny plankton?
			if (r is short)
				return l + (short) r;
			if (r is ushort)
				return l + (ushort) r;
			if (r is byte)
				return l + (byte) r;
			if (r is sbyte)
				return l + (sbyte) r;



			return null;
		}

	}

	class MathHelperDoubleType
	{
		public static object Add(double l, object r)
		{
			if (r is int)
				return l + (int)r;
			if (r is decimal)
				return (decimal)l + (decimal)r;
			if (r is long)
				return l + (long)r;
			if (r is double)
				return l + (double)r;



			// todo: check if convertible to decimal?
			// todo: czy to się opłaca? może dla pewnych typów lepiej z haszmapy brać operatory? będzie wolniej. ale bez przesady

			// ulongi też są rzadk używane chyba....
			if (r is uint)
				return (uint)l + (uint)r;
			if (r is ulong)
				return (ulong)l + (ulong)r;


			if (r is float)
				return l + (float)r;

			// to jest totalny plankton?
			if (r is short)
				return l + (short)r;
			if (r is ushort)
				return l + (ushort)r;
			if (r is byte)
				return l + (byte)r;
			if (r is sbyte)
				return l + (sbyte)r;



			return null;
		}

	}

	class MathHelperSByteType
	{
		public static object Add(sbyte l, object r)
		{
			if (r is int)
				return l + (int)r;

			if (r is decimal)
				return l + (decimal)r;

			if (r is long)
				return l + (long)r;

			if (r is double)
				return l + (double)r;


			//Func<object, object, object> func;
			//if (opAdd.TryGetValue(r.GetType(), out func))
			//	return func(l, r);

			var convR = r as IConvertible;
			if (convR != null)
			{
				return functions[(int)convR.GetTypeCode()](l, r);
			}

			throw new InvalidOperationException("srypałem się.");

			// todo: check if convertible to decimal?
			// todo: czy to się opłaca? może dla pewnych typów lepiej z haszmapy brać operatory? będzie wolniej. ale bez przesady

			// ulongi też są rzadk używane chyba....
			if (r is uint)
				return (uint)l + (uint)r;
			if (r is ulong)
				return (ulong)l + (ulong)r;


			if (r is float)
				return l + (float)r;

			// to jest totalny plankton?
			if (r is short)
				return l + (short)r;
			if (r is ushort)
				return l + (ushort)r;
			if (r is byte)
				return l + (byte)r;
			if (r is sbyte)
				return l + (sbyte)r;



			return null;
		}

		private static Dictionary<Type, Func<object, object, object>> opAdd = new Dictionary
			<Type, Func<object, object, object>>()
			{
				{typeof(sbyte), (a, b) => (sbyte) a + (sbyte) b}
			};

		private static readonly Func<sbyte, object, object>[] functions = new Func<sbyte, object, object>[19]
		{
			(a, b) => null, //Empty = 0,
			(a, b) => null, //Object = 1, // todo: funkcja rzutująca! to jest zajebisty pomysł!
			(a, b) => null, //DBNull = 2,
			(a, b) => null, //Boolean = 3,
			(a, b) => null, //Char = 4,
			(a, b) => a + (sbyte)b, //SByte = 5,
			null, //Byte = 6,
			null, //Int16 = 7,
			null, //UInt16 = 8,
			null, //Int32 = 9,
			null, //UInt32 = 10,
			null, //Int64 = 11,
			null, //UInt64 = 12,
			null, //Single = 13,
			null, //Double = 14,
			null, //Decimal = 15,
			(a, b) => null, //DateTime = 16,
			(a, b) => null, // brak wartości
			(a, b) => null, //String = 18,
		};

	}


}

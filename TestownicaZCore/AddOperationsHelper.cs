using System;
using System.Collections.Generic;
using System.Text;

namespace SpringUtil
{
	delegate object BinaryOp(object a, object b);

	static class AddOperationsHelper
	{
		private static BinaryOp[] ProduceKiszka()
		{
			var result = new BinaryOp[256];

			result[(9 << 4) + 9] = (a, b) => (int) a + (int) b;


			return result;
		}

		public static readonly BinaryOp[] AdditionsSingleArray = ProduceKiszka();




		// dodawanie dla intów
		private static readonly BinaryOp[] intAddition = new BinaryOp[19]
			{
				(a, b) => null, //Empty = 0,
				(a, b) => null, //Object = 1, // todo: funkcja rzutująca! to jest zajebisty pomysł!
				(a, b) => null, //DBNull = 2,
				(a, b) => null, //Boolean = 3,
				(a, b) => null, //Char = 4,
				(a, b) => (int)a + (sbyte)b, //SByte = 5,
				(a, b) => (int)a + (byte)b, //Byte = 6,
				(a, b) => (int)a + (short)b, //Int16 = 7,
				(a, b) => (int)a + (ushort)b, //UInt16 = 8,
				(a, b) => (int)a + (int)b, //Int32 = 9,
				(a, b) => (int)a + (uint)b, //UInt32 = 10,
				(a, b) => (int)a + (long)b, //Int64 = 11,
				(a, b) => (ulong)(int)a + (ulong)b, //UInt64 = 12,
				(a, b) => (int)a + (float)b, //Single = 13,
				(a, b) => (int)a + (double)b, //Double = 14,
				(a, b) => (int)a + (decimal)b, //Decimal = 15,
				(a, b) => null, //DateTime = 16,
				(a, b) => null, // brak wartości
				(a, b) => null, //String = 18,
			};

		// dodawanie dla decimali
		private static readonly BinaryOp[] decimalAddition = new BinaryOp[19]
			{
				(a, b) => null, //Empty = 0,
				(a, b) => null, //Object = 1, // todo: funkcja rzutująca! to jest zajebisty pomysł!
				(a, b) => null, //DBNull = 2,
				(a, b) => null, //Boolean = 3,
				(a, b) => null, //Char = 4,
				(a, b) => (decimal)a + (sbyte)b, //SByte = 5,
				(a, b) => (decimal)a + (byte)b, //Byte = 6,
				(a, b) => (decimal)a + (short)b, //Int16 = 7,
				(a, b) => (decimal)a + (ushort)b, //UInt16 = 8,
				(a, b) => (decimal)a + (int)b, //Int32 = 9,
				(a, b) => (decimal)a + (uint)b, //UInt32 = 10,
				(a, b) => (decimal)a + (long)b, //Int64 = 11,
				(a, b) => (decimal)a + (ulong)b, //UInt64 = 12,
				(a, b) => (decimal)a + (decimal)(float)b, //Single = 13,
				(a, b) => (decimal)a + (decimal)(double)b, //Double = 14,
				(a, b) => (decimal)a + (decimal)b, //Decimal = 15,
				(a, b) => null, //DateTime = 16,
				(a, b) => null, // brak wartości
				(a, b) => null, //String = 18,
			};



		public static readonly BinaryOp[][] Additions = new BinaryOp[19][]
			{
				null, // Empty = 0,
				null, // Object = 1,
				null, // 	DBNull = 2,
				null, // 	Boolean = 3,
				null, // 	Char = 4,
				null, // 	SByte = 5,
				null, // 	Byte = 6,
				null, // 	Int16 = 7,
				null, // 	UInt16 = 8,
				intAddition, // 	Int32 = 9,
				null, // 	UInt32 = 10,
				null, // 	Int64 = 11,
				null, // 	UInt64 = 12,
				null, // 	Single = 13,
				null, // 	Double = 14,
				decimalAddition, // 	Decimal = 15,
				null, // 	DateTime = 16,
				null, //    brak wartości
				null, // 	String = 18,
			};









		private static readonly BinaryOp[] sByteAddition = new BinaryOp[19]
			{
				(a, b) => null, //Empty = 0,
				(a, b) => null, //Object = 1, // todo: funkcja rzutująca! to jest zajebisty pomysł!
				(a, b) => null, //DBNull = 2,
				(a, b) => null, //Boolean = 3,
				(a, b) => null, //Char = 4,
				(a, b) => (sbyte)a + (sbyte)b, //SByte = 5,
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

using System;
using System.ComponentModel;
using Core.Types;
using SpringCore.TypeResolution;
using SpringExpressions;

namespace TestownicaZCore
{
	class Program
	{
		static class ToolsS
		{
			    // stringi... fullprecitionstring, itd.
			    // a także do typów numerycznych!
			public static string Dec2(Decimal dec)
			{
				return "*" + dec.ToString();
			}

			public static Decimal Decimal(Decimal2 dec)
			{
				return dec;
			}
		}

		class Potylica
		{
			public Decimal DecVal { get; set; }
			public Decimal2 Dec2Val { get; set; }
			public Decimal3 Dec3Val { get; set; }
		}


		static void Main(string[] args)
		{
			var potylica = new Potylica
				{
					DecVal = 12.12m,
					Dec2Val = (Decimal2)3.45m,
					Dec3Val = (Decimal3)3.451m
				};

			TypeConverter converter = TypeDescriptor.GetConverter(potylica.Dec2Val);

			bool canConvert = converter.CanConvertFrom(typeof (Decimal));
			Console.WriteLine("Can convert: " + canConvert);



			TypeRegistry.RegisterType("To", typeof(ToolsS));

			Console.WriteLine((string)ExpressionEvaluator.GetValue(null, "'Hello World'"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(null, "2m * 3m"));


			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "2m * Dec2Val"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "Dec2Val * 2m"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "Dec2Val * Dec2Val"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "Dec3Val * Dec2Val"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "Dec2Val * Dec3Val"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "-Dec3Val"));

			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "4 * Dec2Val"));

			Console.WriteLine((bool)ExpressionEvaluator.GetValue(potylica, "Dec2Val == Dec3Val"));

			Console.WriteLine((string)ExpressionEvaluator.GetValue(potylica, "(2m * DecVal).ToString()"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "2m * To.Decimal(Dec2Val)"));
			Console.WriteLine((string)ExpressionEvaluator.GetValue(potylica, "To.Dec2(6 * DecVal)"));
			Console.WriteLine((Decimal)ExpressionEvaluator.GetValue(potylica, "Dec2Val = 2m"));
			Console.WriteLine((Decimal2)ExpressionEvaluator.GetValue(potylica, "Dec2Val = new Core.Types.Decimal2(3m)"));

			Console.WriteLine((string)ExpressionEvaluator.GetValue(potylica, "Dec2Val.ToFullPrecisionString()"));

			Console.ReadKey();
		}
	}
}

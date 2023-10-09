using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Core.Types;
using PerformanceMeasurement;
using SpringCore.TypeResolution;
using SpringExpressions;
using SpringUtil;

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

		class Stolec
		{
			public decimal WewDec { get; set; }
			public decimal Kiszka = 12;
		}

		class Potylica
		{
			public decimal DecVal { get; set; }
			public Decimal2 Dec2Val { get; set; }
			public Decimal3 Dec3Val { get; set; }

			public Stolec Stolec { get; set; }

			public Potylica()
			{
				Stolec = new Stolec();
			}

			public decimal DawajDecimala()
			{
				return 23m;
			}
			public decimal DawajDecimala(decimal value)
			{
				return value;
			}
		}


		static void Main(string[] args)
		{
			var potylica = new Potylica
				{
					DecVal = 12.12m,
					Dec2Val = (Decimal2)3.45m,
					Dec3Val = (Decimal3)3.451m,
					Stolec = new Stolec()
				};

			potylica.Stolec.WewDec = 12.1m;

			TypeConverter converter = TypeDescriptor.GetConverter(potylica.Dec2Val);

			bool canConvert = converter.CanConvertFrom(typeof (Decimal));
			Console.WriteLine("Can convert: " + canConvert);



			TypeRegistry.RegisterType("To", typeof(ToolsS));
			/*
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
*/
			var opAdd = new OpADD();
			opAdd.addChild(new IntLiteralNode("2"));
			opAdd.addChild(new IntLiteralNode("5"));
			var ctx = new object();
			int rezultat = 1;
			int kupa = 1;
			sbyte kupaSbyte = 3;
			long kupaLong = 3;
			double kupaDouble = 3;
			MultiSampleCodeTimer timer = new MultiSampleCodeTimer(8, 1000000);
			opAdd.GetValue(ctx);
			/**/

			var promoContext = new PromoContext();

			// działa zajebiście
			{
				var exp = Expression.Parse("_Attr.Has('CurrentReceipt.TotalQuantityForArticleIndex0000125837')");
				var result = exp.GetValue<bool, PromoContext>(promoContext);
                Console.WriteLine($"exp.GetValue<bool, PromoContext>(promoContext) = {result}");
			}

			{
				var exp = Expression.Parse("_Attr.Has('CurrentReceipt.TotalQuantityForArticleIndex0000125837') ? 1m : 0m");
				var result = (decimal)exp.GetValue<decimal, PromoContext>(promoContext);
                Console.WriteLine($"exp.GetValue<decimal, PromoContext>(promoContext) = {result}");
            }

			{
				var exp = Expression.Parse(
					"{ _Attr.Has('CurrentReceipt.TotalQuantityForArticleIndex0000125837') ? 1m : 0m, " 
					+ "_Attr.Has('CurrentReceipt.TotalQuantityForArticleIndex0000268621') ? 1m : 0m }");
				var result = (System.Collections.IEnumerable)exp.GetValue(promoContext);
			}


			{
				var exp = Expression.Parse(
					"{ _Attr.Has('CurrentReceipt.TotalQuantityForArticleIndex0000125837') ? 1m : 0m, "
					+ "_Attr.Has('CurrentReceipt.TotalQuantityForArticleIndex0000268621') ? 1m : 0m }.sum() < 2");
				var result = exp.GetValue(promoContext);
			}

			{
				var exp = Expression.Parse(
					"{ _Attr['CurrentReceipt.TotalValueForArticleIndex0000125837', 0m],"
					+ "_Attr['CurrentReceipt.TotalValueForArticleIndex0000130123', 0m]}.max() "
					+ " == _Attr['CurrentReceipt.TotalValueForArticleIndex0000125837', 0m]");

				var result = exp.GetValue(promoContext);
			}

			{
				var exp = Expression.Parse("_Attr.SelectMin({'Dupa','Kiszka','Pies'}).Item1");
				var result = exp.GetValue(promoContext);
			}

			{
				var exp = Expression.Parse("_Attr.SelectMax({'Dupa','Kiszka','Pies'})");
				var result = exp.GetValue(promoContext);
			}

			{
				var exp = Expression.Parse("5m < 6m ? 'ala'[1] : 'ola'[6]");
				var result = (char)exp.GetValue(null);
			}

			{
				var exp = Expression.Parse("5m < 6m ? 2 + 3 : 8");
				var result = (int)exp.GetValue(null);
			}

			{
				var exp = Expression.Parse("2 + 3");
				var result = (int)exp.GetValue(null);
			}

			{
				var exp = Expression.Parse("2m + 3 + DawajDecimala(4m + 3m) < Stolec.WewDec * DecVal + Stolec.Kiszka");
				var result = (bool)exp.GetValue(potylica);
			}


			{
				var exp = Expression.Parse("2m + 3 < 3.0 * 4m");
				var result = (bool) exp.GetValue(null);
			}

			{
				var exp = Expression.Parse("2m + 3 < 3.0 * DecVal");
				var result = (bool)exp.GetValue(new Potylica());
			}
			/**/
			
			{
				var exp = Expression.Parse("T(System.Globalization.CultureInfo).InvariantCulture");
				var result = (CultureInfo)exp.GetValue(new Potylica());
				Console.WriteLine("mam kulturkę {0}, {1}", result, result == null ? "null": "jakaś jest");
			}

			{
				var exp = Expression.Parse("T(System.Globalization.CultureInfo).GetCultureInfo('')");
				var result = (CultureInfo)exp.GetValue(new Potylica());
				Console.WriteLine("mam kulturkę {0}, {1}", result, result == null ? "null" : "jakaś jest");
			}

			{
				var exp = Expression.Parse("DateTime.Now");
				var result = (DateTime)exp.GetValue(new Potylica());
				Console.WriteLine("czasik {0}", result);
			}

			/*
			{
				var exp = Expression.Parse("'kupa' < 'sraczka2'");
				var result = (bool)exp.GetValue(null);
			}
			*/
			timer.Measure("10 x Dodawanie przez OpADD pewnie skompilowane",
					() =>
							{
								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);

								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);
								opAdd.GetValue(ctx);
							}
						);
			{
				var springExp = Expression.Parse("2 + 3");

				timer.Measure("2 + 3 parsed expression",
					() =>
					{
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);

						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
					}
				);

				if ((int)springExp.GetValue(null) != 5)
					throw new AbnormalProgramTerminationException("pipa");
			}

			{
				var springExp = (BaseNode)Expression.Parse("2 + 3");

				timer.Measure("2 + 3 parsed expression as GetIntValue",
					() =>
					{
						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);

						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);
						springExp.GetValue<int, object>(null, null);
					}
				);

				if ((int)springExp.GetValue(null) != 5)
					throw new AbnormalProgramTerminationException("pipa");
			}

			{
				var springExp = Expression.Parse("2 + 3 - 4 + 5 + 6");

				timer.Measure("2 + 3 - 4 + 5 + 6 parsed expression",
					() =>
					{
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);

						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
						springExp.GetValue(null);
					}
				);

				if ((int)springExp.GetValue(null) != 12)
					throw new AbnormalProgramTerminationException("12");
			}

			timer.Measure("10 x DodajIntyNatywnie() natywne",
				() =>
				{
					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);

					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);
					DodajIntyNatywnie(rezultat, kupa);
				});

			timer.Measure("10 x DodajIntyNatywnieZwrocObject() boxing tylko res",
				() =>
				{
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);

					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
					DodajIntyNatywnieZwrocObject(rezultat, kupa);
				});

			timer.Measure("10 x DodajIntyPrzezObjectZwrInt() boxing tylko params.",
				() =>
				{
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);

					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
					DodajIntyPrzezObjectZwrInt(rezultat, kupa);
				});

			timer.Measure("10 x DodajIntyPrzezObject() czyli wszystko boxing",
				() =>
				{
					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);

					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);
					DodajIntyPrzezObject(rezultat, kupa);
				});


			timer.Measure("10 x AddIfPossible()2 czyli boxing i rezult object",
				() =>
				{
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);

					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
					NumberUtils.AddIfPossible2(kupaSbyte, kupaSbyte);
				});


			timer.Measure("10 x AddIfPossible6() czyli pobranie dwóch GetTypeCode + << 3 single dim",
				() =>
				{
					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);

					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);
					NumberUtils.AddIfPossible6(rezultat, kupa);
				});

			timer.Measure("10 x AddIfPossible7() czyli pobranie dwóch GetTypeCode + jagged array",
				() =>
				{
					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);

					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);
					NumberUtils.AddIfPossible7(rezultat, kupa);
				});

			Console.ReadKey();
		}

		private static int DodajIntyNatywnie(int a, int b)
		{
			return a + b;
		}

		private static object DodajIntyNatywnieZwrocObject(int a, int b)
		{
			return a + b;
		}

		private static int DodajIntyPrzezObjectZwrInt(object a, object b)
		{
			return (int)a + (int)b;
		}

		private static object DodajIntyPrzezObject(object a, object b)
		{
			return (int)a + (int)b;
		}
	}

	internal class PromoContext
	{
		public readonly SpringExpressionAttributesContext _Attr
			= new SpringExpressionAttributesContext(new Dictionary<string, Decimal2>
			{
				{"Dupa", 3},
				{"Kiszka", -1},
				{"Pies", 100},
				{"CurrentReceipt.TotalQuantityForArticleIndex0000125837", 1 }
			});
	}

    internal class ByteCodeShower
    {
        public void Dupa(RegexOptions sraczka)
        {
            var dupa = ~sraczka;
            Console.WriteLine(dupa);
        }

        public RegexOptions JebajPlemie(RegexOptions r1, RegexOptions r2)
        {
            return r1 ^ r2;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace SpringExpressions.Expressions.GenericProcessors
{
	static class IntProcessor
	{
		public static int sum(IEnumerable<int> collection)
		{ return collection.Sum(); }

		public static int max(IEnumerable<int> collection)
		{ return collection.Max(); }

		public static int min(IEnumerable<int> collection)
		{ return collection.Min(); }

		public static int count(IEnumerable<int> collection)
		{ return collection.Count(); }
	}
}

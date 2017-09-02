using System.Collections.Generic;
using System.Linq;


namespace SpringExpressions.Expressions.GenericProcessors
{
    /**
     * DO NOT CHANGE NAMES! 
     **/
    static class DecimalProcessor
    {
        public static decimal sum(IEnumerable<decimal> collection)
        { return collection.Sum(); }

        public static decimal max(IEnumerable<decimal> collection)
        { return collection.Max(); }

        public static decimal min(IEnumerable<decimal> collection)
        { return collection.Min(); }

        public static decimal count(IEnumerable<decimal> collection)
        { return collection.Count(); }
    }
}

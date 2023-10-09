using System;
using System.Reflection;

namespace SpringExpressions.Expressions.LinqExpressionHelpers
{
    internal static class TimeSpanMethods
    {
        public static readonly MethodInfo TimeSpanParseMethodInfo
            = typeof(TimeSpan).GetMethod("Parse", new[] { typeof(string) });

        public static readonly MethodInfo TimeSpanFromDaysMethodInfo
            = typeof(TimeSpan).GetMethod("FromDays", new[] { typeof(double) });

    }
}

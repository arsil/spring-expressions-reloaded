using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SpringExpressions.Expressions.LinqExpressionHelpers
{
    internal static class DateTimeMethods
    {
        public static readonly MethodInfo DateTimeAddTimeSpanMethodInfo
            = typeof(DateTime).GetMethod("op_Addition", new[] { typeof(DateTime), typeof(TimeSpan) });

        public static readonly MethodInfo DateTimeAddDateTimeMethodInfo
            = typeof(DateTime).GetMethod("op_Addition", new[] { typeof(DateTime), typeof(DateTime) });


        public static readonly MethodInfo DateTimeSubTimeSpanMethodInfo
            = typeof(DateTime).GetMethod("op_Subtraction", new[] { typeof(DateTime), typeof(TimeSpan) });

        public static readonly MethodInfo DateTimeSubDateTimeMethodInfo
            = typeof(DateTime).GetMethod("op_Subtraction", new[] { typeof(DateTime), typeof(DateTime) });


    }
}

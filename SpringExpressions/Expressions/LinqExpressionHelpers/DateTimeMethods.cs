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

        // There is no DateTimeAddDateTimeMethodInfo, and there never could be: the BCL declares no
        // op_Addition(DateTime, DateTime) - adding two points in time is meaningless - so this lookup
        // returned null at type-initialisation and OpADD's branch for it crashed on every use. The
        // subtraction below is the real asymmetry: DateTime - DateTime *is* defined, yielding a
        // TimeSpan.

        public static readonly MethodInfo DateTimeSubTimeSpanMethodInfo
            = typeof(DateTime).GetMethod("op_Subtraction", new[] { typeof(DateTime), typeof(TimeSpan) });

        public static readonly MethodInfo DateTimeSubDateTimeMethodInfo
            = typeof(DateTime).GetMethod("op_Subtraction", new[] { typeof(DateTime), typeof(DateTime) });


    }
}

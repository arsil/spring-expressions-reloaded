using System;
using System.Linq.Expressions;

using JetBrains.Annotations;

using LExpression = System.Linq.Expressions.Expression;



namespace SpringExpressions.Expressions.Compiling
{
    internal static class ExpressionTypeHelper
    {
        /// <summary>
        /// Whether the value this expression produces could be a null reference at run time.
        /// </summary>
        /// <remarks>
        /// <p>
        /// Asked of every element of a collection <i>literal</i>, and for one question only: if
        /// <b>every</b> element could be null, then at run time they all might be, and the interpreter
        /// - which reads the item type off the values - would have nothing to read and answer
        /// <c>object</c> where the compiled path kept the static type. <c>{NullName}</c> was
        /// <c>List&lt;string&gt;</c> compiled and <c>List&lt;object&gt;</c> interpreted for exactly
        /// that reason.
        /// </p>
        /// <p>
        /// Deliberately only two shapes answer "no": a non-nullable value type, which has no null to
        /// hold, and a constant, whose value is in the expression. That keeps <c>{1, 2}</c> and
        /// <c>{'a', 'b'}</c> - the literals anybody actually writes - compiling, and it is honest
        /// about everything else: a <c>string</c> property may hold null however it was declared.
        /// </p>
        /// </remarks>
        public static bool CanBeNullAtRuntime([NotNull] LExpression expression)
        {
            if (expression is ConstantExpression constant)
                return constant.Value == null;

            var type = expression.Type;
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        /// <summary>
        /// Whether LINQ built <paramref name="conversion"/> on a type's own explicit operator, which
        /// this engine runs only where the expression writes a cast.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <c>LExpression.Convert</c> and <c>ConvertChecked</c> resolve a user-defined
        /// <c>op_Explicit</c> on their own, so a sink that converts by asking them applies one
        /// without saying so. An operator is explicit because it can lose information - a
        /// two-decimal-place type truncating 12.3456 to 12.34 - and C# runs it only for a written
        /// cast. So does this engine, at every typed sink: <c>x as T</c> is the spelling.
        /// </p>
        /// <p>
        /// User-defined means declared on a type the CLR does not know by type code. decimal's own
        /// <c>op_Explicit</c> to double and to the integrals are C#'s predefined numeric
        /// conversions, which each sink rules on separately, so they are not this question.
        /// </p>
        /// </remarks>
        public static bool AppliesUserDefinedExplicitOperator([NotNull] LExpression conversion)
        {
            return conversion is UnaryExpression unary
                   && unary.Method != null
                   && unary.Method.Name == "op_Explicit"
                   && Type.GetTypeCode(unary.Method.DeclaringType) == TypeCode.Object;
        }

        public static bool IsNumericExpression([NotNull] LExpression expression)
        {
            //   0 - A null reference.
            //   1 - Object
            //   2 - DBNull
            //   3 - Boolean
            //   4 - Char

            //   5 - sByte
            //   6 - Byte
            //   7 - Int16
            //   8 - UInt16
            //   9 - Int32
            //  10 - UInt32
            //  11 - Int64
            //  12 - UInt64
            //  13 - Single
            //  14 - Double
            //  15 - Decimal

            //  16 - DateTime	
            //  18 - String

            // For Enum types, the type code of the underlying integral type is returned.

            var expressionType = expression.Type;
            var code = (int)Type.GetTypeCode(expressionType);
            return code >= 5 && code <= 15 && !expressionType.IsEnum;
        }

        public static bool IsNumericOrNullableNumericExpression(
            [NotNull] LExpression expression, out bool isNullable, out TypeCode typeCode)
        {
            var expressionType = expression.Type;

            var expressionTypeCode = Type.GetTypeCode(expressionType);
            var code = (int)expressionTypeCode;
            if (code >= 5 && code <= 15 && !expressionType.IsEnum)
            {
                isNullable = false;
                typeCode = expressionTypeCode;
                return true;
            }

            if (expressionType.IsGenericType && expressionType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var itemType = Type.GetTypeCode(expressionType.GetGenericArguments()[0]);
                code = (int)itemType;

                if (code >= 5 && code <= 15 && !expressionType.IsEnum)
                {
                    isNullable = true;
                    typeCode = itemType;
                    return true;
                }
            }

            isNullable = false;
            typeCode = expressionTypeCode;
            return false;
        }


        public static bool IsIntegerExpression([NotNull] LExpression expression)
        {
            //   0 - A null reference.
            //   1 - Object
            //   2 - DBNull
            //   3 - Boolean
            //   4 - Char

            //   5 - sByte
            //   6 - Byte
            //   7 - Int16
            //   8 - UInt16
            //   9 - Int32
            //  10 - UInt32
            //  11 - Int64
            //  12 - UInt64

            //  13 - Single
            //  14 - Double
            //  15 - Decimal
            //  16 - DateTime	
            //  18 - String

            // For Enum types, the type code of the underlying integral type is returned.

            var expressionType = expression.Type;
            var code = (int)System.Type.GetTypeCode(expressionType);
            return code >= 5 && code <= 12 && !expressionType.IsEnum;
        }

        public static bool IsIntegerOrNullableIntegerExpression(
            [NotNull] LExpression expression, out bool isNullable, out TypeCode typeCode)
        {
            var expressionType = expression.Type;

            var expressionTypeCode = Type.GetTypeCode(expressionType);
            var code = (int)expressionTypeCode;
            if (code >= 5 && code <= 12 && !expressionType.IsEnum)
            {
                isNullable = false;
                typeCode = expressionTypeCode;
                return true;
            }

            if (expressionType.IsGenericType && expressionType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var itemType = Type.GetTypeCode(expressionType.GetGenericArguments()[0]);
                code = (int)itemType;

                if (code >= 5 && code <= 12 && !expressionType.IsEnum)
                {
                    isNullable = true;
                    typeCode = itemType;
                    return true;
                }
            }

            isNullable = false;
            typeCode = expressionTypeCode;
            return false;
        }

    }
}

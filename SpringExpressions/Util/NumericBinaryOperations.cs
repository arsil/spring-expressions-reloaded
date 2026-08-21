using System;

using JetBrains.Annotations;
using SpringExpressions.Expressions.Compiling.Expressions;
using SpringUtil;
using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    internal class NumericBinaryOperations
    {
        public static object Add([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, AddTable);

        public static object Sub([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, SubTable);

        public static object Mul([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, MulTable);

        public static object Div([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, DivTable);

        public static object Mod([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, ModTable);



        public static object And([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, AndTable);

        public static object Or([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, OrTable);

        public static object Xor([NotNull] object arg1, [NotNull] object arg2)
            => PerformOp(arg1, arg2, XorTable);



        /// <summary>
        /// Compares two boxed numbers under the same binary numeric promotion the operations above
        /// and the compiled comparison run on, so the backends answer alike by construction. Pairs
        /// the promotion refuses (int against ulong) throw, exactly as arithmetic on the same
        /// operands does.
        /// </summary>
        public static int Compare([NotNull] object arg1, [NotNull] object arg2)
        {
            arg1 = NumberUtils.ToBuiltInRealIfPossible(arg1);
            arg2 = NumberUtils.ToBuiltInRealIfPossible(arg2);

            var func = CompareTable[(int)Type.GetTypeCode(arg1.GetType()), (int)Type.GetTypeCode(arg2.GetType())];

            if (func == null)
            {
                throw new BinaryNumericPromotionException(
                    left: arg1.GetType(),
                    right: arg2.GetType());
            }

            return func(arg1, arg2);
        }



        private static object PerformOp(
            [NotNull] object arg1,
            [NotNull] object arg2,
            [NotNull] Func<object, object, object>[,] funcTable)
        {
            // A custom real-valued operand - a caller's own struct with an implicit conversion to
            // decimal, say - has TypeCode.Object and would miss the table; converted through its own
            // operator first, it participates like the built-in real it converts to.
            arg1 = NumberUtils.ToBuiltInRealIfPossible(arg1);
            arg2 = NumberUtils.ToBuiltInRealIfPossible(arg2);

            var func = funcTable[(int)Type.GetTypeCode(arg1.GetType()), (int)Type.GetTypeCode(arg2.GetType())];

            if (func == null)
            {
                throw new BinaryNumericPromotionException(
                    left: arg1.GetType(),
                    right: arg2.GetType());

//                throw new ArgumentException(
//                    $"'{arg1.GetType()}' and/or '{arg2.GetType()}' are not one of the supported numeric types.");
            }

            return func(arg1, arg2);
        }


        private static readonly Func<object, object, object>[,] AddTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.Add);

        private static readonly Func<object, object, object>[,] SubTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.Subtract);

        private static readonly Func<object, object, object>[,] MulTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.Multiply);

        private static readonly Func<object, object, object>[,] DivTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.Divide);

        private static readonly Func<object, object, object>[,] ModTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.Modulo);



        private static readonly Func<object, object, object>[,] AndTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.And);

        private static readonly Func<object, object, object>[,] OrTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.Or);

        private static readonly Func<object, object, object>[,] XorTable
            = NumericBinaryOperatorGenerator.CreateFunctionTable(LExpression.ExclusiveOr);



        private static readonly Func<object, object, int>[,] CompareTable
            = NumericBinaryOperatorGenerator.CreateCompareFunctionTable();

    }
}

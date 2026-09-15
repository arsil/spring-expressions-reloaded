using SpringCore.TypeResolution;
using SpringExpressions.Util;
using SpringUtil;
using System;
using System.Reflection;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// The cast operator: 'x as T(...)' means C#'s cast, on both backends. The compiled path emits
    /// LExpression.Convert - which IS the C# cast - and the interpreter performs the same conversion
    /// through CastOperations, whose converters are compiled from LExpression.Convert per type pair,
    /// so the backends agree by construction. The operator is this fork's own; the old interpreted
    /// Convert.ChangeType (banker's rounding, checked overflow, string parsing) was never a cast.
    /// </summary>
    public class CastNode : UnaryOperator
    {
        public CastNode()
        {
        }

                protected override object Get(object context, EvaluationContext evalContext)
        {
            if (type == null)
            {
                lock (this)
                {
                    type = TypeResolutionUtils.ResolveTypeForExpression(
                        getText(), evalContext.SandboxPolicy);
                }
            }

            object operand = GetValue(Operand, context, evalContext);
            return CastOperations.Cast(operand, type);
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            var operandExpression = GetExpressionTreeIfPossible(
                (BaseNode)getFirstChild(), contextExpression, compilationContext);

            // todo: error: raise condition?
            if (type == null)
            {
                lock (this)
                {
                    try
                    {
                        type = TypeResolutionUtils.ResolveTypeForExpression(
                            getText(), compilationContext.SandboxPolicy);
                    }
                    catch (TypeLoadException)
                    {
                        // ResolveType throws rather than returning null. While a tree is being built
                        // that is a compile refusal - letting the TypeLoadException escape would
                        // blind the weak path's fallback - and the interpreter reports the
                        // unresolvable type name at evaluation.
                        throw CannotCompile("the type name does not resolve");
                    }
                }
            }

            // A conversion operator that lands short of the target, plus the widening C# allows after
            // it. LExpression.Convert resolves one operator and will not chain, so a struct whose only
            // conversion is 'implicit operator int' cast to a decimal reported "no cast from the
            // operand's static type" - where C# casts it happily, and does not even require the cast.
            // CastOperations does the same two steps for the interpreter, which is what keeps the one
            // ruling - 'as' means C#'s cast - true of both backends.
            MethodInfo conversion;
            if (!type.IsAssignableFrom(operandExpression.Type)
                && TypeCheckingUtils.TryGetImplicitConversion(
                    operandExpression.Type, type, out conversion)
                && conversion.ReturnType != type)
            {
                operandExpression = LExpression.Convert(
                    operandExpression, conversion.ReturnType, conversion);
            }

            try
            {
                return LExpression.Convert(operandExpression, type);
            }
            catch (InvalidOperationException)
            {
                // C# has no cast from the operand's STATIC type to the target (CS0030). The
                // interpreter decides from the runtime type instead - an object-typed operand
                // holding something castable still casts, via the fallback.
                throw CannotCompile("no cast from the operand's static type to the target type");
            }
            catch (ArgumentException)
            {
                throw CannotCompile("no cast from the operand's static type to the target type");
            }
        }

        private Type type;
    }
}

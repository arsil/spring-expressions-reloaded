using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    public class BaseCompiledTests
    {
        protected static IGetterExpression<TRoot, TResult> CompileGetter<TRoot, TResult>(string expression)
        {
            return Expression.ParseGetter<TRoot, TResult>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }

        protected static IGetterExpression<TResult> CompileGetter<TResult>(string expression)
        {
            return Expression.ParseGetter<TResult>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }

        protected static TResult CompileAndExecuteGetter<TResult>(string expression)
            => CompileGetter<TResult>(expression).GetValue();

        protected static ISetterExpression<TRoot, TArg> CompileSetter<TRoot, TArg>(string expression)
        {
            return Expression.ParseSetter<TRoot, TArg>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }

        protected static ISetterExpression<TArg> CompileSetter<TArg>(string expression)
        {
            return Expression.ParseSetter<TArg>(
                expression, CompileOptions.CompileOnParse | CompileOptions.MustCompile);
        }



        protected static IGetterExpression<TRoot, TResult> InterpretGetter<TRoot, TResult>(string expression)
        {
            return Expression.ParseGetter<TRoot, TResult>(
                expression, CompileOptions.MustUseInterpreter);
        }

        protected static IGetterExpression<TResult> InterpretGetter<TResult>(string expression)
        {
            return Expression.ParseGetter<TResult>(
                expression, CompileOptions.MustUseInterpreter);
        }



        protected static TestCompiledAssertionChecker<TResult> TestCompiledVsInterpreted<TResult>(string expression)
        {
            var compiledObjectValue = CompileGetter<object>(expression);
            var interpretedObjectValue = Expression.ParseGetter<object>(expression, CompileOptions.MustUseInterpreter);

            var expectedType = (interpretedObjectValue.GetValue() ?? NullType).GetType();
            var actualType = (compiledObjectValue.GetValue() ?? NullType).GetType();
            Assert.AreEqual(expectedType, actualType, $"Type mismatch: Interpreted: {expectedType}, compiled: {actualType}. " +
                $"Expression: {expression}");

            var compiled = CompileGetter<TResult>(expression);
            var interpreted = Expression.ParseGetter<TResult>(expression, CompileOptions.MustUseInterpreter);

            var expectedValue = interpreted.GetValue();
            var actualValue = compiled.GetValue();

            Assert.AreEqual(expectedValue, actualValue, $"Value mismatch: Interpreted {expectedValue}, but was {actualValue}. " +
                $"Expression: {expression}");

            return new TestCompiledAssertionChecker<TResult>(actualValue);
        }

        protected static TestCompiledAssertionChecker<TResult> TestCompiledVsInterpreted<TRoot, TResult>(
            string expression, TRoot root)
        {
            var compiledObjectValue = CompileGetter<TRoot, object>(expression);
            var interpretedObjectValue = Expression.ParseGetter<TRoot, object>(expression, CompileOptions.MustUseInterpreter);

            var expectedType = (interpretedObjectValue.GetValue(root) ?? NullType).GetType();
            var actualType = (compiledObjectValue.GetValue(root) ?? NullType).GetType();

            Assert.AreEqual(expectedType, actualType, $"Type mismatch: Interpreted: {expectedType}, compiled: {actualType}. " +
                $"Expression: {expression}");

            var compiled = CompileGetter<TRoot, TResult>(expression);
            var interpreted = Expression.ParseGetter<TRoot, TResult>(expression, CompileOptions.MustUseInterpreter);

            var expectedValue = interpreted.GetValue(root);
            var actualValue = compiled.GetValue(root);

            Assert.AreEqual(expectedValue, actualValue, $"Value mismatch: Interpreted: {expectedValue}, compiled: {actualValue}. " +
                $"Expression: {expression}");

            return new TestCompiledAssertionChecker<TResult>(actualValue);
        }

        public class TestCompiledAssertionChecker<TResult>
        {
            public TResult Result { get; }

            public void ResultEqualsTo(TResult expected)
            {
                Assert.AreEqual(expected, Result);
            }

            public TestCompiledAssertionChecker(TResult result)
            { Result = result; }
        }

        private class Null { }
        private static readonly object NullType = new Null();
    }
}

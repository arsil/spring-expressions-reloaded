using System;

using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace NUnit.Framework
{
    /// <summary>
    /// NUnit 2.6 attributes that NUnit 3 dropped, reimplemented so the copied test bodies do not have to
    /// change.
    /// </summary>
    /// <remarks>
    /// The vendored nunit.framework.dll 2.6.4 is .NET Framework only and cannot load on .NET Core, so
    /// moving to NUnit 3 is forced rather than chosen. Rewriting 32 tests to Assert.Throws would work, but
    /// it would also mix NUnit's migration cost into a suite whose whole purpose is to measure the cost of
    /// migrating to THIS library. Shimming keeps the two separate: every remaining edit in this project is
    /// one the expression engine forced.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class ExpectedExceptionAttribute : NUnitAttribute, IWrapTestMethod
    {
        private readonly Type _expectedType;

        public ExpectedExceptionAttribute(Type expectedType)
        {
            _expectedType = expectedType;
        }

        public TestCommand Wrap(TestCommand command)
        {
            return new ExpectedExceptionCommand(command, _expectedType);
        }

        private class ExpectedExceptionCommand : DelegatingTestCommand
        {
            private readonly Type _expectedType;

            public ExpectedExceptionCommand(TestCommand innerCommand, Type expectedType)
                : base(innerCommand)
            {
                _expectedType = expectedType;
            }

            public override TestResult Execute(TestExecutionContext context)
            {
                Exception caught = null;

                try
                {
                    innerCommand.Execute(context);
                }
                catch (Exception ex)
                {
                    caught = ex;

                    // NUnit wraps whatever the test method threw.
                    while (caught is NUnitException && caught.InnerException != null)
                        caught = caught.InnerException;
                }

                TestResult result = context.CurrentResult;

                if (caught == null)
                {
                    result.SetResult(
                        ResultState.Failure,
                        "Expected " + _expectedType.FullName + " but no exception was thrown.");
                }
                else if (caught.GetType() == _expectedType)
                {
                    // NUnit 2.6 compared the exception type exactly rather than by assignability, and so
                    // does this - a derived exception was a failure there and stays one here.
                    result.SetResult(ResultState.Success);
                }
                else
                {
                    result.SetResult(
                        ResultState.Failure,
                        "Expected " + _expectedType.FullName + " but was " + caught.GetType().FullName
                        + ": " + caught.Message);
                }

                return result;
            }
        }
    }

    /// <summary>
    /// NUnit 3 renamed this to <see cref="OneTimeTearDownAttribute"/>. Deriving from it keeps the original
    /// spelling working, because NUnit matches teardown methods by attribute assignability.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class TestFixtureTearDownAttribute : OneTimeTearDownAttribute
    {
    }
}

using System;

using NUnit.Framework;

using SpringExpressions;

namespace SpringExpressionsTests.Expressions
{
    [TestFixture]
    public class ServiceLocatorTests
    {
        [Test(Description = "SPRNET-1381")]
        public void TestLocator()
        {

            ReferenceObjectFactory.CreateObject += new SimpleServiceLocator().DoGetInstance;

            object decValue = ExpressionEvaluator.GetValue(new object(), "@(Decimal)");
            Assert.AreEqual(666m, decValue);

            decValue = ExpressionEvaluator.GetValue(new object(), "@(Decimal:Trzy)");
            Assert.AreEqual(3m, decValue);

            decValue = ExpressionEvaluator.GetValue(new object(), "@(Decimal:Jeden)");
            Assert.AreEqual(1m, decValue);

        }

        private class SimpleServiceLocator
        {
            public object DoGetInstance(Type serviceType, string key)
            {
                if (serviceType == typeof(Decimal) && key == null)
                    return 666m;

                if (serviceType == typeof(Decimal) && key == "Trzy")
                    return 3m;

                if (serviceType == typeof(Decimal) && key == "Jeden")
                    return 1m;

                throw new InvalidOperationException("XXX");
            }
        }
    }
}

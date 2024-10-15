using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace API_Aggregator_Testing
{
    [TestClass]
    public class ReqMethodTests
    {
        [TestMethod]
        public void RequestMethod_MethodIsPOST_FunctionDoesNotExecute()
        {
            //  'Triple A Test Pattern'
            //  Example below of how to set up a test, my code needs to be 'function-alised' so that I can actually test it

            //  Arrange
            //  Creating a version of whatever you're wanting to test could be a request body / cookie / whatever
            var reservation = new Reservation();

            //  Act
            //  Testing whatever you're wanting to test - Saving to a var for assertions
            var result = reservation.MethodToTest();

            //  Assert
            //  MSTest built in Assert class & methods
            Assert.IsTrue(result);
        }
    }
}

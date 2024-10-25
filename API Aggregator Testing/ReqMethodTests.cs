using Microsoft.VisualStudio.TestTools.UnitTesting;
using Amazon.Lambda.APIGatewayEvents;
using API_Aggregator;

namespace API_Aggregator_Testing
{
    [TestClass]
    public class ReqMethodTests
    {
        private API_Aggregator_Main _function;

        [TestInitialize]
        public void Initialize()
        {
            _function = new API_Aggregator_Main();
        }

        [TestMethod]
        public void ReqMethodChecker_WhenMethodIsPOST_ReturnsFalse()
        {
            // Arrange
            var request = new APIGatewayHttpApiV2ProxyRequest
            {
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "POST"
                    }
                }
            };
            // Act
            bool result = _function.ReqMethodChecker(request);
            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ReqMethodChecker_WhenMethodIsNotPOST_ReturnsTrue()
        {
            // Arrange
            var request = new APIGatewayHttpApiV2ProxyRequest
            {
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "GET"
                    }
                }
            };
            // Act
            bool result = _function.ReqMethodChecker(request);
            // Assert
            Assert.IsTrue(result);
        }
    }
}
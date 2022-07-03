using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Moq;
using Moq.Contrib.HttpClient;
using Moq.Protected;

using Shouldly;

using Xunit;

namespace KeyVault.Acmebot.Tests.Providers
{
    public class DomeneShopDnsProvider_ListZonesAsyncShould
    {
        private DomeneShopProvider _provider;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

        public DomeneShopDnsProvider_ListZonesAsyncShould()
        {
            var options = new DomeneShopOptions
            {
                ApiKeyUser = "test",
                ApiKeyPassword = "test",
                PropagationSeconds = 60
            };
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _provider = new DomeneShopProvider(options, httpClient);
        }

        [Fact]
        public async Task ReturnListOfDomains()
        {
            // Arrange
            var responseData = TestData.ReadResourceAsString(TestData.DomeneShopDomainsResponse_sample1);
            _mockHttpMessageHandler.Protected().As<IHttpMessageHandler>()
                .Setup(x => x.SendAsync(
                    It.Is<HttpRequestMessage>(r =>
                    r.Method == HttpMethod.Get &&
                    r.RequestUri == new Uri($"{DomeneShopConstants.BaseUri}domains")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateHttpResponseMessage(HttpStatusCode.OK, responseData));

            // Act
            var actualDnsZones = await _provider.ListZonesAsync();

            // Assert
            actualDnsZones.ShouldNotBeNull("An object was returned");
            actualDnsZones.Count.ShouldBe(3, "Three domains were returned");
            foreach (var zone in actualDnsZones)
            {
                zone.Id.ShouldNotBeNullOrWhiteSpace("Id has been populated");
                zone.Name.ShouldNotBeNullOrWhiteSpace("Name has been populated");
            }
        }

        private HttpResponseMessage CreateHttpResponseMessage(HttpStatusCode httpStatusCode, string responseString)
        {
            var response = new HttpResponseMessage(httpStatusCode);
            response.Content = new StringContent(responseString, Encoding.UTF8, "application/json");

            return response;
        }
    }
}

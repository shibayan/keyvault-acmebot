using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Moq;
using Moq.Contrib.HttpClient;

using Newtonsoft.Json;

using Xunit;

namespace KeyVault.Acmebot.Tests.Providers;
public class DomeneShopDnsProvider_CreateTxtRecordAsyncShould
{
    private DomeneShopProvider _provider;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public DomeneShopDnsProvider_CreateTxtRecordAsyncShould()
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

    [Theory]
    [InlineData("example1.com", "_acme-challenge.www", "OTRlOWZhODYtNGU0NC00MDZiLWFjZWItODUxNGJmMjY1ZDRm")]
    [InlineData("example1.com", "_acme-challenge", "N2Y1M2U5YWItMDM3Zi00M2MzLWI3YWYtNzc1Mzk1NDdjZWU4")]
    public async Task CreateExpectedTxtRecords(string domain, string relativeRecordName, string challengeValue)
    {
        // Arrange
        string domeneShopDomainsJson = TestData.ReadResourceAsString(TestData.DomeneShopDomainsResponse_sample1);
        DomeneShopProvider.DomeneShopDomain[] domeneShopDomains = JsonConvert.DeserializeObject<DomeneShopProvider.DomeneShopDomain[]>(domeneShopDomainsJson);
        DnsZone dnsZone = domeneShopDomains
            .Where(d => string.Equals(d.Domain, domain, StringComparison.InvariantCultureIgnoreCase))
            .Select(d => d.ToDnsZone(_provider)).FirstOrDefault();
        IEnumerable<string> challengeValues = new List<string>(new string[] { challengeValue });
        string expectedHost = relativeRecordName;
        int expectedTtl = 60;
        string expectedType = "TXT";
        string expectedData = challengeValue;
        var expectedRequestUri = new Uri($"{DomeneShopConstants.BaseUri}domains/{dnsZone.Id}/dns");
        Func<HttpRequestMessage, Task<bool>> requestMessageMatch = async request =>
        {
            var json = await request.Content.ReadAsStringAsync();
            var model = JsonConvert.DeserializeObject<CreateDnsRecordBody>(json);
            return model.Host == expectedHost &&
                    model.Ttl == expectedTtl &&
                    model.Type == expectedType &&
                    model.Data == expectedData;
        };
        _mockHttpMessageHandler
            .SetupRequest(HttpMethod.Post, expectedRequestUri, requestMessageMatch)
            .ReturnsResponse(HttpStatusCode.Created, CreateResponseDataForPostDnsRequest())
            .Verifiable();

        // Act
        await _provider.CreateTxtRecordAsync(dnsZone, relativeRecordName, challengeValues);

        // Assert
        _mockHttpMessageHandler.VerifyRequest(
            method: HttpMethod.Post,
            requestUri: expectedRequestUri,
            match: requestMessageMatch,
            times: Times.Once(),
            failMessage: "The expected request was called once"
            );
    }

    private string CreateResponseDataForPostDnsRequest()
    {
        int id = new Random().Next(23000, 25000);
        string responseData = @"{ ""id"":" + id + @"}";

        return responseData;
    }

    private HttpResponseMessage CreateHttpResponseMessage(HttpStatusCode httpStatusCode, string responseString)
    {
        var response = new HttpResponseMessage(httpStatusCode);
        response.Content = new StringContent(responseString, Encoding.UTF8, "application/json");

        return response;
    }

    public class CreateDnsRecordBody
    {
        public string Host { get; set; }
        public int Ttl { get; set; }
        public string Type { get; set; }
        public string Data { get; set; }
    }
}

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
public class DomeneShopDnsProvider_DeleteTxtRecordAsyncShould
{
    private DomeneShopDnsProvider _provider;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public DomeneShopDnsProvider_DeleteTxtRecordAsyncShould()
    {
        var options = new DomeneShopDnsOptions
        {
            ApiKeyUser = "test",
            ApiKeyPassword = "test",
            PropagationSeconds = 60
        };
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _provider = new DomeneShopDnsProvider(options, httpClient);
    }

    [Theory]
    [InlineData("_acme-challenge.www", true)]
    [InlineData("_acme-challenge", true)]
    [InlineData("_acme-challenge.notexists", false)]
    public async Task DeleteAnExistingTxtRecord(string relativeRecordName, bool isDeleteExpected)
    {
        // Arrange
        string domeneShopListDnsRecordsResponseJson = TestData.ReadResourceAsString(TestData.DomeneShopDnsRecordsResponse_example1_com_sample1);
        GetDnsRecordBody[] dnsRecords = JsonConvert.DeserializeObject<GetDnsRecordBody[]>(domeneShopListDnsRecordsResponseJson);
        string domeneShopDomainsJson = TestData.ReadResourceAsString(TestData.DomeneShopDomainsResponse_sample1);
        DomeneShopDnsProvider.DomeneShopDomain[] domeneShopDomains = JsonConvert.DeserializeObject<DomeneShopDnsProvider.DomeneShopDomain[]>(domeneShopDomainsJson);
        DnsZone dnsZone = domeneShopDomains
            .Where(d => string.Equals(d.Domain, "example1.com", StringComparison.InvariantCultureIgnoreCase))
            .Select(d => d.ToDnsZone(_provider)).FirstOrDefault();

        // Expect a call to get all DNS records for the domain
        var expectedGetDnsRecordsUri = new Uri($"{DomeneShopConstants.BaseUri}domains/{dnsZone.Id}/dns");
        _mockHttpMessageHandler
         .SetupRequest(HttpMethod.Get, expectedGetDnsRecordsUri)
         .ReturnsResponse(HttpStatusCode.OK, CreateJsonHttpContent(domeneShopListDnsRecordsResponseJson))
         .Verifiable();

        // Expect (or not) a call to delete DNS record for the domain
        Uri expectedDeleteDnsRecordUri = null;

        if (isDeleteExpected)
        {
            var expectedDnsRecord = dnsRecords.Where(r =>
                string.Equals(relativeRecordName, r.Host, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals("TXT", r.Type)).FirstOrDefault();

            if (expectedDnsRecord == null)
            {
                throw new InvalidOperationException($"Test data doesn't contain a TXT record for host {relativeRecordName}. Unable to expect a DELETE request for an unknown DNS record id.");
            }

            expectedDeleteDnsRecordUri = new Uri($"{DomeneShopConstants.BaseUri}domains/{dnsZone.Id}/dns/{expectedDnsRecord.Id}");

            _mockHttpMessageHandler
              .SetupRequest(HttpMethod.Delete, expectedDeleteDnsRecordUri)
              .ReturnsResponse(HttpStatusCode.NoContent)
              .Verifiable();
        }

        // Act
        await _provider.DeleteTxtRecordAsync(dnsZone, relativeRecordName);

        // Assert
        _mockHttpMessageHandler.VerifyRequest(
            method: HttpMethod.Get,
            requestUri: expectedGetDnsRecordsUri,
            times: Times.Once(),
            failMessage: "The expected GET DNS records request was called once"
            );

        if (isDeleteExpected)
        {
            _mockHttpMessageHandler.VerifyRequest(
                method: HttpMethod.Delete,
                requestUri: expectedDeleteDnsRecordUri,
                times: Times.Once(),
                failMessage: "The expexted DELETE DNS record request was called once"
                );
        }
    }

    private HttpContent CreateJsonHttpContent(string responseString)
    {
        return new StringContent(responseString, Encoding.UTF8, "application/json");
    }

    public class CreateDnsRecordBody
    {
        public string Host { get; set; }
        public int Ttl { get; set; }
        public string Type { get; set; }
        public string Data { get; set; }
    }

    public class GetDnsRecordBody
    {
        public string Data { get; set; }
        public string Host { get; set; }
        public int Id { get; set; }
        public string Port { get; set; }
        public string Priority { get; set; }
        public int Ttl { get; set; }
        public string Type { get; set; }
        public string Weight { get; set; }
    }
}

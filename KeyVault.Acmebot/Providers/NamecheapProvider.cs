using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers;

public class NamecheapProvider : IDnsProvider
{
    public const string NamecheapXmlNamespace = @"http://api.namecheap.com/xml.response";
    public const string Endpoint = @"https://api.namecheap.com/xml.response";
    public const string IpfyOrgEndpoint = @"https://api.ipfy.org";
    public const string ListDomainsCommand = "namecheap.domains.getList";
    public const string GetHostsCommand = "namecheap.domains.dns.getHosts";
    public const string SetHostsCommand = "namecheap.domains.dns.setHosts";

    public NamecheapProvider(NamecheapOptions options, HttpClient namecheapHttpClient, HttpClient ipfyOrgHttpClient)
    {
        _httpClient = namecheapHttpClient;
        _httpClient.BaseAddress = new Uri(Endpoint);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        _ipfyOrgHttpClient = ipfyOrgHttpClient;
        _ipfyOrgHttpClient.BaseAddress = new Uri(IpfyOrgEndpoint);
        _ipfyOrgHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        PropagationSeconds = options.PropagationSeconds;
        ApiUser = options.ApiUser;
        ApiKey = options.ApiKey;
        Username = options.ApiUser;
    }

    private readonly HttpClient _httpClient;

    private readonly HttpClient _ipfyOrgHttpClient;

    public int PropagationSeconds { get; }

    public string ApiUser { get; }

    public string ApiKey { get; }

    public string Username { get; }

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        return (await ListZonesPageAsync())
            .ToArray();
    }

    private async Task<IEnumerable<DnsZone>> ListZonesPageAsync(int? page = null, IEnumerable<DnsZone> prevPages = null, string clientIp = null)
    {
        List<DnsZone> dnsZones = (prevPages != null ? new List<DnsZone>(prevPages) : new List<DnsZone>());

        // Resolve public IP address only once
        if (string.IsNullOrEmpty(clientIp))
        {
            clientIp = await ResolvePublicIPAsync();
        }

        // Load a page of domains
        string relativeUri = await CreateRelativeUriAsync(ListDomainsCommand, page, clientIp);
        var response = await _httpClient.GetAsync(relativeUri);
        response.EnsureSuccessStatusCode();

        // Parse XML response
        var responseBody = await response.Content.ReadAsStringAsync();
        NamecheapDomainListResponse responseContent = ParseDomainListResponse(responseBody);
        responseContent.EnsureNotErrorResponse();
        var moreZones = responseContent.Domains.Select(d => new DnsZone(this)
        {
            Id = d.ID,
            Name = d.Name,
            NameServers = null
        });
        dnsZones.AddRange(moreZones);

        // Call recursively until all pages are loaded
        if (dnsZones.Count < responseContent.TotalItems)
        {
            return await ListZonesPageAsync(responseContent.CurrentPage + 1, dnsZones, clientIp);
        }

        return dnsZones;
    }

    private NamecheapDomainListResponse ParseDomainListResponse(string responseBody)
    {
        var xmlDoc = new XmlDocument() { PreserveWhitespace = true };
        xmlDoc.LoadXml(responseBody);
        var nsMgr = CreateNamespaceManager(xmlDoc.NameTable);
        var domainListResponse = new NamecheapDomainListResponse();

        // Parse Domain elements
        var domainElements = xmlDoc.SelectNodes(@"/nc:ApiResponse/nc:CommandResponse/nc:DomainGetListResult/nc:Domain", nsMgr);

        foreach (XmlNode domainElement in domainElements)
        {
            domainListResponse.Domains.Add(new NamecheapDomain
            {
                ID = domainElement.Attributes["ID"].Value,
                Name = domainElement.Attributes["Name"].Value
            });
        }

        ParseCommonElements(domainListResponse, xmlDoc, nsMgr);

        return domainListResponse;
    }

    private void ParseCommonElements(NamecheapPageResponse response, XmlDocument responseXml, XmlNamespaceManager nsMgr)
    {
        ParsePagingElement(response, responseXml, nsMgr);
        ParseBaseElements(response, responseXml, nsMgr);
    }

    private void ParsePagingElement(NamecheapPageResponse response, XmlDocument responseXml, XmlNamespaceManager nsMgr)
    {
        var pagingElement = responseXml.SelectSingleNode(@"/nc:ApiResponse/nc:CommandResponse/nc:Paging", nsMgr);

        if (pagingElement != null)
        {
            response.TotalItems = int.Parse(pagingElement.SelectSingleNode(@"nc:TotalItems", nsMgr)?.InnerText);
            response.CurrentPage = int.Parse(pagingElement.SelectSingleNode(@"nc:CurrentPage", nsMgr)?.InnerText);
            response.PageSize = int.Parse(pagingElement.SelectSingleNode(@"nc:PageSize", nsMgr)?.InnerText);
        }
    }

    private void ParseBaseElements(NamecheapResponse response, XmlDocument responseXml, XmlNamespaceManager nsMgr)
    {
        response.Status = responseXml.SelectSingleNode(@"/nc:ApiResponse/@Status", nsMgr)?.Value;

        // Parse Errors
        var errorElements = responseXml.SelectNodes(@"/nc:ApiResponse/nc:Errors/nc:Error", nsMgr);

        if (errorElements != null)
        {
            foreach (XmlNode errorElement in errorElements)
            {
                string errorNumber = errorElement.SelectSingleNode("@Number", nsMgr)?.InnerText;
                string errorMessage = errorElement.InnerText;
                response.Errors.Add(new NamecheapError
                {
                    Number = errorNumber,
                    Message = errorMessage
                });
            }
        }

        // Parse Warnings
        var warningElements = responseXml.SelectNodes(@"/nc:ApiResponse/nc:Warnings/nc:Warning", nsMgr);

        if (warningElements != null)
        {
            foreach (XmlNode warningElement in warningElements)
            {
                string warningNumber = warningElement.Attributes["Number"]?.Value;
                string warningMessage = warningElement.InnerText;
                response.Warnings.Add(new NamecheapError
                {
                    Number = warningNumber,
                    Message = warningMessage
                });
            }
        }
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        string clientIp = await ResolvePublicIPAsync();

        // Namecheap API is destructive! We have to send all exists hosts to add a new one, if not all existing will be deleted!
        NamecheapGetHostsResponse getHostsResponse = await GetHosts(zone, clientIp);

        // Add the host that we're going to create
        getHostsResponse.Hosts.Add(new NamecheapHost
        {
            HostName = relativeRecordName,
            Address = string.Join(",", values),
            RecordType = "TXT",
            MxPref = "10",
            Ttl = "1799"
        });

        // Call the dangerous setHosts command and validate the result
        await SetHostsAsync(zone, getHostsResponse, clientIp);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        string clientIp = await ResolvePublicIPAsync();

        // Namecheap API is destructive! We have to send all exists hosts to remove just one, if not all existing will be deleted!
        NamecheapGetHostsResponse getHostsResponse = await GetHosts(zone, clientIp);

        // Remove the host that we should delete. If not found, assume it's already deleted.
        var hostToDelete = getHostsResponse.Hosts.Where(h => h.HostName == relativeRecordName).FirstOrDefault();

        if (hostToDelete != null)
        {
            getHostsResponse.Hosts.Remove(hostToDelete);

            // Call the dangerous setHosts command and validate the result
            await SetHostsAsync(zone, getHostsResponse, clientIp);
        }
    }

    private async Task SetHostsAsync(DnsZone zone, NamecheapGetHostsResponse setHostsData, string clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            clientIp = await ResolvePublicIPAsync();
        }

        // Turn all host information into a long GET request uri. Even if POST is recommended, there's no documentation of that type of request.
        var requestUri = await CreateSetHostsCommandUri(zone, setHostsData, clientIp);
        var response = await _httpClient.GetAsync(requestUri);

        response.EnsureSuccessStatusCode();

        // Throw Exception if response doen't indicate success
        string responseBody = await response.Content.ReadAsStringAsync();
        DomainDNSSetHostsResult resultResponse = ParseSetHostsResult(responseBody);
        resultResponse.EnsureNotErrorResponse();
    }

    private async Task<string> CreateSetHostsCommandUri(DnsZone zone, NamecheapGetHostsResponse requestData, string clientIp = null)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            clientIp = await ResolvePublicIPAsync();
        }

        string relativeUri = await CreateRelativeUriAsync(SetHostsCommand, zone, clientIp);

        if (!string.IsNullOrWhiteSpace(requestData.EmailType))
        {
            relativeUri += "&EmailType=" + requestData.EmailType;
        }

        if (!string.IsNullOrWhiteSpace(requestData.Flag))
        {
            relativeUri += "&Flag=" + requestData.Flag;
        }

        if (!string.IsNullOrWhiteSpace(requestData.Tag))
        {
            relativeUri += "&Tag=" + requestData.Tag;
        }

        int hostCounter = 0;

        foreach (var host in requestData.Hosts)
        {
            hostCounter++;
            relativeUri += $"&HostName{hostCounter}={host.HostName}";
            relativeUri += $"&RecordType{hostCounter}={host.RecordType}";
            relativeUri += $"&Address{hostCounter}={host.Address}";
            relativeUri += $"&MXPref{hostCounter}={host.MxPref}";
            relativeUri += $"&TTL{hostCounter}={host.Ttl}";
        }

        return relativeUri;
    }

    private DomainDNSSetHostsResult ParseSetHostsResult(string responseBody)
    {
        var xmlDoc = new XmlDocument() { PreserveWhitespace = true };
        xmlDoc.LoadXml(responseBody);
        var nsMgr = CreateNamespaceManager(xmlDoc.NameTable);
        var setHostsResult = new DomainDNSSetHostsResult();

        // Parse DomainDNSGetHostsResult element
        var setHostsResultElement = xmlDoc.SelectSingleNode(@"/nc:ApiResponse/nc:CommandResponse/nc:DomainDNSSetHostsResult", nsMgr);
        setHostsResult.IsSuccess = setHostsResultElement.Attributes["IsSuccess"]?.Value;
        setHostsResult.Domain = setHostsResultElement.Attributes["Domain"]?.Value;

        // Parse Status and Errors
        ParseBaseElements(setHostsResult, xmlDoc, nsMgr);

        return setHostsResult;
    }

    private async Task<NamecheapGetHostsResponse> GetHosts(DnsZone zone, string clientIp = null)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            clientIp = await ResolvePublicIPAsync();
        }

        // Load all hosts for the domain
        string relativeUri = await CreateRelativeUriAsync(GetHostsCommand, zone, clientIp);
        var response = await _httpClient.GetAsync(relativeUri);
        response.EnsureSuccessStatusCode();

        // Parse XML response
        var responseBody = await response.Content.ReadAsStringAsync();
        NamecheapGetHostsResponse getHotsResponse = ParseGetHostsResponse(responseBody);
        getHotsResponse.EnsureNotErrorResponse();

        return getHotsResponse;
    }

    private NamecheapGetHostsResponse ParseGetHostsResponse(string responseBody)
    {
        var xmlDoc = new XmlDocument() { PreserveWhitespace = true };
        xmlDoc.LoadXml(responseBody);
        var nsMgr = CreateNamespaceManager(xmlDoc.NameTable);
        var getHostsResponse = new NamecheapGetHostsResponse();

        // Parse DomainDNSGetHostsResult element
        var getHostsResultElement = xmlDoc.SelectSingleNode(@"/nc:ApiResponse/nc:CommandResponse/nc:DomainDNSGetHostsResult", nsMgr);
        getHostsResponse.EmailType = getHostsResultElement.Attributes["EmailType"]?.Value;
        getHostsResponse.Domain = getHostsResultElement.Attributes["Domain"]?.Value;

        // Parse host elements
        var hostElements = getHostsResultElement.SelectNodes(@"nc:host", nsMgr);

        foreach (XmlNode hostElement in hostElements)
        {
            getHostsResponse.Hosts.Add(new NamecheapHost
            {
                HostName = hostElement.Attributes["Name"]?.Value,
                RecordType = hostElement.Attributes["Type"]?.Value,
                Address = hostElement.Attributes["Address"]?.Value,
                MxPref = hostElement.Attributes["MXPref"]?.Value,
                Ttl = hostElement.Attributes["TTL"]?.Value
            });
        }

        // Parse Status and Errors
        ParseBaseElements(getHostsResponse, xmlDoc, nsMgr);

        return getHostsResponse;
    }

    private async Task<string> CreateRelativeUriAsync(string command, DnsZone domain, string clientIp = null)
    {
        string relativeUri = await CreateRelativeUriCommonAsync(command, clientIp);

        if (domain != null)
        {
            int dot = domain.Name.IndexOf('.');
            string sld = domain.Name.Substring(0, dot);
            string tld = domain.Name.Substring(dot + 1);
            relativeUri += "&SLD=" + sld;
            relativeUri += "&TLD=" + tld;
        }

        return relativeUri;
    }

    private async Task<string> CreateRelativeUriAsync(string command, int? page = null, string clientIp = null)
    {
        string relativeUri = await CreateRelativeUriCommonAsync(command, clientIp);

        if (page != null)
        {
            relativeUri += $"&Page={page}";
        }

        return relativeUri;
    }

    private async Task<string> CreateRelativeUriCommonAsync(string command, string clientIp = null)
    {
        if (string.IsNullOrEmpty(clientIp))
        {
            clientIp = await ResolvePublicIPAsync();
        }

        return $"?ApiUser={ApiUser}&ApiKey={ApiKey}&UserName={Username}&ClientIp={clientIp}&Command={command}";
    }

    private async Task<string> ResolvePublicIPAsync()
    {
        var response = await _ipfyOrgHttpClient.GetAsync("?format=json");

        response.EnsureSuccessStatusCode();

        var ipFyResponse = await response.Content.ReadAsAsync<IpfyResponse>();

        return ipFyResponse.Ip;
    }

    private XmlNamespaceManager CreateNamespaceManager(XmlNameTable nameTable)
    {
        var ns = new XmlNamespaceManager(nameTable);
        ns.AddNamespace("nc", NamecheapXmlNamespace);

        return ns;
    }

    public class IpfyResponse
    {
        public string Ip { get; set; }
    }

    public class NamecheapDomain
    {
        public string ID { get; set; }
        public string Name { get; set; }
    }

    public class NamecheapDomainListResponse : NamecheapPageResponse
    {
        public List<NamecheapDomain> Domains { get; set; } = new List<NamecheapDomain>();
    }

    public class NamecheapGetHostsResponse : NamecheapResponse
    {
        public string Domain { get; set; }
        public string EmailType { get; set; }
        public string Flag { get; set; }
        public string Tag { get; set; }
        public List<NamecheapHost> Hosts { get; set; } = new List<NamecheapHost>();
    }

    public class NamecheapHost
    {
        public string HostName { get; set; }
        public string RecordType { get; set; }
        public string Address { get; set; }
        public string MxPref { get; set; }
        public string Ttl { get; set; }
    }

    public class DomainDNSSetHostsResult : NamecheapResponse
    {
        public string Domain { get; set; }
        public string IsSuccess { get; set; }

        public override void EnsureNotErrorResponse()
        {
            // Normal validation of Status and Errors values
            base.EnsureNotErrorResponse();

            // Additional IsSuccess attribute
            if (!"true".Equals(IsSuccess, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception("setHosts command failed with IsSuccess=false as the only indication of failure");
            }
        }
    }

    public class NamecheapPageResponse : NamecheapResponse
    {
        public int TotalItems { get; set; } = 0;
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class NamecheapResponse
    {
        public string Status { get; set; }
        public List<NamecheapError> Errors { get; set; } = new List<NamecheapError>();
        public List<NamecheapError> Warnings { get; set; } = new List<NamecheapError>();

        public virtual void EnsureNotErrorResponse()
        {
            var errorParser = new NamecheapErrorParser(this);

            if (errorParser.IsErrorResponse())
            {
                errorParser.ThrowDescriptiveException();
            }
        }
    }

    public class NamecheapError
    {
        public string Number { get; set; }
        public string Message { get; set; }
    }

    public class NamecheapErrorParser
    {
        public const string ErrorStatus = "ERROR";
        public const string ErrorMessageUnknown = "Unknown error returned from Namecheap API";
        public const string ErrorCodeInvalidRequestIp = "1011150";
        public const string IpAddressTemplate = "{ipAddress}";
        public const string ErrorMessageInvalidRequestIp = $"IP address {IpAddressTemplate} must be whitelisted at your Namecheap account";
        private readonly NamecheapResponse _namecheapResponse;

        public NamecheapErrorParser(NamecheapResponse namecheapResponse)
        {
            _namecheapResponse = namecheapResponse;
        }

        public bool IsErrorResponse()
        {
            return ErrorStatus.Equals(_namecheapResponse.Status, StringComparison.InvariantCultureIgnoreCase);
        }

        public void ThrowDescriptiveException()
        {
            if (_namecheapResponse.Errors == null || _namecheapResponse.Errors.Count == 0)
            {
                throw new Exception(ErrorMessageUnknown);
            }
            else if (_namecheapResponse.Errors.Count == 1)
            {
                throw CreateDescriptiveException(_namecheapResponse.Errors.First());
            }
            else
            {
                throw new AggregateException("Several errors returned from Namecheap API", _namecheapResponse.Errors.Select(e => CreateDescriptiveException(e)));
            }
        }

        private Exception CreateDescriptiveException(NamecheapError error)
        {
            if (!string.IsNullOrEmpty(error.Number))
            {
                switch (error.Number.ToLower())
                {
                    case ErrorCodeInvalidRequestIp:
                        string ipAddress = ParseIpAddress(error.Message);

                        if (!string.IsNullOrWhiteSpace(ipAddress))
                        {
                            return new Exception(ErrorMessageInvalidRequestIp.Replace(IpAddressTemplate, ipAddress));
                        }

                        return new Exception(error.Message);
                    default:
                        return new Exception(error.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return new Exception(error.Message);
            }
            else
            {
                return new Exception(ErrorMessageUnknown);
            }
        }

        private string ParseIpAddress(string errorMessage)
        {
            var regex = new Regex(@"(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?!$)|$)){4}$", RegexOptions.Multiline);
            var match = regex.Match(errorMessage);

            if (match.Success)
            {
                return match.Groups[0].Value;
            }

            return null;
        }
    }
}

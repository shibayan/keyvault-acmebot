using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models;

public class CertificatePolicyItem : IValidatableObject
{
    [JsonProperty("certificateName")]
    [RegularExpression("^[0-9a-zA-Z-]+$")]
    public string CertificateName { get; set; }

    [JsonProperty("dnsNames")]
    public string[] DnsNames { get; set; }

    [JsonProperty("dnsProviderName")]
    public string DnsProviderName { get; set; }

    [JsonProperty("keyType")]
    [RegularExpression("^(RSA|EC)$")]
    public string KeyType { get; set; }

    [JsonProperty("keySize")]
    public int? KeySize { get; set; }

    [JsonProperty("keyCurveName")]
    [RegularExpression(@"^P\-(256|384|521|256K)$")]
    public string KeyCurveName { get; set; }

    [JsonProperty("reuseKey")]
    public bool? ReuseKey { get; set; }

    [JsonProperty("dnsAlias")]
    public string DnsAlias { get; set; }

    public IEnumerable<string> AliasedDnsNames => string.IsNullOrEmpty(DnsAlias) ? DnsNames : new[] { DnsAlias };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DnsNames is null || DnsNames.Length == 0)
        {
            yield return new ValidationResult($"The {nameof(DnsNames)} is required.", new[] { nameof(DnsNames) });
        }
    }
}

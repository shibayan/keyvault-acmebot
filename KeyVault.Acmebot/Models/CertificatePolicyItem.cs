using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Azure.Security.KeyVault.Certificates;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models
{
    public class CertificatePolicyItem : IValidatableObject
    {
        [JsonProperty("certificateName")]
        [RegularExpression("^[0-9a-zA-Z-]+$")]
        public string CertificateName { get; set; }

        [JsonProperty("dnsNames")]
        public string[] DnsNames { get; set; }

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

        public CertificatePolicy ToCertificatePolicy()
        {
            var subjectAlternativeNames = new SubjectAlternativeNames();

            foreach (var dnsName in DnsNames)
            {
                subjectAlternativeNames.DnsNames.Add(dnsName);
            }

            var certificatePolicy = new CertificatePolicy(WellKnownIssuerNames.Unknown, subjectAlternativeNames)
            {
                KeySize = KeySize,
                ReuseKey = ReuseKey
            };

            if (!string.IsNullOrEmpty(KeyType))
            {
                certificatePolicy.KeyType = KeyType;
            }

            if (!string.IsNullOrEmpty(KeyCurveName))
            {
                certificatePolicy.KeyCurveName = KeyCurveName;
            }

            return certificatePolicy;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DnsNames == null || DnsNames.Length == 0)
            {
                yield return new ValidationResult($"The {nameof(DnsNames)} is required.", new[] { nameof(DnsNames) });
            }
        }
    }
}

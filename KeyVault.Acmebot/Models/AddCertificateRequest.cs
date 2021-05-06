using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot.Models
{
    public class AddCertificateRequest : IValidatableObject
    {
        public string[] DnsNames { get; set; }

        [RegularExpression("^[0-9a-zA-Z-]+$")]
        public string CertificateName { get; set; }

        [RegularExpression("^(RSA|EC)$")]
        public string KeyType { get; set; }

        public int? KeySize { get; set; }

        [RegularExpression(@"^P(256|384|521|256K)$")]
        public string EllipticCurveName { get; set; }

        public bool? ReuseKeyOnRenewal { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DnsNames == null || DnsNames.Length == 0)
            {
                yield return new ValidationResult($"The {nameof(DnsNames)} is required.", new[] { nameof(DnsNames) });
            }
        }
    }
}

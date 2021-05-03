using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot.Models
{
    public class AddCertificateRequest : IValidatableObject
    {
        [RegularExpression("^[0-9a-zA-Z-]+$")]
        public string CertificateName { get; set; }

        public string[] DnsNames { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DnsNames == null || DnsNames.Length == 0)
            {
                yield return new ValidationResult($"The {nameof(DnsNames)} is required.", new[] { nameof(DnsNames) });
            }
        }
    }
}

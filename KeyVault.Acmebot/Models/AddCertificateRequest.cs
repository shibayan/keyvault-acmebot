using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot.Models
{
    public class AddCertificateRequest : IValidatableObject
    {
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

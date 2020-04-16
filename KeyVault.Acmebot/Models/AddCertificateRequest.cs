using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot.Models
{
    public class AddCertificateRequest : IValidatableObject
    {
        public string[] Domains { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Domains == null || Domains.Length == 0)
            {
                yield return new ValidationResult($"The {nameof(Domains)} is required.", new[] { nameof(Domains) });
            }
        }
    }
}

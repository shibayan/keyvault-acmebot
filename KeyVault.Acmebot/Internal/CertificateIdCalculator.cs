using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using KeyVault.Acmebot.Models;

namespace KeyVault.Acmebot.Internal
{
    /// <summary>
    /// Utility class for calculating certificate identifiers for ACME Renewal Information (ARI) requests
    /// </summary>
    public static class CertificateIdCalculator
    {
        /// <summary>
        /// OID for Authority Key Identifier extension
        /// </summary>
        private const string AuthorityKeyIdentifierOid = "2.5.29.35";

        /// <summary>
        /// Extracts certificate identifier components from an X.509 certificate
        /// </summary>
        /// <param name="certificate">The X.509 certificate to process</param>
        /// <returns>Certificate identifier with AKI, serial number, and encoded certificate ID</returns>
        /// <exception cref="ArgumentNullException">Thrown when certificate is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when required certificate components cannot be extracted</exception>
        public static CertificateIdentifier ExtractCertificateId(X509Certificate2 certificate)
        {
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));

            var authorityKeyIdentifier = ExtractAuthorityKeyIdentifier(certificate);
            var serialNumber = ExtractSerialNumber(certificate);
            var certificateId = GenerateCertificateId(authorityKeyIdentifier, serialNumber);

            return new CertificateIdentifier
            {
                AuthorityKeyIdentifier = Convert.ToHexString(authorityKeyIdentifier).ToLowerInvariant(),
                SerialNumber = Convert.ToHexString(serialNumber).ToLowerInvariant(),
                CertificateId = certificateId
            };
        }

        /// <summary>
        /// Extracts Authority Key Identifier from certificate extensions
        /// </summary>
        private static byte[] ExtractAuthorityKeyIdentifier(X509Certificate2 certificate)
        {
            var akiExtension = certificate.Extensions[AuthorityKeyIdentifierOid];
            if (akiExtension == null)
                throw new InvalidOperationException("Certificate does not contain Authority Key Identifier extension");

            // Parse AKI extension according to RFC 5280
            // AKI is an OCTET STRING containing a SEQUENCE with optional keyIdentifier [0]
            var akiBytes = akiExtension.RawData;
            
            try
            {
                // Simple parsing assuming keyIdentifier is present as [0] tag
                // Look for tag 0x80 (context-specific, primitive, tag 0)
                for (int i = 0; i < akiBytes.Length - 1; i++)
                {
                    if (akiBytes[i] == 0x80) // keyIdentifier tag
                    {
                        var length = akiBytes[i + 1];
                        if (i + 2 + length <= akiBytes.Length)
                        {
                            var keyId = new byte[length];
                            Array.Copy(akiBytes, i + 2, keyId, 0, length);
                            return keyId;
                        }
                    }
                }
                
                throw new InvalidOperationException("Cannot parse Authority Key Identifier from certificate extension");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to extract Authority Key Identifier from certificate", ex);
            }
        }

        /// <summary>
        /// Extracts serial number from certificate
        /// </summary>
        private static byte[] ExtractSerialNumber(X509Certificate2 certificate)
        {
            try
            {
                // Get serial number as byte array, ensuring proper encoding
                var serialString = certificate.SerialNumber;
                
                // Serial number is returned as hex string, convert to bytes
                // Handle both even and odd length strings
                if (serialString.Length % 2 != 0)
                    serialString = "0" + serialString;
                
                var serialBytes = new byte[serialString.Length / 2];
                for (int i = 0; i < serialBytes.Length; i++)
                {
                    serialBytes[i] = Convert.ToByte(serialString.Substring(i * 2, 2), 16);
                }
                
                return serialBytes;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to extract serial number from certificate", ex);
            }
        }

        /// <summary>
        /// Generates certificate ID by combining AKI and serial number per ARI specification
        /// </summary>
        private static string GenerateCertificateId(byte[] authorityKeyIdentifier, byte[] serialNumber)
        {
            try
            {
                // Combine AKI and serial number as per ARI specification
                var combined = new byte[authorityKeyIdentifier.Length + serialNumber.Length];
                Array.Copy(authorityKeyIdentifier, 0, combined, 0, authorityKeyIdentifier.Length);
                Array.Copy(serialNumber, 0, combined, authorityKeyIdentifier.Length, serialNumber.Length);
                
                // Encode using base64url (RFC 4648 Section 5)
                return Base64UrlEncode(combined);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate certificate ID", ex);
            }
        }

        /// <summary>
        /// Encodes byte array using base64url encoding per RFC 4648 Section 5
        /// </summary>
        private static string Base64UrlEncode(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            // Convert to base64 then make it URL-safe
            var base64 = Convert.ToBase64String(data);
            
            // Replace URL-unsafe characters and remove padding
            return base64
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Validates that a certificate can be used for ARI (has required extensions)
        /// </summary>
        /// <param name="certificate">Certificate to validate</param>
        /// <returns>True if certificate is suitable for ARI, false otherwise</returns>
        public static bool IsValidForAri(X509Certificate2 certificate)
        {
            if (certificate == null)
                return false;

            try
            {
                // Check if certificate has Authority Key Identifier
                var akiExtension = certificate.Extensions[AuthorityKeyIdentifierOid];
                if (akiExtension == null)
                    return false;

                // Check if we can extract serial number
                if (string.IsNullOrEmpty(certificate.SerialNumber))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

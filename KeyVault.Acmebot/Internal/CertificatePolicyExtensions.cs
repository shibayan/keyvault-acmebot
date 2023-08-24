﻿using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;

namespace KeyVault.Acmebot.Internal;

internal static class CertificatePolicyExtensions
{
    public static CertificatePolicy ToCertificatePolicy(this CertificatePolicyItem certificatePolicyItem)
    {
        var subjectAlternativeNames = new SubjectAlternativeNames();

        foreach (var dnsName in certificatePolicyItem.DnsNames)
        {
            subjectAlternativeNames.DnsNames.Add(dnsName);
        }

        var certificatePolicy = new CertificatePolicy(WellKnownIssuerNames.Unknown, $"CN={certificatePolicyItem.DnsNames[0]}", subjectAlternativeNames)
        {
            KeySize = certificatePolicyItem.KeySize,
            ReuseKey = certificatePolicyItem.ReuseKey
        };

        if (!string.IsNullOrEmpty(certificatePolicyItem.KeyType))
        {
            certificatePolicy.KeyType = certificatePolicyItem.KeyType;
        }

        if (!string.IsNullOrEmpty(certificatePolicyItem.KeyCurveName))
        {
            certificatePolicy.KeyCurveName = certificatePolicyItem.KeyCurveName;
        }

        return certificatePolicy;
    }
}

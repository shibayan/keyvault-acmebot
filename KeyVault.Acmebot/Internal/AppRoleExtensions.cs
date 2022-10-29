using System;
using System.Linq;
using System.Security.Claims;

namespace KeyVault.Acmebot.Internal;

internal static class AppRoleExtensions
{
    private const string IssueCertificateAppRole = "Acmebot.IssueCertificate";
    private const string RevokeCertificateAppRole = "Acmebot.RevokeCertificate";

    private static bool IsAppRoleRequired => bool.TryParse(Environment.GetEnvironmentVariable("Acmebot:AppRoleRequired"), out var result) && result;

    private static bool IsInAppRole(this ClaimsPrincipal claimsPrincipal, string role)
    {
        var roles = claimsPrincipal.Claims.Where(x => x.Type == "roles").Select(x => x.Value);

        return roles.Contains(role);
    }

    public static bool HasIssueCertificateRole(this ClaimsPrincipal claimsPrincipal) => !IsAppRoleRequired || claimsPrincipal.IsInAppRole(IssueCertificateAppRole);

    public static bool HasRevokeCertificateRole(this ClaimsPrincipal claimsPrincipal) => !IsAppRoleRequired || claimsPrincipal.IsInAppRole(RevokeCertificateAppRole);
}

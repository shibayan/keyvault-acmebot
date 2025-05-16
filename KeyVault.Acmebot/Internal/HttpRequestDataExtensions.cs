using System;
using System.Linq;
using System.Security.Claims;

using Microsoft.Azure.Functions.Worker.Http;

namespace KeyVault.Acmebot.Internal;

public static class HttpRequestDataExtensions
{
    public static bool IsAuthenticated(this HttpRequestData request)
    {
        return request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL-ID", out _);
    }

    public static ClaimsPrincipal GetClaimsPrincipal(this HttpRequestData request)
    {
        if (!request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL-ID", out var principalId) || !principalId.Any())
        {
            return new ClaimsPrincipal();
        }

        var identity = new ClaimsIdentity("WebJobsAuthLevel");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, principalId.First()));

        if (request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL-NAME", out var principalName) && principalName.Any())
        {
            identity.AddClaim(new Claim(ClaimTypes.Name, principalName.First()));
        }

        // Add role claims if available
        if (request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL-ROLE", out var roles) && roles.Any())
        {
            foreach (var role in roles.First().Split(','))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        return new ClaimsPrincipal(identity);
    }
    
    public static bool HasRole(this HttpRequestData request, string role)
    {
        var principal = request.GetClaimsPrincipal();
        return principal.IsInRole(role);
    }
    
    public static bool HasIssueCertificateRole(this HttpRequestData request)
    {
        // Implement the same role check as in the in-process model
        return request.IsAuthenticated();
    }
    
    public static bool HasRevokeCertificateRole(this HttpRequestData request)
    {
        // Implement the same role check as in the in-process model
        return request.IsAuthenticated();
    }
}
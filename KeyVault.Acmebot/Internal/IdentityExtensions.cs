using System;
using System.Linq;
using System.Security.Claims;

namespace KeyVault.Acmebot.Internal
{
    internal static class IdentityExtensions
    {
        private const string AppRole = "Acmebot.IssueCertificate";

        private static bool IsAppRoleRequired => bool.TryParse(Environment.GetEnvironmentVariable("Acmebot:AppRoleRequired"), out var result) && result;

        private static bool IsInAppRole(this ClaimsPrincipal claimsPrincipal, string role)
        {
            var roles = claimsPrincipal.Claims.Where(x => x.Type == "roles").Select(x => x.Value);

            return roles.Contains(role);
        }

        public static bool IsAppAuthorized(this ClaimsPrincipal claimsPrincipal)
        {
            if (!claimsPrincipal.Identity.IsAuthenticated)
            {
                return false;
            }

            if (IsAppRoleRequired && !claimsPrincipal.IsInAppRole(AppRole))
            {
                return false;
            }

            return true;
        }
    }
}

using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Identity;

using Microsoft.Rest;

namespace KeyVault.Acmebot.Internal
{
    internal class ManagedIdentityTokenProvider : ITokenProvider
    {
        public ManagedIdentityTokenProvider(IAzureEnvironment environment)
        {
            _environment = environment;

            _tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AuthorityHost = new Uri(environment.ActiveDirectory)
            });
        }

        private readonly IAzureEnvironment _environment;
        private readonly TokenCredential _tokenCredential;

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var context = new TokenRequestContext(new[] { _environment.ResourceManager });

            var accessToken = await _tokenCredential.GetTokenAsync(context, cancellationToken);

            return new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }
    }
}

using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;

namespace KeyVault.Acmebot.Internal
{
    internal class AppAuthenticationTokenProvider : ITokenProvider
    {
        public AppAuthenticationTokenProvider(IAzureEnvironment environment)
        {
            _environment = environment;
            _tokenProvider = new AzureServiceTokenProvider(azureAdInstance: _environment.ActiveDirectory);
        }

        private readonly IAzureEnvironment _environment;
        private readonly AzureServiceTokenProvider _tokenProvider;

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(_environment.ResourceManager, cancellationToken: cancellationToken);

            return new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }
}

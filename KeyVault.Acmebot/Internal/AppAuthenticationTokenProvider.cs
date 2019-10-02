using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;

namespace KeyVault.Acmebot.Internal
{
    internal class AppAuthenticationTokenProvider : ITokenProvider
    {
        private readonly AzureServiceTokenProvider _tokenProvider = new AzureServiceTokenProvider();

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync("https://management.azure.com/", cancellationToken: cancellationToken);

            return new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }
}

using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;

namespace AzureKeyVault.LetsEncrypt.Internal
{
    internal class AppAuthenticationTokenProvider : ITokenProvider
    {
        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var tokenProvider = new AzureServiceTokenProvider();

            var accessToken = await tokenProvider.GetAccessTokenAsync("https://management.azure.com/", cancellationToken: cancellationToken);

            return new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }
}

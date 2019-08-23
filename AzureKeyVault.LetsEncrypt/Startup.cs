using AzureKeyVault.LetsEncrypt.Internal;

using DnsClient;

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;

[assembly: FunctionsStartup(typeof(AzureKeyVault.LetsEncrypt.Startup))]

namespace AzureKeyVault.LetsEncrypt
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();

            builder.Services.AddSingleton(new LookupClient { UseCache = false });

            builder.Services.AddSingleton(provider => new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback)));

            builder.Services.AddSingleton(provider => new DnsManagementClient(new TokenCredentials(new AppAuthenticationTokenProvider()))
            {
                SubscriptionId = Settings.Default.SubscriptionId
            });

            builder.Services.AddScoped(provider => AcmeProtocolClientExtensions.CreateAcmeProtocolClient());
        }
    }
}

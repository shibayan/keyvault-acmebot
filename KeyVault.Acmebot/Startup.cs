using DnsClient;

using KeyVault.Acmebot;
using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Providers;

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Rest;

[assembly: FunctionsStartup(typeof(Startup))]

namespace KeyVault.Acmebot
{
    public class Startup : FunctionsStartup
    {
        public Startup()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables();

            Configuration = config.Build();
        }

        public IConfiguration Configuration { get; }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.Replace(ServiceDescriptor.Transient(typeof(IOptionsFactory<>), typeof(OptionsFactory<>)));

            builder.Services.AddHttpClient();

            builder.Services.AddSingleton(new LookupClient(new LookupClientOptions { UseCache = false }));

            builder.Services.AddSingleton(provider =>
                new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback)));

            builder.Services.AddSingleton<IAcmeProtocolClientFactory, AcmeProtocolClientFactory>();

            builder.Services.AddSingleton<WebhookClient>();
            builder.Services.AddSingleton<ILifeCycleNotificationHelper, WebhookLifeCycleNotification>();

            var section = Configuration.GetSection("Acmebot");

            // Select DNS Provider
            var dnsProvider = section["DnsProvider"];

            if (dnsProvider == "GratisDNS")
                builder.Services.AddSingleton<IDnsProvider, GratisDnsProvider>();

            else
            {
                // Default (Azure DNS)
                builder.Services.AddSingleton(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

                    return new DnsManagementClient(new TokenCredentials(new AppAuthenticationTokenProvider()))
                    {
                        SubscriptionId = options.Value.SubscriptionId
                    };
                });

                builder.Services.AddSingleton<IDnsProvider, AzureDnsProvider>();
            }

            builder.Services.AddOptions<AcmebotOptions>()
                   .Bind(section.Exists() ? section : Configuration.GetSection("LetsEncrypt"))
                   .ValidateDataAnnotations();
        }
    }
}

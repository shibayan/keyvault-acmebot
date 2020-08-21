using System;

using DnsClient;

using KeyVault.Acmebot;
using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

[assembly: FunctionsStartup(typeof(Startup))]

namespace KeyVault.Acmebot
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.Replace(ServiceDescriptor.Transient(typeof(IOptionsFactory<>), typeof(OptionsFactory<>)));

            builder.Services.AddHttpClient();

            builder.Services.AddSingleton(new LookupClient(new LookupClientOptions { UseCache = false }));

            builder.Services.AddSingleton<IAzureEnvironment>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

                return AzureEnvironment.Get(options.Value.Environment);
            });

            builder.Services.AddSingleton(provider =>
            {
                var environment = provider.GetRequiredService<IAzureEnvironment>();
                var tokenProvider = new AzureServiceTokenProvider(azureAdInstance: environment.ActiveDirectory);

                return new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
            });

            builder.Services.AddSingleton<IAcmeProtocolClientFactory, AcmeProtocolClientFactory>();

            builder.Services.AddSingleton<WebhookClient>();
            builder.Services.AddSingleton<ILifeCycleNotificationHelper, WebhookLifeCycleNotification>();

            builder.Services.AddSingleton<IDnsProvider>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
                var environment = provider.GetRequiredService<IAzureEnvironment>();

                if (options.Cloudflare != null)
                {
                    return new CloudflareProvider(options.Cloudflare);
                }

                if (options.GratisDns != null)
                {
                    return new GratisDnsProvider(options.GratisDns);
                }

                if (options.AzureDns != null)
                {
                    return new AzureDnsProvider(options.AzureDns, environment);
                }

                if (options.SubscriptionId != null)
                {
                    return new AzureDnsProvider(new AzureDnsOptions { SubscriptionId = options.SubscriptionId }, environment);
                }

                throw new NotSupportedException();
            });

            var context = builder.GetContext();

            var section = context.Configuration.GetSection("Acmebot");

            builder.Services.AddOptions<AcmebotOptions>()
                   .Bind(section.Exists() ? section : context.Configuration.GetSection("LetsEncrypt"))
                   .ValidateDataAnnotations();
        }
    }
}

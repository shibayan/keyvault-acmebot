using System;

using Azure.Identity;
using Azure.Security.KeyVault.Certificates;

using DnsClient;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

[assembly: FunctionsStartup(typeof(KeyVault.Acmebot.Startup))]

namespace KeyVault.Acmebot
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var context = builder.GetContext();

            var section = context.Configuration.GetSection("Acmebot");

            // Add Options
            builder.Services.AddOptions<AcmebotOptions>()
                   .Bind(section.Exists() ? section : context.Configuration.GetSection("LetsEncrypt"))
                   .ValidateDataAnnotations();

            // Add Services
            builder.Services.Replace(ServiceDescriptor.Transient(typeof(IOptionsFactory<>), typeof(OptionsFactory<>)));

            builder.Services.AddHttpClient();

            builder.Services.AddSingleton<ITelemetryInitializer, ApplicationVersionInitializer<Startup>>();

            builder.Services.AddSingleton(new LookupClient(new LookupClientOptions(NameServer.GooglePublicDns, NameServer.GooglePublicDns2)
            {
                UseCache = false,
                UseRandomNameServer = true
            }));

            builder.Services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

                return AzureEnvironment.Get(options.Value.Environment);
            });

            builder.Services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();
                var environment = provider.GetRequiredService<AzureEnvironment>();

                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    AuthorityHost = environment.ActiveDirectory
                });

                return new CertificateClient(new Uri(options.Value.VaultBaseUrl), credential);
            });

            builder.Services.AddSingleton<AcmeProtocolClientFactory>();

            builder.Services.AddSingleton<WebhookInvoker>();
            builder.Services.AddSingleton<ILifeCycleNotificationHelper, WebhookLifeCycleNotification>();

            builder.Services.AddSingleton<IDnsProvider>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
                var environment = provider.GetRequiredService<AzureEnvironment>();

                if (options.Cloudflare != null)
                {
                    return new CloudflareProvider(options.Cloudflare);
                }

                if (options.CustomDns != null)
                {
                    return new CustomDnsProvider(options.CustomDns);
                }

                if (options.DnsMadeEasy != null)
                {
                    return new DnsMadeEasyProvider(options.DnsMadeEasy);
                }

                if (options.Gandi != null)
                {
                    return new GandiProvider(options.Gandi);
                }

                if (options.GoDaddy != null)
                {
                    return new GoDaddyProvider(options.GoDaddy);
                }

                if (options.GoogleDns != null || options.Google != null)
                {
                    return new GoogleDnsProvider(options.GoogleDns ?? options.Google);
                }

                if (options.GratisDns != null)
                {
                    return new GratisDnsProvider(options.GratisDns);
                }

                if (options.Route53 != null)
                {
                    return new Route53Provider(options.Route53);
                }

                if (options.TransIp != null)
                {
                    return new TransIpProvider(options, options.TransIp, environment);
                }

                // Backward compatibility
                if (options.AzureDns != null)
                {
                    return new AzureDnsProvider(options.AzureDns, environment);
                }

                if (options.SubscriptionId != null)
                {
                    return new AzureDnsProvider(new AzureDnsOptions { SubscriptionId = options.SubscriptionId }, environment);
                }

                throw new NotSupportedException("DNS Provider is not configured. Please check the documentation and configure it.");
            });
        }
    }
}

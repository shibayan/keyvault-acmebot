using System;
using System.Collections.Generic;

using Azure.Identity;
using Azure.Security.KeyVault.Certificates;

using DnsClient;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

[assembly: FunctionsStartup(typeof(KeyVault.Acmebot.Startup))]

namespace KeyVault.Acmebot;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var context = builder.GetContext();

        // Add Options
        builder.Services.AddOptions<AcmebotOptions>()
               .Bind(context.Configuration.GetSection("Acmebot"))
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

        // Add DNS Providers
        builder.Services.AddSingleton<IEnumerable<IDnsProvider>>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
            var environment = provider.GetRequiredService<AzureEnvironment>();

            var dnsProviders = new List<IDnsProvider>();

            dnsProviders.TryAdd(options.AzureDns, () => new AzureDnsProvider(options.AzureDns, environment));
            dnsProviders.TryAdd(options.Cloudflare, () => new CloudflareProvider(options.Cloudflare));
            dnsProviders.TryAdd(options.CustomDns, () => new CustomDnsProvider(options.CustomDns));
            dnsProviders.TryAdd(options.DnsMadeEasy, () => new DnsMadeEasyProvider(options.DnsMadeEasy));
            dnsProviders.TryAdd(options.Gandi, () => new GandiProvider(options.Gandi));
            dnsProviders.TryAdd(options.GoDaddy, () => new GoDaddyProvider(options.GoDaddy));
            dnsProviders.TryAdd(options.GoogleDns, () => new GoogleDnsProvider(options.GoogleDns));
            dnsProviders.TryAdd(options.Route53, () => new Route53Provider(options.Route53));
            dnsProviders.TryAdd(options.TransIp, () => new TransIpProvider(options, options.TransIp, environment));

            if (dnsProviders.Count == 0)
            {
                throw new NotSupportedException("DNS Provider is not configured. Please check the documentation and configure it.");
            }

            return dnsProviders;
        });
    }
}

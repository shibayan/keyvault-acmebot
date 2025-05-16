using Azure.Identity;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Certificates;

using DnsClient;

using KeyVault.Acmebot.Functions;
using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Net.Http;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.UseMiddleware<ApplicationInsightsLoggingMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Add Application Insights services
        services.AddApplicationInsightsTelemetryWorkerService();

        // Options
        services.Configure<AcmebotOptions>(configuration.GetSection("Acmebot"));
        services.Configure<ExternalAccountBindingOptions>(configuration.GetSection("ExternalAccountBinding"));

        // DNS Providers
        services.Configure<AzureDnsOptions>(configuration.GetSection("AzureDns"));
        services.Configure<AzurePrivateDnsOptions>(configuration.GetSection("AzurePrivateDns"));
        services.Configure<CloudflareOptions>(configuration.GetSection("Cloudflare"));
        services.Configure<CustomDnsOptions>(configuration.GetSection("CustomDns"));
        services.Configure<DnsMadeEasyOptions>(configuration.GetSection("DnsMadeEasy"));
        services.Configure<GandiOptions>(configuration.GetSection("Gandi"));
        services.Configure<GandiLiveDnsOptions>(configuration.GetSection("GandiLiveDns"));
        services.Configure<GoDaddyOptions>(configuration.GetSection("GoDaddy"));
        services.Configure<GoogleDnsOptions>(configuration.GetSection("GoogleDns"));
        services.Configure<Route53Options>(configuration.GetSection("Route53"));
        services.Configure<TransIpOptions>(configuration.GetSection("TransIp"));

        // HTTP Client
        services.AddHttpClient();

        // DNS Lookup Client
        services.AddSingleton<LookupClient>();

        // Azure Environment
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();
            return AzureEnvironment.Get(options.Value.Environment);
        });

        // Azure ResourceManager Clients
        services.AddSingleton(provider => 
        {
            var azureEnvironment = provider.GetRequiredService<AzureEnvironment>();
            
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AuthorityHost = azureEnvironment.AuthorityHost
            });

            // Create ArmClient with the credential
            return new ArmClient(credential);
        });

        services.AddSingleton(provider => provider.GetRequiredService<ArmClient>().GetDefaultSubscriptionAsync().GetAwaiter().GetResult());

        // Azure KeyVault Clients
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();
            var azureEnvironment = provider.GetRequiredService<AzureEnvironment>();

            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AuthorityHost = azureEnvironment.AuthorityHost
            });

            return new CertificateClient(new Uri(options.Value.VaultBaseUrl), credential);
        });

        // ACME Protocol Clients
        services.AddSingleton<AcmeProtocolClientFactory>();

        // Webhook
        services.AddSingleton<WebhookInvoker>();
        services.AddSingleton<IWebhookPayloadBuilder, TeamsPayloadBuilder>();

        // Individual DNS Providers
        services.AddSingleton<AzureDnsProvider>();
        services.AddSingleton<AzurePrivateDnsProvider>();
        services.AddSingleton<CloudflareProvider>();
        services.AddSingleton<CustomDnsProvider>();
        services.AddSingleton<DnsMadeEasyProvider>();
        services.AddSingleton<GandiProvider>();
        services.AddSingleton<GandiLiveDnsProvider>();
        services.AddSingleton<GoDaddyProvider>();
        services.AddSingleton<GoogleDnsProvider>();
        services.AddSingleton<Route53Provider>();
        services.AddSingleton<TransIpProvider>();

        // DNS Provider Factory
        services.AddSingleton<IDnsProvider>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();
            
            // Find the provider based on options
            if (options.Value.AzureDns != null)
            {
                return provider.GetRequiredService<AzureDnsProvider>();
            }
            else if (options.Value.AzurePrivateDns != null)
            {
                return provider.GetRequiredService<AzurePrivateDnsProvider>();
            }
            // Add similar checks for other providers
            else if (options.Value.Cloudflare != null)
            {
                return provider.GetRequiredService<CloudflareProvider>();
            }
            // etc.

            throw new NotSupportedException($"No DNS provider is configured.");
        });

        // Shared Activity
        services.AddSingleton<SharedActivity>();
    })
    .Build();

await host.RunAsync();
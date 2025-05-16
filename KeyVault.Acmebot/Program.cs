using System;
using System.Collections.Generic;

using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;

using DnsClient;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.AddApplicationInsights()
               .AddApplicationInsightsLogger();
    })
    .ConfigureServices((context, services) =>
    {
        // Add Options
        services.AddOptions<AcmebotOptions>()
               .Bind(context.Configuration.GetSection("Acmebot"))
               .ValidateDataAnnotations();

        // Add Services
        services.Replace(ServiceDescriptor.Transient(typeof(IOptionsFactory<>), typeof(OptionsFactory<>)));

        services.AddHttpClient();

        services.AddSingleton<ITelemetryInitializer, ApplicationVersionInitializer>();

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

            var lookupClientOptions = options.Value.UseSystemNameServer ? new LookupClientOptions() : new LookupClientOptions(NameServer.GooglePublicDns, NameServer.GooglePublicDns2);

            lookupClientOptions.UseCache = false;
            lookupClientOptions.UseRandomNameServer = true;

            return new LookupClient(lookupClientOptions);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

            return AzureEnvironment.Get(options.Value.Environment);
        });

        services.AddSingleton<TokenCredential>(provider =>
        {
            var environment = provider.GetRequiredService<AzureEnvironment>();

            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AuthorityHost = environment.AuthorityHost
            });
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();
            var credential = provider.GetRequiredService<TokenCredential>();

            return new CertificateClient(new Uri(options.Value.VaultBaseUrl), credential);
        });

        services.AddSingleton<AcmeProtocolClientFactory>();

        // Add Webhook invoker
        services.AddSingleton<WebhookInvoker>();
        services.AddSingleton<ILifeCycleNotificationHelper, WebhookLifeCycleNotification>();

        services.AddSingleton<IWebhookPayloadBuilder>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;

            if (options.Webhook is null)
            {
                return new GenericPayloadBuilder(options);
            }

            if (options.Webhook.Host.EndsWith("hooks.slack.com", StringComparison.OrdinalIgnoreCase))
            {
                return new SlackPayloadBuilder();
            }

            if (options.Webhook.Host.EndsWith(".logic.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return new TeamsPayloadBuilder();
            }

            if (options.Webhook.Host.EndsWith(".office.com", StringComparison.OrdinalIgnoreCase))
            {
                return new LegacyTeamsPayloadBuilder();
            }

            return new GenericPayloadBuilder(options);
        });

        // Add DNS Providers
        services.AddSingleton<IEnumerable<IDnsProvider>>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
            var environment = provider.GetRequiredService<AzureEnvironment>();
            var credential = provider.GetRequiredService<TokenCredential>();

            var dnsProviders = new List<IDnsProvider>();

            dnsProviders.TryAdd(options.AzureDns, o => new AzureDnsProvider(o, environment, credential));
            dnsProviders.TryAdd(options.AzurePrivateDns, o => new AzurePrivateDnsProvider(o, environment, credential));
            dnsProviders.TryAdd(options.Cloudflare, o => new CloudflareProvider(o));
            dnsProviders.TryAdd(options.CustomDns, o => new CustomDnsProvider(o));
            dnsProviders.TryAdd(options.DnsMadeEasy, o => new DnsMadeEasyProvider(o));
            dnsProviders.TryAdd(options.Gandi, o => new GandiProvider(o));
            dnsProviders.TryAdd(options.GandiLiveDns, o => new GandiLiveDnsProvider(o));
            dnsProviders.TryAdd(options.GoDaddy, o => new GoDaddyProvider(o));
            dnsProviders.TryAdd(options.GoogleDns, o => new GoogleDnsProvider(o));
            dnsProviders.TryAdd(options.Route53, o => new Route53Provider(o));
            dnsProviders.TryAdd(options.TransIp, o => new TransIpProvider(options, o, credential));

            if (dnsProviders.Count == 0)
            {
                throw new NotSupportedException("DNS Provider is not configured. Please check the documentation and configure it.");
            }

            return dnsProviders;
        });
    })
    .Build();

host.Run();
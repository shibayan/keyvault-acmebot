﻿using System;
using System.Collections.Generic;
using System.Text.Json;

using Azure.Core;
using Azure.Functions.Worker.Extensions.HttpApi.Config;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;

using DnsClient;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication()
    .AddHttpApi();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.Configure<JsonSerializerOptions>(options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Add Options
builder.Services.AddOptions<AcmebotOptions>()
       .Bind(builder.Configuration.GetSection("Acmebot"))
       .ValidateDataAnnotations();

// Add Services
builder.Services.AddHttpClient();

builder.Services.AddSingleton<ITelemetryInitializer, ApplicationVersionInitializer>();

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

    var lookupClientOptions = options.Value.UseSystemNameServer ? new LookupClientOptions() : new LookupClientOptions(NameServer.GooglePublicDns, NameServer.GooglePublicDns2);

    lookupClientOptions.UseCache = false;
    lookupClientOptions.UseRandomNameServer = true;

    return new LookupClient(lookupClientOptions);
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

    return AzureEnvironment.Get(options.Value.Environment);
});

builder.Services.AddSingleton<TokenCredential>(provider =>
{
    var environment = provider.GetRequiredService<AzureEnvironment>();
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();

    return new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        AuthorityHost = environment.AuthorityHost,
        ManagedIdentityClientId = options.Value.ManagedIdentityClientId
    });
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>();
    var credential = provider.GetRequiredService<TokenCredential>();

    return new CertificateClient(new Uri(options.Value.VaultBaseUrl), credential);
});

builder.Services.AddSingleton<AcmeProtocolClientFactory>();

// Add Webhook invoker
builder.Services.AddSingleton<WebhookInvoker>();

builder.Services.AddSingleton<IWebhookPayloadBuilder>(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;

    if (options.Webhook is null)
    {
        return new GenericPayloadBuilder(options);
    }

    var host = options.Webhook.Host;

    if (host.EndsWith("hooks.slack.com", StringComparison.OrdinalIgnoreCase))
    {
        return new SlackPayloadBuilder();
    }

    if (host.EndsWith(".logic.azure.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".environment.api.powerplatform.com", StringComparison.OrdinalIgnoreCase))
    {
        return new TeamsPayloadBuilder();
    }

    if (host.EndsWith(".office.com", StringComparison.OrdinalIgnoreCase))
    {
        return new LegacyTeamsPayloadBuilder();
    }

    return new GenericPayloadBuilder(options);
});

// Add DNS Providers
builder.Services.AddSingleton<IEnumerable<IDnsProvider>>(provider =>
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

builder.Build().Run();

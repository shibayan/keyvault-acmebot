using System;
using System.Collections.Generic;

using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Internal;

internal class GenericPayloadBuilder : IWebhookPayloadBuilder
{
    public GenericPayloadBuilder(AcmebotOptions options)
    {
        _options = options;
    }

    private readonly AcmebotOptions _options;

    public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        return new
        {
            certificateName,
            expirationDate,
            dnsNames,
            acmeEndpoint,
            keyVaultName = new Uri(_options.VaultBaseUrl).Host,
            functionAppName = Constants.FunctionAppName
        };
    }

    public object BuildFailed(string functionName, string reason)
    {
        return new
        {
            functionName,
            reason,
            keyVaultName = new Uri(_options.VaultBaseUrl).Host,
            functionAppName = Constants.FunctionAppName
        };
    }
}

using System;
using System.Collections.Generic;

namespace KeyVault.Acmebot.Internal;

internal class GenericPayloadBuilder : IWebhookPayloadBuilder
{
    public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        return new
        {
            certificateName,
            expirationDate,
            dnsNames,
            acmeEndpoint
        };
    }

    public object BuildFailed(string functionName, string reason)
    {
        return new
        {
            functionName,
            reason
        };
    }
}

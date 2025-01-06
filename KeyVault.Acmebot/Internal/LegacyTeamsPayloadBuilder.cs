using System;
using System.Collections.Generic;

namespace KeyVault.Acmebot.Internal;

internal class LegacyTeamsPayloadBuilder : IWebhookPayloadBuilder
{
    public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        return new
        {
            title = "Acmebot",
            text = @$"A new certificate has been issued.

**Certificate Name**: {certificateName}

**Expiration Date**: {expirationDate}

**ACME Endpoint**: {acmeEndpoint}

**DNS Names**: {string.Join(", ", dnsNames)}",
            themeColor = "2EB886"
        };
    }

    public object BuildFailed(string functionName, string reason)
    {
        return new
        {
            title = "Acmebot",
            text = @$"**{functionName}**

**Reason**

{reason}",
            themeColor = "A30200"
        };
    }
}

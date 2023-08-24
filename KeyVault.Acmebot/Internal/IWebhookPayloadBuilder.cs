using System;
using System.Collections.Generic;

namespace KeyVault.Acmebot.Internal;

public interface IWebhookPayloadBuilder
{
    object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint);
    object BuildFailed(string functionName, string reason);
}

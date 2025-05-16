using System;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class PurgeInstanceHistory
{
    private readonly ILogger _logger;

    public PurgeInstanceHistory(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PurgeInstanceHistory>();
    }

    [Function($"{nameof(PurgeInstanceHistory)}_{nameof(Timer)}")]
    public Task Timer([TimerTrigger("0 0 0 1 * *")] object timerInfo, [DurableClient] DurableTaskClient starter)
    {
        _logger.LogInformation("This function is a placeholder for purging instance history. In the isolated model, you'll need to implement a custom solution for purging old instances.");
        
        // In the isolated model, DurableTaskClient doesn't have a direct method for purging instance history.
        // We'll keep this function as a placeholder and implement a proper solution when needed.
        return Task.CompletedTask;
    }
}
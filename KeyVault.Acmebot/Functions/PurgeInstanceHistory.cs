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
    public Task Timer([TimerTrigger("0 0 0 1 * *")] FunctionContext context, [DurableClient] DurableTaskClient starter)
    {
        _logger.LogInformation("Purging instance history for completed and failed orchestrations older than one month");
        
        return starter.PurgeInstancesAsync(
            new PurgeInstancesOptions
            {
                CreatedTimeFrom = DateTime.MinValue,
                CreatedTimeTo = DateTime.UtcNow.AddMonths(-1),
                RuntimeStatusFilter = { OrchestrationRuntimeStatus.Completed, OrchestrationRuntimeStatus.Failed }
            });
    }
}

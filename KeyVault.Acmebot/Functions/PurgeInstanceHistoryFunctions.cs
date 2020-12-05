using System;
using System.Threading.Tasks;

using DurableTask.Core;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace KeyVault.Acmebot.Functions
{
    public class PurgeInstanceHistoryFunctions
    {
        [FunctionName(nameof(PurgeInstanceHistory_Timer))]
        public Task PurgeInstanceHistory_Timer(
            [TimerTrigger("0 0 6 * * 0")] TimerInfo timer,
            [DurableClient] IDurableClient starter)
        {
            return starter.PurgeInstanceHistoryAsync(
                DateTime.MinValue,
                DateTime.UtcNow.AddDays(-30),
                new[]
                {
                    OrchestrationStatus.Completed,
                    OrchestrationStatus.Failed
                });
        }
    }
}

using System;
using System.Threading.Tasks;

using DurableTask.Core;

using Microsoft.Azure.WebJobs;

namespace KeyVault.Acmebot
{
    public class PurgeInstanceHistoryFunctions
    {
        [FunctionName(nameof(PurgeInstanceHistory_Timer))]
        public Task PurgeInstanceHistory_Timer(
            [TimerTrigger("0 0 6 * * 0")] TimerInfo timer,
            [OrchestrationClient] DurableOrchestrationClient starter)
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

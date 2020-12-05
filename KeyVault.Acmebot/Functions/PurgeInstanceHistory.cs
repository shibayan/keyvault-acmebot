﻿using System;
using System.Threading.Tasks;

using DurableTask.Core;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace KeyVault.Acmebot.Functions
{
    public class PurgeInstanceHistory
    {
        [FunctionName(nameof(PurgeInstanceHistory) + "_" + nameof(Timer))]
        public Task Timer([TimerTrigger("0 0 6 * * 0")] TimerInfo timer, [DurableClient] IDurableClient starter)
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

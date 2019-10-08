using System;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot
{
    public class PurgeHistoryFunctions
    {
        [FunctionName(nameof(PurgeHistory_Timer))]
        public async Task PurgeHistory_Timer([TimerTrigger("0 */5 * * * *")] TimerInfo timer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}

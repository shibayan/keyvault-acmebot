using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;

namespace KeyVault.Acmebot.Internal
{
    internal static class AzureSdkExtensions
    {
        public static async Task<IList<Zone>> ListAllAsync(this IZonesOperations operations)
        {
            var zones = new List<Zone>();

            var list = await operations.ListAsync();

            zones.AddRange(list);

            while (list.NextPageLink != null)
            {
                list = await operations.ListNextAsync(list.NextPageLink);

                zones.AddRange(list);
            }

            return zones;
        }

        public static async Task<RecordSet> GetOrDefaultAsync(this IRecordSetsOperations operations, string resourceGroupName, string zoneName, string relativeRecordSetName, RecordType recordType)
        {
            try
            {
                return await operations.GetAsync(resourceGroupName, zoneName, relativeRecordSetName, recordType);
            }
            catch
            {
                return null;
            }
        }
    }
}

using System;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace KeyVault.Acmebot.Internal
{
    public static class DurableClientExtensions
    {
        public static Task<HttpResponseData> CreateCheckStatusResponseAsync(
            this DurableTaskClient client,
            HttpRequestData requestData,
            string instanceId,
            TimeSpan? timeout = null)
        {
            // Create a custom polling URL
            var statusUrl = GetStatusUrl(requestData.Url, instanceId);
            
            // Create a 202 response with a Location header
            var response = requestData.CreateResponse(System.Net.HttpStatusCode.Accepted);
            response.Headers.Add("Location", statusUrl);
            
            // Add retry headers if a timeout is specified
            if (timeout.HasValue)
            {
                response.Headers.Add("Retry-After", Math.Ceiling(timeout.Value.TotalSeconds).ToString());
            }
            
            return Task.FromResult(response);
        }

        private static string GetStatusUrl(Uri requestUrl, string instanceId)
        {
            // Generate a URL to the status endpoint for this instance
            return $"{requestUrl.GetLeftPart(UriPartial.Authority)}/api/state/{instanceId}";
        }
    }
}
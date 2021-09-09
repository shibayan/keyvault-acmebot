using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Internal
{
    internal static class HttpClientExtensions
    {
        public static Task<HttpResponseMessage> PostAsync<T>(this HttpClient client, string requestUri, T value) => client.PostAsync(requestUri, SerializeToJson(value));

        public static Task<HttpResponseMessage> PutAsync<T>(this HttpClient client, string requestUri, T value) => client.PutAsync(requestUri, SerializeToJson(value));

        public static Task<HttpResponseMessage> PatchAsync<T>(this HttpClient client, string requestUri, T value) => client.PatchAsync(requestUri, SerializeToJson(value));

        public static Task<HttpResponseMessage> DeleteAsync<T>(this HttpClient client, string requestUri, T value)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
            {
                Content = SerializeToJson(value)
            };

            return client.SendAsync(request);
        }

        private static HttpContent SerializeToJson<T>(T value) => new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");
    }
}

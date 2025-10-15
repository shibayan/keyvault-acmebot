using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace KeyVault.Acmebot.Internal;

internal static class HttpContentExtensions
{
    public static async Task<T> ReadAsAsync<T>(this HttpContent content)
    {
        await using var contentStream = await content.ReadAsStreamAsync();

        return await JsonSerializer.DeserializeAsync<T>(contentStream);
    }
}

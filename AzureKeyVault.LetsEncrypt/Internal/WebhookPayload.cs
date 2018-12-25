using Newtonsoft.Json;

namespace AzureKeyVault.LetsEncrypt.Internal
{
    internal class WebhookPayload
    {
        [JsonProperty("isSuccess")]
        public bool IsSuccess { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("hostNames")]
        public string[] HostNames { get; set; }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;

using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using Newtonsoft.Json;

namespace AzureKeyVault.LetsEncrypt.Internal
{
    internal static class AcmeProtocolClientExtensions
    {
        private static readonly Uri _acmeEndpoint = new Uri("https://acme-v02.api.letsencrypt.org/");

        internal static AcmeProtocolClient CreateAcmeProtocolClient()
        {
            var account = LoadState<AccountDetails>("account.json");
            var accountKey = LoadState<AccountKey>("account_key.json");
            var acmeDir = LoadState<ServiceDirectory>("directory.json");

            var acmeProtocolClient = new AcmeProtocolClient(_acmeEndpoint, acmeDir, account, accountKey?.GenerateSigner());

            acmeProtocolClient.EnsureInitializedAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            return acmeProtocolClient;
        }

        internal static async Task EnsureInitializedAsync(this AcmeProtocolClient acmeProtocolClient)
        {
            if (acmeProtocolClient.Directory.NewNonce == null)
            {
                var directory = await acmeProtocolClient.GetDirectoryAsync();

                acmeProtocolClient.Directory = directory;
            }

            await acmeProtocolClient.GetNonceAsync();

            if (acmeProtocolClient.Account == null)
            {
                var account = await acmeProtocolClient.CreateAccountAsync(new[] { "mailto:" + Settings.Default.Contacts }, true);

                var accountKey = new AccountKey
                {
                    KeyType = acmeProtocolClient.Signer.JwsAlg,
                    KeyExport = acmeProtocolClient.Signer.Export()
                };

                SaveState(account, "account.json");
                SaveState(accountKey, "account_key.json");

                acmeProtocolClient.Account = account;
            }
        }

        private static TState LoadState<TState>(string path)
        {
            var fullPath = Environment.ExpandEnvironmentVariables(@"%HOME%\.acme\" + path);

            if (!File.Exists(fullPath))
            {
                return default;
            }

            var json = File.ReadAllText(fullPath);

            return JsonConvert.DeserializeObject<TState>(json);
        }

        private static void SaveState<TState>(TState value, string path)
        {
            var fullPath = Environment.ExpandEnvironmentVariables(@"%HOME%\.acme\" + path);
            var directoryPath = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonConvert.SerializeObject(value, Formatting.Indented);

            File.WriteAllText(fullPath, json);
        }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;

using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Internal
{
    public interface IAcmeProtocolClientFactory
    {
        Task<AcmeProtocolClient> CreateClientAsync();
    }

    internal class AcmeProtocolClientFactory : IAcmeProtocolClientFactory
    {
        public AcmeProtocolClientFactory(IOptions<AcmebotOptions> options)
        {
            _options = options.Value;
            _baseUri = new Uri(_options.Endpoint);
        }

        private readonly AcmebotOptions _options;
        private readonly Uri _baseUri;

        public async Task<AcmeProtocolClient> CreateClientAsync()
        {
            var account = LoadState<AccountDetails>("account.json");
            var accountKey = LoadState<AccountKey>("account_key.json");
            var directory = LoadState<ServiceDirectory>("directory.json");

            var acmeProtocolClient = new AcmeProtocolClient(_baseUri, directory, account, accountKey?.GenerateSigner());

            if (directory == null)
            {
                directory = await acmeProtocolClient.GetDirectoryAsync();

                acmeProtocolClient.Directory = directory;
            }

            await acmeProtocolClient.GetNonceAsync();

            if (acmeProtocolClient.Account == null)
            {
                account = await acmeProtocolClient.CreateAccountAsync(new[] { "mailto:" + _options.Contacts }, true);

                accountKey = new AccountKey
                {
                    KeyType = acmeProtocolClient.Signer.JwsAlg,
                    KeyExport = acmeProtocolClient.Signer.Export()
                };

                SaveState(account, "account.json");
                SaveState(accountKey, "account_key.json");

                acmeProtocolClient.Account = account;
            }

            return acmeProtocolClient;
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

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

using ACMESharp.Crypto;
using ACMESharp.Crypto.JOSE;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using KeyVault.Acmebot.Options;

using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Internal
{
    public class AcmeProtocolClientFactory
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

            var acmeProtocolClient = new AcmeProtocolClient(_baseUri, directory, account, accountKey?.GenerateSigner(), usePostAsGet: true);

            if (directory == null)
            {
                try
                {
                    directory = await acmeProtocolClient.GetDirectoryAsync();
                }
                catch (AcmeProtocolException)
                {
                    acmeProtocolClient.Directory.Directory = "";

                    directory = await acmeProtocolClient.GetDirectoryAsync();
                }

                SaveState(directory, "directory.json");

                acmeProtocolClient.Directory = directory;
            }

            await acmeProtocolClient.GetNonceAsync();

            if (acmeProtocolClient.Account == null)
            {
                var externalAccountBinding = directory.Meta.ExternalAccountRequired ?? false ? CreateExternalAccountBinding(acmeProtocolClient) : null;

                account = await acmeProtocolClient.CreateAccountAsync(new[] { $"mailto:{_options.Contacts}" }, true, externalAccountBinding);

                accountKey = new AccountKey
                {
                    KeyType = acmeProtocolClient.Signer.JwsAlg,
                    KeyExport = acmeProtocolClient.Signer.Export()
                };

                SaveState(account, "account.json");
                SaveState(accountKey, "account_key.json");

                acmeProtocolClient.Account = account;
            }

            if (acmeProtocolClient.Account.Payload.Contact[0] != $"mailto:{_options.Contacts}")
            {
                account = await acmeProtocolClient.UpdateAccountAsync(new[] { $"mailto:{_options.Contacts}" });

                SaveState(account, "account.json");

                acmeProtocolClient.Account = account;
            }

            return acmeProtocolClient;
        }

        private object CreateExternalAccountBinding(AcmeProtocolClient acmeProtocolClient)
        {
            byte[] HmacSignature(byte[] x)
            {
                var hmacKeyBytes = CryptoHelper.Base64.UrlDecode(_options.ExternalAccountBinding.HmacKey);

                var hmac = (HMAC)(_options.ExternalAccountBinding.Algorithm switch
                {
                    "HS256" => new HMACSHA256(hmacKeyBytes),
                    "HS384" => new HMACSHA384(hmacKeyBytes),
                    "HS512" => new HMACSHA512(hmacKeyBytes),
                    _ => throw new NotSupportedException($"The signature algorithm {_options.ExternalAccountBinding.Algorithm} is not supported.")
                });

                return hmac.ComputeHash(x);
            }

            if (string.IsNullOrEmpty(_options.ExternalAccountBinding.KeyId) || string.IsNullOrEmpty(_options.ExternalAccountBinding.HmacKey))
            {
                throw new PreconditionException("This ACME endpoint requires External Account Binding.");
            }

            var payload = JsonConvert.SerializeObject(acmeProtocolClient.Signer.ExportJwk());

            var protectedHeaders = new
            {
                alg = _options.ExternalAccountBinding.Algorithm,
                kid = _options.ExternalAccountBinding.KeyId,
                url = acmeProtocolClient.Directory.NewAccount
            };

            return JwsHelper.SignFlatJsonAsObject(HmacSignature, payload, protectedHeaders);
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

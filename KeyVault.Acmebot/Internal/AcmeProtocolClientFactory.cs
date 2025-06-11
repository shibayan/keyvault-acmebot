using System;
using System.IO;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;

using ACMESharp.Crypto;
using ACMESharp.Crypto.JOSE;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using KeyVault.Acmebot.Options;

using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Internal;

public class AcmeProtocolClientFactory
{
    public AcmeProtocolClientFactory(IOptions<AcmebotOptions> options)
    {
        _options = options.Value;
    }

    private readonly AcmebotOptions _options;

    public async Task<AcmeProtocolClient> CreateClientAsync()
    {
        var account = LoadState<AccountDetails>("account.json");
        var accountKey = LoadState<AccountKey>("account_key.json");
        var directory = LoadTempState<ServiceDirectory>("directory.json");

        var acmeProtocolClient = new AcmeProtocolClient(_options.Endpoint, directory, account, accountKey?.GenerateSigner(), usePostAsGet: true)
        {
            BeforeHttpSend = (_, req) =>
            {
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue("KeyVault-Acmebot", Constants.ApplicationVersion));
            }
        };

        if (directory is null)
        {
            try
            {
                directory = await acmeProtocolClient.GetDirectoryAsync();
            }
            catch
            {
                acmeProtocolClient.Directory.Directory = "directory";

                directory = await acmeProtocolClient.GetDirectoryAsync();
            }

            SaveTempState(directory, "directory.json");

            acmeProtocolClient.Directory = directory;
        }

        await acmeProtocolClient.GetNonceAsync();

        if (acmeProtocolClient.Account is null)
        {
            var externalAccountBinding = CreateExternalAccountBinding(acmeProtocolClient);

            if (externalAccountBinding is null && (directory.Meta.ExternalAccountRequired ?? false))
            {
                throw new PreconditionException("This ACME endpoint requires External Account Binding.");
            }

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

        if (acmeProtocolClient.Account.Payload.Contact is { Length: > 0 } && acmeProtocolClient.Account.Payload.Contact[0] != $"mailto:{_options.Contacts}")
        {
            account = await acmeProtocolClient.UpdateAccountAsync(new[] { $"mailto:{_options.Contacts}" });

            SaveState(account, "account.json");

            acmeProtocolClient.Account = account;
        }

        return acmeProtocolClient;
    }

    private object CreateExternalAccountBinding(AcmeProtocolClient acmeProtocolClient)
    {
        if (string.IsNullOrEmpty(_options.ExternalAccountBinding?.KeyId) || string.IsNullOrEmpty(_options.ExternalAccountBinding?.HmacKey))
        {
            return null;
        }

        byte[] HmacSignature(byte[] x)
        {
            var hmacKeyBytes = CryptoHelper.Base64.UrlDecode(_options.ExternalAccountBinding.HmacKey);

            return _options.ExternalAccountBinding.Algorithm switch
            {
                "HS256" => HMACSHA256.HashData(hmacKeyBytes, x),
                "HS384" => HMACSHA384.HashData(hmacKeyBytes, x),
                "HS512" => HMACSHA512.HashData(hmacKeyBytes, x),
                _ => throw new NotSupportedException($"The signature algorithm {_options.ExternalAccountBinding.Algorithm} is not supported. (supported values are HS256 / HS384 / HS512)")
            };
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

    private TState LoadState<TState>(string path)
    {
        var fullPath = ResolveStateFullPath(path);

        if (!File.Exists(fullPath))
        {
            return default;
        }

        var json = File.ReadAllText(fullPath);

        return JsonConvert.DeserializeObject<TState>(json);
    }

    private void SaveState<TState>(TState value, string path)
    {
        var fullPath = ResolveStateFullPath(path);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonConvert.SerializeObject(value, Formatting.Indented);

        File.WriteAllText(fullPath, json);
    }

    private TState LoadTempState<TState>(string path)
    {
        var fullPath = ResolveTempStateFullPath(path);

        if (!File.Exists(fullPath))
        {
            return default;
        }

        var json = File.ReadAllText(fullPath);

        return JsonConvert.DeserializeObject<TState>(json);
    }

    private void SaveTempState<TState>(TState value, string path)
    {
        var fullPath = ResolveTempStateFullPath(path);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonConvert.SerializeObject(value, Formatting.Indented);

        File.WriteAllText(fullPath, json);
    }

    private string ResolveStateFullPath(string path) => Environment.ExpandEnvironmentVariables($"%HOME%/data/.acmebot/{_options.Endpoint.Host}/{path}");

    private string ResolveTempStateFullPath(string path) => Environment.ExpandEnvironmentVariables($"%TEMP%/.acmebot/{_options.Endpoint.Host}/{path}");
}

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using ACMESharp.Protocol;

namespace KeyVault.Acmebot.Internal
{
    internal static class AcmeProtocolClientExtensions
    {
        public static async Task<X509Certificate2Collection> GetOrderCertificateAsync(this AcmeProtocolClient acmeProtocolClient, OrderDetails order,
                                                                                      string preferredChain, CancellationToken cancel = default)
        {
            IEnumerable<string> linkHeaders;
            X509Certificate2Collection defaultX509Certificates;

            using (var resp = await acmeProtocolClient.GetAsync(order.Payload.Certificate, cancel))
            {
                defaultX509Certificates = await resp.Content.ReadAsCertificatesAsync();

                // 証明書チェーンが未指定の場合は即返す
                if (string.IsNullOrEmpty(preferredChain))
                {
                    return defaultX509Certificates;
                }

                linkHeaders = resp.Headers.GetValues("Link");
            }

            foreach (var linkHeader in linkHeaders)
            {
                // Link ヘッダーから alternate で指定されている URL を拾ってくる
                var rel = Regex.Match(linkHeader, "(?<=rel=\").+?(?=\")", RegexOptions.IgnoreCase);

                if (!string.Equals(rel.Value, "alternate", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var url = Regex.Match(linkHeader, "(?<=<).+?(?=>)", RegexOptions.IgnoreCase);

                // 代替の証明書をダウンロードする
                using (var resp = await acmeProtocolClient.GetAsync(url.Value, cancel))
                {
                    var x509Certificates = await resp.Content.ReadAsCertificatesAsync();

                    // ルート CA の名前が指定された証明書チェーンに一致する場合は返す
                    if (x509Certificates[^1].GetNameInfo(X509NameType.DnsName, true) == preferredChain)
                    {
                        return x509Certificates;
                    }
                }
            }

            // マッチする証明書チェーンが存在しない場合はデフォルトを返す
            return defaultX509Certificates;
        }
    }
}

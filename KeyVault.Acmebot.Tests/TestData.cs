using System.Text;

namespace KeyVault.Acmebot.Tests;

internal class TestData
{
    public const string DomeneShopDomainsResponse_sample1 = "DomeneShopDomainsResponse_sample1";
    public const string DomeneShopDnsRecordsResponse_example1_com_sample1 = "DomeneShopDnsRecordsResponse_example1_com_sample1";

    public static string ReadResourceAsString(string resourceKey)
    {
        byte[] data = Properties.Resources.ResourceManager.GetObject(resourceKey) as byte[];
        return Encoding.UTF8.GetString(data);
    }
}

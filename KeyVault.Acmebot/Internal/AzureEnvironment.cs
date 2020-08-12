using System.Collections.Generic;

namespace KeyVault.Acmebot.Internal
{
    public interface IAzureEnvironment
    {
        string ActiveDirectory { get; set; }
        string ResourceManager { get; set; }
    }

    internal class AzureEnvironment : IAzureEnvironment
    {
        public string ActiveDirectory { get; set; }
        public string ResourceManager { get; set; }

        public static AzureEnvironment Get(string name)
        {
            return _environments[name];
        }

        private static readonly Dictionary<string, AzureEnvironment> _environments = new Dictionary<string, AzureEnvironment>
        {
            {
                "AzureCloud", new AzureEnvironment
                {
                    ActiveDirectory = "https://login.microsoftonline.com",
                    ResourceManager = "https://management.azure.com"
                }
            },
            {
                "AzureChinaCloud", new AzureEnvironment
                {
                    ActiveDirectory = "https://login.chinacloudapi.cn",
                    ResourceManager = "https://management.chinacloudapi.cn"
                }
            },
            {
                "AzureUSGovernment", new AzureEnvironment
                {
                    ActiveDirectory = "https://login.microsoftonline.us",
                    ResourceManager = "https://management.usgovcloudapi.net"
                }
            }
        };
    }
}

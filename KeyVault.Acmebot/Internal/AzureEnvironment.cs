using System;
using System.Collections.Generic;

namespace KeyVault.Acmebot.Internal;

public class AzureEnvironment
{
    public Uri ActiveDirectory { get; private init; }
    public Uri ResourceManager { get; private init; }

    public static AzureEnvironment Get(string name) => s_environments[name];

    private static readonly Dictionary<string, AzureEnvironment> s_environments = new()
    {
        {
            "AzureCloud",
            new AzureEnvironment
            {
                ActiveDirectory = new Uri("https://login.microsoftonline.com"),
                ResourceManager = new Uri("https://management.azure.com")
            }
        },
        {
            "AzureChinaCloud",
            new AzureEnvironment
            {
                ActiveDirectory = new Uri("https://login.chinacloudapi.cn"),
                ResourceManager = new Uri("https://management.chinacloudapi.cn")
            }
        },
        {
            "AzureUSGovernment",
            new AzureEnvironment
            {
                ActiveDirectory = new Uri("https://login.microsoftonline.us"),
                ResourceManager = new Uri("https://management.usgovcloudapi.net")
            }
        }
    };
}

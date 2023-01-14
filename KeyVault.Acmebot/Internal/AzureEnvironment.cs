using System;
using System.Collections.Generic;

using Azure.Identity;
using Azure.ResourceManager;

namespace KeyVault.Acmebot.Internal;

public class AzureEnvironment
{
    public Uri AuthorityHost { get; private init; }
    public ArmEnvironment ResourceManager { get; private init; }

    public static AzureEnvironment Get(string name) => s_environments[name];

    private static readonly Dictionary<string, AzureEnvironment> s_environments = new()
    {
        {
            "AzureCloud",
            new AzureEnvironment
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                ResourceManager = ArmEnvironment.AzurePublicCloud
            }
        },
        {
            "AzureChinaCloud",
            new AzureEnvironment
            {
                AuthorityHost = AzureAuthorityHosts.AzureChina,
                ResourceManager = ArmEnvironment.AzureChina
            }
        },
        {
            "AzureUSGovernment",
            new AzureEnvironment
            {
                AuthorityHost = AzureAuthorityHosts.AzureGovernment,
                ResourceManager = ArmEnvironment.AzureGovernment
            }
        }
    };
}

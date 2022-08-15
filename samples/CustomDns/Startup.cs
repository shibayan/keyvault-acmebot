using Azure.Identity;
using Azure.ResourceManager;

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(CustomDns.Startup))]

namespace CustomDns;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var context = builder.GetContext();

        builder.Services.AddSingleton(_ => new ArmClient(new DefaultAzureCredential(), context.Configuration["SubscriptionId"]));
    }
}

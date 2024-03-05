using System.Reflection;

namespace KeyVault.Acmebot.Internal;

internal static class Constants
{
    public static string ApplicationVersion { get; } = typeof(Startup).Assembly
                                                                      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                                                      ?.InformationalVersion;
}

using System;
using System.Reflection;

namespace KeyVault.Acmebot.Internal;

internal static class Constants
{
    public static string FunctionAppName { get; } = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "Unknown";

    public static string ApplicationVersion { get; } = typeof(Startup).Assembly
                                                                      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                                                      ?.InformationalVersion;
}

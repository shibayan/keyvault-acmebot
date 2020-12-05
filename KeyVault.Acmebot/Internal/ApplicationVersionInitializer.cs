using System.Reflection;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace KeyVault.Acmebot.Internal
{
    internal class ApplicationVersionInitializer<TStartup> : ITelemetryInitializer
    {
        public ApplicationVersionInitializer()
        {
            ApplicationVersion = typeof(TStartup).Assembly
                                                 .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                                 ?.InformationalVersion;
        }

        public string ApplicationVersion { get; set; }

        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Component.Version = ApplicationVersion;
        }
    }
}

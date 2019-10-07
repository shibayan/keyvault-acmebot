using System;

using Microsoft.Extensions.Configuration;

namespace KeyVault.Acmebot.Internal
{
    [Obsolete]
    internal class Settings
    {
        public Settings()
        {
            var configuration = new ConfigurationBuilder()
                                .AddJsonFile("local.settings.json", true)
                                .AddEnvironmentVariables()
                                .Build();

            _section = configuration.GetSection("LetsEncrypt");
        }

        private readonly IConfiguration _section;

        public string Webhook => _section[nameof(Webhook)];

        public static Settings Default { get; } = new Settings();
    }
}

using Microsoft.Extensions.Configuration;
using System;

namespace StarAIAgentUI.Configs
{
    public static class AppConfig
    {
        public static IConfigurationRoot Build()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("Configs/appsettings.json", optional: false)
                .Build();
        }
    }
}

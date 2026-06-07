using AgenticBrowserAI.Agents;
using AgenticBrowserAI.Helpers;
using AgenticBrowserAI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using StarAIAgentUI.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace StarAIAgentUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            var config = AppConfig.Build();

            var services = new ServiceCollection();

            var openAiConfig = config
                .GetSection("AzureOpenAI")
                .Get<AzureOpenAiConfig>();

            services.AddSingleton(openAiConfig!);
            services.AddScoped<IPlaywrightRepository, PlaywrightRepository>();
            services.AddHttpClient<IPlannerAgent, PlannerAgent>();
            services.AddTransient<IAnalyzerAgent, AnalyzerAgent>();
            services.AddTransient<IExecutorAgent, ExecutorAgent>();
            services.AddTransient<IAgentOrchestrator, AgentOrchestrator>();
            //services.AddScoped<IPlaywrightAutomation, PlaywrightAutomation>();

            Services = services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var orchestrator = Services.GetService<IAgentOrchestrator>();

            _window = new MainWindow(orchestrator);
            _window.Activate();
        }
    }
}

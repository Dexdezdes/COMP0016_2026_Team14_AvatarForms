using AvatarFormsApp.Activation;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Services;
using AvatarFormsApp.Helpers;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace AvatarFormsApp;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    #if WINDOWS
        private static WindowEx? _mainWindow;
        public static WindowEx MainWindow => _mainWindow ??= GetService<MainWindow>() as WindowEx;
    #else
        private static Window? _mainWindow;
        public static Window MainWindow 
        { 
            get 
            {
                if (_mainWindow == null)
                {
                    _mainWindow = GetService<MainWindow>();
                }
                return _mainWindow;
            }
        }
    #endif



    public App()
    {
        InitializeLogging();
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers

            // Services
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<DashboardPageViewModel>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ConversationPage>();
            services.AddTransient<CreateQuestionnairePage>();
            services.AddSingleton<MainWindow>();
            services.AddTransient<ShellPageViewModel>();
            services.AddTransient<ConversationPageViewModel>();
            services.AddTransient<CreateQuestionnairePage>();
            services.AddTransient<CreateQuestionnairePageViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        UnhandledException += App_UnhandledException;
    }

    private static void InitializeLogging()
    {
        var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug); // Capture everything
            
            // Force Uno-specific categories to show up
            builder.AddFilter("Uno", LogLevel.Debug);
            builder.AddFilter("Microsoft", LogLevel.Information);
        });

        // Link Uno's internal logging to this factory
        Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        var window = App.MainWindow;
        window.SetIcon("Assets/Icons/favicon.ico");

        var shell = App.GetService<ShellPage>();
        window.Content = shell;

        window.Activate(); 
    }
}

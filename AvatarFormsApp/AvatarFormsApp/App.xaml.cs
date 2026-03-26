using AvatarFormsApp.Activation;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Views;
using AvatarFormsApp.Data;
using Microsoft.Extensions.Hosting;
using AvatarFormsApp.Data; // ADD THIS

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;

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

    // Single shared CoreWebView2Environment for the entire process.
    // All WebView2 instances MUST use the same environment or initialization fails.
    private static CoreWebView2Environment? _sharedWebViewEnvironment;
    private static readonly SemaphoreSlim _webViewEnvLock = new(1, 1);

    public static async Task<CoreWebView2Environment> GetOrCreateWebViewEnvironmentAsync()
    {
        if (_sharedWebViewEnvironment is not null)
            return _sharedWebViewEnvironment;

        await _webViewEnvLock.WaitAsync();
        try
        {
            if (_sharedWebViewEnvironment is null)
            {
                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--ignore-gpu-blocklist --enable-gpu-rasterization"
                };
                _sharedWebViewEnvironment = await CoreWebView2Environment.CreateWithOptionsAsync(null, null, options);
            }
        }
        finally
        {
            _webViewEnvLock.Release();
        }

        return _sharedWebViewEnvironment!;
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

            // Register DbContext with SQLite
            services.AddDbContext<AppDbContext>(options =>
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dbPath = Path.Combine(appDataPath, "AvatarFormsApp", "questionnaires.db");

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                options.UseSqlite($"Data Source={dbPath}");
            });

            // Register Questionnaire Service
            services.AddScoped<IQuestionnaireService, QuestionnaireService>();

            // Register Questionnaire API Service
            services.AddSingleton<IQuestionnaireAPIService, QuestionnaireAPIService>();

            // Register Response API Service (HTTP server for receiving responses)
            services.AddSingleton<IResponseAPIService, ResponseAPIService>();

            // Register Process Services
            services.AddSingleton<ILlamafileProcessService, LlamafileProcessService>();
            services.AddSingleton<IPythonProcessService, PythonProcessService>();
            // *** END DATABASE SERVICES ***

            services.AddSingleton<FormLinkParserService>();

            // Views and ViewModels
            services.AddTransient<DashboardPageViewModel>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<QuestionnaireDetailPageViewModel>();
            services.AddTransient<QuestionnaireDetailPage>();
            services.AddTransient<ResponsesPageViewModel>();
            services.AddTransient<ResponsesPage>();
            services.AddTransient<ResponseDetailPageViewModel>();
            services.AddTransient<ResponseDetailPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<CreateQuestionnairePage>();  
            services.AddSingleton<MainWindow>();
            services.AddTransient<ShellPageViewModel>();
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

        // *** INITIALIZE DATABASE ***
        using (var scope = Host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Delete database before each run
            // await dbContext.Database.EnsureDeletedAsync();

            // Create database with schema if it doesn't exist
            await dbContext.Database.EnsureCreatedAsync();
        }

        var window = App.MainWindow;

        var shell = App.GetService<ShellPage>();
        window.Content = shell;

        window.Activate();
    }
}

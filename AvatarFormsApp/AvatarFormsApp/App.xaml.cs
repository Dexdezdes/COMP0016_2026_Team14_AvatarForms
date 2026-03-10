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
            services.AddTransient<ConversationPage>();
            services.AddTransient<CreateQuestionnairePage>();  
            services.AddSingleton<MainWindow>();
            services.AddTransient<ShellPageViewModel>();
            services.AddTransient<ConversationPageViewModel>();
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

            // Seed with sample data only if database is empty
            var existingCount = await dbContext.Questionnaires.CountAsync();
            if (existingCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("Database is empty - seeding sample data...");
                await SeedSampleDataAsync(dbContext);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Database already contains {existingCount} questionnaire(s) at {dbContext.Database.GetConnectionString}");
            }
        }

        var window = App.MainWindow;

        var shell = App.GetService<ShellPage>();
        window.Content = shell;

        window.Activate();
    }

    private async Task SeedSampleDataAsync(AppDbContext dbContext)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== STARTING SEED DATA ===");

            // Sample 1: Sleep Survey
            var sleepId = Guid.NewGuid().ToString();
            var sleep = new Questionnaire
            {
                Id = sleepId,
                Name = "Sleep Quality Survey",
                OwnerId = "user1",
                Status = "Pending",
                Color = "#4CB3B3",
                Description = "This questionnaire is designed to get complete information about the user in a friendly manner and get to know if they've been sleeping well.",
                CreatedDate = DateTime.UtcNow
            };

            var sleepQ1Id = Guid.NewGuid().ToString();
            sleep.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = sleepId,
                Text = "What is your full name?",
                Type = QuestionType.OpenEnded,
                Order = 1,
                IsRequired = false
            });

            var sleepQ2Id = Guid.NewGuid().ToString();
            sleep.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = sleepId,
                Text = "How did you sleep last night?",
                Type = QuestionType.OpenEnded,
                Order = 2,
                IsRequired = false
            });

            var sleepQ3Id = Guid.NewGuid().ToString();
            sleep.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = sleepId,
                Text = "How many hours of sleep did you get?",
                Type = QuestionType.OpenEnded,
                Order = 3,
                IsRequired = false
            });

            var sleepQ4Id = Guid.NewGuid().ToString();
            sleep.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = sleepId,
                Text = "Have you had any problem sleeping the past few weeks?",
                Type = QuestionType.OpenEnded,
                Order = 4,
                IsRequired = false
            });

            // Sample 2: Education Feedback
            var educationId = Guid.NewGuid().ToString();
            var education = new Questionnaire 
            { 
                Id = educationId,
                Name = "Student Satisfaction Survey", 
                OwnerId = "user2", 
                Status = "Active", 
                Color = "#B34CB3",
                Description = "This questionnaire is designed to get feedback about the educational content and delivery.",
                CreatedDate = DateTime.UtcNow
            };

            var educationQ1Id = Guid.NewGuid().ToString();
            education.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = educationId,
                Text = "What is your full name?",
                Type = QuestionType.OpenEnded,
                Order = 1,
                IsRequired = false
            });

            var educationQ2Id = Guid.NewGuid().ToString();
            education.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = educationId,
                Text = "What do you think about the quality of the educational content?",
                Type = QuestionType.OpenEnded,
                Order = 2,
                IsRequired = false
            });

            var educationQ3Id = Guid.NewGuid().ToString();
            education.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = educationId,
                Text = "Are the facilities well-maintained?",
                Type = QuestionType.OpenEnded,
                Order = 3,
                IsRequired = false
            });

            var educationQ4Id = Guid.NewGuid().ToString();
            education.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = educationId,
                Text = "Is your academic advisor useful?",
                Type = QuestionType.OpenEnded,
                Order = 4,
                IsRequired = false
            });

            var educationQ5Id = Guid.NewGuid().ToString();
            education.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = educationId,
                Text = "How safe do you feel on campus?",
                Type = QuestionType.OpenEnded,
                Order = 5,
                IsRequired = false
            });

            var educationQ6Id = Guid.NewGuid().ToString();
            education.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = educationId,
                Text = "How would you rate the overall experience at our institution on a scale of 1 to 10?",
                Type = QuestionType.OpenEnded,
                Order = 6,
                IsRequired = false
            });

            // Example 3 : Teaching Assistant Evaluation
            var teachingID = Guid.NewGuid().ToString();
            var teaching = new Questionnaire
            {
                Id = teachingID,
                Name = "Teaching Assistant Evaluation Survey",
                OwnerId = "user3",
                Status = "Active",
                Color = "#F06292",
                Description = "This questionnaire is designed to get feedback about the teaching quality and effectiveness.",
                CreatedDate = DateTime.UtcNow
            };

            var teachingQ1Id = Guid.NewGuid().ToString();
            teaching.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = teachingID,
                Text = "What is your full name?",
                Type = QuestionType.OpenEnded,
                Order = 1,
                IsRequired = false
            });

            var teachingQ2Id = Guid.NewGuid().ToString();
            teaching.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = teachingID,
                Text = "How prepared was your teaching assistant in class?",
                Type = QuestionType.OpenEnded,
                Order = 2,
                IsRequired = false
            });

            var teachingQ3Id = Guid.NewGuid().ToString();
            teaching.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = teachingID,
                Text = "How effective was your teaching assistant in explaining the course material?",
                Type = QuestionType.OpenEnded,
                Order = 3,
                IsRequired = false
            });

            var teachingQ4Id = Guid.NewGuid().ToString();
            teaching.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = teachingID,
                Text = "How approachable was your teaching assistant for questions and help?",
                Type = QuestionType.OpenEnded,
                Order = 4,
                IsRequired = false
            });

            var teachingQ5Id = Guid.NewGuid().ToString();
            teaching.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = teachingID,
                Text = "How would you rate your overall experience with your teaching assistant on a scale of 1 to 10?",
                Type = QuestionType.OpenEnded,
                Order = 5,
                IsRequired = false
            });

            // Example 4 : K-12 Parent Survey
            var parentId = Guid.NewGuid().ToString();
            var parent = new Questionnaire
            {
                Id = parentId,
                Name = "K-12 Parent Survey",
                OwnerId = "user4",
                Status = "Active",
                Color = "#81C784",
                Description = "This questionnaire is designed to discover parents' thoughts and attitudes about their child's school.",
                CreatedDate = DateTime.UtcNow
            };

            var parentQ1Id = Guid.NewGuid().ToString();
            parent.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = parentId,
                Text = "What is your full name?",
                Type = QuestionType.OpenEnded,
                Order = 1,
                IsRequired = false
            });

            var parentQ2Id = Guid.NewGuid().ToString();
            parent.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = parentId,
                Text = "How often do you meet in person with teachers at your child's school?",
                Type = QuestionType.OpenEnded,
                Order = 2,
                IsRequired = false
            });

            var parentQ3Id = Guid.NewGuid().ToString();
            parent.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = parentId,
                Text = "How confident are you that you can help your child develop good friendships?",
                Type = QuestionType.OpenEnded,
                Order = 3,
                IsRequired = false
            });

            var parentQ4Id = Guid.NewGuid().ToString();
            parent.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = parentId,
                Text = "How much effort do you put into helping your child learn to do things for themselves?",
                Type = QuestionType.OpenEnded,
                Order = 4,
                IsRequired = false
            });

            var parentQ5Id = Guid.NewGuid().ToString();
            parent.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = parentId,
                Text = "In the past year, how often have you discussed your child's school with other parents from the school?",
                Type = QuestionType.OpenEnded,
                Order = 5,
                IsRequired = false
            });


            System.Diagnostics.Debug.WriteLine("Adding questionnaires to context...");
            dbContext.Questionnaires.AddRange(sleep, education, teaching, parent);

            System.Diagnostics.Debug.WriteLine("Saving changes to database...");
            await dbContext.SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine("✅ Sample data seeded successfully!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ ERROR seeding sample data: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }
}

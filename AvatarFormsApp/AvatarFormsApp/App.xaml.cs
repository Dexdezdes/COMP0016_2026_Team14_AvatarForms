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

            // *** DATABASE SERVICES - ADD THESE ***
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
            // *** END DATABASE SERVICES ***

            // Views and ViewModels
            services.AddTransient<DashboardPageViewModel>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<QuestionnaireDetailPageViewModel>();
            services.AddTransient<QuestionnaireDetailPage>();
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
        
        // *** INITIALIZE DATABASE - ADD THIS ***
        // Ensure database is created on first launch
        using (var scope = Host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            
            // Seed sample data if database is empty
            await SeedSampleDataAsync(dbContext);
        }
        // *** END DATABASE INITIALIZATION ***
        
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
            
            // Check if we already have data
            var existingCount = await dbContext.Questionnaires.CountAsync();
            System.Diagnostics.Debug.WriteLine($"Existing questionnaires count: {existingCount}");
            
            if (existingCount > 0)
            {
                System.Diagnostics.Debug.WriteLine("Data already exists - skipping seed");
                return;
            }

            System.Diagnostics.Debug.WriteLine("No data found - starting seeding...");

            // Sample 1: DASS Questionnaire
            var dassId = Guid.NewGuid().ToString();
            var dass = new Questionnaire 
            { 
                Id = dassId,
                Name = "DASS Questionnaire", 
                OwnerId = "user1", 
                Status = "Pending", 
                Color = "#4CB3B3",
                Description = "Depression Anxiety Stress Scales",
                CreatedDate = DateTime.UtcNow
            };
            
            var dassQ1Id = Guid.NewGuid().ToString();
            dass.Questions = new List<Question>
            {
                new Question
                {
                    Id = dassQ1Id,
                    QuestionnaireId = dassId,
                    Text = "Over the past week, how often did you feel down or hopeless?",
                    Type = QuestionType.MCQ,
                    Order = 1,
                    IsRequired = true,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ1Id, Text = "Never", Order = 1 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ1Id, Text = "Sometimes", Order = 2 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ1Id, Text = "Often", Order = 3 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ1Id, Text = "Always", Order = 4 }
                    }
                }
            };

            var dassQ2Id = Guid.NewGuid().ToString();
            dass.Questions.Add(new Question
            {
                Id = dassQ2Id,
                QuestionnaireId = dassId,
                Text = "I found it hard to wind down",
                Type = QuestionType.MCQ,
                Order = 2,
                IsRequired = true,
                Options = new List<QuestionOption>
                {
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ2Id, Text = "Did not apply to me at all", Order = 1 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ2Id, Text = "Applied to me to some degree", Order = 2 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ2Id, Text = "Applied to me a considerable degree", Order = 3 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = dassQ2Id, Text = "Applied to me very much", Order = 4 }
                }
            });

            dass.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = dassId,
                Text = "Please share any additional thoughts or concerns",
                Type = QuestionType.OpenEnded,
                Order = 3,
                IsRequired = false
            });

            // Sample 2: Customer Satisfaction
            var satisfactionId = Guid.NewGuid().ToString();
            var satisfaction = new Questionnaire 
            { 
                Id = satisfactionId,
                Name = "Customer Satisfaction Survey", 
                OwnerId = "user1", 
                Status = "Pending", 
                Color = "#FF6B6B",
                Description = "Help us improve our services",
                CreatedDate = DateTime.UtcNow
            };

            var satQ1Id = Guid.NewGuid().ToString();
            satisfaction.Questions = new List<Question>
            {
                new Question
                {
                    Id = satQ1Id,
                    QuestionnaireId = satisfactionId,
                    Text = "How satisfied are you with our service?",
                    Type = QuestionType.MCQ,
                    Order = 1,
                    IsRequired = true,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ1Id, Text = "Very Dissatisfied", Order = 1 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ1Id, Text = "Dissatisfied", Order = 2 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ1Id, Text = "Neutral", Order = 3 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ1Id, Text = "Satisfied", Order = 4 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ1Id, Text = "Very Satisfied", Order = 5 }
                    }
                }
            };

            satisfaction.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = satisfactionId,
                Text = "What can we improve?",
                Type = QuestionType.OpenEnded,
                Order = 2,
                IsRequired = false
            });

            var satQ3Id = Guid.NewGuid().ToString();
            satisfaction.Questions.Add(new Question
            {
                Id = satQ3Id,
                QuestionnaireId = satisfactionId,
                Text = "Would you recommend us to others?",
                Type = QuestionType.MCQ,
                Order = 3,
                IsRequired = true,
                Options = new List<QuestionOption>
                {
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ3Id, Text = "Yes", Order = 1 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ3Id, Text = "No", Order = 2 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = satQ3Id, Text = "Maybe", Order = 3 }
                }
            });

            // Sample 3: Employee Feedback
            var feedbackId = Guid.NewGuid().ToString();
            var feedback = new Questionnaire 
            { 
                Id = feedbackId,
                Name = "Employee Feedback Form", 
                OwnerId = "user1", 
                Status = "Closed", 
                Color = "#95E1D3",
                Description = "Annual employee satisfaction survey",
                CreatedDate = DateTime.UtcNow.AddMonths(-1)
            };

            var feedQ1Id = Guid.NewGuid().ToString();
            feedback.Questions = new List<Question>
            {
                new Question
                {
                    Id = feedQ1Id,
                    QuestionnaireId = feedbackId,
                    Text = "Do you feel valued at work?",
                    Type = QuestionType.MCQ,
                    Order = 1,
                    IsRequired = true,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = feedQ1Id, Text = "Yes", Order = 1 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = feedQ1Id, Text = "No", Order = 2 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = feedQ1Id, Text = "Sometimes", Order = 3 }
                    }
                }
            };

            feedback.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = feedbackId,
                Text = "What suggestions do you have for improvement?",
                Type = QuestionType.OpenEnded,
                Order = 2,
                IsRequired = false
            });

            // Sample 4: Product Review
            var reviewId = Guid.NewGuid().ToString();
            var review = new Questionnaire 
            { 
                Id = reviewId,
                Name = "Product Review", 
                OwnerId = "user1", 
                Status = "Pending", 
                Color = "#F38181",
                Description = "Tell us what you think about our product",
                CreatedDate = DateTime.UtcNow
            };

            var revQ1Id = Guid.NewGuid().ToString();
            review.Questions = new List<Question>
            {
                new Question
                {
                    Id = revQ1Id,
                    QuestionnaireId = reviewId,
                    Text = "How would you rate the product quality?",
                    Type = QuestionType.MCQ,
                    Order = 1,
                    IsRequired = true,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ1Id, Text = "Poor", Order = 1 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ1Id, Text = "Fair", Order = 2 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ1Id, Text = "Good", Order = 3 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ1Id, Text = "Very Good", Order = 4 },
                        new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ1Id, Text = "Excellent", Order = 5 }
                    }
                }
            };

            var revQ2Id = Guid.NewGuid().ToString();
            review.Questions.Add(new Question
            {
                Id = revQ2Id,
                QuestionnaireId = reviewId,
                Text = "Would you recommend this product?",
                Type = QuestionType.MCQ,
                Order = 2,
                IsRequired = true,
                Options = new List<QuestionOption>
                {
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ2Id, Text = "Definitely", Order = 1 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ2Id, Text = "Probably", Order = 2 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ2Id, Text = "Not sure", Order = 3 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ2Id, Text = "Probably not", Order = 4 },
                    new QuestionOption { Id = Guid.NewGuid().ToString(), QuestionId = revQ2Id, Text = "Definitely not", Order = 5 }
                }
            });

            review.Questions.Add(new Question
            {
                Id = Guid.NewGuid().ToString(),
                QuestionnaireId = reviewId,
                Text = "Any additional comments?",
                Type = QuestionType.OpenEnded,
                Order = 3,
                IsRequired = false
            });

            System.Diagnostics.Debug.WriteLine("Adding questionnaires to context...");
            dbContext.Questionnaires.AddRange(dass, satisfaction, feedback, review);
            
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

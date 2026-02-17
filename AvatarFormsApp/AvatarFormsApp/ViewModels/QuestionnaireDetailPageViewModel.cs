using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class QuestionnaireDetailPageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;
    private readonly INavigationService _navigationService;
    private readonly ILlamafileProcessService _llamafileProcessService;
    private readonly IPythonProcessService _pythonProcessService;
    private readonly IQuestionnaireAPIService _questionnaireAPIService;

    [ObservableProperty]
    private Questionnaire? questionnaire;

    [ObservableProperty]
    private ObservableCollection<Question> questions = new();

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string pageTitle = "Questionnaire";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isStartingBackend = false;

    public QuestionnaireDetailPageViewModel(
        IQuestionnaireService questionnaireService,
        INavigationService navigationService,
        ILlamafileProcessService llamafileProcessService,
        IPythonProcessService pythonProcessService,
        IQuestionnaireAPIService questionnaireAPIService)
    {
        _questionnaireService = questionnaireService;
        _navigationService = navigationService;
        _llamafileProcessService = llamafileProcessService;
        _pythonProcessService = pythonProcessService;
        _questionnaireAPIService = questionnaireAPIService;

        _pythonProcessService.OutputReceived += (output) =>
        {
            System.Diagnostics.Debug.WriteLine($"[PYTHON] {output}");
        };
        _pythonProcessService.ErrorReceived += (error) =>
        {
            System.Diagnostics.Debug.WriteLine($"[PYTHON ERROR] {error}");
        };
    }

    public async Task LoadQuestionnaireAsync(string questionnaireId)
    {
        try
        {
            IsLoading = true;

            Questionnaire = await _questionnaireService.GetByIdAsync(questionnaireId);
            
            if (Questionnaire != null)
            {
                PageTitle = Questionnaire.Name;
                Questions = new ObservableCollection<Question>(
                    Questionnaire.Questions.OrderBy(q => q.Order)
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading questionnaire: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToAvatarAsync()
    {
        try
        {
            IsStartingBackend = true;
            StatusMessage = "Starting llamafile server...";

            // 1. Start llamafile server
            if (!_llamafileProcessService.IsRunning)
            {
                bool llamafileReady = await _llamafileProcessService.StartAsync();
                if (!llamafileReady)
                {
                    StatusMessage = "Failed to start llamafile server.";
                    return;
                }
            }

            // 2. Start Python backend process
            StatusMessage = "Starting avatar process...";
            if (!_pythonProcessService.IsRunning)
            {
                // Start with local mode enabled, ports: 8081 (llama), 8883 (websocket), 8882 (http)
                bool pythonReady = await _pythonProcessService.StartAsync(
                    useLocal: true, 
                    llamaPort: 8081, 
                    websocketPort: 8883,
                    httpPort: 8882);

                if (!pythonReady)
                {
                    StatusMessage = "Failed to start avatar.";
                    return;
                }

                // Delay to allow HTTP server to initialize
                StatusMessage = "Waiting for avatar to initialize...";
                await Task.Delay(2000);
            }

            // 3. Send questionnaire data to Python backend
            StatusMessage = "Uploading questionnaire...";
            if (Questionnaire != null)
            {
                bool sent = await _questionnaireAPIService.SendQuestionnaireAsync(
                    Questionnaire.Id, 
                    port: 8882);

                if (!sent)
                {
                    StatusMessage = "Failed to upload questionnaire data to avatar.";
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Questionnaire '{Questionnaire.Name}' uploaded successfully");
            }
            else
            {
                StatusMessage = "No questionnaire uploaded.";
                return;
            }

            // 4. Navigate to avatar page
            StatusMessage = string.Empty;
            _navigationService.NavigateTo(typeof(AvatarPageViewModel).Name);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"NavigateToAvatar error: {ex}");
        }
        finally
        {
            IsStartingBackend = false;
        }
    }
}

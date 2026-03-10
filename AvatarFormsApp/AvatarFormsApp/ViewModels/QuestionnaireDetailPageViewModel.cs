using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Contracts.ViewModels;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class QuestionnaireDetailPageViewModel : ObservableRecipient, INavigationAware
{
    private readonly IQuestionnaireService _questionnaireService;
    private readonly INavigationService _navigationService;
    private readonly ILlamafileProcessService _llamafileProcessService;
    private readonly IPythonProcessService _pythonProcessService;
    private readonly IQuestionnaireAPIService _questionnaireAPIService;
    private readonly IResponseAPIService _responseAPIService;

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
    [NotifyCanExecuteChangedFor(nameof(NavigateToAvatarCommand))]
    private bool isStartingBackend = false;

    public QuestionnaireDetailPageViewModel(
        IQuestionnaireService questionnaireService,
        INavigationService navigationService,
        ILlamafileProcessService llamafileProcessService,
        IPythonProcessService pythonProcessService,
        IQuestionnaireAPIService questionnaireAPIService,
        IResponseAPIService responseAPIService)
    {
        _questionnaireService = questionnaireService;
        _navigationService = navigationService;
        _llamafileProcessService = llamafileProcessService;
        _pythonProcessService = pythonProcessService;
        _questionnaireAPIService = questionnaireAPIService;
        _responseAPIService = responseAPIService;

        _responseAPIService.AllResponsesReceived += OnAllResponsesReceived;
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

    private bool CanNavigateToAvatar() => !IsStartingBackend;

    [RelayCommand(CanExecute = nameof(CanNavigateToAvatar))]
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

            // 2. Start response API Server
            StatusMessage = "Starting response API server...";
            if (!_responseAPIService.IsRunning)
            {
                await _responseAPIService.StartServerAsync(port: 5000);

                // Set expected question count
                if (Questionnaire != null)
                {
                    _responseAPIService.SetExpectedQuestionCount(Questionnaire.Questions.Count);
                }
            }

            // 3. Start Python backend process
            StatusMessage = "Starting avatar process...";
            if (!_pythonProcessService.IsRunning)
            {
                // Start with local mode enabled, ports: 8081 (llama), 8883 (websocket), 8882 (http), 5000 (response)
                bool pythonReady = await _pythonProcessService.StartAsync(
                    useLocal: true, 
                    llamaPort: 8081, 
                    websocketPort: 8883,
                    httpPort: 8882,
                    responsePort: 5000);

                if (!pythonReady)
                {
                    StatusMessage = "Failed to start avatar.";
                    return;
                }

                // Delay to allow HTTP server to initialize
                StatusMessage = "Waiting for avatar to initialize...";
                await Task.Delay(2000);
            }

            // 4. Send questionnaire data to Python backend
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

            }
            else
            {
                StatusMessage = "No questionnaire uploaded.";
                return;
            }

            // 5. Navigate to avatar page
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

    private async void OnAllResponsesReceived()
    {
        _responseAPIService.AllResponsesReceived -= OnAllResponsesReceived;

        System.Diagnostics.Debug.WriteLine("All responses received! Stopping Response API server...");

        try
        {
            await Task.Delay(500);
            await _responseAPIService.StopServerAsync();
            System.Diagnostics.Debug.WriteLine("Response API server stopped successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping Response API server: {ex.Message}");
        }
    }

    public void OnNavigatedTo(object parameter) { }

    public void OnNavigatedFrom()
    {
        _responseAPIService.AllResponsesReceived -= OnAllResponsesReceived;
    }
}

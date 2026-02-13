using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.DTOs;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class QuestionnaireDetailPageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;
    private readonly INavigationService _navigationService;
    private readonly ILlamafileProcessService _llamafileProcessService;
    private readonly IPythonProcessService _pythonProcessService;
    private readonly IPythonBackendService _pythonBackendService;

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
        IPythonBackendService pythonBackendService)
    {
        _questionnaireService = questionnaireService;
        _navigationService = navigationService;
        _llamafileProcessService = llamafileProcessService;
        _pythonProcessService = pythonProcessService;
        _pythonBackendService = pythonBackendService;
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
        if (Questionnaire == null)
        {
            StatusMessage = "No questionnaire loaded.";
            return;
        }

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
            StatusMessage = "Starting Python backend...";
            if (!_pythonProcessService.IsRunning)
            {
                bool pythonReady = await _pythonProcessService.StartAsync();
                if (!pythonReady)
                {
                    StatusMessage = "Failed to start Python backend.";
                    return;
                }
            }

            // 3. Send questionnaire data to the Python HTTP API
            StatusMessage = "Sending questionnaire data...";
            var dto = QuestionnaireTransferDto.FromQuestionnaire(Questionnaire);
            var response = await _pythonBackendService.SendQuestionnaireAsync(dto);

            if (!response.IsSuccess)
            {
                StatusMessage = response.Message ?? "Failed to send questionnaire to backend.";
                System.Diagnostics.Debug.WriteLine($"Backend error: {response.ErrorDetails}");
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

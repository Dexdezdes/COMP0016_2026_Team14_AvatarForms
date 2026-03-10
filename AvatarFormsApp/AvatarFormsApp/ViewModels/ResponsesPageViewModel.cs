using CommunityToolkit.Mvvm.ComponentModel;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class ResponsesPageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;

    [ObservableProperty]
    private string questionnaireName = string.Empty;

    [ObservableProperty]
    private string questionnaireColor = "#4CB3B3";

    [ObservableProperty]
    private ObservableCollection<ResponseSession> responseSessions = new();

    [ObservableProperty]
    private bool hasResponseSessions = false;

    [ObservableProperty]
    private bool isLoading = false;

    private string? _questionnaireId;

    public ResponsesPageViewModel(IQuestionnaireService questionnaireService)
    {
        _questionnaireService = questionnaireService;
    }

    public async Task LoadResponseSessionsAsync(string questionnaireId)
    {
        _questionnaireId = questionnaireId;

        try
        {
            IsLoading = true;

            var questionnaire = await _questionnaireService.GetByIdAsync(questionnaireId);
            if (questionnaire != null)
            {
                QuestionnaireName = questionnaire.Name;
                QuestionnaireColor = questionnaire.Color;
            }

            var sessions = await _questionnaireService.GetResponseSessionsAsync(questionnaireId);
            ResponseSessions = new ObservableCollection<ResponseSession>(sessions);
            HasResponseSessions = sessions.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading response sessions: {ex.Message}");
            ResponseSessions = new ObservableCollection<ResponseSession>();
            HasResponseSessions = false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

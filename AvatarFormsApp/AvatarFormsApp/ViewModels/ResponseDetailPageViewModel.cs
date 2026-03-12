using CommunityToolkit.Mvvm.ComponentModel;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class ResponseDetailPageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;

    [ObservableProperty]
    private string pageTitle = "Response Session";

    [ObservableProperty]
    private string questionnaireName = string.Empty;

    [ObservableProperty]
    private string questionnaireColor = "#4CB3B3";

    [ObservableProperty]
    private string submittedDate = string.Empty;

    [ObservableProperty]
    private bool isComplete = false;

    [ObservableProperty]
    private int responseCount = 0;

    [ObservableProperty]
    private ObservableCollection<ResponseDetailItem> responseItems = new();

    [ObservableProperty]
    private bool isLoading = false;

    private string? _questionnaireId;

    public ResponseDetailPageViewModel(IQuestionnaireService questionnaireService)
    {
        _questionnaireService = questionnaireService;
    }

    public async Task LoadSessionAsync(string sessionId)
    {
        try
        {
            IsLoading = true;

            var session = await _questionnaireService.GetResponseSessionByIdAsync(sessionId);
            if (session == null) return;

            _questionnaireId = session.QuestionnaireId;
            QuestionnaireName = session.Questionnaire?.Name ?? string.Empty;
            QuestionnaireColor = session.Questionnaire?.Color ?? "#4CB3B3";
            SubmittedDate = session.SubmittedDate.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");
            IsComplete = session.IsComplete;
            ResponseCount = session.Responses.Count;
            PageTitle = $"Session — {session.Id[..8]}…";

            var items = session.Responses
                .Where(r => r.Question != null)
                .OrderBy(r => r.Question!.Order)
                .Select(r =>
                {
                    var isMcq = r.Question!.IsMCQ;
                    var answerText = r.AnswerText ?? "(no answer)";

                    return new ResponseDetailItem
                    {
                        Order = r.Question!.Order,
                        QuestionText = r.Question.Text,
                        AnswerText = answerText,
                        QuestionnaireColor = QuestionnaireColor,
                        IsMCQ = isMcq,
                        Options = isMcq
                            ? r.Question.Options
                                .OrderBy(o => o.Order)
                                .Select(o => new ResponseOptionItem
                                {
                                    Text = o.Text,
                                    IsSelected = string.Equals(o.Text, answerText, StringComparison.OrdinalIgnoreCase)
                                })
                                .ToList()
                            : []
                    };
                })
                .ToList();

            ResponseItems = new ObservableCollection<ResponseDetailItem>(items);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading response session: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public string? QuestionnaireId => _questionnaireId;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Messages;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;

namespace AvatarFormsApp.ViewModels;

public partial class CreateQuestionnairePageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;
    private readonly FormLinkParserService _formLinkParser;

    [ObservableProperty] private string _questionnaireName = string.Empty;
    [ObservableProperty] private string _questionnaireDescription = string.Empty;
    [ObservableProperty] private string _formLink = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasParsedQuestions;

    public ObservableCollection<ParsedQuestion> ParsedQuestions { get; } = new();

    public CreateQuestionnairePageViewModel(
        IQuestionnaireService questionnaireService,
        FormLinkParserService formLinkParser)
    {
        _questionnaireService = questionnaireService;
        _formLinkParser = formLinkParser;
    }

    // Called directly from page click handler (UI thread), passing the hidden WebView2
    public async Task ParseLinkAsync(WebView2 webView)
    {
        if (string.IsNullOrWhiteSpace(FormLink))
        {
            StatusMessage = "Please enter a form link first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Fetching form data...";
        HasParsedQuestions = false;
        ParsedQuestions.Clear();

        try
        {
            // WebView2 must run on UI thread - no Task.Run needed or wanted
            var questions = await _formLinkParser.ParseAsync(FormLink, webView);

            if (questions.Count == 0)
            {
                StatusMessage = "No questions found. Check the link and try again.";
                return;
            }

            foreach (var q in questions)
                ParsedQuestions.Add(q);

            HasParsedQuestions = true;
            StatusMessage = $"Loaded {questions.Count} question(s). Review and click create questionnaire to create session.";

            if (string.IsNullOrWhiteSpace(QuestionnaireName))
                QuestionnaireName = !string.IsNullOrEmpty(_formLinkParser.FormTitle) ? _formLinkParser.FormTitle : questions.FirstOrDefault()?.Section ?? "Imported Form";

            if (string.IsNullOrWhiteSpace(QuestionnaireDescription) && !string.IsNullOrEmpty(_formLinkParser.FormDescription))
                QuestionnaireDescription = _formLinkParser.FormDescription;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error parsing form: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CreateQuestionnaireAsync()
    {
        if (ParsedQuestions.Count == 0) { StatusMessage = "Nothing to save. Please parse a form link first."; return; }
        if (string.IsNullOrWhiteSpace(QuestionnaireName)) { StatusMessage = "Please enter a name for the questionnaire."; return; }
        if (string.IsNullOrWhiteSpace(QuestionnaireDescription)) { StatusMessage = "Please enter a description for the questionnaire."; return; }

        IsBusy = true;
        StatusMessage = "Saving questionnaire...";
        try
        {
            var questionnaire = MapToQuestionnaire();
            await _questionnaireService.AddAsync(questionnaire);
            Messenger.Send(new QuestionnaireCreatedMessage());
            StatusMessage = $"Questionnaire \"{questionnaire.Name}\" created successfully!";
            HasParsedQuestions = false;
        }
        catch (Exception ex) { StatusMessage = $"Error saving questionnaire: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public void Clear()
    {
        FormLink = string.Empty; QuestionnaireName = string.Empty; QuestionnaireDescription = string.Empty;
        StatusMessage = string.Empty; HasParsedQuestions = false;
        ParsedQuestions.Clear();
    }

    public void RemoveQuestion(ParsedQuestion question)
    {
        var idx = ParsedQuestions.IndexOf(question);
        if (idx < 0) return;

        ParsedQuestions.RemoveAt(idx);

        // Replace subsequent items so their OneTime x:Bind Index bindings re-evaluate
        for (int i = idx; i < ParsedQuestions.Count; i++)
            ParsedQuestions[i] = CloneWithIndex(ParsedQuestions[i], i + 1);

        HasParsedQuestions = ParsedQuestions.Count > 0;
    }

    public void RemoveOption(ParsedQuestion question, EditableOption option)
    {
        question.Options.Remove(option);
    }

    private static ParsedQuestion CloneWithIndex(ParsedQuestion q, int index) => new()
    {
        Index        = index,
        Id           = q.Id,
        Section      = q.Section,
        Title        = q.Title,
        FullTitle    = q.FullTitle,
        Type         = q.Type,
        Required     = q.Required,
        OrderValue   = q.OrderValue,
        Options      = q.Options,
        IsMatrix     = q.IsMatrix,
        MatrixGroupTitle = q.MatrixGroupTitle,
        Subtitle     = q.Subtitle,
    };

    private static readonly string[] _colorPalette =
    [
        "#4CB3B3", "#E57373", "#81C784", "#64B5F6", "#FFB74D",
        "#BA68C8", "#4DB6AC", "#F06292", "#AED581", "#4FC3F7",
        "#FF8A65", "#A1887F", "#90A4AE", "#7986CB", "#FFF176",
    ];

    private Questionnaire MapToQuestionnaire()
    {
        var questionnaireId = Guid.NewGuid().ToString();
        var questions = ParsedQuestions.Select(pq =>
        {
            var questionId = Guid.NewGuid().ToString();
            var question = new Question
            {
                Id = questionId,
                QuestionnaireId = questionnaireId,
                Text = pq.FullTitle ?? pq.Title,
                Type = MapQuestionType(pq.Type),
                Order = pq.Index,
                IsRequired = pq.Required,
            };
            question.Options = pq.Options.Select((opt, i) => new QuestionOption
            {
                Id = Guid.NewGuid().ToString(),
                QuestionId = questionId,
                Text = opt.Text,
                Order = i + 1,
            }).ToList();
            return question;
        }).ToList();

        return new Questionnaire
        {
            Id = questionnaireId,
            Name = QuestionnaireName,
            Description = QuestionnaireDescription,
            OwnerId = "user1",
            Status = "Pending",
            Color = _colorPalette[Random.Shared.Next(_colorPalette.Length)],
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            Questions = questions,
        };
    }

    private static QuestionType MapQuestionType(string type) => type switch
    {
        // Microsoft Forms types
        "Question.Choice" => QuestionType.MCQ,
        "Question.MultiChoice" => QuestionType.MCQ,
        "Question.MatrixChoice" => QuestionType.MCQ,
        "Question.Ranking" => QuestionType.MCQ,
        // Google Forms types
        "MultipleChoice" => QuestionType.MCQ,
        "Checkboxes" => QuestionType.MCQ,
        "Dropdown" => QuestionType.MCQ,
        "LinearScale" => QuestionType.MCQ,
        "Grid" => QuestionType.MCQ,
        _ => QuestionType.OpenEnded,
    };
}

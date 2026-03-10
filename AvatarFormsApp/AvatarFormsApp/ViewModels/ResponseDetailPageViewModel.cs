using System.Collections.ObjectModel;
using System.Text;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    [ObservableProperty]
    private string questionnaireDescription = string.Empty;

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
            QuestionnaireDescription = session.Questionnaire?.Description ?? string.Empty;
            QuestionnaireColor = session.Questionnaire?.Color ?? "#4CB3B3";
            SubmittedDate = session.SubmittedDate.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");
            IsComplete = session.IsComplete;
            ResponseCount = session.Responses.Count;
            PageTitle = $"Session — {session.Id[..8]}…";

            var items = session.Responses
                .Where(r => r.Question != null)
                .OrderBy(r => r.Question!.Order)
                .Select(r => new ResponseDetailItem
                {
                    Order = r.Question!.Order,
                    QuestionText = r.Question.Text,
                    AnswerText = r.AnswerText ?? "(no answer)",
                    QuestionnaireColor = QuestionnaireColor
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

    [RelayCommand]
    private async Task ExportTocsv()
    {
        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("CSV File", new List<string>() { ".csv" });
        savePicker.SuggestedFileName = $"{QuestionnaireName}_Responses";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            var csv = new System.Text.StringBuilder();

            // Write the Header info
            csv.AppendLine($"Form Title:,\"{QuestionnaireName}\"");
            csv.AppendLine($"Description:,\"{QuestionnaireDescription}\"");
            csv.AppendLine();

            // Write the Table
            csv.AppendLine("Order,Question,Answer");
            foreach (var item in ResponseItems)
            {
                csv.AppendLine($"{item.Order},\"{item.QuestionText}\",\"{item.AnswerText}\"");
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, csv.ToString());
        }
    }
    public string? QuestionnaireId => _questionnaireId;
}

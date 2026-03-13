using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.Tests.ViewModels;

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

    [RelayCommand]
    private async Task ExportAllTocsv()
    {
        if (ResponseSessions == null || !ResponseSessions.Any()) return;

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("CSV File", new List<string>() { ".csv" });
        savePicker.SuggestedFileName = $"{QuestionnaireName.Replace(" ", "_")}_All_Responses";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            var csv = new System.Text.StringBuilder();

            // 1. Metadata Header (matching detail page style)
            // We fetch the description from the first session's questionnaire object
            var description = ResponseSessions.FirstOrDefault()?.Questionnaire?.Description ?? string.Empty;
            csv.AppendLine($"Form Title:,\"{Escapecsv(QuestionnaireName)}\"");
            csv.AppendLine($"Description:,\"{Escapecsv(description)}\"");
            csv.AppendLine();

            // 2. Get All Unique Questions (Ordered by their 'Order' property)
            // This ensures the columns are consistent across all rows
            var questions = ResponseSessions
                .SelectMany(s => s.Responses)
                .Where(r => r.Question != null)
                .Select(r => r.Question!)
                .GroupBy(q => q.Id)
                .Select(g => g.First())
                .OrderBy(q => q.Order)
                .ToList();

            // 3. Generate the Questions Row
            var questionHeader = "Questions," + string.Join(",", questions.Select(q => $"\"{Escapecsv(q.Text)}\""));
            csv.AppendLine(questionHeader);

            // 4. Generate the Answers Rows (one for each session)
            int sessionIndex = 1;
            foreach (var session in ResponseSessions)
            {
                var answers = new List<string>();
                foreach (var q in questions)
                {
                    // Find the response for this specific question in this specific session
                    var response = session.Responses.FirstOrDefault(r => r.QuestionId == q.Id);
                    answers.Add($"\"{Escapecsv(response?.AnswerText ?? string.Empty)}\"");
                }

                var answerRow = $"Answer{sessionIndex}," + string.Join(",", answers);
                csv.AppendLine(answerRow);
                sessionIndex++;
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, csv.ToString());
        }
    }

    // Helper method to handle internal quotes in strings
    private string Escapecsv(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}

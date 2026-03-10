using System.Net.Http.Json;
using AvatarFormsApp.Contracts.Services;

namespace AvatarFormsApp.Services;

public class QuestionnaireAPIService : IQuestionnaireAPIService
{
    private readonly IQuestionnaireService _questionnaireService;
    private readonly HttpClient _httpClient;

    public QuestionnaireAPIService(IQuestionnaireService questionnaireService)
    {
        _questionnaireService = questionnaireService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<bool> SendQuestionnaireAsync(string questionnaireId, int port = 8882)
    {
        var questionnaire = await _questionnaireService.GetWithQuestionsAsync(questionnaireId);

        if (questionnaire == null)
        {
            System.Diagnostics.Debug.WriteLine($"Questionnaire with ID {questionnaireId} not found");
            return false;
        }

        var payload = new
        {
            questionnaire_id = questionnaireId,
            questions = questionnaire.Questions
                .OrderBy(q => q.Order)
                .Select(q => q.Text)
                .ToList(),
            description = questionnaire.Description ?? questionnaire.Name
        };

        var jsonContent = JsonContent.Create(payload);
        var url = $"http://localhost:{port}/questionnaire";

        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to send questionnaire to {url}...");
            var response = await _httpClient.PostAsync(url, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine("Successfully sent questionnaire.");
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"Server error {response.StatusCode}.");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Connection failed: {ex.Message}.");
            return false;
        }
    }
}

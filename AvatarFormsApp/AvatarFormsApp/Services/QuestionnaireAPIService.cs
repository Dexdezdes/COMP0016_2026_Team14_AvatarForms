using System.Net.Http.Json;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.DTOs;

namespace AvatarFormsApp.Services;

public class QuestionnaireAPIService : IQuestionnaireAPIService
{
    private readonly IQuestionnaireService _questionnaireService;
    private readonly HttpClient _httpClient;

    public QuestionnaireAPIService(IQuestionnaireService questionnaireService)
        : this(questionnaireService, new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
    {
    }

    //NEW: This is what the Tests use. It lets us inject a "Fake" HttpClient.
    internal QuestionnaireAPIService(IQuestionnaireService questionnaireService, HttpClient httpClient)
    {
        _questionnaireService = questionnaireService;
        _httpClient = httpClient;
    }

    public async Task<bool> SendQuestionnaireAsync(string questionnaireId, int port = 8882)
    {
        var questionnaire = await _questionnaireService.GetWithQuestionsAsync(questionnaireId);

        if (questionnaire == null)
        {
            System.Diagnostics.Debug.WriteLine($"Questionnaire with ID {questionnaireId} not found");
            return false;
        }

        var payload = QuestionnaireTransferDto.FromQuestionnaire(questionnaire);
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

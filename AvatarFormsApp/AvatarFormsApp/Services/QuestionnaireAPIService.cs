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
        if (questionnaire == null) return false;

        var payload = new
        {
            questions = questionnaire.Questions.OrderBy(q => q.Order).Select(q => q.Text).ToList(),
            description = questionnaire.Description ?? questionnaire.Name
        };

        var url = $"http://localhost:{port}/questionnaire";

        // Loop indefinitely until a successful response is received
        while (true)
        {
            try
            {
                // Re-create content inside the loop because it's disposed after Send
                var jsonContent = JsonContent.Create(payload);

                System.Diagnostics.Debug.WriteLine($"Attempting to send questionnaire to {url}...");
                var response = await _httpClient.PostAsync(url, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("Successfully sent questionnaire.");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"Server error {response.StatusCode}. Retrying...");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection failed: {ex.Message}. Retrying...");
            }

            // Wait 2 seconds before the next attempt to avoid spamming
            await Task.Delay(2000);
        }
    }
}

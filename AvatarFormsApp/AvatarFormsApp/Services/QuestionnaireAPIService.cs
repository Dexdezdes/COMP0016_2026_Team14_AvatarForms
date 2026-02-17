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
        try
        {
            // Get the questionnaire with all questions
            var questionnaire = await _questionnaireService.GetWithQuestionsAsync(questionnaireId);

            if (questionnaire == null)
            {
                System.Diagnostics.Debug.WriteLine($"Questionnaire with ID {questionnaireId} not found");
                return false;
            }

            // Build the JSON payload matching Python backend's expected format
            var payload = new
            {
                questions = questionnaire.Questions
                    .OrderBy(q => q.Order)
                    .Select(q => q.Text)
                    .ToList(),
                description = questionnaire.Description ?? questionnaire.Name
            };

            // Serialize to JSON
            var jsonContent = JsonContent.Create(payload);

            // Send to Python backend HTTP endpoint with retry logic
            var url = $"http://localhost:{port}/questionnaire";

            try
            {
                System.Diagnostics.Debug.WriteLine($"Sending questionnaire '{questionnaire.Name}' to backend at {url}");

                var response = await _httpClient.PostAsync(url, jsonContent);

                if (response .IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully sent questionnaire '{questionnaire.Name}' to backend");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Failed to send questionnaire. Status: {response.StatusCode}, Error: {errorContent}");
                    return false;
                }
            } catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initial attempt to send questionnaire failed: {ex.Message}");
            }

            return false;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP error sending questionnaire: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending questionnaire: {ex.Message}");
            return false;
        }
    }
}

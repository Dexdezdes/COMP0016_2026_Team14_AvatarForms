using System.Net.Http.Json;
using System.Text.Json;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.DTOs;
using Microsoft.Extensions.Configuration;

namespace AvatarFormsApp.Services;

/// Service for communicating with Python backend via HTTP
public class PythonBackendService : IPythonBackendService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public PythonBackendService(IConfiguration configuration)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Get Python backend URL from configuration
        _baseUrl = configuration["PythonBackend:BaseUrl"] ?? "http://localhost:8083";
    }

    /// Sends questionnaire data to Python backend to start an interview
    public async Task<PythonBackendResponse> SendQuestionnaireAsync(QuestionnaireTransferDto questionnaireDto)
    {
        try
        {
            var url = $"{_baseUrl}/api/questionnaire/start";
            
            var response = await _httpClient.PostAsJsonAsync(url, questionnaireDto);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                return new PythonBackendResponse
                {
                    IsSuccess = true,
                    Message = content.GetProperty("message").GetString(),
                    QuestionCount = content.TryGetProperty("question_count", out var qCount) 
                        ? qCount.GetInt32() 
                        : null
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return new PythonBackendResponse
                {
                    IsSuccess = false,
                    Message = "Failed to send questionnaire to Python backend",
                    ErrorDetails = $"Status: {response.StatusCode}, Details: {errorContent}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new PythonBackendResponse
            {
                IsSuccess = false,
                Message = "Unable to connect to Python backend",
                ErrorDetails = $"Connection error: {ex.Message}. Make sure Python backend is running at {_baseUrl}"
            };
        }
        catch (TaskCanceledException ex)
        {
            return new PythonBackendResponse
            {
                IsSuccess = false,
                Message = "Request timed out",
                ErrorDetails = $"The Python backend did not respond in time: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new PythonBackendResponse
            {
                IsSuccess = false,
                Message = "An unexpected error occurred",
                ErrorDetails = ex.Message
            };
        }
    }
}

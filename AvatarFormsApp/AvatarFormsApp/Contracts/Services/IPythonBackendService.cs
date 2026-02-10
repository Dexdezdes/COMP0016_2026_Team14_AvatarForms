using AvatarFormsApp.DTOs;

namespace AvatarFormsApp.Contracts.Services;

public interface IPythonBackendService
{
    /// <summary>
    /// Sends questionnaire data to Python backend to start an interview
    /// </summary>
    /// <param name="questionnaireDto">The questionnaire data to send</param>
    /// <returns>Response indicating success or failure</returns>
    Task<PythonBackendResponse> SendQuestionnaireAsync(QuestionnaireTransferDto questionnaireDto);
}
public class PythonBackendResponse
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public int? QuestionCount { get; set; }
    public string? ErrorDetails { get; set; }
}

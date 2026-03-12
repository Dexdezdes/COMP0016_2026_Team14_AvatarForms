using System.Text.Json.Serialization;

namespace AvatarFormsApp.DTOs;

/// <summary>
/// Data Transfer Object for receiving interview response data from Python backend
/// </summary>
public class ResponseTransferDto
{
    [JsonPropertyName("questionnaire_id")]
    public required string QuestionnaireId { get; set; }

    [JsonPropertyName("question_order")]
    public required int QuestionOrder { get; set; }

    [JsonPropertyName("question")]
    public required string Question { get; set; }

    [JsonPropertyName("answer")]
    public required string Answer { get; set; }

    [JsonPropertyName("question_type")]
    public string QuestionType { get; set; } = "open_ended"; // "open_ended" or "mcq"

    [JsonPropertyName("selected_option")]
    public string? SelectedOption { get; set; } // The matched MCQ option text, if applicable
}

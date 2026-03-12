using System.Text.Json.Serialization;

namespace AvatarFormsApp.DTOs;

/// <summary>
/// Represents a single question sent to the Python backend
/// </summary>
public class QuestionTransferDto
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; } // "open_ended" or "mcq"

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }
}

/// <summary>
/// Data Transfer Object for sending questionnaire data to Python backend
/// </summary>
public class QuestionnaireTransferDto
{
    [JsonPropertyName("questionnaire_id")]
    public required string QuestionnaireId { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("questions")]
    public required List<QuestionTransferDto> Questions { get; set; }

    /// <summary>
    /// Creates a DTO from a Questionnaire model
    /// </summary>
    public static QuestionnaireTransferDto FromQuestionnaire(Models.Questionnaire questionnaire)
    {
        return new QuestionnaireTransferDto
        {
            QuestionnaireId = questionnaire.Id,
            Description = questionnaire.Description ?? string.Empty,
            Questions = questionnaire.Questions
                .OrderBy(q => q.Order)
                .Select(q => new QuestionTransferDto
                {
                    Text = q.Text,
                    Type = q.Type == Models.QuestionType.MCQ ? "mcq" : "open_ended",
                    Options = q.Type == Models.QuestionType.MCQ
                        ? q.Options.OrderBy(o => o.Order).Select(o => o.Text).ToList()
                        : null
                })
                .ToList()
        };
    }
}

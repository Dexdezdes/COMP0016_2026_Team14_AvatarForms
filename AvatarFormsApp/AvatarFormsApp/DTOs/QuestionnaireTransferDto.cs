using System.Text.Json.Serialization;

namespace AvatarFormsApp.DTOs;

/// <summary>
/// Data Transfer Object for sending questionnaire data to Python backend
/// </summary>
public class QuestionnaireTransferDto
{
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("questions")]
    public required List<string> Questions { get; set; }

    /// <summary>
    /// Creates a DTO from a Questionnaire model
    /// </summary>
    public static QuestionnaireTransferDto FromQuestionnaire(Models.Questionnaire questionnaire)
    {
        return new QuestionnaireTransferDto
        {
            Description = questionnaire.Description ?? string.Empty,
            Questions = questionnaire.Questions
                .OrderBy(q => q.Order)
                .Select(q => q.Text)
                .ToList()
        };
    }
}

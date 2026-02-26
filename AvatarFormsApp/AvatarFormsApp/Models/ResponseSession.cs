namespace AvatarFormsApp.Models;

public class ResponseSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string QuestionnaireId { get; set; } // FK
    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
    public bool IsComplete { get; set; } = false;

    // Navigation properties
    public Questionnaire? Questionnaire { get; set; }
    public List<Response> Responses { get; set; } = new();
}

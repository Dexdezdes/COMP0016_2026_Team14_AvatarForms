namespace AvatarFormsApp.Models;

public class Response
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string ResponseSessionId { get; set; } // FK
    public required string QuestionId { get; set; } // FK
    public string? AnswerText { get; set; } // The actual answer
    public DateTime AnsweredDate { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ResponseSession? ResponseSession { get; set; }
    public Question? Question { get; set; }
}

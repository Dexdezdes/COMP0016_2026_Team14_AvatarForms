namespace AvatarFormsApp.Models;

public class Questionnaire
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public required string OwnerId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending"; // "Pending" or "Closed"
    public string Color { get; set; } = "#4CB3B3"; // For UI display
    
    // Navigation properties
    public List<Question> Questions { get; set; } = new();
    public List<ResponseSession> ResponseSessions { get; set; } = new();
}

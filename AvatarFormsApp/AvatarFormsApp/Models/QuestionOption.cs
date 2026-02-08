namespace AvatarFormsApp.Models;

public class QuestionOption
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string QuestionId { get; set; } // FK
    public required string Text { get; set; } // The actual option text (e.g., "Red", "Blue")
    public int Order { get; set; } // Display order (1, 2, 3...)
    
    // Navigation property
    public Question? Question { get; set; }
}

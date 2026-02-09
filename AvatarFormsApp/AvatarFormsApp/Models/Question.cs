namespace AvatarFormsApp.Models;

public class Question
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string QuestionnaireId { get; set; } // FK
    public required string Text { get; set; }
    public QuestionType Type { get; set; }
    public int Order { get; set; } // Display order in questionnaire
    public bool IsRequired { get; set; } = false;
    
    // Navigation properties
    public Questionnaire? Questionnaire { get; set; }
    public List<QuestionOption> Options { get; set; } = new();
    public List<Response> Responses { get; set; } = new();

    // Computed properties
    public bool IsMCQ => Type == QuestionType.MCQ;
    public bool IsTextInput => Type == QuestionType.OpenEnded;
}

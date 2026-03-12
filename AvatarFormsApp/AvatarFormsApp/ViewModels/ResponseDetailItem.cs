using AvatarFormsApp.Models;

namespace AvatarFormsApp.ViewModels;

/// <summary>
/// Flat wrapper pairing a question with its answer for display in ResponseDetailPage.
/// </summary>
public class ResponseDetailItem
{
    public int Order { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public string QuestionnaireColor { get; set; } = "#4CB3B3";

    // MCQ metadata
    public bool IsMCQ { get; set; }
    public List<ResponseOptionItem> Options { get; set; } = [];
}

/// <summary>
/// Represents a single MCQ option with selection state for display.
/// </summary>
public class ResponseOptionItem
{
    public string Text { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

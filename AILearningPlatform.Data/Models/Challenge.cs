namespace AILearningPlatform.Data.Models;

public class Challenge
{
    public Guid Id { get; set; }
    public Guid CourseModuleId { get; set; }
    public CourseModule CourseModule { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty; // Markdown task description
    public string StartingCode { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
}

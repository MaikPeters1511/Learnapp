namespace AILearningPlatform.Data.Models;

public class CourseModule
{
    public Guid Id { get; set; }
    public Guid LearningPathId { get; set; }
    public LearningPath LearningPath { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty; // .NET, Angular, Architecture, Testing, Cloud
    public int Order { get; set; }
    public bool IsCompleted { get; set; }

    public List<Challenge> Challenges { get; set; } = new();
}

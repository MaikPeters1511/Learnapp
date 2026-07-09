namespace AILearningPlatform.Data.Models;

public class LearningPath
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public List<CourseModule> Modules { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

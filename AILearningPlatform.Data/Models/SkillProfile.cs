namespace AILearningPlatform.Data.Models;

public class SkillProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int DotNetLevel { get; set; } // 1-10
    public int AngularLevel { get; set; }
    public int ArchitectureLevel { get; set; }
    public int TestingLevel { get; set; }
    public int CloudLevel { get; set; }
}

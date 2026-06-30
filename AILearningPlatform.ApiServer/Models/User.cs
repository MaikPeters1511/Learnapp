namespace AILearningPlatform.ApiServer.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public SkillProfile? SkillProfile { get; set; }
    public List<LearningPath> LearningPaths { get; set; } = new();
}

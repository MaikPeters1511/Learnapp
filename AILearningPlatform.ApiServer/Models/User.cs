namespace AILearningPlatform.ApiServer.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    // Gamification properties
    public int Points { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime LastActivityDate { get; set; }
    
    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();

    public SkillProfile? SkillProfile { get; set; }
    public List<LearningPath> LearningPaths { get; set; } = new();
}

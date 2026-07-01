using System.ComponentModel.DataAnnotations.Schema;

namespace AILearningPlatform.ApiServer.Models;

public class UserAchievement
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    public int AchievementId { get; set; }
    [ForeignKey("AchievementId")]
    public Achievement Achievement { get; set; } = null!;

    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
}

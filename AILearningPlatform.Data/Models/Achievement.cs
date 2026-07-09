namespace AILearningPlatform.Data.Models;

public class Achievement
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty; // Example: "fa-star"
    public int PointsAwarded { get; set; }
}

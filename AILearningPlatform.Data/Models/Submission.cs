namespace AILearningPlatform.Data.Models;

public class Submission
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;

    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Evaluating, Approved, Rejected
    public string Feedback { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

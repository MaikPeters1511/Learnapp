using AILearningPlatform.Data.Models;

namespace AILearningPlatform.Data.Gamification;

/// <summary>
/// XP-Regeln der Lernplattform. Gemeinsam genutzt von ApiServer und EvaluationWorker.
/// XP stehen in <see cref="User.Points"/>, es ist keine Schemaänderung nötig.
/// </summary>
public static class Xp
{
    public const int SubmitCode = 5;          // erste Abgabe pro Aufgabe
    public const int ApproveChallenge = 50;   // erste genehmigte Abgabe pro Aufgabe
    public const int CompleteModule = 25;
    public const int QuizCorrectAnswer = 10;
    public const int QuizPerfectBonus = 20;
    public const int XpPerLevel = 100;

    public static int LevelFor(int points) => points / XpPerLevel + 1;

    /// <summary>
    /// Vergibt XP und aktualisiert die Lern-Streak (aufeinanderfolgende Tage mit Aktivität, in UTC).
    /// </summary>
    public static void Award(User? user, int xp)
    {
        if (user == null || xp <= 0) return;

        var today = DateTime.UtcNow.Date;
        var lastActive = user.LastActivityDate.Date;

        if (user.CurrentStreak <= 0 || lastActive < today.AddDays(-1))
            user.CurrentStreak = 1;                 // erste Aktivität oder Lücke von mehr als einem Tag
        else if (lastActive == today.AddDays(-1))
            user.CurrentStreak += 1;                // gestern aktiv gewesen
        // lastActive == heute: Streak bleibt unverändert

        user.Points += xp;
        user.LastActivityDate = DateTime.UtcNow;
    }
}

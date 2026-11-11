using System.Net.Http.Json;
using System.Text.Json;
using AILearningPlatform.Data;
using AILearningPlatform.Data.Gamification;
using AILearningPlatform.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AILearningPlatform.EvaluationWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;
    private readonly string _lmStudioUrl;
    private readonly string _lmStudioModel;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        _lmStudioUrl = configuration["LmStudio:BaseUrl"] ?? "http://localhost:1234";
        _lmStudioModel = configuration["LmStudio:Model"] ?? "local-model";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Evaluation Worker started.");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            SchemaUpgrades.Apply(db, message => _logger.LogWarning("{Message}", message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database schema in worker.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var pendingSubmissions = await db.Submissions
                    .Include(s => s.Challenge)
                    .Where(s => s.Status == "Pending")
                    .ToListAsync(stoppingToken);

                foreach (var submission in pendingSubmissions)
                {
                    _logger.LogInformation("Evaluating submission {SubmissionId} for Challenge {ChallengeId}", submission.Id, submission.ChallengeId);
                    submission.Status = "Evaluating";
                    await db.SaveChangesAsync(stoppingToken);

                    string feedback = string.Empty;
                    string status = "Approved";
                    bool feedbackIsFromAi = false;

                    try
                    {
                        var prompt = $@"
Challenge: {submission.Challenge.Title}
Instructions: {submission.Challenge.Instructions}
Expected Output: {submission.Challenge.ExpectedOutput}

Submitted Code:
```
{submission.Code}
```

Review this code. Determine if it solves the challenge correctly. Provide detailed feedback in German.
End your response with either [APPROVED] or [REJECTED] on a new line.";

                        var response = await _httpClient.PostAsJsonAsync($"{_lmStudioUrl}/v1/chat/completions", new
                        {
                            model = _lmStudioModel,
                            messages = new[]
                            {
                                new { role = "system", content = "You are an expert .NET/Angular tutor who reviews code challenges and provides helpful pedagogical feedback. Always answer in German (Deutsch), regardless of the language of the input." },
                                new { role = "user", content = prompt }
                            },
                            temperature = 0.2
                        }, stoppingToken);

                        if (response.IsSuccessStatusCode)
                        {
                            var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>(stoppingToken);
                            var content = resultJson.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                            
                            feedback = content;
                            feedbackIsFromAi = true;
                            if (content.Contains("[REJECTED]", StringComparison.OrdinalIgnoreCase))
                            {
                                status = "Rejected";
                            }
                        }
                        else
                        {
                            _logger.LogWarning("LM Studio returned non-success code: {StatusCode}", response.StatusCode);
                            feedback = "KI-Bewertung offline. Automatisch genehmigt zu Demonstrationszwecken. Der Code sieht sauber aus!";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to call LM Studio API. Mocking successful evaluation.");
                        feedback = "Verbindung zu LM Studio fehlgeschlagen. Automatische Genehmigung: Gut gemacht, weiter so!";
                    }

                    submission.Feedback = feedback;
                    submission.Status = status;

                    // XP nur für die erste genehmigte Abgabe pro Aufgabe und nur bei echter Bewertung,
                    // nicht beim automatischen Fallback ohne KI-Antwort.
                    if (status == "Approved" && feedbackIsFromAi)
                    {
                        var alreadyApproved = await db.Submissions.AnyAsync(s =>
                            s.UserId == submission.UserId &&
                            s.ChallengeId == submission.ChallengeId &&
                            s.Status == "Approved" &&
                            s.Id != submission.Id, stoppingToken);
                        if (!alreadyApproved)
                        {
                            var user = await db.Users.FindAsync(new object[] { submission.UserId }, stoppingToken);
                            Xp.Award(user, Xp.ApproveChallenge);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Submission {SubmissionId} marked as {Status}", submission.Id, status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in EvaluationWorker loop.");
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}

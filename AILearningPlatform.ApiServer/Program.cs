using AILearningPlatform.Data;
using AILearningPlatform.Data.Gamification;
using AILearningPlatform.Data.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("sqldata")));
builder.Services.AddHttpClient();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Configure CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var lmStudioUrl = builder.Configuration["LmStudio:BaseUrl"] ?? "http://localhost:1234";
var lmStudioModel = builder.Configuration["LmStudio:Model"] ?? "local-model";

var app = builder.Build();

app.UseCors("AllowAll");
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Automatically initialize the database (migrations/creation). Retries on transient
// failures (e.g. the shared SQLite volume not mounted yet) instead of crashing the
// whole process, since a momentary hiccup shouldn't take Kestrel down with it.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

    const int maxAttempts = 5;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            SchemaUpgrades.Apply(db);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            startupLogger.LogWarning(ex, "Database initialization attempt {Attempt}/{MaxAttempts} failed, retrying...", attempt, maxAttempts);
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Database initialization failed after {MaxAttempts} attempts; continuing startup anyway.", maxAttempts);
        }
    }

    try
    {
        // Seed a default user if not exists
        var defaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (!db.Users.Any(u => u.Id == defaultUserId))
        {
            db.Users.Add(new User
            {
                Id = defaultUserId,
                Name = "John Doe",
                Email = "john.doe@example.com"
            });
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Failed to seed default user.");
    }
}

// Endpoints

// Get or Create User
app.MapGet("/api/users/{id}", async (Guid id, AppDbContext db) =>
{
    var user = await db.Users
        .Include(u => u.SkillProfile)
        .FirstOrDefaultAsync(u => u.Id == id);
        
    if (user == null)
    {
        user = new User
        {
            Id = id,
            Name = "Demo User",
            Email = "demo@example.com"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
    return Results.Ok(user);
});

// Profil mit echten Daten: Stammdaten, XP/Level, Streak, Einstufung, Statistik und Abzeichen.
// Abzeichen werden aus den vorhandenen Daten abgeleitet (keine eigene Tabelle nötig).
app.MapGet("/api/users/{id}/profile", async (Guid id, AppDbContext db) =>
{
    var user = await db.Users.Include(u => u.SkillProfile).FirstOrDefaultAsync(u => u.Id == id);
    if (user == null) return Results.NotFound("User not found");

    var modules = await db.LearningPaths
        .Where(p => p.UserId == id)
        .SelectMany(p => p.Modules)
        .Select(m => new { m.IsCompleted, ChallengeCount = m.Challenges.Count })
        .ToListAsync();

    var submissions = await db.Submissions
        .Where(s => s.UserId == id)
        .Select(s => new { s.ChallengeId, s.Status, s.SubmittedAt })
        .ToListAsync();

    var totalModules = modules.Count;
    var completedModules = modules.Count(m => m.IsCompleted);
    var totalChallenges = modules.Sum(m => m.ChallengeCount);
    var approved = submissions.Where(s => s.Status == "Approved").ToList();
    var approvedChallenges = approved.Select(s => s.ChallengeId).Distinct().Count();
    var firstSubmission = submissions.Count > 0 ? submissions.Min(s => s.SubmittedAt) : (DateTime?)null;
    var firstApproved = approved.Count > 0 ? approved.Min(s => s.SubmittedAt) : (DateTime?)null;

    var level = Xp.LevelFor(user.Points);
    var badges = new[]
    {
        new { key = "first-step", name = "Erster Schritt", description = "Du hast deine erste Lösung eingereicht.", icon = "👟", earned = submissions.Count > 0, awardedAt = firstSubmission },
        new { key = "approved", name = "Bestanden!", description = "Eine deiner Lösungen wurde von der KI genehmigt.", icon = "✅", earned = approved.Count > 0, awardedAt = firstApproved },
        new { key = "module-master", name = "Modul-Meister", description = "Du hast ein komplettes Modul abgeschlossen.", icon = "🏁", earned = completedModules > 0, awardedAt = (DateTime?)null },
        new { key = "streak-3", name = "Dranbleiber", description = "Mindestens 3 Tage in Folge gelernt.", icon = "🔥", earned = user.CurrentStreak >= 3, awardedAt = (DateTime?)null },
        new { key = "level-5", name = "Fortgeschritten", description = "Level 5 erreicht.", icon = "🚀", earned = level >= 5, awardedAt = (DateTime?)null },
    };

    return Results.Ok(new
    {
        user.Id,
        user.Name,
        user.Email,
        points = user.Points,
        level,
        xpIntoLevel = user.Points % Xp.XpPerLevel,
        xpPerLevel = Xp.XpPerLevel,
        currentStreak = user.CurrentStreak,
        lastActivityDate = user.LastActivityDate,
        skillProfile = user.SkillProfile == null ? null : new
        {
            dotNetLevel = user.SkillProfile.DotNetLevel,
            angularLevel = user.SkillProfile.AngularLevel,
            architectureLevel = user.SkillProfile.ArchitectureLevel,
            testingLevel = user.SkillProfile.TestingLevel,
            cloudLevel = user.SkillProfile.CloudLevel
        },
        stats = new
        {
            totalModules,
            completedModules,
            totalChallenges,
            approvedChallenges,
            submissions = submissions.Count,
            quizzesCompleted = user.QuizzesCompleted
        },
        badges
    });
});

// Submit Skill Assessment
app.MapPost("/api/assessment", async (AssessmentRequest request, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(request.UserId);
    if (user == null) return Results.NotFound("User not found");

    // Upsert SkillProfile
    var profile = await db.SkillProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId);
    if (profile == null)
    {
        profile = new SkillProfile { UserId = request.UserId };
        db.SkillProfiles.Add(profile);
    }
    
    profile.DotNetLevel = request.DotNetLevel;
    profile.AngularLevel = request.AngularLevel;
    profile.ArchitectureLevel = request.ArchitectureLevel;
    profile.TestingLevel = request.TestingLevel;
    profile.CloudLevel = request.CloudLevel;

    // Generate Personalized Learning Path
    // Clear old learning paths for demo purposes
    var oldPaths = db.LearningPaths.Where(p => p.UserId == request.UserId);
    db.LearningPaths.RemoveRange(oldPaths);

    var path = new LearningPath
    {
        UserId = request.UserId,
        Title = "Dein personalisierter Lernpfad",
        Description = "Dieser Pfad wurde basierend auf deinem Skill-Assessment generiert."
    };
    db.LearningPaths.Add(path);
    await db.SaveChangesAsync();

    // Generate modules based on levels
    int moduleOrder = 1;

    // .NET Module
    var dotnetModule = new CourseModule
    {
        LearningPathId = path.Id,
        Title = request.DotNetLevel < 4 ? "C# & .NET 10 Grundlagen" : "Fortgeschrittenes .NET 10 & Performance",
        Description = request.DotNetLevel < 4 ? "Einführung in C# Syntax, Records und .NET CLI." : "Dependency Injection, Asynchronität und Memory-Management.",
        Topic = ".NET",
        Order = moduleOrder++
    };
    db.CourseModules.Add(dotnetModule);

    // Angular Module
    var angularModule = new CourseModule
    {
        LearningPathId = path.Id,
        Title = request.AngularLevel < 4 ? "Angular v22 Einstieg" : "Angular v22 State Management & Performance",
        Description = request.AngularLevel < 4 ? "Komponenten, Services und Routing in der neuesten Angular Version." : "Zoneless Angular, RxJS und optimierte Bundles.",
        Topic = "Angular",
        Order = moduleOrder++
    };
    db.CourseModules.Add(angularModule);

    // Architecture Module
    var archModule = new CourseModule
    {
        LearningPathId = path.Id,
        Title = request.ArchitectureLevel < 4 ? "Software-Architektur Basics" : "Microservices & Clean Architecture",
        Description = request.ArchitectureLevel < 4 ? "SOLID-Prinzipien und Schichtenarchitektur einfach erklärt." : "Event-driven Systems, CQRS und Domain-Driven Design.",
        Topic = "Architektur",
        Order = moduleOrder++
    };
    db.CourseModules.Add(archModule);

    // Testing Module
    var testingModule = new CourseModule
    {
        LearningPathId = path.Id,
        Title = request.TestingLevel < 4 ? "Einführung in Testing" : "Advanced Testing & TDD",
        Description = request.TestingLevel < 4 ? "Schreibe deine ersten Unit Tests mit xUnit und FluentAssertions." : "Integrationstests mit Testcontainers und Playwright E2E-Testing.",
        Topic = "Testing",
        Order = moduleOrder++
    };
    db.CourseModules.Add(testingModule);

    // Cloud Module
    var cloudModule = new CourseModule
    {
        LearningPathId = path.Id,
        Title = request.CloudLevel < 4 ? "Cloud & Docker Basics" : "Kubernetes & Cloud-Native Architektur",
        Description = request.CloudLevel < 4 ? "Verstehe Containerisierung, Dockerfiles und Docker Compose." : "Orchestrierung mit Kubernetes, Helm Charts und Service Meshes.",
        Topic = "Cloud",
        Order = moduleOrder++
    };
    db.CourseModules.Add(cloudModule);

    await db.SaveChangesAsync();

    // Add Challenges for Modules
    db.Challenges.Add(new Challenge
    {
        CourseModuleId = dotnetModule.Id,
        Title = request.DotNetLevel < 4 ? "Schreibe deinen ersten Record" : "Konfiguriere eine Custom Middleware",
        Instructions = request.DotNetLevel < 4 
            ? "Erstelle eine C# Klasse/Record `Person` mit den Properties `FirstName` und `LastName`."
            : "Implementiere eine custom Middleware, die die Ausführungszeit einer Anfrage misst und im Response Header zurückgibt.",
        StartingCode = request.DotNetLevel < 4
            ? "public record Person(string FirstName, string LastName);"
            : "public class PerformanceMiddleware {\n  // Implementiere hier\n}",
        ExpectedOutput = "APPROVED"
    });

    db.Challenges.Add(new Challenge
    {
        CourseModuleId = angularModule.Id,
        Title = request.AngularLevel < 4 ? "Erstelle eine Standalone Component" : "Implementiere ein Signal-based Store",
        Instructions = request.AngularLevel < 4 
            ? "Erstelle eine einfache Angular Standalone-Komponente, die ein Signal mit dem Wert 'Hallo Welt' anzeigt."
            : "Erstelle einen Signal-basierten Service, der den Status eines Counters verwaltet.",
        StartingCode = request.AngularLevel < 4
            ? "@Component({\n  selector: 'app-hello',\n  standalone: true,\n  template: `<h1>{{ message() }}</h1>`\n})\nexport class HelloComponent {\n  message = signal('Hallo Welt');\n}"
            : "export class CounterStore {\n  // Implementiere mit signals\n}",
        ExpectedOutput = "APPROVED"
    });

    db.Challenges.Add(new Challenge
    {
        CourseModuleId = testingModule.Id,
        Title = request.TestingLevel < 4 ? "Schreibe einen Unit Test" : "Implementiere ein Integration Test",
        Instructions = request.TestingLevel < 4
            ? "Schreibe einen xUnit-Test für eine Add-Methode `Calculator.Add(a, b)`."
            : "Schreibe einen Integrationstest für einen API-Endpoint mit WebApplicationFactory.",
        StartingCode = request.TestingLevel < 4
            ? "[Fact]\npublic void Add_ShouldReturnSum() {\n  // Arrange, Act, Assert\n}"
            : "public class ApiTests : IClassFixture<WebApplicationFactory<Program>> {\n  // Testcode hier\n}",
        ExpectedOutput = "APPROVED"
    });

    db.Challenges.Add(new Challenge
    {
        CourseModuleId = cloudModule.Id,
        Title = request.CloudLevel < 4 ? "Schreibe ein Dockerfile" : "Erstelle eine Kubernetes Deployment-Datei",
        Instructions = request.CloudLevel < 4
            ? "Schreibe ein einfaches Dockerfile für eine .NET 10 API basierend auf dem mcr.microsoft.com SDK-Image."
            : "Erstelle ein YAML Deployment für eine App mit 3 Replikaten auf Port 80.",
        StartingCode = request.CloudLevel < 4
            ? "FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build\n# ...weitere Anweisungen"
            : "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: app-deployment\n# ...weitere Konfiguration",
        ExpectedOutput = "APPROVED"
    });

    await db.SaveChangesAsync();

    return Results.Ok(path);
});

// Get Learning Path
app.MapGet("/api/learningpath/{userId}", async (Guid userId, AppDbContext db) =>
{
    var path = await db.LearningPaths
        .Include(p => p.Modules)
        .ThenInclude(m => m.Challenges)
        .FirstOrDefaultAsync(p => p.UserId == userId);

    if (path == null) return Results.NotFound("No learning path found. Please complete the assessment.");
    
    // Sort modules
    path.Modules = path.Modules.OrderBy(m => m.Order).ToList();
    return Results.Ok(path);
});

// Submit Challenge Code
app.MapPost("/api/challenges/{challengeId}/submit", async (Guid challengeId, SubmissionRequest request, AppDbContext db) =>
{
    var challenge = await db.Challenges.FindAsync(challengeId);
    if (challenge == null) return Results.NotFound("Challenge not found");

    // XP nur für die erste Abgabe pro Aufgabe, damit wiederholtes Einreichen nicht farmbar ist.
    var isFirstSubmission = !await db.Submissions
        .AnyAsync(s => s.ChallengeId == challengeId && s.UserId == request.UserId);
    if (isFirstSubmission)
    {
        Xp.Award(await db.Users.FindAsync(request.UserId), Xp.SubmitCode);
    }

    var submission = new Submission
    {
        ChallengeId = challengeId,
        UserId = request.UserId,
        Code = request.Code,
        Status = "Pending"
    };

    db.Submissions.Add(submission);
    await db.SaveChangesAsync();

    return Results.Ok(submission);
});

// Get Submissions for Challenge
app.MapGet("/api/challenges/{challengeId}/submissions/{userId}", async (Guid challengeId, Guid userId, AppDbContext db) =>
{
    var submissions = await db.Submissions
        .Where(s => s.ChallengeId == challengeId && s.UserId == userId)
        .OrderByDescending(s => s.SubmittedAt)
        .ToListAsync();
        
    return Results.Ok(submissions);
});

// Mark Module Complete
app.MapPost("/api/modules/{moduleId}/complete", async (Guid moduleId, AppDbContext db) =>
{
    var module = await db.CourseModules.Include(m => m.LearningPath).FirstOrDefaultAsync(m => m.Id == moduleId);
    if (module == null) return Results.NotFound("Module not found");

    // XP nur beim Übergang auf "erledigt".
    if (!module.IsCompleted)
    {
        Xp.Award(await db.Users.FindAsync(module.LearningPath.UserId), Xp.CompleteModule);
    }
    module.IsCompleted = true;
    await db.SaveChangesAsync();
    return Results.Ok(module);
});

// Generate Quiz
app.MapPost("/api/quiz/generate", async (QuizRequest request) =>
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    try
    {
        var response = await httpClient.PostAsJsonAsync($"{lmStudioUrl}/v1/chat/completions", new
        {
            model = lmStudioModel,
            messages = new[]
            {
                new { role = "system", content = "You are a quiz generator. Write all question and option text in German (Deutsch). Output ONLY a valid raw JSON array of 3 questions without markdown blocks. Format: [{\"questionText\":\"...\",\"options\":[\"...\",\"...\"],\"correctOptionIndex\":0}]" },
                new { role = "user", content = $"Generate 3 multiple-choice questions for {request.Topic} at level {request.Level}." }
            },
            temperature = 0.5
        });

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var rawJson = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            // Clean up possible markdown fence blocks
            rawJson = rawJson.Replace("```json", "").Replace("```", "").Trim();
            return Results.Content(rawJson, "application/json");
        }
    }
    catch { /* Fallback */ }

    // Mock/Fallback Quiz data
    var fallbackQuiz = request.Topic switch
    {
        ".NET" => new object[]
        {
            new { questionText = "Was ist ein Kernkonzept von .NET?", options = new[] { "Common Language Runtime (CLR)", "Java Virtual Machine (JVM)", "V8 Engine" }, correctOptionIndex = 0 },
            new { questionText = "Wie optimiert man die Performance in .NET?", options = new[] { "Gar nicht", "Verwendung von ValueTypes/Span<T> wo sinnvoll", "Exzessive Reflection" }, correctOptionIndex = 1 },
            new { questionText = "Welches C# 10/11 Feature vereinfacht Namensbereiche?", options = new[] { "Alt-Style Namespaces", "Global Imports", "File-scoped Namespaces" }, correctOptionIndex = 2 }
        },
        "Angular" => new object[]
        {
            new { questionText = "Welche API steuert in Angular v22 die Reaktivität?", options = new[] { "NgModules", "Signals", "Promises" }, correctOptionIndex = 1 },
            new { questionText = "Wie werden Komponenten in neuen Angular Versionen standardmäßig definiert?", options = new[] { "Als Dynamic Components", "Als Standalone Components", "Als Modul-Komponenten" }, correctOptionIndex = 1 },
            new { questionText = "Welcher Provider wird für Zoneless Apps verwendet?", options = new[] { "NgZone", "Zone.js", "provideExperimentalZonelessChangeDetection()" }, correctOptionIndex = 2 }
        },
        _ => new object[]
        {
            new { questionText = $"Was beschreibt {request.Topic} am besten?", options = new[] { "Ein fundamentales Framework / Konzept", "Ein veraltetes Programmiermuster", "Ein reines Design-Pattern" }, correctOptionIndex = 0 },
            new { questionText = $"Welche Best Practice ist für {request.Topic} besonders wichtig?", options = new[] { "Hohe Kopplung", "Minimierung von Komplexität und Clean Code", "Verzicht auf automatische Tests" }, correctOptionIndex = 1 },
            new { questionText = $"Welches Tool wird für {request.Topic} typischerweise genutzt?", options = new[] { "Ein Editor oder CLI-Tool", "Ein Web-Browser", "Ein Compiler" }, correctOptionIndex = 0 }
        }
    };
    return Results.Json(fallbackQuiz);
});

// Complete Quiz: vergibt XP für richtige Antworten
app.MapPost("/api/quiz/complete", async (QuizCompleteRequest request, AppDbContext db) =>
{
    if (request.Total < 1 || request.Total > 10 || request.Correct < 0 || request.Correct > request.Total)
        return Results.BadRequest("Ungültiges Quiz-Ergebnis");

    var user = await db.Users.FindAsync(request.UserId);
    if (user == null) return Results.NotFound("User not found");

    var xpGained = request.Correct * Xp.QuizCorrectAnswer
        + (request.Correct == request.Total ? Xp.QuizPerfectBonus : 0);
    Xp.Award(user, xpGained);
    user.QuizzesCompleted++;
    await db.SaveChangesAsync();

    return Results.Ok(new { xpGained, points = user.Points, quizzesCompleted = user.QuizzesCompleted });
});

// Get Project Suggestions
app.MapGet("/api/projects/suggestions/{userId}", async (Guid userId, AppDbContext db) =>
{
    var profile = await db.SkillProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
    int avgLevel = profile != null ? (profile.DotNetLevel + profile.AngularLevel + profile.ArchitectureLevel) / 3 : 3;

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    try
    {
        var response = await httpClient.PostAsJsonAsync($"{lmStudioUrl}/v1/chat/completions", new
        {
            model = lmStudioModel,
            messages = new[]
            {
                new { role = "system", content = "You are a coding career advisor. Generate exactly 3 portfolio project ideas with MIXED difficulty, in this order: one 'Einfach', one 'Mittel' and one 'Schwer' project. Base them on the user's skill levels (each on a scale of 1-10): the easy project should be about 3-4 levels below the user's average, the medium project about at the average, and the hard project about 1-2 levels above it (all values between 1 and 10). Keep descriptions to 2-3 sentences. Write the title, description and difficulty text in German (Deutsch). The difficulty must be exactly 'Einfach', 'Mittel' or 'Schwer' followed by the numeric level in brackets, e.g. 'Mittel (5/10)'. Output ONLY a valid raw JSON array of objects without markdown blocks. Format: [{\"title\":\"...\",\"description\":\"...\",\"difficulty\":\"...\"}]" },
                new { role = "user", content = profile != null
                    ? $"Skill levels (1-10): .NET {profile.DotNetLevel}, Angular {profile.AngularLevel}, Architecture {profile.ArchitectureLevel}, Testing {profile.TestingLevel}, Cloud {profile.CloudLevel}. Average: {avgLevel}/10. Suggest 3 projects."
                    : $"Suggest 3 projects for level {avgLevel} out of 10." }
            },
            temperature = 0.5
        });

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var rawJson = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            rawJson = rawJson.Replace("```json", "").Replace("```", "").Trim();
            return Results.Content(rawJson, "application/json");
        }
    }
    catch { /* Fallback */ }

    var fallbackProjects = new[]
    {
        new { title = "Personal Portfolio", description = "Baue eine Webseite mit Angular v22, um deine Skills zu präsentieren.", difficulty = "Einfach" },
        new { title = "Task Manager API", description = "Implementiere eine REST-API mit .NET 10 und PostgreSQL.", difficulty = "Mittel" },
        new { title = "Distributed Microservices App", description = "Entwickle eine App mit Aspire.NET und Event-Driven Architecture.", difficulty = "Schwer" }
    };
    return Results.Json(fallbackProjects);
});

app.Run();

// DTOs
public record AssessmentRequest(Guid UserId, int DotNetLevel, int AngularLevel, int ArchitectureLevel, int TestingLevel, int CloudLevel);
public record SubmissionRequest(Guid UserId, string Code);
public record QuizRequest(string Topic, int Level);
public record QuizCompleteRequest(Guid UserId, int Correct, int Total);

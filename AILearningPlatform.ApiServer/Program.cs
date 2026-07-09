using AILearningPlatform.Data;
using AILearningPlatform.Data.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddOpenApi();
builder.AddSqlServerDbContext<AppDbContext>("sqldata");
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

var app = builder.Build();

app.UseCors("AllowAll");
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Automatically initialize the database (migrations/creation)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    
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
    var module = await db.CourseModules.FindAsync(moduleId);
    if (module == null) return Results.NotFound("Module not found");

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
        var response = await httpClient.PostAsJsonAsync("http://localhost:1234/v1/chat/completions", new
        {
            model = "local-model",
            messages = new[]
            {
                new { role = "system", content = "You are a quiz generator. Output ONLY a valid raw JSON array of 3 questions without markdown blocks. Format: [{\"questionText\":\"...\",\"options\":[\"...\",\"...\"],\"correctOptionIndex\":0}]" },
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

// Get Project Suggestions
app.MapGet("/api/projects/suggestions/{userId}", async (Guid userId, AppDbContext db) =>
{
    var profile = await db.SkillProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
    int avgLevel = profile != null ? (profile.DotNetLevel + profile.AngularLevel + profile.ArchitectureLevel) / 3 : 3;

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    try
    {
        var response = await httpClient.PostAsJsonAsync("http://localhost:1234/v1/chat/completions", new
        {
            model = "local-model",
            messages = new[]
            {
                new { role = "system", content = "You are a coding career advisor. Generate 3 project ideas matching the user's level. Output ONLY a valid raw JSON array of objects without markdown blocks. Format: [{\"title\":\"...\",\"description\":\"...\",\"difficulty\":\"...\"}]" },
                new { role = "user", content = $"Suggest 3 projects for level {avgLevel} out of 10." }
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

var builder = DistributedApplication.CreateBuilder(args);

// SQLite is a plain file, not a server process, so it needs no Aspire-hosted resource -
// just a shared connection string pointing both services at the same file.
var sqliteConnectionString = "Data Source=../sqldata.db";

// Optional: Claude statt LM Studio. Aktivieren mit `dotnet user-secrets set UseClaude true`
// (oder Umgebungsvariable UseClaude=true). Die Bridge nutzt das Claude Agent SDK und den
// lokalen `claude login` bzw. ANTHROPIC_API_KEY.
var useClaude = bool.TryParse(builder.Configuration["UseClaude"], out var uc) && uc;
const string claudeBridgeUrl = "http://localhost:1235";

if (useClaude)
{
    builder.AddNpmApp("claudebridge", "../AILearningPlatform.ClaudeBridge", "start")
        .WithEnvironment("PORT", "1235");
}

var apiService = builder.AddProject<Projects.AILearningPlatform_ApiServer>("apiservice")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithEnvironment("ConnectionStrings__sqldata", sqliteConnectionString);

var evaluationWorker = builder.AddProject<Projects.AILearningPlatform_EvaluationWorker>("evaluationworker")
    .WithEnvironment("ConnectionStrings__sqldata", sqliteConnectionString);

if (useClaude)
{
    apiService.WithEnvironment("LmStudio__BaseUrl", claudeBridgeUrl);
    evaluationWorker.WithEnvironment("LmStudio__BaseUrl", claudeBridgeUrl);
}

var frontend = builder.AddNpmApp("frontend", "../AILearningPlatform.Frontend", "start")
    .WithReference(apiService)
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();

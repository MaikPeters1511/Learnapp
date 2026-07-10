var builder = DistributedApplication.CreateBuilder(args);

// SQLite is a plain file, not a server process, so it needs no Aspire-hosted resource -
// just a shared connection string pointing both services at the same file.
var sqliteConnectionString = "Data Source=../sqldata.db";

var apiService = builder.AddProject<Projects.AILearningPlatform_ApiServer>("apiservice")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithEnvironment("ConnectionStrings__sqldata", sqliteConnectionString);

var evaluationWorker = builder.AddProject<Projects.AILearningPlatform_EvaluationWorker>("evaluationworker")
    .WithEnvironment("ConnectionStrings__sqldata", sqliteConnectionString);

var frontend = builder.AddNpmApp("frontend", "../AILearningPlatform.Frontend", "start")
    .WithReference(apiService)
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();

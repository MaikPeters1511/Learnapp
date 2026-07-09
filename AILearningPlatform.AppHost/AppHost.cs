var builder = DistributedApplication.CreateBuilder(args);

var sqlserver = builder.AddSqlServer("sqlserver")
    .WithDataVolume()
    .AddDatabase("sqldata");

var apiService = builder.AddProject<Projects.AILearningPlatform_ApiServer>("apiservice")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithReference(sqlserver)
    .WaitFor(sqlserver);

var evaluationWorker = builder.AddProject<Projects.AILearningPlatform_EvaluationWorker>("evaluationworker")
    .WithReference(sqlserver)
    .WaitFor(sqlserver);

var frontend = builder.AddNpmApp("frontend", "../AILearningPlatform.Frontend", "start")
    .WithReference(apiService)
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();

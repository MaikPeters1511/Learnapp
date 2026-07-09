using Microsoft.EntityFrameworkCore;
using AILearningPlatform.Data;
using AILearningPlatform.EvaluationWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<AppDbContext>("sqldata");
builder.Services.AddHttpClient();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

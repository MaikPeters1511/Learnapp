FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AILearningPlatform.EvaluationWorker/AILearningPlatform.EvaluationWorker.csproj AILearningPlatform.EvaluationWorker/
COPY AILearningPlatform.ServiceDefaults/AILearningPlatform.ServiceDefaults.csproj AILearningPlatform.ServiceDefaults/
COPY AILearningPlatform.Data/AILearningPlatform.Data.csproj AILearningPlatform.Data/
RUN dotnet restore AILearningPlatform.EvaluationWorker/AILearningPlatform.EvaluationWorker.csproj

COPY AILearningPlatform.EvaluationWorker/ AILearningPlatform.EvaluationWorker/
COPY AILearningPlatform.ServiceDefaults/ AILearningPlatform.ServiceDefaults/
COPY AILearningPlatform.Data/ AILearningPlatform.Data/

RUN dotnet publish AILearningPlatform.EvaluationWorker/AILearningPlatform.EvaluationWorker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AILearningPlatform.EvaluationWorker.dll"]

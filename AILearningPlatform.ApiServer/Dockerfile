FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AILearningPlatform.ApiServer/AILearningPlatform.ApiServer.csproj AILearningPlatform.ApiServer/
COPY AILearningPlatform.ServiceDefaults/AILearningPlatform.ServiceDefaults.csproj AILearningPlatform.ServiceDefaults/
COPY AILearningPlatform.Data/AILearningPlatform.Data.csproj AILearningPlatform.Data/
RUN dotnet restore AILearningPlatform.ApiServer/AILearningPlatform.ApiServer.csproj

COPY AILearningPlatform.ApiServer/ AILearningPlatform.ApiServer/
COPY AILearningPlatform.ServiceDefaults/ AILearningPlatform.ServiceDefaults/
COPY AILearningPlatform.Data/ AILearningPlatform.Data/

RUN dotnet publish AILearningPlatform.ApiServer/AILearningPlatform.ApiServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AILearningPlatform.ApiServer.dll"]

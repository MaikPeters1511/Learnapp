# AI Learning Platform

Diese Repository enthält die AI Learning Platform, eine moderne Webanwendung zur Verwaltung und Durchführung von KI-gestützten Lernpfaden. Das Projekt nutzt .NET Aspire zur Orchestrierung von Microservices, ein Angular-Frontend und einen SQL Server als Datenquelle.

## Projektübersicht

Die Plattform besteht aus mehreren Komponenten:
- **AppHost**: Der .NET Aspire Orchestrator, der alle Dienste startet und verwaltet.
- **ApiServer**: Ein ASP.NET Core Minimal API Backend für die Geschäftslogik und den Datenzugriff.
- **Frontend**: Eine moderne Angular-Webanwendung (v22).
- **EvaluationWorker**: Ein Hintergrunddienst zur Verarbeitung von Auswertungen.
- **ServiceDefaults**: Gemeinsame Konfigurationen und Erweiterungen für die .NET-Dienste.

## Voraussetzungen

Um dieses Projekt lokal auszuführen, werden folgende Werkzeuge benötigt:
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (Version passend für Angular 22, empfohlen v20+)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (erforderlich für .NET Aspire Ressourcen wie SQL Server)
- [Angular CLI](https://angular.dev/tools/cli) (optional, `npm` reicht aus)

## Setup und Start

### Gesamtes System starten (mit .NET Aspire)

Der einfachste Weg, das gesamte System inklusive Datenbank zu starten, ist über den AppHost:

```bash
dotnet run --project AILearningPlatform.AppHost
```

Dies startet das Aspire Dashboard, über das alle Dienste (API, Frontend, Worker) sowie der SQL Server Container überwacht werden können.

### Einzelne Komponenten (manuell)

**Frontend:**
```bash
cd AILearningPlatform.Frontend
npm install
npm start
```

**API Server:**
```bash
cd AILearningPlatform.ApiServer
dotnet run
```

## Skripte

### Frontend (`AILearningPlatform.Frontend`)
- `npm start`: Startet den Angular Development Server.
- `npm run build`: Erstellt die Produktions-Builds im `dist/` Verzeichnis.
- `npm test`: Führt Unit-Tests mit Vitest aus.
- `npm run watch`: Erstellt Builds im Watch-Modus für die Entwicklung.

### .NET Lösungen
- `dotnet build`: Kompiliert die gesamte Solution.
- `dotnet test`: Führt alle Tests in der Solution aus.

## Umgebungsvariablen

Das Projekt nutzt die Standard-Konfigurationsmechanismen von .NET und Angular.

- **AppHost**: Konfiguriert Endpunkte und Referenzen dynamisch.
- **Frontend**: Verwendet `PORT` (wird vom AppHost gesetzt) und kommuniziert mit dem `apiservice`.
- **API/Worker**: Verwenden Verbindungszeichenfolgen (Connection Strings), die über Aspire (`sqldata`) bereitgestellt werden.
- **OTEL_EXPORTER_OTLP_ENDPOINT**: Wird für Telemetriedaten (OpenTelemetry) verwendet.

*TODO: Weitere spezifische Umgebungsvariablen (z.B. API Keys für KI-Modelle) hier ergänzen.*

## Tests

- **Backend**: Unit- und Integrationstests befinden sich (falls vorhanden) in entsprechenden `.Tests` Projekten. (Aktuell keine dedizierten Testordner in der Root-Struktur sichtbar - *TODO: Testabdeckung prüfen*).
- **Frontend**: Nutzt Vitest für Unit-Tests. Ausführbar via `npm test` im Frontend-Verzeichnis.

## Projektstruktur

```text
.
├── AILearningPlatform.ApiServer/       # Backend API (ASP.NET Core)
├── AILearningPlatform.AppHost/         # .NET Aspire Orchestrator
├── AILearningPlatform.EvaluationWorker/# Hintergrunddienst
├── AILearningPlatform.Frontend/        # Angular Frontend
├── AILearningPlatform.ServiceDefaults/ # Gemeinsame .NET Konfigurationen
└── AILearningPlatform.slnx             # Solution Datei
```

## Lizenz

Dieses Projekt steht unter der [MIT-Lizenz](LICENSE).

---
*Zuletzt aktualisiert: 30. Juni 2026*

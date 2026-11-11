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
- **Auth:JwtKey** (`Auth__JwtKey`): Geheimer Schlüssel zum Signieren der Anmelde-Tokens, mindestens 32 zufällige Zeichen. Siehe Abschnitt „Anmeldung“.
- **Auth:AttemptsPerMinute**: Erlaubte Login-/Registrierungsversuche pro IP und Minute (Standard 10).

*TODO: Weitere spezifische Umgebungsvariablen (z.B. API Keys für KI-Modelle) hier ergänzen.*

## Anmeldung

Benutzer registrieren sich mit Name, E-Mail und Passwort. Passwörter werden mit PBKDF2 (ASP.NET `PasswordHasher`) gesalzen gehasht gespeichert. Die Anmeldung läuft über ein JWT (HS256, 12 Stunden gültig) im `Authorization`-Header. Alle Endpunkte unter `/api` außer `/api/auth/login` und `/api/auth/register` verlangen eine Anmeldung, und jeder Benutzer sieht und ändert nur seine eigenen Daten. Login und Registrierung sind pro IP-Adresse begrenzt.

**Signierschlüssel einrichten** (einmalig, bleibt über Neustarts gleich):

```bash
dotnet user-secrets set "Auth:JwtKey" "$(openssl rand -base64 48)" --project AILearningPlatform.AppHost
```

Ohne Schlüssel nutzt der ApiServer in Development einen zufälligen pro Start, dann enden alle Anmeldungen beim Neustart. Außerhalb von Development startet der ApiServer ohne Schlüssel nicht. Im `docker-compose.yml` muss `AUTH_JWT_KEY` gesetzt sein (z. B. in einer `.env`-Datei).

Hinter einem Reverse-Proxy (etwa dem nginx des Frontend-Containers) sieht die API nur die Adresse des Proxys, das Rate-Limit gilt dann für alle zusammen. Dafür müssten die `X-Forwarded-For`-Header ausgewertet werden.

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

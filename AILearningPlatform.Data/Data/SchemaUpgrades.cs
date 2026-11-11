using Microsoft.EntityFrameworkCore;

namespace AILearningPlatform.Data;

/// <summary>
/// Das Projekt nutzt EnsureCreated() statt Migrationen. EnsureCreated legt in einer bereits
/// bestehenden SQLite-Datei keine neuen Spalten oder Indizes an, deshalb ergänzen wir sie hier idempotent.
/// Frische Datenbanken bekommen Tabellen und Spalten schon aus dem Modell; Indizes legen wir
/// immer hier an, damit sie in frischen und bestehenden Datenbanken gleich aussehen.
/// </summary>
public static class SchemaUpgrades
{
    private const string UniqueEmailIndex = "UX_Users_Email_NoCase";

    /// <param name="warn">Optionaler Rückruf für Hinweise, z. B. ein Logger.</param>
    public static void Apply(AppDbContext db, Action<string>? warn = null)
    {
        if (!TableExists(db, "Users")) return;

        // Users.QuizzesCompleted: serverseitiger Zähler für abgeschlossene Quizze.
        if (!ColumnExists(db, "Users", "QuizzesCompleted"))
        {
            Execute(db, "ALTER TABLE \"Users\" ADD COLUMN \"QuizzesCompleted\" INTEGER NOT NULL DEFAULT 0");
        }

        // Users.PasswordHash: gehashtes Passwort für die Anmeldung (null = Konto ohne Anmeldung).
        if (!ColumnExists(db, "Users", "PasswordHash"))
        {
            Execute(db, "ALTER TABLE \"Users\" ADD COLUMN \"PasswordHash\" TEXT NULL");
        }

        // Eindeutige E-Mail, ohne Beachtung der Groß-/Kleinschreibung (passend zur Prüfung im API-Endpunkt).
        if (!IndexExists(db, UniqueEmailIndex))
        {
            var duplicateGroups = db.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM (SELECT 1 FROM \"Users\" GROUP BY lower(\"Email\") HAVING COUNT(*) > 1)")
                .AsEnumerable().First();

            if (duplicateGroups > 0)
            {
                // Bestehende Daten nie verändern oder löschen: lieber ohne Index weiterlaufen.
                warn?.Invoke(
                    $"Eindeutiger E-Mail-Index nicht angelegt: {duplicateGroups} E-Mail-Adresse(n) kommen mehrfach vor. " +
                    "Bitte die Dubletten in der Tabelle Users bereinigen, der Index wird beim nächsten Start angelegt.");
            }
            else
            {
                Execute(db, $"CREATE UNIQUE INDEX IF NOT EXISTS \"{UniqueEmailIndex}\" ON \"Users\" (\"Email\" COLLATE NOCASE)");
            }
        }
    }

    private static bool TableExists(AppDbContext db, string table) =>
        db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table})")
            .AsEnumerable().First() > 0;

    private static bool ColumnExists(AppDbContext db, string table, string column) =>
        db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
            .AsEnumerable().First() > 0;

    private static bool IndexExists(AppDbContext db, string index) =>
        db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = {index}")
            .AsEnumerable().First() > 0;

    // DDL lässt sich nicht parametrisieren; alle Statements sind feste Zeichenketten aus diesem Code.
    private static void Execute(AppDbContext db, string statement)
    {
        try
        {
            db.Database.ExecuteSqlRaw(statement);
        }
        catch (Exception ex) when (
            ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // ApiServer und EvaluationWorker starten gleichzeitig; der andere war schneller.
        }
    }
}

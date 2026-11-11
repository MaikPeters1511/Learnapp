using Microsoft.EntityFrameworkCore;

namespace AILearningPlatform.Data;

/// <summary>
/// Das Projekt nutzt EnsureCreated() statt Migrationen. EnsureCreated legt in einer bereits
/// bestehenden SQLite-Datei keine neuen Spalten an, deshalb ergänzen wir sie hier idempotent.
/// Frische Datenbanken bekommen alle Spalten schon aus dem Modell.
/// </summary>
public static class SchemaUpgrades
{
    public static void Apply(AppDbContext db)
    {
        // Users.QuizzesCompleted: serverseitiger Zähler für abgeschlossene Quizze.
        if (TableExists(db, "Users") && !ColumnExists(db, "Users", "QuizzesCompleted"))
        {
            AddColumn(db, "ALTER TABLE \"Users\" ADD COLUMN \"QuizzesCompleted\" INTEGER NOT NULL DEFAULT 0");
        }
    }

    private static bool TableExists(AppDbContext db, string table) =>
        db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table})")
            .AsEnumerable().First() > 0;

    private static bool ColumnExists(AppDbContext db, string table, string column) =>
        db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
            .AsEnumerable().First() > 0;

    private static void AddColumn(AppDbContext db, string alterStatement)
    {
        try
        {
            db.Database.ExecuteSqlRaw(alterStatement);
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // ApiServer und EvaluationWorker starten gleichzeitig; der andere war schneller.
        }
    }
}

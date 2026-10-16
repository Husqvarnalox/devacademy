using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace DevAcademy.Infrastructure;

/// <summary>Connection factory plus embedded-SQL migrations. Connections are not pooled: each use opens and closes the file.</summary>
public sealed class SqliteDatabase(string databasePath)
{
    public string DatabasePath { get; } = Path.GetFullPath(databasePath);

    public SqliteConnection Open() => Open(DatabasePath);

    internal static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            ForeignKeys = true,
        }.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    /// <summary>Highest migration version shipped in this assembly.</summary>
    public static int LatestVersion => LoadMigrations().Max(m => m.Version);

    /// <summary>Applies pending migrations in order, each in its own transaction. Safe to call repeatedly.</summary>
    public void Migrate()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        using var connection = Open();
        Exec(connection, "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL)");

        var applied = new HashSet<int>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT version FROM schema_migrations";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) applied.Add(reader.GetInt32(0));
        }

        var migrations = LoadMigrations();
        var unknown = applied.Where(v => migrations.All(m => m.Version != v)).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException($"Database was created by a newer DevAcademy (unknown migration {unknown.Max()}); refusing to open it");

        foreach (var m in migrations.Where(m => !applied.Contains(m.Version)))
        {
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = m.Sql;
            cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO schema_migrations (version, applied_at) VALUES ($v, $t)";
            cmd.Parameters.AddWithValue("$v", m.Version);
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
            tx.Commit();
        }
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private sealed record Migration(int Version, string Sql);

    private static List<Migration> LoadMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var list = new List<Migration>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Migrations/", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal)))
        {
            var file = name["Migrations/".Length..];
            var digits = new string(file.TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                throw new InvalidOperationException($"Migration resource '{name}' must start with a version number");
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            list.Add(new Migration(version, reader.ReadToEnd()));
        }
        if (list.Count == 0) throw new InvalidOperationException("No migrations embedded in DevAcademy.Infrastructure");
        return list.OrderBy(m => m.Version).ToList();
    }
}

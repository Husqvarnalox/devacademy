using Microsoft.Data.Sqlite;

namespace DevAcademy.Infrastructure;

public sealed class BackupService(SqliteDatabase db)
{
    /// <summary>SQLite online backup into a single consistent file, moved into place atomically.</summary>
    public async Task BackupAsync(string destinationFile, CancellationToken ct = default)
    {
        var dest = Path.GetFullPath(destinationFile);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var temp = dest + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var source = db.Open())
            await using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false }.ToString()))
            {
                target.Open();
                source.BackupDatabase(target);
            }
            File.Move(temp, dest, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates the backup (readable SQLite, integrity_check ok, known migrations only), then replaces the
    /// live database file and migrates it. Callers must not hold their own open connections.
    /// </summary>
    public void Restore(string backupFile)
    {
        var source = Path.GetFullPath(backupFile);
        Validate(source);

        SqliteConnection.ClearAllPools();
        var target = db.DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".restore";
        File.Copy(source, temp, overwrite: true);
        try
        {
            // Stale WAL/SHM files of the old database must not be replayed onto the restored file.
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(target + suffix)) File.Delete(target + suffix);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        db.Migrate();
    }

    private static void Validate(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Backup file not found", file);
        try
        {
            using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            c.Open();
            using var integrity = c.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check";
            if (integrity.ExecuteScalar() as string != "ok") throw new InvalidDataException("Backup failed the SQLite integrity check");

            using var version = c.CreateCommand();
            version.CommandText = "SELECT MAX(version) FROM schema_migrations";
            if (version.ExecuteScalar() is not long v) throw new InvalidDataException("Backup has no migration history; not a DevAcademy database");
            if (v > SqliteDatabase.LatestVersion) throw new InvalidDataException($"Backup was created by a newer DevAcademy (schema version {v})");
        }
        catch (SqliteException ex)
        {
            throw new InvalidDataException($"Not a valid DevAcademy backup: {ex.Message}", ex);
        }
    }
}

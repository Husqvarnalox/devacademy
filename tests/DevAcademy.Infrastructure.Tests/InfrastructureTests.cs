using DevAcademy.Domain;
using DevAcademy.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DevAcademy.Infrastructure.Tests;

public sealed class TempDb : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "da-infra-" + Guid.NewGuid().ToString("N"));
    public SqliteDatabase Db { get; }

    public TempDb(bool migrate = true)
    {
        Directory.CreateDirectory(Dir);
        Db = new SqliteDatabase(Path.Combine(Dir, "test.db"));
        if (migrate) Db.Migrate();
    }

    public long Scalar(string sql)
    {
        using var c = Db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(Dir, true); } catch (IOException) { } }
}

public class MigrationTests
{
    [Fact]
    public void AppliesAllMigrationsOnEmptyDatabase()
    {
        using var t = new TempDb();
        var tables = new[] { "settings", "learner_profile", "enrolled_courses", "lesson_progress", "submissions", "quiz_attempts", "drafts", "revealed_hints", "ai_requests", "schema_migrations" };
        foreach (var table in tables)
            Assert.Equal(1, t.Scalar($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'"));
        Assert.Equal(SqliteDatabase.LatestVersion, t.Scalar("SELECT MAX(version) FROM schema_migrations"));
    }

    [Fact]
    public void IsIdempotent()
    {
        using var t = new TempDb();
        t.Db.Migrate();
        t.Db.Migrate();
        Assert.Equal(SqliteDatabase.LatestVersion, t.Scalar("SELECT COUNT(*) FROM schema_migrations")); // versions are sequential from 1
    }

    [Fact]
    public void UsesWalAndForeignKeys()
    {
        using var t = new TempDb();
        using var c = t.Db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode";
        Assert.Equal("wal", cmd.ExecuteScalar());
        cmd.CommandText = "PRAGMA foreign_keys";
        Assert.Equal(1L, cmd.ExecuteScalar());
    }

    [Fact]
    public void RefusesDatabaseFromNewerVersion()
    {
        using var t = new TempDb();
        using (var c = t.Db.Open())
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO schema_migrations VALUES (9999, 'x')";
            cmd.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(() => t.Db.Migrate());
    }
}

public class RepositoryTests
{
    private static readonly DateTimeOffset T = new(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);

    [Fact]
    public async Task ProgressRoundTripAndUpsert()
    {
        using var t = new TempDb();
        var repo = new SqliteProgressRepository(t.Db);
        Assert.Null(await repo.GetAsync("l1"));
        var p = new LessonProgress("l1", T, T.AddHours(1), 0.75, true, true, false, 5, 2, 3, T.AddDays(16), T.AddDays(1), CompletionSource.Assessment);
        await repo.SaveAsync(p);
        Assert.Equal(p, await repo.GetAsync("l1"));

        var changed = p with { Attempts = 6, QuizBestScore = null, NextReviewAt = null, CompletionSource = null };
        await repo.SaveAsync(changed);
        Assert.Equal(changed, await repo.GetAsync("l1"));
        await repo.SaveAsync(new LessonProgress("a0"));
        Assert.Equal(["a0", "l1"], (await repo.GetAllAsync()).Select(x => x.LessonId));
    }

    [Fact]
    public async Task QuizAttemptsRoundTripInOrder()
    {
        using var t = new TempDb();
        var repo = new SqliteProgressRepository(t.Db);
        await repo.AddQuizAttemptAsync(new QuizAttempt("l1", 0.5, false, T));
        await repo.AddQuizAttemptAsync(new QuizAttempt("l1", 1.0, true, T.AddMinutes(1)));
        await repo.AddQuizAttemptAsync(new QuizAttempt("l2", 1.0, true, T));
        var list = await repo.GetQuizAttemptsAsync("l1");
        Assert.Equal([0.5, 1.0], list.Select(a => a.Score));
        Assert.Equal(T, list[0].At);
    }

    [Fact]
    public async Task SubmissionsRoundTripAndDuplicateIdIgnored()
    {
        using var t = new TempDb();
        var repo = new SqliteSubmissionRepository(t.Db);
        var s = new Submission("s1", "l1", "e1", "python", [new("main.py", "print('é')"), new("util/x.py", "")], Verdict.Failed, 1, 3, TimeSpan.FromMilliseconds(250), T);
        Assert.True(await repo.AddAsync(s));
        Assert.False(await repo.AddAsync(s with { Verdict = Verdict.Passed }));
        var back = Assert.Single(await repo.GetAllAsync());
        Assert.Equal(s.Verdict, back.Verdict);
        Assert.Equal(s.Files, back.Files);
        Assert.Equal((s.Passed, s.Total, s.Duration, s.At), (back.Passed, back.Total, back.Duration, back.At));
        Assert.Single(await repo.GetByLessonAsync("l1"));
        Assert.Empty(await repo.GetByLessonAsync("other"));
    }

    [Fact]
    public async Task SettingsProfileAndHintsRoundTrip()
    {
        using var t = new TempDb();
        var repo = new SqliteSettingsRepository(t.Db);
        Assert.Null(await repo.GetAsync("theme"));
        await repo.SetAsync("theme", "dark");
        await repo.SetAsync("theme", "light");
        Assert.Equal("light", await repo.GetAsync("theme"));
        Assert.Equal("light", (await repo.GetAllAsync())["theme"]);

        Assert.Null(await repo.GetProfileAsync());
        var profile = new LearnerProfile("get a job", 3, LearningDepth.Deep, ["js", "py", "ts"], "de");
        await repo.SaveProfileAsync(profile);
        var back = (await repo.GetProfileAsync())!;
        Assert.Equal(profile with { EnrolledCourseIds = [] }, back with { EnrolledCourseIds = [] });
        Assert.Equal(["js", "py", "ts"], back.EnrolledCourseIds);
        await repo.SaveProfileAsync(profile with { EnrolledCourseIds = ["py"] });
        Assert.Equal(["py"], (await repo.GetProfileAsync())!.EnrolledCourseIds);

        Assert.Equal(0, await repo.GetRevealedHintLevelAsync("e1"));
        await repo.SetRevealedHintLevelAsync("e1", 2);
        await repo.SetRevealedHintLevelAsync("e1", 3);
        Assert.Equal(3, await repo.GetRevealedHintLevelAsync("e1"));
    }

    [Fact]
    public async Task DraftsRoundTrip()
    {
        using var t = new TempDb();
        var repo = new SqliteDraftRepository(t.Db);
        await repo.SaveAsync("e1", "main.py", "v1");
        await repo.SaveAsync("e1", "main.py", "v2");
        await repo.SaveAsync("e1", "lib.py", "x");
        await repo.SaveAsync("e2", "main.py", "other");
        Assert.Equal("v2", await repo.GetAsync("e1", "main.py"));
        Assert.Null(await repo.GetAsync("e1", "nope.py"));
        Assert.Equal(["lib.py", "main.py"], (await repo.GetAllAsync("e1")).Keys);
        await repo.DeleteAsync("e1");
        Assert.Empty(await repo.GetAllAsync("e1"));
        Assert.Equal("other", await repo.GetAsync("e2", "main.py"));
    }
}

public class BackupTests
{
    [Fact]
    public async Task BackupThenRestoreBringsBackOldState()
    {
        using var t = new TempDb();
        var settings = new SqliteSettingsRepository(t.Db);
        await settings.SetAsync("k", "before");
        var backupFile = Path.Combine(t.Dir, "out", "backup.db");
        var backup = new BackupService(t.Db);
        await backup.BackupAsync(backupFile);
        Assert.True(File.Exists(backupFile));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(backupFile)!, "*.tmp"));

        await settings.SetAsync("k", "after");
        await settings.SetAsync("extra", "x");
        backup.Restore(backupFile);

        Assert.Equal("before", await settings.GetAsync("k"));
        Assert.Null(await settings.GetAsync("extra"));
    }

    [Fact]
    public async Task BackupOverwritesExistingFile()
    {
        using var t = new TempDb();
        var settings = new SqliteSettingsRepository(t.Db);
        var file = Path.Combine(t.Dir, "b.db");
        var backup = new BackupService(t.Db);
        await settings.SetAsync("k", "1");
        await backup.BackupAsync(file);
        await settings.SetAsync("k", "2");
        await backup.BackupAsync(file);
        await settings.SetAsync("k", "3");
        backup.Restore(file);
        Assert.Equal("2", await settings.GetAsync("k"));
    }

    [Fact]
    public async Task RestoreRejectsInvalidFilesAndKeepsCurrentData()
    {
        using var t = new TempDb();
        var settings = new SqliteSettingsRepository(t.Db);
        await settings.SetAsync("k", "keep");
        var backup = new BackupService(t.Db);

        var garbage = Path.Combine(t.Dir, "garbage.db");
        File.WriteAllText(garbage, "this is not sqlite");
        Assert.Throws<InvalidDataException>(() => backup.Restore(garbage));

        var foreign = Path.Combine(t.Dir, "foreign.db");
        using (var c = new SqliteConnection($"Data Source={foreign};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "CREATE TABLE x (a INTEGER)";
            cmd.ExecuteNonQuery();
        }
        Assert.Throws<InvalidDataException>(() => backup.Restore(foreign));
        Assert.Throws<FileNotFoundException>(() => backup.Restore(Path.Combine(t.Dir, "missing.db")));

        Assert.Equal("keep", await settings.GetAsync("k"));
    }
}

public class AppPathsAndContentTests
{
    [Fact]
    public void ExplicitDirectoryWins()
    {
        var p = new AppPaths(Path.Combine(Path.GetTempPath(), "da-paths"));
        Assert.EndsWith("da-paths", p.DataDir);
        Assert.Equal(Path.Combine(p.DataDir, "devacademy.db"), p.DatabasePath);
    }

    [Fact]
    public void EnvironmentOverrideAndOsDefault()
    {
        var old = Environment.GetEnvironmentVariable(AppPaths.DataDirVariable);
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "da-env");
            Environment.SetEnvironmentVariable(AppPaths.DataDirVariable, dir);
            Assert.Equal(Path.GetFullPath(dir), new AppPaths().DataDir);

            Environment.SetEnvironmentVariable(AppPaths.DataDirVariable, null);
            var def = new AppPaths().DataDir;
            Assert.Contains(OperatingSystem.IsMacOS() ? "Application Support" : OperatingSystem.IsWindows() ? "DevAcademy" : "devacademy", def);
        }
        finally { Environment.SetEnvironmentVariable(AppPaths.DataDirVariable, old); }
    }

    [Fact]
    public async Task AtomicWriteReplacesAndLeavesNoTempFiles()
    {
        using var t = new TempDb(migrate: false);
        var file = Path.Combine(t.Dir, "sub", "export.json");
        await AtomicFile.WriteAllTextAsync(file, "one");
        await AtomicFile.WriteAllTextAsync(file, "two");
        Assert.Equal("two", await File.ReadAllTextAsync(file));
        Assert.Equal([file], Directory.GetFiles(Path.GetDirectoryName(file)!));
    }

    [Fact]
    public void FileContentRepositoryLoadsAndReloads()
    {
        using var t = new TempDb(migrate: false);
        File.WriteAllText(Path.Combine(t.Dir, "catalog.yaml"), "tracks:\n  - id: py\n    kind: language\n    title: Python\n    status: planned\n");
        var repo = new FileContentRepository(t.Dir);
        Assert.Equal("py", Assert.Single(repo.Current.Tracks).Id);
        Assert.Empty(repo.Diagnostics);

        File.WriteAllText(Path.Combine(t.Dir, "catalog.yaml"), "tracks:\n  - id: a\n    kind: language\n    title: A\n    status: planned\n  - id: b\n    kind: language\n    title: B\n    status: planned\n");
        Assert.Single(repo.Current.Tracks);
        repo.Reload();
        Assert.Equal(2, repo.Current.Tracks.Count);
    }

    [Fact]
    public void MissingContentRootIsReportedAsDiagnosticNotException()
    {
        var repo = new FileContentRepository(Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid()));
        Assert.Empty(repo.Current.Tracks);
        Assert.Contains(repo.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}

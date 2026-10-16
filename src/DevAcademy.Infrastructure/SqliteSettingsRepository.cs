using DevAcademy.Application.Ports;
using DevAcademy.Domain;
using static DevAcademy.Infrastructure.SqlHelpers;

namespace DevAcademy.Infrastructure;

public sealed class SqliteSettingsRepository(SqliteDatabase db) : ISettingsRepository
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT value FROM settings WHERE key = $k", ("$k", key));
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd(
            "INSERT INTO settings (key, value) VALUES ($k, $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
            ("$k", key), ("$v", value));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT key, value FROM settings ORDER BY key");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await r.ReadAsync(ct)) map[r.GetString(0)] = r.GetString(1);
        return map;
    }

    public async Task<LearnerProfile?> GetProfileAsync(CancellationToken ct = default)
    {
        await using var c = db.Open();
        string goal, depth, ui;
        int level;
        await using (var cmd = c.Cmd("SELECT goal, level, depth, ui_language FROM learner_profile WHERE id = 1"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) return null;
            (goal, level, depth, ui) = (r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetString(3));
        }
        var courses = new List<string>();
        await using (var cmd = c.Cmd("SELECT course_id FROM enrolled_courses ORDER BY position"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) courses.Add(r.GetString(0));
        return new LearnerProfile(goal, level, Enum.Parse<LearningDepth>(depth), courses, ui);
    }

    public async Task SaveProfileAsync(LearnerProfile p, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var tx = await c.BeginTransactionAsync(ct);
        await using (var cmd = c.Cmd("""
            INSERT INTO learner_profile (id, goal, level, depth, ui_language) VALUES (1, $g, $l, $d, $u)
            ON CONFLICT(id) DO UPDATE SET goal = excluded.goal, level = excluded.level, depth = excluded.depth, ui_language = excluded.ui_language
            """, ("$g", p.Goal), ("$l", p.Level), ("$d", p.Depth.ToString()), ("$u", p.UiLanguage)))
        {
            cmd.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = c.Cmd("DELETE FROM enrolled_courses"))
        {
            cmd.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        var position = 0;
        foreach (var id in p.EnrolledCourseIds.Distinct())
        {
            await using var cmd = c.Cmd("INSERT INTO enrolled_courses (course_id, position) VALUES ($c, $p)", ("$c", id), ("$p", position++));
            cmd.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<int> GetRevealedHintLevelAsync(string exerciseId, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT level FROM revealed_hints WHERE exercise_id = $e", ("$e", exerciseId));
        return await cmd.ExecuteScalarAsync(ct) is long l ? (int)l : 0;
    }

    public async Task SetRevealedHintLevelAsync(string exerciseId, int level, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd(
            "INSERT INTO revealed_hints (exercise_id, level) VALUES ($e, $l) ON CONFLICT(exercise_id) DO UPDATE SET level = excluded.level",
            ("$e", exerciseId), ("$l", level));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

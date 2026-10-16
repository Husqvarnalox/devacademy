using DevAcademy.Application.Ports;
using static DevAcademy.Infrastructure.SqlHelpers;

namespace DevAcademy.Infrastructure;

public sealed class SqliteDraftRepository(SqliteDatabase db) : IDraftRepository
{
    public async Task<string?> GetAsync(string exerciseId, string filePath, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT content FROM drafts WHERE exercise_id = $e AND file_path = $p", ("$e", exerciseId), ("$p", filePath));
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    public async Task SaveAsync(string exerciseId, string filePath, string content, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("""
            INSERT INTO drafts (exercise_id, file_path, content, updated_at) VALUES ($e, $p, $c, $t)
            ON CONFLICT(exercise_id, file_path) DO UPDATE SET content = excluded.content, updated_at = excluded.updated_at
            """, ("$e", exerciseId), ("$p", filePath), ("$c", content), ("$t", Iso(DateTimeOffset.UtcNow)));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(string exerciseId, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT file_path, content FROM drafts WHERE exercise_id = $e ORDER BY file_path", ("$e", exerciseId));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await r.ReadAsync(ct)) map[r.GetString(0)] = r.GetString(1);
        return map;
    }

    public async Task DeleteAsync(string exerciseId, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("DELETE FROM drafts WHERE exercise_id = $e", ("$e", exerciseId));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

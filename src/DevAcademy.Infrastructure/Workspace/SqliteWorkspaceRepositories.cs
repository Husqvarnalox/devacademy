using DevAcademy.Application.Workspace;
using static DevAcademy.Infrastructure.SqlHelpers;

namespace DevAcademy.Infrastructure.Workspace;

public sealed class SqliteRecentProjectsRepository(SqliteDatabase db) : IRecentProjectsRepository
{
    public async Task<IReadOnlyList<RecentProject>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT path, last_opened_at FROM recent_projects ORDER BY last_opened_at DESC, path LIMIT $n", ("$n", limit));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RecentProject>();
        while (await r.ReadAsync(ct)) list.Add(new RecentProject(r.GetString(0), r.ReadRequiredDate(1)));
        return list;
    }

    public async Task TouchAsync(string path, DateTimeOffset when, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd(
            "INSERT INTO recent_projects (path, last_opened_at) VALUES ($p, $t) ON CONFLICT(path) DO UPDATE SET last_opened_at = excluded.last_opened_at",
            ("$p", Path.GetFullPath(path)), ("$t", Iso(when)));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveAsync(string path, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("DELETE FROM recent_projects WHERE path = $p", ("$p", Path.GetFullPath(path)));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteProjectSessionStore(SqliteDatabase db) : IProjectSessionStore
{
    public async Task<ProjectSession> LoadAsync(string projectPath, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT session_json FROM project_sessions WHERE project_path = $p", ("$p", Path.GetFullPath(projectPath)));
        return ProjectSession.FromJson(await cmd.ExecuteScalarAsync(ct) as string);
    }

    public async Task SaveAsync(string projectPath, ProjectSession session, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd(
            """
            INSERT INTO project_sessions (project_path, session_json, updated_at) VALUES ($p, $j, $t)
            ON CONFLICT(project_path) DO UPDATE SET session_json = excluded.session_json, updated_at = excluded.updated_at
            """,
            ("$p", Path.GetFullPath(projectPath)), ("$j", session.ToJson()), ("$t", Iso(DateTimeOffset.UtcNow)));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

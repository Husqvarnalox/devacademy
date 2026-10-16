using System.Text.Json;
using DevAcademy.Application.Ports;
using DevAcademy.Domain;
using Microsoft.Data.Sqlite;
using static DevAcademy.Infrastructure.SqlHelpers;

namespace DevAcademy.Infrastructure;

public sealed class SqliteSubmissionRepository(SqliteDatabase db) : ISubmissionRepository
{
    private const string Columns = "id, lesson_id, exercise_id, language_id, files_json, verdict, passed, total, duration_ms, submitted_at";

    public async Task<bool> AddAsync(Submission s, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd($"INSERT OR IGNORE INTO submissions ({Columns}) VALUES ($id, $l, $e, $lang, $files, $v, $p, $t, $ms, $at)",
            ("$id", s.Id), ("$l", s.LessonId), ("$e", s.ExerciseId), ("$lang", s.LanguageId),
            ("$files", JsonSerializer.Serialize(s.Files)), ("$v", s.Verdict.ToString()), ("$p", s.Passed), ("$t", s.Total),
            ("$ms", (long)s.Duration.TotalMilliseconds), ("$at", Iso(s.At)));
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken ct = default) => QueryAsync("", [], ct);

    public Task<IReadOnlyList<Submission>> GetByLessonAsync(string lessonId, CancellationToken ct = default) =>
        QueryAsync("WHERE lesson_id = $l", [("$l", lessonId)], ct);

    private async Task<IReadOnlyList<Submission>> QueryAsync(string where, (string, object?)[] ps, CancellationToken ct)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd($"SELECT {Columns} FROM submissions {where} ORDER BY submitted_at, id", ps);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Submission>();
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    private static Submission Map(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
        JsonSerializer.Deserialize<List<ExerciseFile>>(r.GetString(4)) ?? [],
        Enum.Parse<Verdict>(r.GetString(5)), r.GetInt32(6), r.GetInt32(7),
        TimeSpan.FromMilliseconds(r.GetInt64(8)), r.ReadRequiredDate(9));
}

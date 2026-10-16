using DevAcademy.Application.Ports;
using DevAcademy.Domain;
using Microsoft.Data.Sqlite;
using static DevAcademy.Infrastructure.SqlHelpers;

namespace DevAcademy.Infrastructure;

public sealed class SqliteProgressRepository(SqliteDatabase db) : IProgressRepository
{
    private const string Columns =
        "lesson_id, started_at, completed_at, quiz_best_score, exercise_passed, challenge_passed, theory_read, attempts, failed_attempts, review_step, next_review_at, last_reviewed_at, completion_source";

    public async Task<IReadOnlyList<LessonProgress>> GetAllAsync(CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd($"SELECT {Columns} FROM lesson_progress ORDER BY lesson_id");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LessonProgress>();
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<LessonProgress?> GetAsync(string lessonId, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd($"SELECT {Columns} FROM lesson_progress WHERE lesson_id = $id", ("$id", lessonId));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task SaveAsync(LessonProgress p, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd($"""
            INSERT INTO lesson_progress ({Columns})
            VALUES ($id, $started, $completed, $quiz, $ex, $ch, $theory, $att, $fail, $step, $next, $last, $src)
            ON CONFLICT(lesson_id) DO UPDATE SET
                started_at = excluded.started_at, completed_at = excluded.completed_at,
                quiz_best_score = excluded.quiz_best_score, exercise_passed = excluded.exercise_passed,
                challenge_passed = excluded.challenge_passed, theory_read = excluded.theory_read,
                attempts = excluded.attempts, failed_attempts = excluded.failed_attempts,
                review_step = excluded.review_step, next_review_at = excluded.next_review_at,
                last_reviewed_at = excluded.last_reviewed_at, completion_source = excluded.completion_source
            """,
            ("$id", p.LessonId), ("$started", Iso(p.StartedAt)), ("$completed", Iso(p.CompletedAt)),
            ("$quiz", p.QuizBestScore), ("$ex", p.ExercisePassed), ("$ch", p.ChallengePassed), ("$theory", p.TheoryRead),
            ("$att", p.Attempts), ("$fail", p.FailedAttempts), ("$step", p.ReviewStep),
            ("$next", Iso(p.NextReviewAt)), ("$last", Iso(p.LastReviewedAt)), ("$src", p.CompletionSource?.ToString()));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AddQuizAttemptAsync(QuizAttempt a, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd(
            "INSERT INTO quiz_attempts (lesson_id, score, passed, attempted_at) VALUES ($l, $s, $p, $t)",
            ("$l", a.LessonId), ("$s", a.Score), ("$p", a.Passed), ("$t", Iso(a.At)));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<QuizAttempt>> GetQuizAttemptsAsync(string lessonId, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd(
            "SELECT lesson_id, score, passed, attempted_at FROM quiz_attempts WHERE lesson_id = $l ORDER BY attempted_at, id", ("$l", lessonId));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<QuizAttempt>();
        while (await r.ReadAsync(ct)) list.Add(new QuizAttempt(r.GetString(0), r.GetDouble(1), r.GetBoolean(2), r.ReadRequiredDate(3)));
        return list;
    }

    private static LessonProgress Map(SqliteDataReader r) => new(
        r.GetString(0), r.ReadDate(1), r.ReadDate(2), r.IsDBNull(3) ? null : r.GetDouble(3),
        r.GetBoolean(4), r.GetBoolean(5), r.GetBoolean(6), r.GetInt32(7), r.GetInt32(8), r.GetInt32(9),
        r.ReadDate(10), r.ReadDate(11), r.IsDBNull(12) ? null : Enum.Parse<CompletionSource>(r.GetString(12)));
}

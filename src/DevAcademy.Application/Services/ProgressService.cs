using System.Text.Json;
using System.Text.Json.Serialization;
using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

/// <param name="CreditedLessonIds">Other lessons credited by assessment as a side effect (placement quiz), if any.</param>
public sealed record ProgressUpdate(LessonProgress Progress, bool JustCompleted, IReadOnlyList<string>? CreditedLessonIds = null);

public sealed record ImportSummary(int LessonsUpdated, int SubmissionsAdded, bool ProfileImported);

/// <param name="Completed">All completed lessons, including those credited by assessment.</param>
public sealed record CourseCompletion(string CourseId, string Title, int Completed, int CreditedByAssessment, int Total)
{
    public int CompletedByPractice => Completed - CreditedByAssessment;
}

public sealed record DayCount(DateOnly Day, int Submissions);

/// <param name="SubmissionsPerDay">The last 30 UTC days ending today, oldest first, zeros included.</param>
public sealed record DashboardStats(
    IReadOnlyList<CourseCompletion> Courses,
    IReadOnlyDictionary<Difficulty, int> SolvedExercisesByDifficulty,
    IReadOnlyList<DayCount> SubmissionsPerDay,
    int DueReviews);

public sealed class ProgressService(
    IContentRepository content,
    IProgressRepository progress,
    ISubmissionRepository submissions,
    ISettingsRepository settings,
    IClock clock)
{
    public const int ExportFormatVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ProgressUpdate> StartLessonAsync(string lessonId, CancellationToken ct = default)
    {
        RequireLesson(lessonId);
        var current = await GetOrNew(lessonId, ct);
        if (current.StartedAt is not null) return new(current, false);
        var updated = current with { StartedAt = clock.UtcNow };
        await progress.SaveAsync(updated, ct);
        return new(updated, false);
    }

    public async Task<(QuizResult Result, ProgressUpdate Update)> RecordQuizAttemptAsync(
        string lessonId, IReadOnlyDictionary<string, QuizAnswer> answers, CancellationToken ct = default)
    {
        var lesson = RequireLesson(lessonId);
        var quiz = lesson.Quiz ?? throw new InvalidOperationException($"Lesson '{lessonId}' has no quiz");
        var result = QuizGrader.Grade(quiz, answers);
        var now = clock.UtcNow;
        await progress.AddQuizAttemptAsync(new QuizAttempt(lessonId, result.Score, result.Passed, now), ct);

        var current = await GetOrNew(lessonId, ct);
        var best = Math.Max(current.QuizBestScore ?? 0, result.Score);
        var update = await SaveAsync(lesson, current with { QuizBestScore = best }, ct);
        if (result.Passed && lesson.AssessmentFor.Count > 0)
            update = update with { CreditedLessonIds = await RecordAssessmentAsync(lesson.AssessmentFor, true, ct) };
        return (result, update);
    }

    /// <summary>
    /// Persists the submission. Passed/Failed/CompileError count as attempts (failures before the first pass
    /// count as failed attempts); RuntimeMissing/Error are not the learner's fault and count as neither.
    /// </summary>
    public async Task<ProgressUpdate> RecordSubmissionAsync(
        string lessonId, string exerciseId, IReadOnlyList<ExerciseFile> files, EvaluationReport report, CancellationToken ct = default,
        string? languageId = null)
    {
        var lesson = RequireLesson(lessonId);
        var isMain = lesson.Exercise?.Id == exerciseId;
        var exercise = isMain ? lesson.Exercise : lesson.Challenge?.Id == exerciseId ? lesson.Challenge : null;
        if (exercise is null) throw new ArgumentException($"Lesson '{lessonId}' has no exercise '{exerciseId}'", nameof(exerciseId));

        // The submission records the language it was written in; progress itself stays keyed by lesson.
        var language = exercise.ForVariant(languageId ?? report.LanguageId).LanguageId;
        var now = clock.UtcNow;
        await submissions.AddAsync(new Submission(Guid.NewGuid().ToString("N"), lessonId, exerciseId, language, files,
            report.Status, report.PassedCount, report.TotalCount, report.Duration, now), ct);

        var current = await GetOrNew(lessonId, ct);
        var counted = report.Status is Verdict.Passed or Verdict.Failed or Verdict.CompileError;
        var passed = report.Status == Verdict.Passed;
        if (counted && isMain)
            current = current with
            {
                Attempts = current.Attempts + 1,
                FailedAttempts = current.FailedAttempts + (passed || current.ExercisePassed ? 0 : 1),
                ExercisePassed = current.ExercisePassed || passed,
            };
        else if (passed) current = current with { ChallengePassed = true };
        return await SaveAsync(lesson, current, ct);
    }

    /// <summary>
    /// Credits the lessons as known after a passed placement check (source Assessment). Already completed lessons
    /// are left untouched. Returns the ids newly credited; does nothing when <paramref name="passed"/> is false.
    /// </summary>
    public async Task<IReadOnlyList<string>> RecordAssessmentAsync(IEnumerable<string> lessonIds, bool passed, CancellationToken ct = default)
    {
        var credited = new List<string>();
        if (!passed) return credited;
        var now = clock.UtcNow;
        foreach (var id in lessonIds.Distinct())
        {
            RequireLesson(id);
            var current = await GetOrNew(id, ct);
            if (current.IsCompleted) continue;
            var review = ReviewSchedule.Initial(now);
            await progress.SaveAsync(current with
            {
                StartedAt = current.StartedAt ?? now,
                CompletedAt = now,
                CompletionSource = CompletionSource.Assessment,
                ReviewStep = review.Step,
                NextReviewAt = review.DueAt,
            }, ct);
            credited.Add(id);
        }
        return credited;
    }

    /// <summary>Only for lessons that have neither exercise nor quiz.</summary>
    public async Task<ProgressUpdate> MarkTheoryReadAsync(string lessonId, CancellationToken ct = default)
    {
        var lesson = RequireLesson(lessonId);
        if (lesson.Exercise is not null || lesson.Quiz is not null)
            throw new InvalidOperationException($"Lesson '{lessonId}' has an exercise or quiz and cannot be completed by reading");
        return await SaveAsync(lesson, (await GetOrNew(lessonId, ct)) with { TheoryRead = true }, ct);
    }

    public async Task<LessonProgress> RecordReviewAsync(string lessonId, bool passed, CancellationToken ct = default)
    {
        RequireLesson(lessonId);
        var current = await GetOrNew(lessonId, ct);
        if (!current.IsCompleted) throw new InvalidOperationException($"Lesson '{lessonId}' is not completed; nothing to review");
        var now = clock.UtcNow;
        var next = ReviewSchedule.AfterReview(current.ReviewStep, passed, now);
        var updated = current with { ReviewStep = next.Step, NextReviewAt = next.DueAt, LastReviewedAt = now };
        await progress.SaveAsync(updated, ct);
        return updated;
    }

    // ---- export / import ----

    public async Task<string> ExportAsync(CancellationToken ct = default)
    {
        var data = new ProgressExport(
            ExportFormatVersion,
            clock.UtcNow,
            await settings.GetProfileAsync(ct),
            (await progress.GetAllAsync(ct)).OrderBy(p => p.LessonId, StringComparer.Ordinal).ToList(),
            (await submissions.GetAllAsync(ct)).OrderBy(s => s.At).ThenBy(s => s.Id, StringComparer.Ordinal).ToList(),
            (await settings.GetAllAsync(ct)).Where(kv => !IsSecretKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
        return JsonSerializer.Serialize(data, Json);
    }

    /// <summary>
    /// Merge rules: per lesson keep the more advanced record (Completed over InProgress over untouched, then more attempts;
    /// ties keep local); submissions are unioned by id; profile and settings are only imported where missing locally.
    /// </summary>
    public async Task<ImportSummary> ImportAsync(string json, CancellationToken ct = default)
    {
        ProgressExport data;
        try
        {
            data = JsonSerializer.Deserialize<ProgressExport>(json, Json) ?? throw new InvalidDataException("Import file is empty");
        }
        catch (JsonException ex) { throw new InvalidDataException($"Import file is not valid: {ex.Message}", ex); }
        if (data.FormatVersion != ExportFormatVersion)
            throw new InvalidDataException($"Unsupported export formatVersion {data.FormatVersion} (expected {ExportFormatVersion})");

        var updated = 0;
        foreach (var incoming in data.Progress ?? [])
        {
            var local = await progress.GetAsync(incoming.LessonId, ct);
            if (local is not null && !IsMoreAdvanced(incoming, local)) continue;
            await progress.SaveAsync(incoming.IsCompleted && incoming.CompletionSource is null
                ? incoming with { CompletionSource = CompletionSource.Imported } : incoming, ct);
            updated++;
        }

        var added = 0;
        foreach (var s in data.Submissions ?? [])
            if (await submissions.AddAsync(s, ct)) added++;

        var profileImported = false;
        if (data.Profile is not null && await settings.GetProfileAsync(ct) is null)
        {
            await settings.SaveProfileAsync(data.Profile, ct);
            profileImported = true;
        }
        foreach (var (key, value) in data.Settings ?? [])
            if (!IsSecretKey(key) && await settings.GetAsync(key, ct) is null) await settings.SetAsync(key, value, ct);

        return new ImportSummary(updated, added, profileImported);
    }

    // ---- dashboard ----

    public async Task<DashboardStats> GetDashboardAsync(CancellationToken ct = default)
    {
        var set = content.Current;
        var all = await progress.GetAllAsync(ct);
        var done = all.Where(p => p.IsCompleted).Select(p => p.LessonId).ToHashSet(StringComparer.Ordinal);
        var credited = all.Where(p => p.IsCompleted && p.CompletionSource == CompletionSource.Assessment).Select(p => p.LessonId).ToHashSet(StringComparer.Ordinal);
        var now = clock.UtcNow;

        var courses = set.Courses.Select(c =>
        {
            var lessons = set.LessonsOf(c.Id).ToList();
            return new CourseCompletion(c.Id, c.Title, lessons.Count(l => done.Contains(l.Id)),
                lessons.Count(l => credited.Contains(l.Id)), lessons.Count);
        }).ToList();

        var solved = Enum.GetValues<Difficulty>().ToDictionary(d => d, _ => 0);
        foreach (var p in all.Where(p => p.ExercisePassed))
            if (set.FindLesson(p.LessonId) is { Exercise: not null } l) solved[l.Difficulty]++;

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var perDay = (await submissions.GetAllAsync(ct)).GroupBy(s => DateOnly.FromDateTime(s.At.UtcDateTime)).ToDictionary(g => g.Key, g => g.Count());
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29))
            .Select(d => new DayCount(d, perDay.GetValueOrDefault(d))).ToList();

        var due = all.Count(p => p.IsCompleted && p.NextReviewAt <= now);
        return new DashboardStats(courses, solved, days, due);
    }

    // ---- helpers ----

    internal static bool IsSecretKey(string key) =>
        new[] { "key", "secret", "token" }.Any(w => key.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static bool IsMoreAdvanced(LessonProgress incoming, LessonProgress local)
    {
        static int Rank(LessonProgress p) => p.IsCompleted ? 2 : p.StartedAt is not null ? 1 : 0;
        return Rank(incoming) != Rank(local) ? Rank(incoming) > Rank(local) : incoming.Attempts > local.Attempts;
    }

    private Lesson RequireLesson(string lessonId) =>
        content.Current.FindLesson(lessonId) ?? throw new ArgumentException($"Unknown lesson '{lessonId}'", nameof(lessonId));

    private async Task<LessonProgress> GetOrNew(string lessonId, CancellationToken ct) =>
        await progress.GetAsync(lessonId, ct) ?? new LessonProgress(lessonId);

    /// <summary>Marks the lesson started, applies the completion rule, and persists.</summary>
    private async Task<ProgressUpdate> SaveAsync(Lesson lesson, LessonProgress p, CancellationToken ct)
    {
        var now = clock.UtcNow;
        p = p with { StartedAt = p.StartedAt ?? now };
        var completes = !p.IsCompleted && CompletionRule.IsSatisfied(lesson, p);
        if (completes)
        {
            var review = ReviewSchedule.Initial(now);
            var source = lesson.Exercise is not null ? CompletionSource.Exercise : lesson.Quiz is not null ? CompletionSource.Quiz : CompletionSource.TheoryRead;
            p = p with { CompletedAt = now, CompletionSource = source, ReviewStep = review.Step, NextReviewAt = review.DueAt };
        }
        await progress.SaveAsync(p, ct);
        return new ProgressUpdate(p, completes);
    }

    private sealed record ProgressExport(
        int FormatVersion,
        DateTimeOffset ExportedAt,
        LearnerProfile? Profile,
        List<LessonProgress>? Progress,
        List<Submission>? Submissions,
        Dictionary<string, string>? Settings);
}

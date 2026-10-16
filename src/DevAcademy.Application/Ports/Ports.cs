using DevAcademy.Domain;

namespace DevAcademy.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface IContentRepository
{
    /// <summary>The loaded content (never null; empty if the content root could not be read).</summary>
    ContentSet Current { get; }

    /// <summary>Diagnostics of the last load.</summary>
    IReadOnlyList<ContentDiagnostic> Diagnostics { get; }

    void Reload();
}

public interface IProgressRepository
{
    Task<IReadOnlyList<LessonProgress>> GetAllAsync(CancellationToken ct = default);
    Task<LessonProgress?> GetAsync(string lessonId, CancellationToken ct = default);
    Task SaveAsync(LessonProgress progress, CancellationToken ct = default);
    Task AddQuizAttemptAsync(QuizAttempt attempt, CancellationToken ct = default);
    Task<IReadOnlyList<QuizAttempt>> GetQuizAttemptsAsync(string lessonId, CancellationToken ct = default);
}

public interface ISubmissionRepository
{
    /// <summary>Inserts the submission; returns false (and changes nothing) if the id already exists.</summary>
    Task<bool> AddAsync(Submission submission, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetByLessonAsync(string lessonId, CancellationToken ct = default);
}

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);

    Task<LearnerProfile?> GetProfileAsync(CancellationToken ct = default);
    Task SaveProfileAsync(LearnerProfile profile, CancellationToken ct = default);

    /// <summary>Highest hint level the learner has revealed for the exercise (0 = none).</summary>
    Task<int> GetRevealedHintLevelAsync(string exerciseId, CancellationToken ct = default);
    Task SetRevealedHintLevelAsync(string exerciseId, int level, CancellationToken ct = default);
}

public interface IDraftRepository
{
    Task<string?> GetAsync(string exerciseId, string filePath, CancellationToken ct = default);
    Task SaveAsync(string exerciseId, string filePath, string content, CancellationToken ct = default);
    /// <summary>All drafts of an exercise keyed by file path.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(string exerciseId, CancellationToken ct = default);
    Task DeleteAsync(string exerciseId, CancellationToken ct = default);
}

/// <summary>Metadata of one AI request. Never contains prompt or response text.</summary>
public sealed record AiRequestRecord(
    long Id,
    string Provider,
    string Model,
    string Scenario,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? InputTokens,
    int? OutputTokens,
    decimal? ApproxCostUsd,
    string Status,
    string? ErrorKind);

public interface IAiRequestRepository
{
    /// <summary>Stores the record (Id ignored) and returns the new id.</summary>
    Task<long> AddAsync(AiRequestRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<AiRequestRecord>> GetRecentAsync(int limit, CancellationToken ct = default);
    /// <summary>Sum of the approximate cost of requests started at or after <paramref name="since"/> (unknown costs count as 0).</summary>
    Task<decimal> SumCostSinceAsync(DateTimeOffset since, CancellationToken ct = default);
    /// <summary>"Delete AI history": removes every row. Returns the number of rows removed.</summary>
    Task<int> DeleteAllAsync(CancellationToken ct = default);
}

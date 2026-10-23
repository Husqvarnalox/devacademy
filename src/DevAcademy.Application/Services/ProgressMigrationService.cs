using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

/// <param name="Copied">(former id, new id) pairs whose progress was carried over by this run.</param>
/// <param name="Skipped">(former id, new id) pairs left alone because the new lesson already had progress.</param>
public sealed record ProgressMigrationReport(IReadOnlyList<(string From, string To)> Copied, IReadOnlyList<(string From, string To)> Skipped)
{
    public bool Changed => Copied.Count > 0;
}

/// <summary>
/// Carries progress over lesson renames declared with <c>formerIds</c>. Content authors declare a former id only when
/// the renamed lesson teaches and checks the same thing, so its completion is still evidence. Rules:
/// <list type="bullet">
/// <item>the record under the former id is copied to the new id unchanged (completion source, attempts, review state);</item>
/// <item>the former record is kept, so history is never lost and an older content version still finds it;</item>
/// <item>a new id that already has progress is never overwritten, which makes the run idempotent;</item>
/// <item>when several former ids of one lesson have progress, the most advanced record wins.</item>
/// </list>
/// Lessons replaced by content that is not equivalent get a new id without <c>formerIds</c>: their old records stay
/// stored as history and credit nothing.
/// </summary>
public sealed class ProgressMigrationService(IContentRepository content, IProgressRepository progress)
{
    public async Task<ProgressMigrationReport> MigrateAsync(CancellationToken ct = default)
    {
        var all = (await progress.GetAllAsync(ct)).ToDictionary(p => p.LessonId, StringComparer.Ordinal);
        var copied = new List<(string, string)>();
        var skipped = new List<(string, string)>();
        foreach (var lesson in content.Current.Lessons)
        {
            var sources = lesson.PreviousIds.Where(all.ContainsKey).Select(id => all[id]).ToList();
            if (sources.Count == 0) continue;
            if (all.ContainsKey(lesson.Id))
            {
                skipped.AddRange(sources.Select(s => (s.LessonId, lesson.Id)));
                continue;
            }
            var best = sources.OrderByDescending(Rank).ThenByDescending(s => s.Attempts).ThenBy(s => s.LessonId, StringComparer.Ordinal).First();
            var migrated = best with { LessonId = lesson.Id };
            await progress.SaveAsync(migrated, ct);
            all[lesson.Id] = migrated;
            copied.Add((best.LessonId, lesson.Id));
        }
        return new ProgressMigrationReport(copied, skipped);
    }

    private static int Rank(LessonProgress p) => p.IsCompleted ? 2 : p.StartedAt is not null ? 1 : 0;
}

using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

/// <summary>
/// Evidence about one competency. There is deliberately no "does not know" state: without evidence a competency is
/// <see cref="NotChecked"/>, and a failed check is <see cref="NeedsWork"/> only until it is passed or the covered lessons
/// are completed.
/// </summary>
public enum CompetencyState
{
    /// <summary>No evidence either way (the check was not attempted, or is in progress).</summary>
    NotChecked,
    /// <summary>The check was attempted and not passed: its quiz scored below the threshold or its exercise failed.</summary>
    NeedsWork,
    /// <summary>The check was passed, or every lesson it covers is completed.</summary>
    Confirmed,
}

/// <param name="Covered">The lessons the check credits (its <c>assessmentFor</c>), in content order.</param>
/// <param name="Gaps">Covered lessons not completed yet: what to study after a failed check.</param>
/// <param name="WaitingFor">Competencies listed in <c>after</c> that are not confirmed yet. The check can still be opened; it is just not suggested.</param>
public sealed record CompetencyStatus(
    Competency Competency,
    Lesson Check,
    CompetencyState State,
    IReadOnlyList<Lesson> Covered,
    IReadOnlyList<Lesson> Gaps,
    IReadOnlyList<Competency> WaitingFor,
    LessonProgress? CheckProgress)
{
    public bool QuizFailed => Check.Quiz is { } q && CheckProgress?.QuizBestScore is { } s && s < q.PassThreshold;
    public bool ExerciseFailed => Check.Exercise is not null && CheckProgress is { Attempts: > 0, ExercisePassed: false };
    public bool CheckPassed => CheckProgress?.IsCompleted == true;
    public int CoveredCompleted => Covered.Count - Gaps.Count;
    /// <summary>Suggested as the next check: not confirmed and nothing it waits for.</summary>
    public bool IsReady => State != CompetencyState.Confirmed && WaitingFor.Count == 0;
}

public sealed record DiagnosticReport(Track Track, TrackRoute Route, IReadOnlyList<CompetencyStatus> Competencies)
{
    /// <summary>The first competency, in route order, that has no evidence yet and whose prerequisites are confirmed.</summary>
    public CompetencyStatus? NextCheck => Competencies.FirstOrDefault(c => c.State == CompetencyState.NotChecked && c.IsReady);
    public int ConfirmedCount => Competencies.Count(c => c.State == CompetencyState.Confirmed);
    public int NeedsWorkCount => Competencies.Count(c => c.State == CompetencyState.NeedsWork);
    public int NotCheckedCount => Competencies.Count(c => c.State == CompetencyState.NotChecked);
}

/// <summary>Computes competency evidence from stored progress. Pure: the same content and progress give the same report.</summary>
public static class DiagnosticEvaluator
{
    public static DiagnosticReport? Evaluate(ContentSet set, string trackId, IReadOnlyDictionary<string, LessonProgress> progress)
    {
        if (set.FindTrack(trackId) is not { } track || set.FindRoute(trackId) is not { Competencies.Count: > 0 } route) return null;
        bool Done(string id) => progress.GetValueOrDefault(id)?.IsCompleted == true;

        var states = new Dictionary<string, CompetencyState>(StringComparer.Ordinal);
        var partial = new List<(Competency C, Lesson Check, List<Lesson> Covered, List<Lesson> Gaps, LessonProgress? P)>();
        foreach (var c in route.Competencies)
        {
            if (set.FindLesson(c.CheckLessonId) is not { } check) continue;
            var covered = check.AssessmentFor.Distinct(StringComparer.Ordinal).Select(set.FindLesson).OfType<Lesson>().ToList();
            var gaps = covered.Where(l => !Done(l.Id)).ToList();
            var p = progress.GetValueOrDefault(check.Id);
            var quizFailed = check.Quiz is { } q && p?.QuizBestScore is { } s && s < q.PassThreshold;
            var exerciseFailed = check.Exercise is not null && p is { Attempts: > 0, ExercisePassed: false };
            var state = Done(check.Id) || (covered.Count > 0 && gaps.Count == 0) ? CompetencyState.Confirmed
                : quizFailed || exerciseFailed ? CompetencyState.NeedsWork
                : CompetencyState.NotChecked;
            states[c.Id] = state;
            partial.Add((c, check, covered, gaps, p));
        }

        var byId = route.Competencies.DistinctBy(c => c.Id).ToDictionary(c => c.Id, StringComparer.Ordinal);
        var result = partial.Select(x => new CompetencyStatus(x.C, x.Check, states[x.C.Id], x.Covered, x.Gaps,
            x.C.After.Where(a => byId.ContainsKey(a) && states.GetValueOrDefault(a) != CompetencyState.Confirmed).Select(a => byId[a]).ToList(),
            x.P)).ToList();
        return new DiagnosticReport(track, route, result);
    }
}

public sealed class DiagnosticService(IContentRepository content, IProgressRepository progress)
{
    /// <summary>Tracks that define competency checks.</summary>
    public IReadOnlyList<Track> GetDiagnosableTracks()
    {
        var set = content.Current;
        return set.Routes.Where(r => r.Competencies.Count > 0).Select(r => set.FindTrack(r.TrackId)).OfType<Track>().ToList();
    }

    public async Task<DiagnosticReport?> GetReportAsync(string trackId, CancellationToken ct = default) =>
        DiagnosticEvaluator.Evaluate(content.Current, trackId,
            (await progress.GetAllAsync(ct)).ToDictionary(p => p.LessonId, StringComparer.Ordinal));
}

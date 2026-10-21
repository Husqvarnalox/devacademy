using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

/// <summary>One row of the Problems list.</summary>
public sealed record ProblemItem(
    Lesson Lesson,
    Course Course,
    Difficulty Difficulty,
    bool Solved,
    LessonState State,
    IReadOnlyList<string> Languages);

/// <summary>Complexity block of a solved problem.</summary>
public sealed record ProblemComplexity(string Time, string Space, string? Explanation);

/// <summary>
/// Algorithm problems: the lessons with an exercise in courses of an <see cref="TrackKind.Algorithms"/> track,
/// plus the post-solve material (complexity, editorial). The editorial is a spoiler: it is returned only after the
/// exercise is passed or after the learner explicitly confirmed (same rule as the reference solution).
/// </summary>
public sealed class ProblemService(IContentRepository content, IProgressRepository progress, LearningPathService path)
{
    public bool HasProblems => Problems(content.Current).Any();

    public async Task<IReadOnlyList<ProblemItem>> ListAsync(
        Difficulty? difficulty = null, bool? solved = null, CancellationToken ct = default)
    {
        var set = content.Current;
        var map = (await progress.GetAllAsync(ct)).ToDictionary(p => p.LessonId, StringComparer.Ordinal);
        var items = new List<ProblemItem>();
        foreach (var (lesson, course) in Problems(set))
        {
            var isSolved = map.TryGetValue(lesson.Id, out var p) && p.ExercisePassed;
            if (difficulty is not null && lesson.Difficulty != difficulty) continue;
            if (solved is not null && isSolved != solved) continue;
            var status = await path.GetLessonStatusAsync(lesson.Id, ct);
            items.Add(new ProblemItem(lesson, course, lesson.Difficulty, isSolved, status?.State ?? LessonState.Available,
                lesson.Exercise!.AllVariants.Select(v => v.LanguageId).ToList()));
        }
        return items;
    }

    /// <summary>
    /// Every lesson exercise of the available tracks, algorithm courses included: the practice list for learners who
    /// check what they know without reading the theory first. One progress read per course.
    /// </summary>
    public async Task<IReadOnlyList<ProblemItem>> ListExercisesAsync(CancellationToken ct = default)
    {
        var set = content.Current;
        var items = new List<ProblemItem>();
        foreach (var course in set.Courses.Where(c => set.FindTrack(c.TrackId) is { Status: ContentStatus.Available }))
        {
            if (await path.GetCoursePathAsync(course.Id, ct) is not { } plan) continue;
            foreach (var status in plan.Lessons.Where(l => l.Lesson.Exercise is not null))
                items.Add(new ProblemItem(status.Lesson, course, status.Lesson.Difficulty, status.Progress?.ExercisePassed == true, status.State,
                    status.Lesson.Exercise!.AllVariants.Select(v => v.LanguageId).ToList()));
        }
        return items;
    }

    public async Task<bool> IsSolvedAsync(string lessonId, CancellationToken ct = default) =>
        (await progress.GetAsync(lessonId, ct))?.ExercisePassed == true;

    /// <summary>Null until the exercise is passed (or when the lesson has no complexity block).</summary>
    public async Task<ProblemComplexity?> GetComplexityAsync(string lessonId, CancellationToken ct = default)
    {
        if (content.Current.FindLesson(lessonId)?.Complexity is not { } c) return null;
        return await IsSolvedAsync(lessonId, ct) ? new ProblemComplexity(c.Time, c.Space, c.Explanation) : null;
    }

    public bool HasEditorial(string lessonId) => content.Current.FindLesson(lessonId)?.Editorial is not null;

    /// <summary>The editorial Markdown, or null if it does not exist or is still locked (not solved and not confirmed).</summary>
    public async Task<string?> GetEditorialAsync(string lessonId, bool confirmedSpoiler, CancellationToken ct = default)
    {
        if (content.Current.FindLesson(lessonId)?.Editorial is not { } text) return null;
        return confirmedSpoiler || await IsSolvedAsync(lessonId, ct) ? text : null;
    }

    private static IEnumerable<(Lesson Lesson, Course Course)> Problems(ContentSet set) =>
        set.Courses
            .Where(c => set.FindTrack(c.TrackId) is { Kind: TrackKind.Algorithms })
            .SelectMany(c => set.LessonsOf(c.Id).Where(l => l.Exercise is not null).Select(l => (l, c)));
}

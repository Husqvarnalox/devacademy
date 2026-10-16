using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

/// <param name="MissingPrerequisiteTitles">Why the lesson is locked: titles of prerequisites not completed yet.</param>
public sealed record LessonStatus(
    Lesson Lesson,
    LessonState State,
    IReadOnlyList<string> MissingPrerequisiteTitles,
    LessonProgress? Progress);

public sealed record CoursePath(Course Course, IReadOnlyList<LessonStatus> Lessons, int Completed, int Total);

/// <param name="Satisfied">Already Completed (possibly in another course), so it is reused, never repeated.</param>
public sealed record PathItem(Lesson Lesson, LessonState State, bool Satisfied, bool InTargetCourse)
{
    /// <summary>Completed through a passed placement check (source Assessment), not practised.</summary>
    public bool CreditedByAssessment { get; init; }

    /// <summary>Titles of the other paths that also contain this lesson (progress is shared by lesson id).</summary>
    public IReadOnlyList<string> SharedWithPaths { get; init; } = [];

    /// <summary>Knowledge reuse: completed, and either learned for a prerequisite course or shared with another path.</summary>
    public bool AlreadyKnown => Satisfied && (!InTargetCourse || SharedWithPaths.Count > 0);
}

/// <summary>One "one of" group of a path. Without a saved choice the first course is the working default (<see cref="IsChosen"/> false).</summary>
public sealed record AlternativeGroup(int Index, IReadOnlyList<Course> Courses, string SelectedCourseId, bool IsChosen);

/// <param name="IsSelected">False for an alternative the learner did not pick: shown, but not part of the required work.</param>
public sealed record PlanCourse(Course Course, IReadOnlyList<PathItem> Items, bool IsAlternative, bool IsSelected)
{
    public int Total => Items.Count;
    public int Completed => Items.Count(i => i.Satisfied);
    public int Credited => Items.Count(i => i.Satisfied && i.CreditedByAssessment);
    public int AlreadyKnown => Items.Count(i => i.AlreadyKnown);
}

/// <param name="Title">Null for derived stages (the UI numbers them).</param>
/// <param name="IsPrerequisites">Courses the path needs but its stages do not list.</param>
public sealed record PlanStage(string? Title, bool IsPrerequisites, IReadOnlyList<PlanCourse> Courses);

public sealed record PathEdge(string FromCourseId, string ToCourseId);

/// <summary>Course dependency graph of a plan: <see cref="Layers"/>[n] are courses whose longest dependency chain has n steps.</summary>
public sealed record PathGraph(IReadOnlyList<IReadOnlyList<Course>> Layers, IReadOnlyList<PathEdge> Edges);

public sealed record PathPlan(LearningPathDefinition Path, IReadOnlyList<Course> Courses, IReadOnlyList<PathItem> Items)
{
    public int SatisfiedCount => Items.Count(i => i.Satisfied);
    public int RemainingCount => Items.Count - SatisfiedCount;

    /// <summary>Completed by assessment credit; counted inside <see cref="SatisfiedCount"/> but reported separately.</summary>
    public int CreditedCount => Items.Count(i => i.Satisfied && i.CreditedByAssessment);
    public int AlreadyKnownCount => Items.Count(i => i.AlreadyKnown);
    public IReadOnlyList<AlternativeGroup> Alternatives { get; init; } = [];
    public IReadOnlyList<PlanStage> Stages { get; init; } = [];
    public PathGraph Graph { get; init; } = new([], []);
}

/// <summary>A placement quiz: passing it credits <see cref="CreditedLessons"/> (the ones not yet completed).</summary>
public sealed record PlacementOffer(Lesson Assessment, IReadOnlyList<Lesson> CreditedLessons)
{
    public int QuestionCount => Assessment.Quiz?.Questions.Count ?? 0;
    public double PassThreshold => Assessment.Quiz?.PassThreshold ?? 1;
}

public enum RecommendationKind { Review, Continue, Start, Next }

public sealed record Recommendation(RecommendationKind Kind, string LessonId, string Title, string Reason);

/// <param name="settings">Where followed paths and alternative choices are persisted; without it choices fall back to defaults.</param>
public sealed class LearningPathService(IContentRepository content, IProgressRepository progress, IClock clock, ISettingsRepository? settings = null)
{
    public const string FollowedPathsKey = "paths.followed";

    private static string AlternativeKey(string pathId, int index) => $"path.alternative.{pathId}.{index}";

    public async Task<CoursePath?> GetCoursePathAsync(string courseId, CancellationToken ct = default)
    {
        var set = content.Current;
        if (set.FindCourse(courseId) is not { } course) return null;
        var map = await LoadProgressAsync(ct);
        var lessons = set.LessonsOf(courseId).Select(l => StatusOf(set, l, map)).ToList();
        return new CoursePath(course, lessons, lessons.Count(l => l.State == LessonState.Completed), lessons.Count);
    }

    public async Task<LessonStatus?> GetLessonStatusAsync(string lessonId, CancellationToken ct = default)
    {
        var set = content.Current;
        return set.FindLesson(lessonId) is { } l ? StatusOf(set, l, await LoadProgressAsync(ct)) : null;
    }

    /// <summary>
    /// Closure of everything needed for the path: courses via `requires`, lessons via prerequisites (also across courses).
    /// Stage courses are part of the path even when no target requires them; of each alternatives group only the chosen course
    /// (default: the first) is required. Items are topologically ordered, required courses first.
    /// </summary>
    public async Task<PathPlan?> BuildPathAsync(string pathId, CancellationToken ct = default)
    {
        var set = content.Current;
        if (set.FindPath(pathId) is not { } path) return null;
        var map = await LoadProgressAsync(ct);
        var groups = await ResolveAlternativesAsync(set, path, ct);
        var (courseIds, lessonIds) = Closure(set, path, groups);

        // Course rank: dependencies first (stable by content order); lessons follow prerequisite edges.
        var rank = RankCourses(set, courseIds.Concat(lessonIds.Select(i => set.FindLesson(i)!.CourseId)).Distinct().ToList());
        var order = OrderLessons(set, lessonIds, rank);
        var targets = path.TargetCourseIds.ToHashSet(StringComparer.Ordinal);

        var sharing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var other in set.Paths.Where(p => p.Id != path.Id))
        {
            var (_, otherLessons) = Closure(set, other, await ResolveAlternativesAsync(set, other, ct));
            foreach (var id in lessonIds.Where(otherLessons.Contains))
            {
                if (!sharing.TryGetValue(id, out var list)) sharing[id] = list = [];
                list.Add(other.Title);
            }
        }

        PathItem MakeItem(string id)
        {
            var l = set.FindLesson(id)!;
            var state = LessonStateCalculator.Compute(l, map);
            return new PathItem(l, state, state == LessonState.Completed, targets.Contains(l.CourseId))
            {
                CreditedByAssessment = state == LessonState.Completed && map.GetValueOrDefault(id)?.CompletionSource == CompletionSource.Assessment,
                SharedWithPaths = sharing.GetValueOrDefault(id) ?? [],
            };
        }

        var items = order.Select(MakeItem).ToList();
        var courses = set.Courses.Where(c => rank.ContainsKey(c.Id)).OrderBy(c => rank[c.Id]).ToList();
        var graph = BuildGraph(set, courses);
        var alternativeIds = groups.SelectMany(g => g.Courses.Select(c => c.Id)).ToHashSet(StringComparer.Ordinal);

        PlanCourse PlanOf(Course c)
        {
            var selected = courseIds.Contains(c.Id);
            var courseItems = selected
                ? items.Where(i => i.Lesson.CourseId == c.Id).ToList()
                : set.LessonsOf(c.Id).Select(l => MakeItem(l.Id)).ToList();
            return new PlanCourse(c, courseItems, alternativeIds.Contains(c.Id), selected);
        }

        var stages = new List<PlanStage>();
        if (path.StageList.Count > 0)
        {
            var listed = path.StageList.SelectMany(s => s.CourseIds).ToHashSet(StringComparer.Ordinal);
            var rest = courses.Where(c => !listed.Contains(c.Id)).Select(PlanOf).ToList();
            if (rest.Count > 0) stages.Add(new PlanStage(null, true, rest));
            foreach (var st in path.StageList)
                stages.Add(new PlanStage(st.Title, false, st.CourseIds.Select(set.FindCourse).OfType<Course>().Select(PlanOf).ToList()));
        }
        else
        {
            foreach (var layer in graph.Layers) stages.Add(new PlanStage(null, false, layer.Select(PlanOf).ToList()));
        }

        return new PathPlan(path, courses, items) { Alternatives = groups, Stages = stages, Graph = graph };
    }

    private static (HashSet<string> Courses, HashSet<string> Lessons) Closure(ContentSet set, LearningPathDefinition path, IReadOnlyList<AlternativeGroup> groups)
    {
        var inGroups = groups.SelectMany(g => g.Courses.Select(c => c.Id)).ToHashSet(StringComparer.Ordinal);
        var chosen = groups.Select(g => g.SelectedCourseId);
        var stageCourses = path.StageList.SelectMany(s => s.CourseIds).Where(id => !inGroups.Contains(id));

        var courseIds = new HashSet<string>(StringComparer.Ordinal);
        var courseQueue = new Queue<string>(path.TargetCourseIds.Concat(stageCourses).Concat(chosen));
        while (courseQueue.Count > 0)
        {
            var id = courseQueue.Dequeue();
            if (set.FindCourse(id) is not { } c || !courseIds.Add(id)) continue;
            foreach (var r in c.Requires) courseQueue.Enqueue(r);
        }

        var lessonIds = new HashSet<string>(StringComparer.Ordinal);
        var lessonQueue = new Queue<string>(set.Lessons.Where(l => courseIds.Contains(l.CourseId)).Select(l => l.Id));
        while (lessonQueue.Count > 0)
        {
            if (set.FindLesson(lessonQueue.Dequeue()) is not { } l || !lessonIds.Add(l.Id)) continue;
            foreach (var p in l.Prerequisites) lessonQueue.Enqueue(p);
        }
        return (courseIds, lessonIds);
    }

    private static PathGraph BuildGraph(ContentSet set, IReadOnlyList<Course> courses)
    {
        var ids = courses.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var deps = courses.ToDictionary(c => c.Id, c => new SortedSet<string>(c.Requires.Where(r => ids.Contains(r) && r != c.Id), StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var l in set.Lessons.Where(l => ids.Contains(l.CourseId)))
            foreach (var pre in l.Prerequisites)
                if (set.FindLesson(pre) is { } p && p.CourseId != l.CourseId && ids.Contains(p.CourseId)) deps[l.CourseId].Add(p.CourseId);

        var depth = new Dictionary<string, int>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        int Depth(string id)
        {
            if (depth.TryGetValue(id, out var d)) return d;
            if (!visiting.Add(id)) return 0; // cycle (reported by the validator): break it
            d = deps[id].Count == 0 ? 0 : deps[id].Max(Depth) + 1;
            visiting.Remove(id);
            return depth[id] = d;
        }
        foreach (var c in courses) Depth(c.Id);

        var layers = courses.GroupBy(c => depth[c.Id]).OrderBy(g => g.Key).Select(g => (IReadOnlyList<Course>)g.ToList()).ToList();
        var edges = courses.SelectMany(c => deps[c.Id].Select(d => new PathEdge(d, c.Id))).ToList();
        return new PathGraph(layers, edges);
    }

    private async Task<IReadOnlyList<AlternativeGroup>> ResolveAlternativesAsync(ContentSet set, LearningPathDefinition path, CancellationToken ct)
    {
        var result = new List<AlternativeGroup>();
        for (var i = 0; i < path.AlternativeGroups.Count; i++)
        {
            var members = path.AlternativeGroups[i].Select(set.FindCourse).OfType<Course>().ToList();
            if (members.Count == 0) continue;
            var saved = settings is null ? null : await settings.GetAsync(AlternativeKey(path.Id, i), ct);
            var pick = members.FirstOrDefault(c => c.Id == saved);
            result.Add(new AlternativeGroup(i, members, (pick ?? members[0]).Id, pick is not null));
        }
        return result;
    }

    /// <summary>Persists which course of an alternatives group the learner follows. Other members stop being required.</summary>
    public async Task SelectAlternativeAsync(string pathId, int groupIndex, string courseId, CancellationToken ct = default)
    {
        var set = content.Current;
        var path = set.FindPath(pathId) ?? throw new ArgumentException($"Unknown path '{pathId}'", nameof(pathId));
        if (groupIndex < 0 || groupIndex >= path.AlternativeGroups.Count || !path.AlternativeGroups[groupIndex].Contains(courseId))
            throw new ArgumentException($"Course '{courseId}' is not in alternatives group {groupIndex} of path '{pathId}'", nameof(courseId));
        if (settings is null) throw new InvalidOperationException("Alternative choices need a settings repository");
        await settings.SetAsync(AlternativeKey(pathId, groupIndex), courseId, ct);
    }

    // ---- followed paths ----

    public async Task<IReadOnlyList<string>> GetFollowedPathIdsAsync(CancellationToken ct = default)
    {
        var raw = settings is null ? null : await settings.GetAsync(FollowedPathsKey, ct);
        var set = content.Current;
        return (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => set.FindPath(id) is not null).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Follows or unfollows a path (several can be followed at once; progress is shared). Following enrols the learner
    /// in the path's courses so recommendations and the dashboard include them.
    /// </summary>
    public async Task SetFollowedAsync(string pathId, bool followed, CancellationToken ct = default)
    {
        if (content.Current.FindPath(pathId) is null) throw new ArgumentException($"Unknown path '{pathId}'", nameof(pathId));
        if (settings is null) throw new InvalidOperationException("Followed paths need a settings repository");
        var ids = (await GetFollowedPathIdsAsync(ct)).ToList();
        if (followed && !ids.Contains(pathId)) ids.Add(pathId);
        if (!followed) ids.Remove(pathId);
        await settings.SetAsync(FollowedPathsKey, string.Join(',', ids), ct);

        if (followed && await settings.GetProfileAsync(ct) is { } profile && await BuildPathAsync(pathId, ct) is { } plan)
        {
            var enrolled = profile.EnrolledCourseIds.Concat(plan.Courses.Select(c => c.Id)).Distinct(StringComparer.Ordinal).ToList();
            if (enrolled.Count != profile.EnrolledCourseIds.Count) await settings.SaveProfileAsync(profile with { EnrolledCourseIds = enrolled }, ct);
        }
    }

    /// <summary>Paths with at least one course (target, stage or alternative) written in <paramref name="languageId"/>.</summary>
    public IReadOnlyList<LearningPathDefinition> GetPathsForLanguage(string languageId)
    {
        var set = content.Current;
        return set.Paths.Where(p => p.TargetCourseIds.Concat(p.StageList.SelectMany(s => s.CourseIds)).Concat(p.AlternativeGroups.SelectMany(g => g))
            .Select(set.FindCourse).Any(c => c is not null && string.Equals(c.LanguageId, languageId, StringComparison.Ordinal))).ToList();
    }

    /// <summary>
    /// The next step on a path: the first recommendation (reviews, in-progress, next lessons) that belongs to the plan;
    /// otherwise the first unfinished, unlocked lesson in plan order. Null when the path is finished.
    /// </summary>
    public async Task<Recommendation?> GetPathNextStepAsync(string pathId, LearnerProfile? profile, CancellationToken ct = default)
    {
        if (await BuildPathAsync(pathId, ct) is not { } plan) return null;
        var inPlan = plan.Items.Select(i => i.Lesson.Id).ToHashSet(StringComparer.Ordinal);
        if ((await GetRecommendationsAsync(profile, ct)).FirstOrDefault(r => inPlan.Contains(r.LessonId)) is { } rec) return rec;
        var next = plan.Items.FirstOrDefault(i => i.State is LessonState.InProgress or LessonState.Available);
        return next is null ? null : new Recommendation(
            next.State == LessonState.InProgress ? RecommendationKind.Continue : RecommendationKind.Start,
            next.Lesson.Id, next.Lesson.Title, $"Next lesson on the path \"{plan.Path.Title}\"");
    }

    // ---- placement ----

    /// <summary>
    /// Placement quizzes found among <paramref name="lessonIds"/> (a course or a group of lessons): lessons with a quiz and
    /// `assessmentFor` that would still credit at least one lesson. Empty means "no placement check": the UI shows no skip button.
    /// </summary>
    public async Task<IReadOnlyList<PlacementOffer>> GetPlacementOffersAsync(IEnumerable<string> lessonIds, CancellationToken ct = default)
    {
        var set = content.Current;
        var map = await LoadProgressAsync(ct);
        var offers = new List<PlacementOffer>();
        foreach (var id in lessonIds.Distinct(StringComparer.Ordinal))
        {
            if (set.FindLesson(id) is not { Quiz.Questions.Count: > 0, AssessmentFor.Count: > 0 } l) continue;
            if (map.GetValueOrDefault(id)?.IsCompleted == true) continue;
            var open = l.AssessmentFor.Distinct(StringComparer.Ordinal).Select(set.FindLesson).OfType<Lesson>()
                .Where(t => map.GetValueOrDefault(t.Id)?.IsCompleted != true).ToList();
            if (open.Count > 0) offers.Add(new PlacementOffer(l, open));
        }
        return offers;
    }

    public Task<IReadOnlyList<PlacementOffer>> GetCoursePlacementOffersAsync(string courseId, CancellationToken ct = default) =>
        GetPlacementOffersAsync(content.Current.LessonsOf(courseId).Select(l => l.Id), ct);

    private static Dictionary<string, int> RankCourses(ContentSet set, List<string> ids)
    {
        var members = set.Courses.Where(c => ids.Contains(c.Id)).ToList();
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        var remaining = members.ToList();
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(c => c.Requires.All(r => rank.ContainsKey(r) || members.All(m => m.Id != r))) ?? remaining[0];
            rank[next.Id] = rank.Count;
            remaining.Remove(next);
        }
        return rank;
    }

    private static List<string> OrderLessons(ContentSet set, HashSet<string> ids, Dictionary<string, int> rank)
    {
        var position = set.Lessons.Select((l, i) => (l.Id, i)).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().i);
        var pending = ids.ToDictionary(id => id, id => set.FindLesson(id)!.Prerequisites.Count(p => ids.Contains(p) && p != id), StringComparer.Ordinal);
        var ready = new SortedSet<(int Course, int Pos, string Id)>(
            pending.Where(kv => kv.Value == 0).Select(kv => (rank[set.FindLesson(kv.Key)!.CourseId], position[kv.Key], kv.Key)));
        var result = new List<string>();
        while (ready.Count > 0)
        {
            var next = ready.Min;
            ready.Remove(next);
            result.Add(next.Id);
            foreach (var l in ids.Where(i => set.FindLesson(i)!.Prerequisites.Contains(next.Id) && i != next.Id))
                if (--pending[l] == 0) ready.Add((rank[set.FindLesson(l)!.CourseId], position[l], l));
        }
        // Lessons on a prerequisite cycle (reported by the validator) go last.
        result.AddRange(ids.Except(result).OrderBy(i => position[i]));
        return result;
    }

    /// <summary>
    /// Deterministic order: (1) due reviews, oldest first; (2) lessons in progress, by start time;
    /// (3) first Available lesson of each enrolled course; (4) `next` links of the last completed lesson.
    /// A lesson appears at most once.
    /// </summary>
    public async Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(LearnerProfile? profile, CancellationToken ct = default)
    {
        var set = content.Current;
        var map = await LoadProgressAsync(ct);
        var now = clock.UtcNow;
        var result = new List<Recommendation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(RecommendationKind kind, Lesson l, string reason)
        {
            if (seen.Add(l.Id)) result.Add(new Recommendation(kind, l.Id, l.Title, reason));
        }

        foreach (var p in map.Values.Where(p => p.IsCompleted && p.NextReviewAt <= now).OrderBy(p => p.NextReviewAt).ThenBy(p => p.LessonId, StringComparer.Ordinal))
            if (set.FindLesson(p.LessonId) is { } l) Add(RecommendationKind.Review, l, $"Review due since {p.NextReviewAt:yyyy-MM-dd}");

        foreach (var p in map.Values.Where(p => !p.IsCompleted && p.StartedAt is not null).OrderBy(p => p.StartedAt).ThenBy(p => p.LessonId, StringComparer.Ordinal))
            if (set.FindLesson(p.LessonId) is { } l) Add(RecommendationKind.Continue, l, "In progress: pick up where you left off");

        foreach (var courseId in profile?.EnrolledCourseIds ?? [])
        {
            if (set.FindCourse(courseId) is not { } course) continue;
            var first = set.LessonsOf(courseId).FirstOrDefault(l => LessonStateCalculator.Compute(l, map) == LessonState.Available);
            if (first is not null) Add(RecommendationKind.Start, first, $"Next lesson in {course.Title}");
        }

        var last = map.Values.Where(p => p.IsCompleted).OrderByDescending(p => p.CompletedAt).ThenBy(p => p.LessonId, StringComparer.Ordinal).FirstOrDefault();
        if (last is not null && set.FindLesson(last.LessonId) is { } lastLesson)
            foreach (var nextId in lastLesson.Next)
                if (set.FindLesson(nextId) is { } n && !(map.TryGetValue(nextId, out var np) && np.IsCompleted))
                    Add(RecommendationKind.Next, n, $"Suggested after \"{lastLesson.Title}\"");

        return result;
    }

    private async Task<Dictionary<string, LessonProgress>> LoadProgressAsync(CancellationToken ct) =>
        (await progress.GetAllAsync(ct)).ToDictionary(p => p.LessonId, StringComparer.Ordinal);

    private static LessonStatus StatusOf(ContentSet set, Lesson lesson, Dictionary<string, LessonProgress> map)
    {
        var state = LessonStateCalculator.Compute(lesson, map);
        var missing = state == LessonState.Locked
            ? LessonStateCalculator.MissingPrerequisites(lesson, map).Select(id => set.FindLesson(id)?.Title ?? id).ToList()
            : [];
        return new LessonStatus(lesson, state, missing, map.GetValueOrDefault(lesson.Id));
    }
}

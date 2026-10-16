namespace DevAcademy.Domain;

/// <summary>Immutable, indexed view of all loaded content. Lessons are ordered by course order.</summary>
public sealed class ContentSet
{
    private readonly Dictionary<string, Track> _tracks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Course> _courses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Lesson> _lessons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Lesson Lesson, Exercise Exercise)> _exercises = new(StringComparer.Ordinal);

    public static ContentSet Empty { get; } = new([], [], []);

    public ContentSet(IEnumerable<Track> tracks, IEnumerable<Course> courses, IEnumerable<Lesson> lessons,
        IEnumerable<LearningPathDefinition>? paths = null, IEnumerable<ProjectTemplate>? templates = null,
        IEnumerable<Comparison>? comparisons = null)
    {
        Comparisons = (comparisons ?? []).ToList();
        Templates = (templates ?? []).ToList();
        Paths = (paths ?? []).ToList();
        Tracks = tracks.ToList();
        Courses = courses.ToList();
        Lessons = lessons.ToList();
        // First definition wins on duplicate ids; the validator reports duplicates.
        foreach (var t in Tracks) _tracks.TryAdd(t.Id, t);
        foreach (var c in Courses) _courses.TryAdd(c.Id, c);
        foreach (var l in Lessons)
        {
            _lessons.TryAdd(l.Id, l);
            if (l.Exercise is { } e) _exercises.TryAdd(e.Id, (l, e));
            if (l.Challenge is { } ch) _exercises.TryAdd(ch.Id, (l, ch));
        }
    }

    public IReadOnlyList<Track> Tracks { get; }
    public IReadOnlyList<Course> Courses { get; }
    public IReadOnlyList<Lesson> Lessons { get; }
    public IReadOnlyList<LearningPathDefinition> Paths { get; }
    public IReadOnlyList<ProjectTemplate> Templates { get; }
    public IReadOnlyList<Comparison> Comparisons { get; }

    public Track? FindTrack(string id) => _tracks.GetValueOrDefault(id);
    public Course? FindCourse(string id) => _courses.GetValueOrDefault(id);
    public LearningPathDefinition? FindPath(string id) => Paths.FirstOrDefault(p => p.Id == id);
    public Comparison? FindComparison(string id) => Comparisons.FirstOrDefault(c => c.Id == id);
    public Lesson? FindLesson(string id) => _lessons.GetValueOrDefault(id);

    /// <summary>Finds an exercise or challenge by id, with its owning lesson.</summary>
    public (Lesson Lesson, Exercise Exercise)? FindExercise(string id) =>
        _exercises.TryGetValue(id, out var v) ? v : null;

    public IEnumerable<Lesson> LessonsOf(string courseId) =>
        Lessons.Where(l => l.CourseId == courseId);

    /// <summary>
    /// Lesson ids with prerequisites first; ties keep content order. Unknown prerequisites are ignored.
    /// Lessons on a prerequisite cycle are appended at the end (the validator reports the cycle).
    /// </summary>
    public IReadOnlyList<string> TopologicalOrder()
    {
        var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var l in Lessons) indegree.TryAdd(l.Id, 0);
        foreach (var l in Lessons.DistinctBy(l => l.Id))
            foreach (var p in l.Prerequisites.Distinct())
            {
                if (!_lessons.ContainsKey(p) || p == l.Id) continue;
                indegree[l.Id]++;
                if (!dependents.TryGetValue(p, out var list)) dependents[p] = list = [];
                list.Add(l.Id);
            }

        var position = Lessons.Select((l, i) => (l.Id, i)).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().i);
        var ready = new SortedSet<(int Pos, string Id)>(indegree.Where(kv => kv.Value == 0).Select(kv => (position[kv.Key], kv.Key)));
        var result = new List<string>();
        while (ready.Count > 0)
        {
            var next = ready.Min;
            ready.Remove(next);
            result.Add(next.Id);
            if (!dependents.TryGetValue(next.Id, out var deps)) continue;
            foreach (var d in deps)
                if (--indegree[d] == 0) ready.Add((position[d], d));
        }
        if (result.Count < indegree.Count)
        {
            var done = result.ToHashSet(StringComparer.Ordinal);
            result.AddRange(indegree.Keys.Where(k => !done.Contains(k)).OrderBy(k => position[k]));
        }
        return result;
    }
}

using DevAcademy.Domain;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DevAcademy.Content;

// Learning paths (paths/*.yaml) and comparisons (comparisons/*.yaml). Kept apart from ContentLoader so the two content
// types evolve independently; ContentLoader only calls Load. Problems are reported through the supplied error callback.

internal sealed class PathFileDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public List<string>? Targets { get; set; }
    public List<PathStageDto>? Stages { get; set; }
    public List<List<string>>? Alternatives { get; set; }
}

internal sealed class PathStageDto
{
    public string? Title { get; set; }
    public List<string>? Courses { get; set; }
}

internal sealed class ComparisonDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public List<ComparisonSubjectDto>? Subjects { get; set; }
    public List<ComparisonCriterionDto>? Criteria { get; set; }
    public Dictionary<string, Dictionary<string, string>>? Cells { get; set; }
    public string? VerdictGuidance { get; set; }
    public List<string>? Sources { get; set; }
}

internal sealed class ComparisonSubjectDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Kind { get; set; }
    public string? Version { get; set; }
    public List<string>? Docs { get; set; }
}

internal sealed class ComparisonCriterionDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}

internal static class SideContentLoader
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static T? Read<T>(string absolute, string rel, Action<string, string> error) where T : class
    {
        try
        {
            var dto = Yaml.Deserialize<T>(File.ReadAllText(absolute));
            if (dto is null) error(rel, "file is empty");
            return dto;
        }
        catch (YamlException ex)
        {
            error(rel, $"invalid YAML at line {ex.Start.Line}: {ex.InnerException?.Message ?? ex.Message}");
            return null;
        }
        catch (IOException ex) { error(rel, ex.Message); return null; }
    }

    public static List<LearningPathDefinition> LoadPaths(string root, Action<string, string> error)
    {
        var result = new List<LearningPathDefinition>();
        var dir = Path.Combine(root, "paths");
        if (!Directory.Exists(dir)) return result;
        foreach (var file in Directory.GetFiles(dir, "*.yaml").Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (Read<PathFileDto>(file, rel, error) is not { } dto) continue;
            var ok = Require(dto.Id, rel, "id", error) & Require(dto.Title, rel, "title", error) & Require(dto.Goal, rel, "goal", error);
            if (dto.Targets is not { Count: > 0 }) { error(rel, "'targets' needs at least one course id"); ok = false; }

            var stages = new List<PathStage>();
            foreach (var s in dto.Stages ?? [])
            {
                if (string.IsNullOrWhiteSpace(s.Title)) { error(rel, "stages[].title is required"); ok = false; continue; }
                if (s.Courses is not { Count: > 0 }) { error(rel, $"stage '{s.Title}' needs at least one course"); ok = false; continue; }
                stages.Add(new PathStage(s.Title.Trim(), s.Courses));
            }
            var alternatives = (dto.Alternatives ?? []).Select(g => (IReadOnlyList<string>)(g ?? [])).ToList();
            if (ok) result.Add(new LearningPathDefinition(dto.Id!, dto.Title!, dto.Description ?? "", dto.Goal!, dto.Targets!, rel, stages, alternatives));
        }
        return result;
    }

    public static List<Comparison> LoadComparisons(string root, Action<string, string> error)
    {
        var result = new List<Comparison>();
        var dir = Path.Combine(root, "comparisons");
        if (!Directory.Exists(dir)) return result;
        foreach (var file in Directory.GetFiles(dir, "*.yaml").Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (Read<ComparisonDto>(file, rel, error) is not { } dto) continue;
            var ok = Require(dto.Id, rel, "id", error) & Require(dto.Title, rel, "title", error) & Require(dto.VerdictGuidance, rel, "verdictGuidance", error);
            if (dto.Id is not null && dto.Id != Path.GetFileNameWithoutExtension(file))
            { error(rel, $"'id' ({dto.Id}) must match the file name '{Path.GetFileNameWithoutExtension(file)}'"); ok = false; }

            var subjects = new List<ComparisonSubject>();
            foreach (var s in dto.Subjects ?? [])
            {
                var sok = Require(s.Id, rel, "subjects[].id", error) & Require(s.Title, rel, $"subjects[{s.Id}].title", error);
                if (!Enum.TryParse<SubjectKind>(s.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
                { error(rel, $"subjects[{s.Id}].kind must be one of: library, framework, language, runtime (got '{s.Kind}')"); sok = false; }
                if (sok) subjects.Add(new ComparisonSubject(s.Id!, s.Title!, kind, string.IsNullOrWhiteSpace(s.Version) ? null : s.Version.Trim(), s.Docs ?? []));
                else ok = false;
            }
            var criteria = new List<ComparisonCriterion>();
            foreach (var c in dto.Criteria ?? [])
            {
                if (Require(c.Id, rel, "criteria[].id", error) & Require(c.Title, rel, $"criteria[{c.Id}].title", error))
                    criteria.Add(new ComparisonCriterion(c.Id!, c.Title!, c.Description ?? ""));
                else ok = false;
            }
            var cells = (dto.Cells ?? []).ToDictionary(
                row => row.Key,
                row => (IReadOnlyDictionary<string, string>)(row.Value ?? []).ToDictionary(c => c.Key, c => c.Value ?? "", StringComparer.Ordinal),
                StringComparer.Ordinal);
            if (ok) result.Add(new Comparison(dto.Id!, dto.Title!, subjects, criteria, cells, dto.VerdictGuidance!, dto.Sources ?? [], rel));
        }
        return result;
    }

    /// <summary><c>content/routes/&lt;track&gt;.yaml</c>: entry lessons and competency checks of a track.</summary>
    public static List<TrackRoute> LoadRoutes(string root, Action<string, string> error)
    {
        var result = new List<TrackRoute>();
        var dir = Path.Combine(root, "routes");
        if (!Directory.Exists(dir)) return result;
        foreach (var file in Directory.GetFiles(dir, "*.yaml").Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (Read<RouteDto>(file, rel, error) is not { } dto) continue;
            var ok = Require(dto.Track, rel, "track", error);
            if (dto.Track is not null && dto.Track != Path.GetFileNameWithoutExtension(file))
            { error(rel, $"'track' ({dto.Track}) must match the file name '{Path.GetFileNameWithoutExtension(file)}'"); ok = false; }
            var competencies = new List<Competency>();
            foreach (var c in dto.Competencies ?? [])
            {
                if (Require(c.Id, rel, "competencies[].id", error) & Require(c.Title, rel, $"competencies[{c.Id}].title", error)
                    & Require(c.Check, rel, $"competencies[{c.Id}].check", error))
                    competencies.Add(new Competency(c.Id!, c.Title!.Trim(), c.Description?.Trim() ?? "", c.Check!, c.After ?? []));
                else ok = false;
            }
            static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            if (ok) result.Add(new TrackRoute(dto.Track!, Blank(dto.Entry?.Beginner), Blank(dto.Entry?.Programmer), competencies, rel));
        }
        return result;
    }

    private static bool Require(string? value, string path, string field, Action<string, string> error)
    {
        if (!string.IsNullOrWhiteSpace(value)) return true;
        error(path, $"'{field}' is required");
        return false;
    }
}

/// <summary>Checks over loaded paths and comparisons; called from <see cref="ContentValidator"/>.</summary>
internal static class SideContentValidator
{
    public static void Validate(ContentSet set, Action<string, string> error, Action<string, string> warn)
    {
        foreach (var path in set.Paths) ValidatePath(set, path, error);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in set.Comparisons)
        {
            if (!seen.Add(c.Id)) error(c.SourcePath, $"duplicate comparison id '{c.Id}'");
            ValidateComparison(c, error, warn);
        }

        var routeTracks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in set.Routes)
        {
            if (!routeTracks.Add(r.TrackId)) error(r.SourcePath, $"duplicate route for track '{r.TrackId}'");
            ValidateRoute(set, r, error, warn);
        }
    }

    private static void ValidateRoute(ContentSet set, TrackRoute route, Action<string, string> error, Action<string, string> warn)
    {
        var p = route.SourcePath;
        if (set.FindTrack(route.TrackId) is not { } track) { error(p, $"unknown track '{route.TrackId}'"); return; }
        bool InTrack(Lesson l) => track.CourseIds.Contains(l.CourseId);

        void Entry(string? id, string field)
        {
            if (id is null) return;
            if (set.FindLesson(id) is not { } l) error(p, $"entry.{field} references unknown lesson '{id}'");
            else if (!InTrack(l)) error(p, $"entry.{field} lesson '{id}' is not in a course of track '{track.Id}'");
            else if (l.Prerequisites.Count > 0) warn(p, $"entry.{field} lesson '{id}' has prerequisites, so it opens Locked");
        }
        Entry(route.BeginnerEntry, "beginner");
        Entry(route.ProgrammerEntry, "programmer");
        if (route.BeginnerEntry is { } b && set.FindLesson(b) is { } bl && set.FindCourse(bl.CourseId) is { Level: > 0 } bc)
            warn(p, $"entry.beginner is in course '{bc.Id}' of level {bc.Level}; an absolute beginner should start in a level-0 course");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var checks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in route.Competencies)
        {
            if (!ids.Add(c.Id)) error(p, $"duplicate competency id '{c.Id}'");
            if (!checks.Add(c.CheckLessonId)) error(p, $"competency '{c.Id}': check lesson '{c.CheckLessonId}' is used by another competency");
            if (set.FindLesson(c.CheckLessonId) is not { } check) { error(p, $"competency '{c.Id}': unknown check lesson '{c.CheckLessonId}'"); continue; }
            if (!InTrack(check)) error(p, $"competency '{c.Id}': check lesson '{check.Id}' is not in a course of track '{track.Id}'");
            if (check.AssessmentFor.Count == 0) error(p, $"competency '{c.Id}': check lesson '{check.Id}' has no assessmentFor, so passing it confirms nothing");
            if (check.Exercise is null) warn(p, $"competency '{c.Id}': check lesson '{check.Id}' has no exercise; a quiz alone is weak evidence of a skill");
        }
        foreach (var c in route.Competencies)
            foreach (var a in c.After)
                if (a == c.Id) error(p, $"competency '{c.Id}' lists itself in after");
                else if (!ids.Contains(a)) error(p, $"competency '{c.Id}': after references unknown competency '{a}'");

        // after must be acyclic, or a check could never be offered.
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var byId = route.Competencies.DistinctBy(c => c.Id).ToDictionary(c => c.Id, StringComparer.Ordinal);
        bool Cyclic(string id)
        {
            if (state.TryGetValue(id, out var s)) return s == 1;
            state[id] = 1;
            var cyc = byId.TryGetValue(id, out var c) && c.After.Where(byId.ContainsKey).Any(Cyclic);
            state[id] = 2;
            return cyc;
        }
        foreach (var c in route.Competencies)
            if (Cyclic(c.Id)) { error(p, $"competency order (after) has a cycle through '{c.Id}'"); break; }
    }

    private static HashSet<string> RequiresClosure(ContentSet set, IEnumerable<string> start)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(start);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (set.FindCourse(id) is not { } c || !result.Add(id)) continue;
            foreach (var r in c.Requires) queue.Enqueue(r);
        }
        return result;
    }

    private static void ValidatePath(ContentSet set, LearningPathDefinition path, Action<string, string> error)
    {
        var p = path.SourcePath;
        var stageSeen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in path.StageList)
            foreach (var id in stage.CourseIds)
            {
                if (set.FindCourse(id) is null) error(p, $"stage '{stage.Title}' references unknown course '{id}'");
                else if (!stageSeen.Add(id)) error(p, $"course '{id}' is listed in more than one stage");
            }

        var alt = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < path.AlternativeGroups.Count; i++)
        {
            var group = path.AlternativeGroups[i];
            if (group.Count < 2) error(p, $"alternatives[{i}] needs at least two courses (one of ...)");
            foreach (var id in group)
            {
                if (set.FindCourse(id) is null) { error(p, $"alternatives[{i}] references unknown course '{id}'"); continue; }
                if (group.Count(x => x == id) > 1) { error(p, $"alternatives[{i}] lists '{id}' twice"); continue; }
                if (!alt.TryAdd(id, i)) error(p, $"course '{id}' is in more than one alternatives group");
                if (path.TargetCourseIds.Contains(id)) error(p, $"alternative '{id}' is also a target (a target is always required)");
            }
            // Choosing one member must never force another member of the same group.
            foreach (var id in group.Distinct().Where(x => set.FindCourse(x) is not null))
                foreach (var other in RequiresClosure(set, set.FindCourse(id)!.Requires))
                    if (group.Contains(other)) error(p, $"alternative '{id}' requires '{other}' of the same group");
        }
        if (alt.Count == 0) return;

        // Courses that are always needed (targets, stage courses outside any group) must not depend on a group member.
        var fixedCourses = path.TargetCourseIds.Concat(stageSeen.Where(id => !alt.ContainsKey(id)));
        var needed = RequiresClosure(set, fixedCourses);
        foreach (var id in alt.Keys.Order(StringComparer.Ordinal))
            if (needed.Contains(id) && !path.TargetCourseIds.Contains(id))
                error(p, $"alternative '{id}' is required by another course of the path, so it cannot be optional");
    }

    private static bool IsHttps(string s) => Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;

    private static void ValidateComparison(Comparison c, Action<string, string> error, Action<string, string> warn)
    {
        var p = c.SourcePath;
        if (c.Subjects.Count < 2) error(p, "a comparison needs at least two subjects");
        if (c.Criteria.Count == 0) error(p, "a comparison needs at least one criterion");
        if (c.Sources.Count == 0) error(p, "'sources' needs at least one https URL");
        foreach (var s in c.Sources.Where(s => !IsHttps(s))) error(p, $"sources entry is not an https URL: '{s}'");

        var subjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in c.Subjects)
        {
            if (!subjectIds.Add(s.Id)) error(p, $"duplicate subject id '{s.Id}'");
            if (s.Docs.Count == 0) error(p, $"subject '{s.Id}' needs at least one https docs URL");
            foreach (var d in s.Docs.Where(d => !IsHttps(d))) error(p, $"subject '{s.Id}' docs entry is not an https URL: '{d}'");
            if (s.Version is null) warn(p, $"subject '{s.Id}' does not state the version the cells were written against");
        }
        var criterionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in c.Criteria)
            if (!criterionIds.Add(k.Id)) error(p, $"duplicate criterion id '{k.Id}'");

        foreach (var row in c.Cells)
        {
            if (!criterionIds.Contains(row.Key)) error(p, $"cells references unknown criterion '{row.Key}'");
            foreach (var cell in row.Value.Keys.Where(k => !subjectIds.Contains(k)))
                error(p, $"cells[{row.Key}] references unknown subject '{cell}'");
        }
        foreach (var k in c.Criteria)
            foreach (var s in c.Subjects)
                if (string.IsNullOrWhiteSpace(c.Cell(k.Id, s.Id))) error(p, $"cell for criterion '{k.Id}' and subject '{s.Id}' is missing or empty");

        foreach (var s in c.Subjects.Where(s => !c.VerdictGuidance.Contains(s.Title, StringComparison.OrdinalIgnoreCase)))
            warn(p, $"verdictGuidance never mentions '{s.Title}' (say when to choose each subject)");
    }
}

using DevAcademy.Domain;

namespace DevAcademy.Content;

/// <summary>Static checks over a loaded <see cref="ContentSet"/>. The C# rules here are authoritative.</summary>
public static class ContentValidator
{
    /// <param name="packages">The package-set index exercises must be covered by; null looks for the shipped/repository one (a warning when absent).</param>
    /// <param name="packageSearchStart">Where the default lookup starts (default: the executable's directory).</param>
    public static IReadOnlyList<ContentDiagnostic> Validate(ContentSet set, ContentPackageIndex? packages = null, string? packageSearchStart = null)
    {
        var d = new List<ContentDiagnostic>();
        void Error(string path, string msg) => d.Add(new(DiagnosticSeverity.Error, path, msg));
        void Warn(string path, string msg) => d.Add(new(DiagnosticSeverity.Warning, path, msg));

        void Unique<T>(IEnumerable<T> items, Func<T, string> id, Func<T, string> path, string what)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
                if (!seen.Add(id(item))) Error(path(item), $"duplicate {what} id '{id(item)}'");
        }

        Unique(set.Tracks, t => t.Id, _ => "catalog.yaml", "track");
        Unique(set.Courses, c => c.Id, c => c.SourcePath, "course");
        Unique(set.Lessons, l => l.Id, l => l.SourcePath, "lesson");
        Unique(set.Lessons.SelectMany(l => new[] { l.Exercise, l.Challenge }.OfType<Exercise>().Select(e => (l, e))),
            x => x.e.Id, x => x.l.SourcePath, "exercise");

        Unique(set.Paths, p => p.Id, p => p.SourcePath, "learning path");
        foreach (var path in set.Paths)
            foreach (var target in path.TargetCourseIds)
                if (set.FindCourse(target) is null) Error(path.SourcePath, $"target references unknown course '{target}'");

        foreach (var course in set.Courses)
        {
            foreach (var req in course.Requires)
                if (set.FindCourse(req) is null) Error(course.SourcePath, $"requires references unknown course '{req}'");
            if (course.Requires.Contains(course.Id)) Error(course.SourcePath, "course requires itself");
            foreach (var doc in course.Framework?.Docs ?? [])
                if (!Uri.TryCreate(doc, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    Error(course.SourcePath, $"framework.docs entry is not an https URL: '{doc}'");
        }
        CheckCourseCycles(set, Error);

        foreach (var track in set.Tracks)
        {
            if (track.Status == ContentStatus.Available && track.CourseIds.Count == 0)
                Error("catalog.yaml", $"available track '{track.Id}' has no courses");
            foreach (var cid in track.CourseIds)
            {
                var course = set.FindCourse(cid);
                if (course is not null && course.TrackId != track.Id)
                    Error(course.SourcePath, $"course declares track '{course.TrackId}' but is listed under track '{track.Id}'");
            }
        }

        var packageIndex = packages;
        var packageIndexSearched = packages is not null;
        ContentPackageIndex? PackageIndex()
        {
            if (packageIndexSearched) return packageIndex;
            packageIndexSearched = true;
            packageIndex = ContentPackageIndex.TryLoadDefault(packageSearchStart);
            if (packageIndex is null) Warn("content-packages/index.json", "package-set index not found (content-packages next to the executable, or tools/content-packages): coverage of exercise package requirements was not checked");
            return packageIndex;
        }

        foreach (var lesson in set.Lessons)
        {
            var p = lesson.SourcePath;
            CheckReferences(lesson, set, Error);
            CheckTheory(lesson, set, Error, Warn);
            CheckFacts(lesson, p, Error);
            CheckAssessment(lesson, set, Error);
            if (lesson.Quiz is { } quiz) CheckQuiz(quiz, p, Error);
            if (lesson.Editorial is not null && lesson.Exercise is null) Error(p, "editorial requires the lesson to have an exercise");
            if (lesson.Exercise is { } ex) { CheckExercise(ex, p, "exercise", Error, Warn); CheckPackageSet(ex, p, "exercise", PackageIndex, Error); }
            if (lesson.Challenge is { } ch) { CheckExercise(ch, p, "challenge", Error, Warn); CheckPackageSet(ch, p, "challenge", PackageIndex, Error); }
        }

        CheckCycles(set, Error);
        SideContentValidator.Validate(set, Error, Warn);
        return d;
    }

    private static void CheckReferences(Lesson lesson, ContentSet set, Action<string, string> error)
    {
        void Check(IEnumerable<string> ids, string field)
        {
            foreach (var id in ids)
                if (set.FindLesson(id) is null) error(lesson.SourcePath, $"{field} references unknown lesson '{id}'");
        }
        Check(lesson.Prerequisites, "prerequisites");
        Check(lesson.Related, "related");
        Check(lesson.Next, "next");
        if (lesson.Prerequisites.Contains(lesson.Id)) error(lesson.SourcePath, "lesson lists itself as a prerequisite");
    }

    private static void CheckAssessment(Lesson lesson, ContentSet set, Action<string, string> error)
    {
        if (lesson.AssessmentFor.Count == 0) return;
        if (lesson.Quiz is null) error(lesson.SourcePath, "assessmentFor requires the lesson to have a quiz");
        foreach (var id in lesson.AssessmentFor)
        {
            if (id == lesson.Id) error(lesson.SourcePath, "lesson cannot be an assessment for itself");
            else if (set.FindLesson(id) is null) error(lesson.SourcePath, $"assessmentFor references unknown lesson '{id}'");
        }
    }

    private static void CheckCourseCycles(ContentSet set, Action<string, string> error)
    {
        // Course edges: explicit `requires` plus any cross-course lesson prerequisite.
        var edges = set.Courses.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => new SortedSet<string>(g.First().Requires.Where(r => set.FindCourse(r) is not null && r != g.Key), StringComparer.Ordinal));
        foreach (var lesson in set.Lessons)
            foreach (var pre in lesson.Prerequisites)
                if (set.FindLesson(pre) is { } p && p.CourseId != lesson.CourseId && edges.ContainsKey(lesson.CourseId))
                    edges[lesson.CourseId].Add(p.CourseId);

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        void Visit(string id)
        {
            state[id] = 1;
            stack.Add(id);
            foreach (var next in edges.GetValueOrDefault(id) ?? [])
            {
                if (!state.TryGetValue(next, out var s)) Visit(next);
                else if (s == 1)
                    error(set.FindCourse(id)!.SourcePath, "course dependency cycle: " + string.Join(" -> ", stack.Skip(stack.IndexOf(next)).Append(next)));
            }
            stack.RemoveAt(stack.Count - 1);
            state[id] = 2;
        }
        foreach (var id in edges.Keys.Order(StringComparer.Ordinal))
            if (!state.ContainsKey(id)) Visit(id);
    }

    private static void CheckTheory(Lesson lesson, ContentSet set, Action<string, string> error, Action<string, string> warn)
    {
        var path = lesson.SourcePath.Replace("lesson.yaml", "theory.md");
        if (lesson.Theory.Markdown.Length == 0) return; // missing file already reported by the loader
        foreach (var required in TheoryParser.RequiredSections)
            if (!lesson.Theory.Sections.TryGetValue(required, out var body) || string.IsNullOrWhiteSpace(body))
                error(path, $"missing or empty required section '{required}'");
        foreach (var name in lesson.Theory.Sections.Keys)
            if (!TheoryParser.RequiredSections.Contains(name) && !TheoryParser.OptionalSections.Contains(name))
                warn(path, $"unknown section '{name}'");

        var markdown = lesson.Theory.Markdown + "\n\n" + lesson.Exercise?.Prompt + "\n\n" + lesson.Challenge?.Prompt + "\n\n" + lesson.Editorial;
        foreach (var (info, problem) in TheoryParser.Visualizations(lesson.Theory.Markdown))
            if (problem is not null) error(path, $"```{info}```: {problem}");
        foreach (var target in TheoryParser.LessonLinks(markdown).Distinct())
            if (set.FindLesson(target) is null) error(path, $"link 'lesson:{target}' does not resolve to a lesson");
    }

    private static void CheckFacts(Lesson lesson, string path, Action<string, string> error)
    {
        foreach (var fact in lesson.Facts)
        {
            var label = $"fact ({fact.Kind}) '{Shorten(fact.Text)}'";
            if (fact.Kind is FactKind.Historical or FactKind.Internals && fact.Sources.Count == 0)
                error(path, $"{label} needs at least one https source");
            foreach (var s in fact.Sources)
                if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    error(path, $"{label} has a source that is not an https URL: '{s}'");
        }
    }

    private static void CheckQuiz(Quiz quiz, string path, Action<string, string> error)
    {
        if (quiz.PassThreshold is <= 0 or > 1) error(path, $"quiz passThreshold must be in (0, 1], got {quiz.PassThreshold}");
        if (quiz.Questions.Count == 0) error(path, "quiz has no questions");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var q in quiz.Questions)
        {
            var label = $"quiz question '{q.Id}'";
            if (!seen.Add(q.Id)) error(path, $"duplicate question id '{q.Id}'");
            switch (q.Type)
            {
                case QuestionType.Single or QuestionType.Multiple:
                    if (q.Options.Count < 2) error(path, $"{label} needs at least 2 options");
                    foreach (var i in q.CorrectIndices)
                        if (i < 0 || i >= q.Options.Count) error(path, $"{label} answer index {i} is out of range (0-{q.Options.Count - 1})");
                    if (q.Type == QuestionType.Single && q.CorrectIndices.Count != 1) error(path, $"{label} (single) needs exactly one answer index");
                    if (q.CorrectIndices.Distinct().Count() != q.CorrectIndices.Count) error(path, $"{label} repeats an answer index");
                    break;
                case QuestionType.Short:
                    if (q.AcceptedAnswers.Count == 0 || q.AcceptedAnswers.Any(string.IsNullOrWhiteSpace))
                        error(path, $"{label} needs non-empty accepted answers");
                    break;
                case QuestionType.Numeric:
                    if (q.NumericAnswer is null) error(path, $"{label} needs a numeric answer");
                    if (q.Tolerance < 0) error(path, $"{label} tolerance must not be negative");
                    break;
            }
        }
    }

    private static void CheckExercise(Exercise ex, string path, string kind, Action<string, string> error, Action<string, string> warn)
    {
        var label = $"{kind} '{ex.Id}'";
        foreach (var v in ex.AllVariants)
        {
            var vl = ex.HasExplicitVariants ? $"{label} variant '{v.LanguageId}'" : label;
            if (string.IsNullOrWhiteSpace(v.EntryFile)) { error(path, $"{vl}: entryFile is empty"); continue; }
            if (v.Starter.Count == 0) error(path, $"{vl}: starter directory is missing or empty");
            else if (!v.Starter.Any(f => f.Path == v.EntryFile)) error(path, $"{vl}: starter has no entry file '{v.EntryFile}'");
            if (v.Solution.Count == 0) error(path, $"{vl}: solution directory is missing or empty");
            else if (!v.Solution.Any(f => f.Path == v.EntryFile)) error(path, $"{vl}: solution has no entry file '{v.EntryFile}'");

            if (ex.Evaluation == EvaluationKind.Harness)
            {
                if (string.IsNullOrWhiteSpace(v.HarnessEntry)) error(path, $"{vl}: harness evaluation needs 'harnessEntry'");
                else if (!v.Harness.Any(f => f.Path == v.HarnessEntry)) error(path, $"{vl}: harness directory is missing or has no '{v.HarnessEntry}'");
                foreach (var h in v.Harness)
                    if (v.Starter.Any(f => f.Path == h.Path) || v.Solution.Any(f => f.Path == h.Path))
                        error(path, $"{vl}: harness file '{h.Path}' collides with a starter/solution file");
            }
        }

        CheckRequirements(ex, path, label, error, warn);
        if (ex.Hints.Count is < 1 or > 4) error(path, $"{label} needs 1-4 hints, has {ex.Hints.Count}");
        if (ex.Evaluation == EvaluationKind.Command)
        {
            CheckCommand(ex, path, label, error);
            return;
        }
        foreach (var t in ex.Tests.Where(t => t.Match is not null))
            error(path, $"{label}: test '{t.Id}' sets 'match', which only applies to command evaluation");
        if (!ex.Tests.Any(t => !t.Hidden)) error(path, $"{label} needs at least one public test");
        if (!ex.Tests.Any(t => t.Hidden)) error(path, $"{label} needs at least one hidden test");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in ex.Tests)
        {
            if (!seen.Add(t.Id)) error(path, $"{label}: duplicate test id '{t.Id}'");
            if (ex.Evaluation == EvaluationKind.Io && t.Expected is null) error(path, $"{label}: io test '{t.Id}' has no 'expected'");
            if (t.Tolerance < 0) error(path, $"{label}: test '{t.Id}' tolerance must not be negative");
        }
    }

    private static void CheckCommand(Exercise ex, string path, string label, Action<string, string> error)
    {
        if (ex.HasExplicitVariants) error(path, $"{label}: command evaluation does not support 'variants'");
        if (ex.Command is not { } cmd) { error(path, $"{label}: command evaluation needs 'command'"); return; }

        if (CommandPolicy.Resolve(ex.LanguageId, cmd, out var argv) is { } problem) error(path, $"{label}: {problem}");
        else if (cmd.UsesNodeTool)
        {
            if (ex.Requires?.Npm.Contains(cmd.Tool!, StringComparer.Ordinal) != true)
                error(path, $"{label}: command.tool '{cmd.Tool}' needs requires.npmPackages to list '{cmd.Tool}'");
            if (cmd.Report != CommandReportFormat.Junit || cmd.ReportFile is null)
                error(path, $"{label}: a {cmd.Tool} command reports through junit: set report: junit and reportFile (matching --outputFile=)");
            else if (!argv.Contains("--outputFile=" + cmd.ReportFile, StringComparer.Ordinal) || !argv.Contains("--reporter=junit", StringComparer.Ordinal))
                error(path, $"{label}: command.args must contain --reporter=junit and --outputFile={cmd.ReportFile}");
        }
        if (cmd.TimeoutSeconds is { } to && (to < 1 || to > CommandPolicy.MaxTimeoutSeconds))
            error(path, $"{label}: command.timeoutSeconds must be 1-{CommandPolicy.MaxTimeoutSeconds}, got {to}");
        if (cmd.ReportFile is { } rf && (string.IsNullOrWhiteSpace(rf) || Path.IsPathRooted(rf) || rf.Split('/', '\\').Contains("..")))
            error(path, $"{label}: command.reportFile must be a relative path without '..'");
        if (cmd.Report == CommandReportFormat.GoTestJson && cmd.ReportFile is not null)
            error(path, $"{label}: go-test-json reports are read from stdout; remove command.reportFile");
        if (!cmd.UsesNodeTool && cmd.Report == CommandReportFormat.Junit && cmd.ReportFile is null && !cmd.Run.Any(a => a.Contains("junit", StringComparison.Ordinal)))
            error(path, $"{label}: a junit report needs command.reportFile (or a junit reporter writing to stdout)");
        if (cmd.TestsTarget != "." && (Path.IsPathRooted(cmd.TestsTarget) || cmd.TestsTarget.Split('/', '\\').Contains("..")))
            error(path, $"{label}: command.testsTarget must be '.' or a relative path without '..'");

        if (cmd.TestFiles.Count == 0) error(path, $"{label}: tests_dir is missing or empty (hidden test files are required)");
        var target = cmd.TestsTarget == "." ? "" : cmd.TestsTarget + "/";
        var starter = ex.Starter.Select(f => f.Path).Concat(ex.Solution.Select(f => f.Path)).ToHashSet(StringComparer.Ordinal);
        foreach (var f in cmd.TestFiles)
            if (starter.Contains(target + f.Path)) error(path, $"{label}: test file '{target}{f.Path}' collides with a starter/solution file");
        if (cmd.ReportFile is not null && (starter.Contains(cmd.ReportFile) || cmd.TestFiles.Any(f => target + f.Path == cmd.ReportFile)))
            error(path, $"{label}: command.reportFile '{cmd.ReportFile}' collides with a starter/solution/test file");

        if (!ex.Tests.Any(t => !t.Hidden)) error(path, $"{label} needs at least one public test");
        if (!ex.Tests.Any(t => t.Hidden)) error(path, $"{label} needs at least one hidden test");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in ex.Tests)
        {
            if (!seen.Add(t.Id)) error(path, $"{label}: duplicate test id '{t.Id}'");
            if (string.IsNullOrWhiteSpace(t.Match)) error(path, $"{label}: command test '{t.Id}' needs 'match' (the reported test name)");
            else if (t.Match.Trim('*').Length == 0) error(path, $"{label}: command test '{t.Id}' has a match that selects everything");
            if (t.Stdin is not null || t.Expected is not null) error(path, $"{label}: command test '{t.Id}' must not set stdin/expected");
        }
    }

    private static void CheckRequirements(Exercise ex, string path, string label, Action<string, string> error, Action<string, string> warn)
    {
        var usesPython = ex.AllVariants.Any(v => v.LanguageId == "python");
        var usesJavaScript = ex.AllVariants.Any(v => v.LanguageId is "javascript" or "typescript");
        if ((ex.Requires is { HasPython: true } || ex.Accelerator != AcceleratorNeed.None) && !usesPython)
            error(path, $"{label}: 'requires.python/packages' and 'accelerator' only apply to Python exercises, but no variant is python");
        if (ex.Requires?.MemoryMb is { } mem)
        {
            if (mem < ExerciseRequirements.MinMemoryMb || mem > ExerciseRequirements.MaxMemoryMb)
                error(path, $"{label}: requires.memoryMb must be {ExerciseRequirements.MinMemoryMb}-{ExerciseRequirements.MaxMemoryMb}, got {mem}");
            if (!usesPython)
                error(path, $"{label}: 'requires.memoryMb' is supported only for Python exercises");
        }
        if (ex.Requires is { HasNode: true } && !usesJavaScript)
            error(path, $"{label}: 'requires.node/npmPackages' only apply to javascript/typescript exercises, but no variant is javascript or typescript");
        if (ex.Requires is { HasGo: true } && !ex.AllVariants.Any(v => v.LanguageId == "go"))
            error(path, $"{label}: 'requires.go/goModules' only apply to go exercises, but no variant is go");
        if (ex.Command is { UsesNodeTool: true } && ex.Requires is not { HasNode: true })
            error(path, $"{label}: command.tool needs requires.npmPackages");
        if (ex.Requires is { } req)
        {
            if (req.Python is { } py)
            {
                if (!PythonSpecifierSet.TryParse(py, out var set, out var perr)) error(path, $"{label}: requires.python: {perr}");
                else if (set.Clauses.Count == 0) error(path, $"{label}: requires.python is empty");
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pkg in req.Packages)
            {
                if (!PythonPackageSpec.TryParse(pkg, out var spec, out var err)) error(path, $"{label}: requires.packages: {err}");
                else if (!seen.Add(spec.NormalizedName)) error(path, $"{label}: requires.packages lists '{spec.Name}' twice");
            }
            if (req.Node is { } node)
            {
                if (!PythonSpecifierSet.TryParse(node, out var nset, out var nerr)) error(path, $"{label}: requires.node: {nerr}");
                else if (nset.Clauses.Count == 0) error(path, $"{label}: requires.node is empty");
            }
            if (req.Go is { } go)
            {
                if (!PythonSpecifierSet.TryParse(go, out var gset, out var gerr)) error(path, $"{label}: requires.go: {gerr}");
                else if (gset.Clauses.Count == 0) error(path, $"{label}: requires.go is empty");
            }
            var goSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in req.Modules)
            {
                if (!GoModulePath.IsValid(module)) error(path, $"{label}: requires.goModules: '{module}' is not a plain Go module path (host/path, no versions or URLs)");
                else if (!goSeen.Add(module)) error(path, $"{label}: requires.goModules lists '{module}' twice");
            }
            var npmSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pkg in req.Npm)
            {
                if (!NpmPackageName.IsValid(pkg)) error(path, $"{label}: requires.npmPackages: '{pkg}' is not a plain npm package name (no versions, URLs or paths)");
                else if (!npmSeen.Add(pkg)) error(path, $"{label}: requires.npmPackages lists '{pkg}' twice");
            }
        }
        if (ex.Accelerator == AcceleratorNeed.Required && !(ex.Requires?.Packages.Count > 0))
            warn(path, $"{label}: accelerator 'required' without requires.packages (which framework uses it?)");
    }

    /// <summary>Every exercise that needs packages must be coverable by ONE package set (the learner installs sets, never single packages).</summary>
    private static void CheckPackageSet(Exercise ex, string path, string kind, Func<ContentPackageIndex?> index, Action<string, string> error)
    {
        var python = ContentPackageIndex.RequiredPythonPackages(ex);
        var node = ContentPackageIndex.RequiredNodePackages(ex);
        var go = ContentPackageIndex.RequiredGoModules(ex);
        if (python.Count == 0 && node.Count == 0 && go.Count == 0) return;
        if (index() is not { } packages) return;
        var label = $"{kind} '{ex.Id}'";
        foreach (var (language, required) in new[] { (PackageSetLanguage.Python, python), (PackageSetLanguage.Node, node), (PackageSetLanguage.Go, go) })
            if (required.Count > 0 && packages.Resolve(language, required).Error is { } problem)
                error(path, $"{label}: {problem}");
    }

    private static void CheckCycles(ContentSet set, Action<string, string> error)
    {
        // Depth-first search with an explicit path; each cycle is reported once.
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 = on path, 2 = done
        var stack = new List<string>();
        void Visit(Lesson lesson)
        {
            state[lesson.Id] = 1;
            stack.Add(lesson.Id);
            foreach (var pid in lesson.Prerequisites)
            {
                if (set.FindLesson(pid) is not { } pre) continue;
                if (!state.TryGetValue(pid, out var s)) Visit(pre);
                else if (s == 1)
                {
                    var cycle = stack.Skip(stack.IndexOf(pid)).Append(pid);
                    error(lesson.SourcePath, "prerequisite cycle: " + string.Join(" -> ", cycle));
                }
            }
            stack.RemoveAt(stack.Count - 1);
            state[lesson.Id] = 2;
        }
        foreach (var l in set.Lessons)
            if (!state.ContainsKey(l.Id)) Visit(l);
    }

    private static string Shorten(string s) => s.Length <= 40 ? s : s[..40] + "...";
}

using System.Globalization;
using DevAcademy.Domain;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DevAcademy.Content;

public sealed record ContentLoadResult(ContentSet Content, IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// Loads a content root into domain objects and runs <see cref="ContentValidator"/>.
/// Never throws on bad content: problems are returned as diagnostics.
/// </summary>
public static class ContentLoader
{
    /// <summary>Largest accepted data file (<c>stdinFile</c> / <c>expectedFile</c>): 16 MB.</summary>
    public const long MaxDataFileBytes = 16L * 1024 * 1024;

    public static ContentLoadResult Load(string root)
    {
        var run = new Run(Path.GetFullPath(root));
        var set = run.LoadAll();
        var diagnostics = run.Diagnostics.Concat(ContentValidator.Validate(set)).ToList();
        return new ContentLoadResult(set, diagnostics);
    }

    private sealed class Run(string root)
    {
        private static readonly IDeserializer Yaml = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        public List<ContentDiagnostic> Diagnostics { get; } = [];

        private void Error(string path, string message) =>
            Diagnostics.Add(new ContentDiagnostic(DiagnosticSeverity.Error, path, message));

        private void Warn(string path, string message) =>
            Diagnostics.Add(new ContentDiagnostic(DiagnosticSeverity.Warning, path, message));

        private string Rel(string absolute) => Path.GetRelativePath(root, absolute).Replace('\\', '/');

        public ContentSet LoadAll()
        {
            var catalog = Read<CatalogDto>(Path.Combine(root, "catalog.yaml"));
            if (catalog is null) return ContentSet.Empty;

            var tracks = new List<Track>();
            var courses = new List<Course>();
            var lessons = new List<Lesson>();
            var loadedCourseIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var t in catalog.Tracks ?? [])
            {
                var track = ParseTrack(t);
                if (track is null) continue;
                tracks.Add(track);
                foreach (var courseId in track.CourseIds)
                {
                    if (!loadedCourseIds.Add(courseId)) continue;
                    LoadCourse(courseId, courses, lessons);
                }
            }

            var paths = SideContentLoader.LoadPaths(root, Error);
            var comparisons = SideContentLoader.LoadComparisons(root, Error);

            var coursesDir = Path.Combine(root, "courses");
            if (Directory.Exists(coursesDir))
                foreach (var dir in Directory.GetDirectories(coursesDir).Order(StringComparer.Ordinal))
                    if (!loadedCourseIds.Contains(Path.GetFileName(dir)))
                        Warn(Rel(dir), "course directory is not referenced by any track in catalog.yaml");

            return new ContentSet(tracks, courses, lessons, paths, LoadTemplates(), comparisons);
        }

        private List<ProjectTemplate> LoadTemplates()
        {
            var result = new List<ProjectTemplate>();
            var dir = Path.Combine(root, "templates");
            if (!Directory.Exists(dir)) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tdir in Directory.GetDirectories(dir).Order(StringComparer.Ordinal))
            {
                var file = Path.Combine(tdir, "template.yaml");
                var rel = Rel(file);
                var dto = Read<TemplateDto>(file);
                if (dto is null) continue;
                var ok = Require(dto.Id, rel, "id") & Require(dto.Title, rel, "title") & Require(dto.LanguageId, rel, "languageId");
                if (!ok) continue;
                if (dto.Id != Path.GetFileName(tdir)) { Error(rel, $"'id' ({dto.Id}) must match the directory name '{Path.GetFileName(tdir)}'"); continue; }
                if (!seen.Add(dto.Id!)) { Error(rel, $"duplicate template id '{dto.Id}'"); continue; }
                var filesRel = string.IsNullOrWhiteSpace(dto.Files) ? "files" : dto.Files!;
                var filesDir = Path.GetFullPath(Path.Combine(tdir, filesRel));
                if (!filesDir.StartsWith(Path.GetFullPath(tdir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                { Error(rel, "'files' must stay inside the template directory"); continue; }
                if (!Directory.Exists(filesDir) || !Directory.EnumerateFileSystemEntries(filesDir).Any())
                { Error(rel, $"files directory '{filesRel}' is missing or empty"); continue; }
                if (!ValidateTemplateCommands(dto, rel, filesDir)) continue;
                result.Add(new ProjectTemplate(dto.Id!, dto.Title!, dto.LanguageId!, dto.Description ?? "", filesDir, dto.PostCreate ?? [], dto.Scripts));
            }
            return result;
        }

        /// <summary>Offline checks only. postCreate is typed into a terminal without Enter, so one entry must be one line; declared scripts must exist in the template's package.json.</summary>
        private bool ValidateTemplateCommands(TemplateDto dto, string rel, string filesDir)
        {
            var ok = true;
            foreach (var c in dto.PostCreate ?? [])
                if (string.IsNullOrWhiteSpace(c) || c.Contains('\n') || c.Contains('\r'))
                { Error(rel, "'postCreate' entries must be non-empty single-line commands (they are typed into a terminal and never executed by the app)"); ok = false; }
            if (dto.Scripts is not { Count: > 0 } scripts) return ok;

            var pkg = Path.Combine(filesDir, "package.json");
            HashSet<string> declared = [];
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(pkg));
                if (doc.RootElement.TryGetProperty("scripts", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.Object)
                    foreach (var p in s.EnumerateObject()) declared.Add(p.Name);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
            { Error(rel, "'scripts' needs a readable package.json in the template files"); return false; }

            foreach (var (button, script) in scripts)
            {
                if (button is not ("dev" or "test" or "build")) { Error(rel, $"unknown 'scripts' key '{button}' (allowed: dev, test, build)"); ok = false; }
                else if (!declared.Contains(script)) { Error(rel, $"scripts.{button} refers to '{script}', which is not in the template's package.json scripts"); ok = false; }
            }
            foreach (var required in new[] { "dev", "test", "build" })
                if (!scripts.ContainsKey(required)) { Error(rel, $"'scripts' must define '{required}'"); ok = false; }
            return ok;
        }

        private T? Read<T>(string absolutePath) where T : class
        {
            var rel = Rel(absolutePath);
            if (!File.Exists(absolutePath)) { Error(rel, "file not found"); return null; }
            try
            {
                return Yaml.Deserialize<T>(File.ReadAllText(absolutePath)) ?? Fail<T>(rel, "file is empty");
            }
            catch (YamlException ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                Error(rel, $"invalid YAML at line {ex.Start.Line}: {inner}");
                return null;
            }
            catch (IOException ex) { Error(rel, ex.Message); return null; }
        }

        private T? Fail<T>(string rel, string message) where T : class { Error(rel, message); return null; }

        private TEnum? ParseEnum<TEnum>(string? value, string path, string field, TEnum? fallback = null) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (fallback is null) Error(path, $"'{field}' is required");
                return fallback;
            }
            if (Enum.TryParse<TEnum>(value.Replace("-", ""), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
            Error(path, $"'{field}' has unknown value '{value}' (expected one of: {string.Join(", ", Enum.GetNames<TEnum>().Select(n => n.ToLowerInvariant()))})");
            return null;
        }

        private double ParseDouble(string? value, double fallback, string path, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            Error(path, $"'{field}' is not a number: '{value}'");
            return fallback;
        }

        private bool Require(string? value, string path, string field)
        {
            if (!string.IsNullOrWhiteSpace(value)) return true;
            Error(path, $"'{field}' is required");
            return false;
        }

        private Track? ParseTrack(TrackDto t)
        {
            const string path = "catalog.yaml";
            var ok = Require(t.Id, path, "tracks[].id");
            ok &= Require(t.Title, path, $"tracks[{t.Id}].title");
            var kind = ParseEnum<TrackKind>(t.Kind, path, $"tracks[{t.Id}].kind");
            var status = ParseEnum<ContentStatus>(t.Status, path, $"tracks[{t.Id}].status", ContentStatus.Available);
            if (!ok || kind is null || status is null) return null;
            return new Track(t.Id!, kind.Value, t.Title!, t.Description ?? "", t.LanguageId, status.Value, t.Courses ?? []);
        }

        private void LoadCourse(string courseId, List<Course> courses, List<Lesson> lessons)
        {
            var courseDir = Path.Combine(root, "courses", courseId);
            var courseFile = Path.Combine(courseDir, "course.yaml");
            var rel = Rel(courseFile);
            var dto = Read<CourseDto>(courseFile);
            if (dto is null) return;

            var ok = Require(dto.Id, rel, "id") & Require(dto.Version, rel, "version") & Require(dto.Title, rel, "title")
                & Require(dto.LanguageId, rel, "languageId") & Require(dto.Track, rel, "track");
            if (dto.Level is null or < 0 or > 6) { Error(rel, "'level' is required and must be 0-6"); ok = false; }
            if (!ok) return;
            if (dto.Id != courseId) Error(rel, $"course id '{dto.Id}' does not match its directory name '{courseId}'");
            if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Version!, @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$"))
                Error(rel, $"'version' is not semver: '{dto.Version}'");

            var lessonIds = new List<string>();
            foreach (var dirName in dto.Lessons ?? [])
            {
                var lessonDir = Path.Combine(courseDir, "lessons", dirName);
                if (!Directory.Exists(lessonDir)) { Error(rel, $"lesson directory 'lessons/{dirName}' not found"); continue; }
                var lesson = LoadLesson(lessonDir, dto.Id!, dto.LanguageId!);
                if (lesson is null) continue;
                lessons.Add(lesson);
                lessonIds.Add(lesson.Id);
            }
            courses.Add(new Course(dto.Id!, dto.Version!, dto.Track!, dto.LanguageId!, dto.Title!,
                dto.Description ?? "", dto.Level!.Value, lessonIds, rel, dto.Requires ?? [], ParseFramework(dto.Framework, rel)));
        }

        private FrameworkInfo? ParseFramework(FrameworkDto? f, string path)
        {
            if (f is null || !Require(f.Id, path, "framework.id")) return null;
            return new FrameworkInfo(f.Id!, f.Version, f.Docs ?? [], f.Compatibility,
                (f.Deprecated ?? []).Select(x => new DeprecatedFeature(x.Feature ?? "", x.Status ?? "", x.Note ?? "")).ToList(),
                f.MigrationNotes);
        }

        private Lesson? LoadLesson(string dir, string courseId, string courseLanguage)
        {
            var file = Path.Combine(dir, "lesson.yaml");
            var rel = Rel(file);
            var dto = Read<LessonDto>(file);
            if (dto is null) return null;

            var ok = Require(dto.Id, rel, "id") & Require(dto.Title, rel, "title") & Require(dto.Goal, rel, "goal");
            var difficulty = ParseEnum<Difficulty>(dto.Difficulty, rel, "difficulty", Difficulty.Intro);
            if (!ok || difficulty is null) return null;

            var theoryFile = Path.Combine(dir, "theory.md");
            Theory theory;
            if (File.Exists(theoryFile)) theory = TheoryParser.Parse(File.ReadAllText(theoryFile));
            else
            {
                Error(Rel(theoryFile), "file not found");
                theory = new Theory("", new Dictionary<string, string>(), []);
            }

            ComplexityInfo? complexity = null;
            if (dto.Complexity is { } cx)
            {
                if (Require(cx.Time, rel, "complexity.time") & Require(cx.Space, rel, "complexity.space"))
                    complexity = new ComplexityInfo(cx.Time!.Trim(), cx.Space!.Trim(), string.IsNullOrWhiteSpace(cx.Explanation) ? null : cx.Explanation.Trim());
            }
            string? editorial = null;
            if (!string.IsNullOrWhiteSpace(dto.Editorial))
            {
                var editorialFile = Path.GetFullPath(Path.Combine(dir, dto.Editorial));
                if (!editorialFile.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    Error(rel, $"'editorial' must stay inside the lesson directory: '{dto.Editorial}'");
                else if (!File.Exists(editorialFile)) Error(rel, $"editorial file '{dto.Editorial}' not found");
                else editorial = File.ReadAllText(editorialFile);
            }
            else if (dto.Editorial is not null) Error(rel, "'editorial' is empty");

            return new Lesson(
                dto.Id!, courseId, dto.Title!, dto.Goal!, difficulty.Value, dto.EstimatedMinutes ?? 0,
                dto.Prerequisites ?? [], dto.Objectives ?? [], dto.MasteryCriteria ?? [],
                dto.Related ?? [], dto.Next ?? [], dto.SeeAlso ?? [], dto.Tags ?? [],
                theory,
                (dto.Facts ?? []).Select(f => ParseFact(f, rel)).OfType<Fact>().ToList(),
                dto.Quiz is null ? null : ParseQuiz(dto.Quiz, rel),
                dto.Exercise is null ? null : ParseExercise(dto.Exercise, dto, dir, rel, courseLanguage),
                dto.Challenge is null ? null : ParseExercise(dto.Challenge, dto, dir, rel, courseLanguage),
                rel, dto.AssessmentFor ?? [], complexity, editorial);
        }

        private Fact? ParseFact(FactDto f, string path)
        {
            var kind = ParseEnum<FactKind>(f.Kind, path, "facts[].kind");
            if (kind is null || !Require(f.Text, path, "facts[].text")) return null;
            return new Fact(kind.Value, f.Text!, f.Sources ?? []);
        }

        private Quiz ParseQuiz(QuizDto q, string path)
        {
            var threshold = ParseDouble(q.PassThreshold, 0.7, path, "quiz.passThreshold");
            var questions = (q.Questions ?? []).Select(x => ParseQuestion(x, path)).OfType<QuizQuestion>().ToList();
            return new Quiz(threshold, questions);
        }

        private QuizQuestion? ParseQuestion(QuestionDto q, string path)
        {
            var ok = Require(q.Id, path, "quiz.questions[].id") & Require(q.Prompt, path, $"quiz.questions[{q.Id}].prompt");
            var type = ParseEnum<QuestionType>(q.Type, path, $"quiz.questions[{q.Id}].type");
            if (!ok || type is null) return null;

            var where = $"quiz.questions[{q.Id}].answer";
            var indices = new List<int>();
            var accepted = new List<string>();
            double? number = null;
            var scalars = q.Answer switch
            {
                null => [],
                string s => new List<string> { s },
                IEnumerable<object> list => list.Select(o => o?.ToString() ?? "").ToList(),
                _ => [q.Answer.ToString() ?? ""],
            };
            if (scalars.Count == 0) { Error(path, $"'{where}' is required"); return null; }

            switch (type.Value)
            {
                case QuestionType.Single or QuestionType.Multiple:
                    foreach (var s in scalars)
                    {
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)) indices.Add(idx);
                        else { Error(path, $"'{where}' must be option indices, got '{s}'"); return null; }
                    }
                    break;
                case QuestionType.Short:
                    accepted.AddRange(scalars);
                    break;
                case QuestionType.Numeric:
                    if (scalars.Count == 1 && double.TryParse(scalars[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) number = n;
                    else { Error(path, $"'{where}' must be a single number"); return null; }
                    break;
            }
            return new QuizQuestion(q.Id!, type.Value, q.Prompt!, q.Options ?? [], indices, accepted, number,
                ParseDouble(q.Tolerance, 0, path, $"quiz.questions[{q.Id}].tolerance"), q.Explanation ?? "");
        }

        /// <summary>Parses the exercise and attaches <c>requires</c> / <c>accelerator</c> (the exercise's own, else the lesson's).</summary>
        private Exercise? ParseExercise(ExerciseDto e, LessonDto lesson, string lessonDir, string path, string courseLanguage)
        {
            var exercise = ParseExerciseCore(e, lessonDir, path, courseLanguage);
            if (exercise is null) return null;
            var where = $"exercise[{e.Id}]";
            var requires = ParseRequires(e.Requires ?? lesson.Requires);
            var accelerator = ParseEnum<AcceleratorNeed>(e.Accelerator ?? lesson.Accelerator, path, $"{where}.accelerator", AcceleratorNeed.None);
            return accelerator is null ? null : exercise with { Requires = requires, Accelerator = accelerator.Value };
        }

        private static ExerciseRequirements? ParseRequires(RequiresDto? r) =>
            r is null ? null : new ExerciseRequirements(string.IsNullOrWhiteSpace(r.Python) ? null : r.Python.Trim(),
                (r.Packages ?? []).Select(p => p.Trim()).ToList(),
                string.IsNullOrWhiteSpace(r.Node) ? null : r.Node.Trim(),
                (r.NpmPackages ?? []).Select(p => p.Trim()).ToList(),
                string.IsNullOrWhiteSpace(r.Go) ? null : r.Go.Trim(),
                (r.GoModules ?? []).Select(p => p.Trim()).ToList(),
                r.MemoryMb,
                (r.NugetPackages ?? []).Select(p => p.Trim()).ToList(),
                (r.MavenArtifacts ?? []).Select(p => p.Trim()).ToList());

        private Exercise? ParseExerciseCore(ExerciseDto e, string lessonDir, string path, string courseLanguage)
        {
            var ok = Require(e.Id, path, "exercise.id") & Require(e.Title, path, $"exercise[{e.Id}].title");
            var kind = ParseEnum<EvaluationKind>(e.Evaluation, path, $"exercise[{e.Id}].evaluation", EvaluationKind.Io);
            var tests = (e.Tests ?? []).Select(t => ParseTest(t, path, e.Id!, lessonDir)).OfType<TestCase>().ToList();

            if (e.Variants is not null)
            {
                var variants = ParseVariants(e, kind, lessonDir, path);
                if (!ok || kind is null || variants is null) return null;
                var first = variants[0];
                return new Exercise(e.Id!, e.Title!, e.Prompt ?? "", first.LanguageId, first.EntryFile, first.Starter, first.Solution,
                    kind.Value, first.Harness, first.HarnessEntry, tests, e.Hints ?? [], null, variants);
            }

            ok &= Require(e.EntryFile, path, $"exercise[{e.Id}].entryFile");
            if (!ok || kind is null) return null;
            var harness = kind == EvaluationKind.Harness
                ? ReadFiles(Path.Combine(lessonDir, e.Harness ?? "exercise/harness"))
                : [];
            var command = kind == EvaluationKind.Command ? ParseCommand(e, lessonDir, path) : null;
            if (kind == EvaluationKind.Command && command is null) return null;
            return new Exercise(
                e.Id!, e.Title!, e.Prompt ?? "", e.LanguageId ?? courseLanguage, e.EntryFile!,
                ReadFiles(Path.Combine(lessonDir, e.Starter ?? "exercise/starter")),
                ReadFiles(Path.Combine(lessonDir, e.Solution ?? "exercise/solution")),
                kind.Value, harness, e.HarnessEntry, tests, e.Hints ?? [], command);
        }

        /// <summary>The map key is the language id. Top-level language fields are not allowed next to 'variants'.</summary>
        private List<ExerciseVariant>? ParseVariants(ExerciseDto e, EvaluationKind? kind, string lessonDir, string path)
        {
            var where = $"exercise[{e.Id}]";
            var ok = true;
            foreach (var (field, value) in new[]
                     {
                         ("languageId", e.LanguageId), ("entryFile", e.EntryFile), ("starter", e.Starter),
                         ("solution", e.Solution), ("harness", e.Harness), ("harnessEntry", e.HarnessEntry),
                     })
                if (value is not null)
                {
                    Error(path, $"{where}: '{field}' cannot be combined with 'variants' (declare it inside each variant)");
                    ok = false;
                }
            if (e.Variants!.Count == 0) { Error(path, $"{where}: 'variants' needs at least one language"); return null; }

            var result = new List<ExerciseVariant>();
            foreach (var (language, v) in e.Variants)
            {
                var vw = $"{where}.variants[{language}]";
                if (string.IsNullOrWhiteSpace(language)) { Error(path, $"{where}: a variant has an empty language id"); ok = false; continue; }
                if (v is null) { Error(path, $"{vw} is empty"); ok = false; continue; }
                if (!Require(v.EntryFile, path, $"{vw}.entryFile")) { ok = false; continue; }
                var harness = kind == EvaluationKind.Harness
                    ? ReadFiles(Path.Combine(lessonDir, v.Harness ?? $"exercise/{language}/harness"))
                    : [];
                result.Add(new ExerciseVariant(language, v.EntryFile!,
                    ReadFiles(Path.Combine(lessonDir, v.Starter ?? $"exercise/{language}/starter")),
                    ReadFiles(Path.Combine(lessonDir, v.Solution ?? $"exercise/{language}/solution")),
                    harness, v.HarnessEntry));
            }
            return ok && result.Count > 0 ? result : null;
        }

        private TestCase? ParseTest(TestDto t, string path, string exerciseId, string lessonDir)
        {
            var where = $"exercise[{exerciseId}].tests[{t.Id}]";
            if (!Require(t.Id, path, $"exercise[{exerciseId}].tests[].id")) return null;
            var compare = ParseEnum<OutputComparison>(t.Compare, path, $"{where}.compare", OutputComparison.Trim);
            if (compare is null) return null;
            var ok = true;
            var stdin = ResolveData(t.Stdin, t.StdinFile, "stdin", "stdinFile", lessonDir, path, where, ref ok);
            var expected = ResolveData(t.Expected, t.ExpectedFile, "expected", "expectedFile", lessonDir, path, where, ref ok);
            if (!ok) return null;
            return new TestCase(t.Id!, t.Title ?? t.Id!, stdin, expected, compare.Value,
                ParseDouble(t.Tolerance, OutputComparer.DefaultTolerance, path, $"{where}.tolerance"),
                t.Hidden ?? false, t.Hint, string.IsNullOrWhiteSpace(t.Match) ? null : t.Match);
        }

        private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

        /// <summary>
        /// The inline value, or the content of the referenced file (path relative to the lesson directory, inside it,
        /// at most <see cref="MaxDataFileBytes"/>, valid UTF-8). Inline and file are mutually exclusive.
        /// </summary>
        private string? ResolveData(string? inline, string? file, string inlineField, string fileField,
            string lessonDir, string path, string where, ref bool ok)
        {
            if (file is null) return inline;
            if (inline is not null) { Error(path, $"{where}: '{inlineField}' and '{fileField}' are mutually exclusive"); ok = false; return null; }
            if (string.IsNullOrWhiteSpace(file)) { Error(path, $"{where}.{fileField} is empty"); ok = false; return null; }
            var root = Path.GetFullPath(lessonDir) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(lessonDir, file));
            if (Path.IsPathRooted(file) || !full.StartsWith(root, StringComparison.Ordinal))
            { Error(path, $"{where}.{fileField} must be a relative path inside the lesson directory: '{file}'"); ok = false; return null; }
            var info = new FileInfo(full);
            if (!info.Exists) { Error(path, $"{where}.{fileField}: file '{file}' not found"); ok = false; return null; }
            if (info.Length > MaxDataFileBytes)
            { Error(path, $"{where}.{fileField}: file '{file}' is {info.Length} bytes, the limit is {MaxDataFileBytes} (16 MB)"); ok = false; return null; }
            try
            {
                var text = StrictUtf8.GetString(File.ReadAllBytes(full));
                return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
            }
            catch (System.Text.DecoderFallbackException) { Error(path, $"{where}.{fileField}: file '{file}' is not valid UTF-8"); }
            catch (IOException ex) { Error(path, $"{where}.{fileField}: {ex.Message}"); }
            ok = false;
            return null;
        }

        private CommandSpec? ParseCommand(ExerciseDto e, string lessonDir, string path)
        {
            var where = $"exercise[{e.Id}].command";
            if (e.Command is not { } c) { Error(path, $"exercise[{e.Id}]: command evaluation needs 'command'"); return null; }
            var report = ParseEnum<CommandReportFormat>(c.Report, path, $"{where}.report");
            var dir = e.TestsDir ?? "exercise/tests";
            var testsDir = Path.GetFullPath(Path.Combine(lessonDir, dir));
            if (!testsDir.StartsWith(Path.GetFullPath(lessonDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            { Error(path, $"exercise[{e.Id}].tests_dir must stay inside the lesson directory: '{dir}'"); return null; }
            if (report is null) return null;
            return new CommandSpec(c.Run ?? [], report.Value, c.ReportFile, c.TimeoutSeconds,
                string.IsNullOrWhiteSpace(c.TestsTarget) ? "tests" : c.TestsTarget.Trim().TrimEnd('/'), ReadFiles(testsDir),
                string.IsNullOrWhiteSpace(c.Tool) ? null : c.Tool.Trim(), c.Args);
        }

        private static List<ExerciseFile> ReadFiles(string dir) =>
            !Directory.Exists(dir)
                ? []
                : Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
                    .Order(StringComparer.Ordinal)
                    .Select(rel => new ExerciseFile(rel, File.ReadAllText(Path.Combine(dir, rel))))
                    .ToList();
    }
}

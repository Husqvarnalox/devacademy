using DevAcademy.Application.Evaluation;
using DevAcademy.Content;
using DevAcademy.Domain;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;

return await Cli.RunAsync(args);

internal static class Cli
{
    private const string Usage = """
        usage:
          devacademy validate-content [--content <dir>] [--run-solutions]
          devacademy list [--content <dir>]
          devacademy runtimes
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help") { Console.WriteLine(Usage); return args.Length == 0 ? 2 : 0; }
        if (args[0] == "runtimes") return args.Length == 1 ? await RuntimesAsync() : 2;
        var options = Parse(args.Skip(1).ToArray());
        if (options is null) { Console.Error.WriteLine(Usage); return 2; }

        var root = options.ContentDir ?? FileContentRepository.FindContentRoot();
        if (root is null || !File.Exists(Path.Combine(root, "catalog.yaml")))
        {
            Console.Error.WriteLine($"error: no content/catalog.yaml found{(root is null ? "" : " in " + root)}; use --content <dir>");
            return 2;
        }

        switch (args[0])
        {
            case "validate-content": return await ValidateAsync(root, options.RunSolutions);
            case "list" when !options.RunSolutions: return List(root);
            default: Console.Error.WriteLine(Usage); return 2;
        }
    }

    private static async Task<int> RuntimesAsync()
    {
        var statuses = await ExecutionEngine.CreateDefault().DetectRuntimesAsync(CancellationToken.None);
        foreach (var s in statuses)
        {
            Console.WriteLine($"{s.LanguageId,-11} {s.Availability,-11} {s.Version ?? "-",-24} {s.ExecutablePath ?? ""}");
            if (s.Notes is { Length: > 0 }) Console.WriteLine($"{"",-11} note: {s.Notes}");
            if (s.Availability != RuntimeAvailability.Available && s.InstallHint is { Length: > 0 })
                Console.WriteLine($"{"",-11} install: {s.InstallHint}");
        }
        return 0;
    }

    private sealed record Options(string? ContentDir, bool RunSolutions);

    private static Options? Parse(string[] rest)
    {
        string? dir = null;
        var run = false;
        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--content" when i + 1 < rest.Length: dir = rest[++i]; break;
                case "--run-solutions": run = true; break;
                default: Console.Error.WriteLine($"error: unknown or incomplete argument '{rest[i]}'"); return null;
            }
        }
        return new Options(dir, run);
    }

    private static int List(string root)
    {
        var result = ContentLoader.Load(root);
        var set = result.Content;
        foreach (var track in set.Tracks)
        {
            Console.WriteLine($"{track.Title} [{track.Id}] ({track.Kind}, {track.Status})");
            foreach (var course in track.CourseIds.Select(set.FindCourse).OfType<Course>())
            {
                Console.WriteLine($"  {course.Title} [{course.Id}] v{course.Version} level {course.Level}");
                foreach (var l in set.LessonsOf(course.Id))
                    Console.WriteLine($"    {l.Id}  {l.Title} ({l.Difficulty}, {l.EstimatedMinutes} min)");
            }
        }
        if (result.HasErrors) Console.Error.WriteLine("note: content has errors; run validate-content");
        return 0;
    }

    private static async Task<int> ValidateAsync(string root, bool runSolutions)
    {
        var result = ContentLoader.Load(root);
        var diagnostics = result.Diagnostics.ToList();
        if (runSolutions && !result.HasErrors) diagnostics.AddRange(await RunSolutionsAsync(result.Content));
        else if (runSolutions) Console.WriteLine("skipping --run-solutions: fix content errors first");

        foreach (var d in diagnostics.OrderBy(d => d.Path, StringComparer.Ordinal).ThenBy(d => d.Severity))
            Console.WriteLine(d);
        var errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = diagnostics.Count - errors;
        Console.WriteLine($"{result.Content.Lessons.Count} lessons in {result.Content.Courses.Count} courses, {result.Content.Templates.Count} templates: {errors} error(s), {warnings} warning(s)");
        return errors > 0 ? 1 : 0;
    }

    private static async Task<List<ContentDiagnostic>> RunSolutionsAsync(ContentSet set)
    {
        var diagnostics = new List<ContentDiagnostic>();
        var runner = ExecutionEngine.CreateDefault();
        var service = new ExerciseEvaluationService(runner);
        var missing = new HashSet<string>(StringComparer.Ordinal);

        void Add(DiagnosticSeverity s, Lesson l, string msg) => diagnostics.Add(new ContentDiagnostic(s, l.SourcePath, msg));
        void RuntimeMissing(Lesson l, string language)
        {
            if (missing.Add(language)) Add(DiagnosticSeverity.Warning, l, $"runtime for '{language}' is not installed: its solutions and code samples are skipped");
        }

        foreach (var lesson in set.Lessons)
        {
            foreach (var ex in new[] { lesson.Exercise, lesson.Challenge }.OfType<Exercise>())
            {
                // Every language variant must have a passing reference solution and a failing starter.
                foreach (var variant in ex.AllVariants)
                {
                    var tag = ex.HasExplicitVariants ? $"exercise '{ex.Id}' [{variant.LanguageId}]" : $"exercise '{ex.Id}'";
                    var solution = await service.EvaluateAsync(ex, variant.Solution, languageId: variant.LanguageId);
                    if (solution.Status == Verdict.RuntimeMissing) { RuntimeMissing(lesson, variant.LanguageId); continue; }
                    if (solution.Status != Verdict.Passed)
                        Add(DiagnosticSeverity.Error, lesson, $"{tag}: reference solution did not pass ({solution.Status}): {Describe(solution)}");

                    var starter = await service.EvaluateAsync(ex, variant.Starter, languageId: variant.LanguageId);
                    if (starter.Status == Verdict.Passed)
                        Add(DiagnosticSeverity.Error, lesson, $"{tag}: starter code already passes all tests");
                }
            }

            foreach (var sample in lesson.Theory.Samples.Where(s => s.ExpectedOutput is not null))
            {
                var file = EntryFileFor(sample.LanguageId);
                var run = await runner.RunOnceAsync(
                    new ExecutionRequest(sample.LanguageId, [new SourceFile(file, sample.Code)], file, IsolationLevel.OsSandbox),
                    null, ResourceLimits.DefaultRun, CancellationToken.None);
                if (run.Status == ExecutionStatus.RuntimeMissing) { RuntimeMissing(lesson, sample.LanguageId); continue; }
                if (!run.IsSuccess)
                    Add(DiagnosticSeverity.Error, lesson, $"code sample ({sample.LanguageId}) failed with {run.Status}: {First(run.CompileOutput + run.Stderr)}");
                else if (!OutputComparer.AreEqual(sample.ExpectedOutput!, run.Stdout, OutputComparison.Trim))
                    Add(DiagnosticSeverity.Error, lesson, $"code sample ({sample.LanguageId}) output differs. expected: {First(sample.ExpectedOutput!)} actual: {First(run.Stdout)}");
            }
        }
        return diagnostics;
    }

    private static string Describe(EvaluationReport r) =>
        r.Cases.Count == 0
            ? First(r.Message ?? "")
            : string.Join("; ", r.Cases.Where(c => !c.Passed).Select(c => $"{c.Title}: {First(c.Message ?? "failed")}"));

    private static string First(string s)
    {
        s = s.Trim().ReplaceLineEndings(" | ");
        return s.Length <= 200 ? s : s[..200] + "...";
    }

    private static string EntryFileFor(string language) => ExecutionEngine.DefaultEntryFile(language) ?? "main.txt";
}

using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Go;
using DevAcademy.Application.Node;
using DevAcademy.Application.Python;
using DevAcademy.Content;
using DevAcademy.Domain;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;

return await Cli.RunAsync(args);

internal static class Cli
{
    private const string Usage = """
        usage:
          devacademy validate-content [--content <dir>] [--run-solutions [--python-env <venv dir>] [--node-env <dir>] [--go-env <dir>]]
          devacademy python-env-check --python-env <venv dir> [--content <dir>]
          devacademy list [--content <dir>]
          devacademy runtimes
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help") { Console.WriteLine(Usage); return args.Length == 0 ? 2 : 0; }
        if (args[0] == "runtimes") return args.Length == 1 ? await RuntimesAsync() : 2;
        var options = Parse(args.Skip(1).ToArray(), out var parseError);
        if (options is null) { Console.Error.WriteLine(parseError); Console.Error.WriteLine(Usage); return 2; }

        var root = options.ContentDir ?? FileContentRepository.FindContentRoot();
        if (root is null || !File.Exists(Path.Combine(root, "catalog.yaml")))
        {
            Console.Error.WriteLine($"error: no content/catalog.yaml found{(root is null ? "" : " in " + root)}; use --content <dir>");
            return 2;
        }

        switch (args[0])
        {
            case "validate-content" when (options.PythonEnv is not null || options.NodeEnv is not null || options.GoEnv is not null) && !options.RunSolutions:
                Console.Error.WriteLine("error: --python-env, --node-env and --go-env only apply together with --run-solutions");
                return 2;
            case "validate-content":
                {
                    PythonEnvironmentManager? manager = null;
                    PythonRunSelection? selection = null;
                    if (options.PythonEnv is not null)
                    {
                        (manager, selection, var envError) = await OpenEnvironmentAsync(options.PythonEnv);
                        if (selection is null) { Console.Error.WriteLine(envError); return 2; }
                    }
                    NodeEnvironmentService? node = null;
                    if (options.NodeEnv is not null)
                    {
                        var full = Path.GetFullPath(options.NodeEnv);
                        if (!NodeEnvironmentService.HasInstalledSet(full))
                        {
                            Console.Error.WriteLine($"error: node environment '{options.NodeEnv}' has no installed package sets (a node_modules per set subdirectory; run `make node-env`)");
                            return 2;
                        }
                        if (ContentPackageIndex.TryLoadDefault() is not { } packages)
                        {
                            Console.Error.WriteLine("error: tools/content-packages/index.json (the package sets) was not found next to the CLI or in a parent directory");
                            return 2;
                        }
                        node = new NodeEnvironmentService(new SystemToolRunner(), full, packages);
                    }
                    GoEnvironmentService? go = null;
                    if (options.GoEnv is not null)
                    {
                        var full = Path.GetFullPath(options.GoEnv);
                        if (!GoEnvironmentService.HasInstalledSet(full))
                        {
                            Console.Error.WriteLine($"error: Go environment '{options.GoEnv}' has no installed package sets (a pkg/mod per set subdirectory; run `make go-env`)");
                            return 2;
                        }
                        if (ContentPackageIndex.TryLoadDefault() is not { } packages)
                        {
                            Console.Error.WriteLine("error: tools/content-packages/index.json (the package sets) was not found next to the CLI or in a parent directory");
                            return 2;
                        }
                        go = new GoEnvironmentService(new SystemToolRunner(), full, packages);
                    }
                    return await ValidateAsync(root, options.RunSolutions, manager, selection, node, go);
                }
            case "python-env-check" when options.PythonEnv is not null && !options.RunSolutions:
                {
                    var (manager, selection, envError) = await OpenEnvironmentAsync(options.PythonEnv);
                    if (selection is null) { Console.Error.WriteLine(envError); return 2; }
                    return await EnvCheckAsync(root, manager!, selection);
                }
            case "python-env-check":
                Console.Error.WriteLine("error: python-env-check needs --python-env <venv dir> (and takes no --run-solutions)");
                return 2;
            case "list" when !options.RunSolutions && options.PythonEnv is null && options.NodeEnv is null && options.GoEnv is null: return List(root);
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

    internal sealed record Options(string? ContentDir, bool RunSolutions, string? PythonEnv, string? NodeEnv = null, string? GoEnv = null);

    internal static Options? Parse(string[] rest, out string? error)
    {
        string? dir = null, env = null, nodeEnv = null, goEnv = null;
        var run = false;
        error = null;
        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--content" when i + 1 < rest.Length: dir = rest[++i]; break;
                case "--python-env" when i + 1 < rest.Length && !rest[i + 1].StartsWith("--", StringComparison.Ordinal): env = rest[++i]; break;
                case "--node-env" when i + 1 < rest.Length && !rest[i + 1].StartsWith("--", StringComparison.Ordinal): nodeEnv = rest[++i]; break;
                case "--go-env" when i + 1 < rest.Length && !rest[i + 1].StartsWith("--", StringComparison.Ordinal): goEnv = rest[++i]; break;
                case "--run-solutions": run = true; break;
                default: error = $"error: unknown or incomplete argument '{rest[i]}'"; return null;
            }
        }
        return new Options(dir, run, env, nodeEnv, goEnv);
    }

    /// <summary>Finds the interpreter of a venv directory (bin/python or Scripts/python.exe) and checks that it answers.</summary>
    private static async Task<(PythonEnvironmentManager? Manager, PythonRunSelection? Selection, string? Error)> OpenEnvironmentAsync(string envDir)
    {
        var full = Path.GetFullPath(envDir);
        if (!Directory.Exists(full)) return (null, null, $"error: python environment '{envDir}' does not exist (run `make python-env`)");
        var manager = new PythonEnvironmentManager(Path.GetDirectoryName(full.TrimEnd(Path.DirectorySeparatorChar)) ?? full);
        var python = manager.ResolveInterpreter(full);
        if (python is null) return (null, null, $"error: no interpreter in '{envDir}' (expected bin/python or Scripts/python.exe)");
        var probe = await manager.ProbeAsync(python, [], CancellationToken.None);
        if (!probe.Ok) return (null, null, $"error: the interpreter '{python}' does not work: {First(probe.Error ?? "no answer")}");
        return (manager, new PythonRunSelection(python, full), null);
    }

    private static async Task<int> EnvCheckAsync(string root, PythonEnvironmentManager manager, PythonRunSelection selection)
    {
        var result = ContentLoader.Load(root);
        var requirements = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal); // spec text -> lessons
        foreach (var lesson in result.Content.Lessons)
            foreach (var ex in new[] { lesson.Exercise, lesson.Challenge }.OfType<Exercise>().Where(e => e.HasRequirements))
            {
                var req = ex.Requires!;
                if (!string.IsNullOrWhiteSpace(req.Python)) Use($"python{req.Python}");
                foreach (var p in req.Packages) Use(p);
                void Use(string spec)
                {
                    if (!requirements.TryGetValue(spec, out var users)) requirements[spec] = users = new SortedSet<string>(StringComparer.Ordinal);
                    users.Add(lesson.Id);
                }
            }

        var names = requirements.Keys.Where(k => !k.StartsWith("python", StringComparison.Ordinal))
            .Select(k => PythonPackageSpec.TryParse(k, out var spec, out _) ? spec.Name : null).OfType<string>().ToList();
        var probe = await manager.ProbeAsync(selection.InterpreterPath, names, CancellationToken.None);
        if (!probe.Ok) { Console.Error.WriteLine($"error: cannot probe '{selection.InterpreterPath}': {First(probe.Error ?? "")}"); return 2; }

        Console.WriteLine($"environment {selection.EnvironmentPath}: Python {probe.Version} ({selection.InterpreterPath})");
        var missing = 0;
        foreach (var (spec, users) in requirements)
        {
            var isPython = spec.StartsWith("python", StringComparison.Ordinal);
            var check = RequirementChecker.Check(isPython ? new ExerciseRequirements(spec[6..], []) : new ExerciseRequirements(null, [spec]), probe);
            var shown = isPython ? $"Python {probe.Version}"
                : PythonPackageSpec.TryParse(spec, out var ps, out _) && probe.Installed.TryGetValue(ps.Name, out var v) ? "installed " + v : "not installed";
            var label = check.Satisfied ? "ok     " : "MISSING";
            if (!check.Satisfied) missing++;
            Console.WriteLine($"  {label} {spec,-24} {shown,-22} used by {users.Count} lesson(s): {string.Join(", ", users.Take(3))}{(users.Count > 3 ? ", ..." : "")}");
        }
        Console.WriteLine($"{requirements.Count} requirement(s) in content, {missing} not satisfied");
        if (missing > 0) Console.WriteLine("hint: make python-env  (installs tools/python-dev-requirements.txt into .venv-content; downloads from PyPI)");
        return missing > 0 ? 1 : 0;
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

    private static async Task<int> ValidateAsync(string root, bool runSolutions, PythonEnvironmentManager? manager, PythonRunSelection? python, NodeEnvironmentService? node = null, GoEnvironmentService? go = null)
    {
        var result = ContentLoader.Load(root);
        var diagnostics = result.Diagnostics.ToList();
        if (runSolutions && !result.HasErrors) diagnostics.AddRange(await RunSolutionsAsync(result.Content, manager, python, node, go));
        else if (runSolutions) Console.WriteLine("skipping --run-solutions: fix content errors first");

        foreach (var d in diagnostics.OrderBy(d => d.Path, StringComparer.Ordinal).ThenBy(d => d.Severity))
            Console.WriteLine(d);
        var errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = diagnostics.Count - errors;
        Console.WriteLine($"{result.Content.Lessons.Count} lessons in {result.Content.Courses.Count} courses, {result.Content.Templates.Count} templates: {errors} error(s), {warnings} warning(s)");
        return errors > 0 ? 1 : 0;
    }

    private static string DescribeRequirements(ExerciseRequirements r)
    {
        var parts = new List<string>();
        if (r.Packages.Count > 0) parts.Add("packages " + string.Join(", ", r.Packages));
        if (!string.IsNullOrWhiteSpace(r.Python)) parts.Add("python " + r.Python);
        return string.Join(" and ", parts);
    }

    private static async Task<List<ContentDiagnostic>> RunSolutionsAsync(ContentSet set, PythonEnvironmentManager? manager, PythonRunSelection? python, NodeEnvironmentService? node, GoEnvironmentService? go)
    {
        var diagnostics = new List<ContentDiagnostic>();
        var runner = ExecutionEngine.CreateDefault();
        var resolver = manager is not null && python is not null ? new ExternalEnvironmentResolver(manager, python) : null;
        var service = new ExerciseEvaluationService(runner, resolver, node, go);
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
                    var needsEnv = variant.LanguageId == "python" && ex.HasRequirements;
                    var needsNode = ex.NeedsNodeEnvironment;
                    if (needsNode && node is null)
                    {
                        Add(DiagnosticSeverity.Warning, lesson, $"{tag}: needs npm packages ({string.Join(", ", ex.Requires?.Npm ?? [])}); pass --node-env <dir> (make node-env): solution and starter were not run");
                        continue;
                    }
                    var needsGo = ex.NeedsGoEnvironment;
                    if (needsGo && go is null)
                    {
                        Add(DiagnosticSeverity.Warning, lesson, $"{tag}: needs Go modules ({string.Join(", ", ex.Requires?.Modules ?? [])}); pass --go-env <dir> (make go-env): solution and starter were not run");
                        continue;
                    }
                    if (needsEnv && resolver is null)
                    {
                        Add(DiagnosticSeverity.Warning, lesson, $"{tag}: requires {DescribeRequirements(ex.Requires!)}; pass --python-env <venv dir> (make python-env): solution and starter were not run");
                        continue;
                    }
                    var solution = await service.EvaluateAsync(ex, variant.Solution, languageId: variant.LanguageId);
                    if (solution.Status != Verdict.Passed && TimedOut(solution))
                    {
                        // A busy machine can push a run past its wall limit: retry once, and keep a pass-on-retry visible as a warning.
                        solution = await service.EvaluateAsync(ex, variant.Solution, languageId: variant.LanguageId);
                        if (solution.Status == Verdict.Passed)
                            Add(DiagnosticSeverity.Warning, lesson, $"{tag}: reference solution timed out and passed only on retry (machine under load?)");
                    }
                    if (solution.Status == Verdict.RuntimeMissing && needsEnv)
                    {
                        Add(DiagnosticSeverity.Error, lesson, $"{tag}: the Python environment cannot run it: {First(solution.Message ?? "")}");
                        continue;
                    }
                    if (solution.Status == Verdict.RuntimeMissing && needsNode)
                    {
                        Add(DiagnosticSeverity.Error, lesson, $"{tag}: the node environment cannot run it: {First(solution.Message ?? "")}");
                        continue;
                    }
                    if (solution.Status == Verdict.RuntimeMissing && needsGo)
                    {
                        Add(DiagnosticSeverity.Error, lesson, $"{tag}: the Go environment cannot run it: {First(solution.Message ?? "")}");
                        continue;
                    }
                    if (solution.Status == Verdict.RuntimeMissing) { RuntimeMissing(lesson, variant.LanguageId); continue; }
                    if (solution.Status != Verdict.Passed)
                        Add(DiagnosticSeverity.Error, lesson, $"{tag}: reference solution did not pass ({solution.Status}): {Describe(solution)}");

                    var starter = await service.EvaluateAsync(ex, variant.Starter, languageId: variant.LanguageId);
                    if (starter.Status == Verdict.Passed)
                        Add(DiagnosticSeverity.Error, lesson, $"{tag}: starter code already passes all tests");
                }
            }

            // Theory samples have no requires of their own: a Python sample needs what the lesson's exercises declare.
            var lessonRequires = new[] { lesson.Exercise, lesson.Challenge }.OfType<Exercise>().FirstOrDefault(e => e.HasRequirements)?.Requires;
            var warnedSamples = false;
            var nodeExercise = new[] { lesson.Exercise, lesson.Challenge }.OfType<Exercise>().FirstOrDefault(e => e.NeedsNodeEnvironment);
            var goExercise = new[] { lesson.Exercise, lesson.Challenge }.OfType<Exercise>().FirstOrDefault(e => e.NeedsGoEnvironment);
            foreach (var sample in lesson.Theory.Samples.Where(s => s.ExpectedOutput is not null))
            {
                PythonRunSelection? sampleEnv = null;
                if (sample.LanguageId == "python" && lessonRequires is not null)
                {
                    if (resolver is null)
                    {
                        if (!warnedSamples) Add(DiagnosticSeverity.Warning, lesson, $"code samples (python) require {DescribeRequirements(lessonRequires)}; pass --python-env <venv dir> (make python-env): they were not run");
                        warnedSamples = true;
                        continue;
                    }
                    var problem = await resolver.CheckAsync(lessonRequires, CancellationToken.None);
                    if (problem is not null) { Add(DiagnosticSeverity.Error, lesson, $"code sample (python): {First(problem)}"); continue; }
                    sampleEnv = python;
                }
                NodeRunSelection? sampleNode = null;
                if (sample.LanguageId == "javascript" && nodeExercise is not null)
                {
                    if (node is null)
                    {
                        if (!warnedSamples) Add(DiagnosticSeverity.Warning, lesson, "code samples (javascript) use npm packages; pass --node-env <dir> (make node-env): they were not run");
                        warnedSamples = true;
                        continue;
                    }
                    var nodePlan = await node.ResolveAsync(nodeExercise, CancellationToken.None);
                    if (nodePlan.BlockedMessage is not null) { Add(DiagnosticSeverity.Error, lesson, $"code sample (javascript): {First(nodePlan.BlockedMessage)}"); continue; }
                    sampleNode = nodePlan.Node;
                }
                // A Go sample that uses a go package set builds as the lesson's exercise does: its main.go beside the exercise's go.mod and go.sum.
                GoRunSelection? sampleGo = null;
                var sampleFiles = new List<SourceFile>();
                var file = EntryFileFor(sample.LanguageId);
                if (sample.LanguageId == "go" && goExercise is not null)
                {
                    if (go is null)
                    {
                        if (!warnedSamples) Add(DiagnosticSeverity.Warning, lesson, "code samples (go) use Go modules; pass --go-env <dir> (make go-env): they were not run");
                        warnedSamples = true;
                        continue;
                    }
                    var goPlan = await go.ResolveAsync(goExercise, CancellationToken.None);
                    if (goPlan.BlockedMessage is not null) { Add(DiagnosticSeverity.Error, lesson, $"code sample (go): {First(goPlan.BlockedMessage)}"); continue; }
                    sampleGo = goPlan.Go;
                    sampleFiles.AddRange(goExercise.Solution.Where(f => f.Path is GoEnvironmentService.ModFile or GoEnvironmentService.SumFile).Select(f => new SourceFile(f.Path, f.Content)));
                }
                sampleFiles.Add(new SourceFile(file, sample.Code));
                var run = await runner.RunOnceAsync(
                    new ExecutionRequest(sample.LanguageId, sampleFiles, file, IsolationLevel.OsSandbox, sampleEnv, null, sampleNode, sampleGo),
                    null, ResourceLimits.DefaultRun, CancellationToken.None);
                if (run.Status == ExecutionStatus.Timeout)
                {
                    run = await runner.RunOnceAsync(
                        new ExecutionRequest(sample.LanguageId, sampleFiles, file, IsolationLevel.OsSandbox, sampleEnv, null, sampleNode, sampleGo),
                        null, ResourceLimits.DefaultRun, CancellationToken.None);
                    if (run.IsSuccess) Add(DiagnosticSeverity.Warning, lesson, $"code sample ({sample.LanguageId}) timed out and passed only on retry (machine under load?)");
                }
                if (run.Status == ExecutionStatus.RuntimeMissing) { RuntimeMissing(lesson, sample.LanguageId); continue; }
                if (!run.IsSuccess)
                    Add(DiagnosticSeverity.Error, lesson, $"code sample ({sample.LanguageId}) failed with {run.Status}: {First(run.CompileOutput + run.Stderr)}");
                else if (!OutputComparer.AreEqual(sample.ExpectedOutput!, run.Stdout, OutputComparison.Trim))
                    Add(DiagnosticSeverity.Error, lesson, $"code sample ({sample.LanguageId}) output differs. expected: {First(sample.ExpectedOutput!)} actual: {First(run.Stdout)}");
            }
        }
        return diagnostics;
    }

    /// <summary>The evaluators report a run over its wall limit as "Time limit exceeded" (IoEvaluator, CommandEvaluator).</summary>
    private static bool TimedOut(EvaluationReport r) =>
        (r.Message ?? "").Contains("Time limit exceeded", StringComparison.Ordinal)
        || r.Cases.Any(c => !c.Passed && (c.Message ?? "").StartsWith("Time limit exceeded", StringComparison.Ordinal));

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

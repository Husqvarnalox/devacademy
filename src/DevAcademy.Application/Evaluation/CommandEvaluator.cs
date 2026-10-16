using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Evaluation;

/// <summary>
/// Grades a multi-file exercise with a real test tool: the learner's files and the content's hidden test files are written
/// into a fresh sandboxed work directory, the declared command runs, and its machine-readable report (JUnit XML or
/// <c>go test -json</c>) is mapped onto the declared cases. A case passes only when a matching reported test passed; a missing
/// or skipped test fails it, and a clean exit code alone never passes anything.
/// </summary>
public sealed class CommandEvaluator(ICodeRunner runner) : IExerciseEvaluator
{
    private const int MaxReportBytes = 4 * 1024 * 1024;

    public EvaluationKind Kind => EvaluationKind.Command;

    public async Task<EvaluationReport> EvaluateAsync(Exercise exercise, IReadOnlyList<ExerciseFile> learnerFiles,
        IsolationLevel isolation, CancellationToken ct, PythonRunSelection? python = null)
    {
        if (exercise.Command is not { } spec)
            return EvaluationReport.Failure(Verdict.Error, $"Content error: exercise '{exercise.Id}' has no command", isolation);
        if (CommandPolicy.Check(exercise.LanguageId, spec.Run) is { } problem)
            return EvaluationReport.Failure(Verdict.Error, $"Content error: {problem}", isolation);

        // Test files win over learner files at the same path, and the learner cannot pre-create the report file.
        var prefix = spec.TestsTarget == "." ? "" : spec.TestsTarget.TrimEnd('/') + "/";
        var tests = spec.TestFiles.Select(f => new ExerciseFile(prefix + f.Path, f.Content)).ToList();
        var reserved = tests.Select(t => t.Path).Concat(spec.ReportFile is null ? [] : [spec.ReportFile]).ToHashSet(StringComparer.Ordinal);
        var files = EvaluatorSupport.ToSource(learnerFiles.Where(f => !reserved.Contains(f.Path)).Concat(tests));

        var request = new ExecutionRequest(exercise.LanguageId, files, exercise.EntryFile, isolation, null, spec.Run);
        await using var program = await runner.PrepareAsync(request, ct);
        if (program.PreparationFailure is { } failure) return EvaluatorSupport.FromPreparationFailure(failure);

        var seconds = Math.Clamp(spec.TimeoutSeconds ?? CommandPolicy.DefaultTimeoutSeconds, 1, CommandPolicy.MaxTimeoutSeconds);
        var limits = ResourceLimits.DefaultCommand with { WallTimeout = TimeSpan.FromSeconds(seconds) };
        var result = await program.RunAsync(null, limits, ct);
        if (result.Status is ExecutionStatus.RuntimeMissing or ExecutionStatus.InternalError or ExecutionStatus.Cancelled)
            return new EvaluationReport(result.Status == ExecutionStatus.RuntimeMissing ? Verdict.RuntimeMissing : Verdict.Error,
                [], result.Duration, result.AppliedIsolation, result.Message ?? result.Status.ToString());

        var reportText = spec.ReportFile is null ? result.Stdout : program.TryReadFile(spec.ReportFile, MaxReportBytes);
        var parsed = reportText is null ? null : spec.Report switch
        {
            CommandReportFormat.Junit => JUnitReportParser.Parse(reportText),
            CommandReportFormat.GoTestJson => GoTestJsonParser.Parse(reportText),
            _ => null,
        };

        var anyDeclaredRan = parsed is not null && exercise.Tests.Any(t => CommandCaseMatcher.Select(parsed.Tests, t.Match).Count > 0);
        var finished = result.Status is ExecutionStatus.Success or ExecutionStatus.RuntimeError;
        if (finished)
        {
            // No report, a reported build failure, or nothing declared ran while the tool failed: the code did not even get to run its tests.
            var failed = result.ExitCode != 0;
            if (parsed is { BuildFailed: true } || (parsed is null && failed) || (!anyDeclaredRan && failed))
                return new EvaluationReport(Verdict.CompileError, [], result.Duration, result.AppliedIsolation,
                    BuildOutput(parsed, result, program.WorkingDirectory), exercise.LanguageId);
            if (parsed is null)
                return new EvaluationReport(Verdict.Error, [], result.Duration, result.AppliedIsolation, "The test tool produced no report", exercise.LanguageId);
        }

        var cases = new List<CaseResult>();
        var hiddenOrdinal = 0;
        foreach (var test in exercise.Tests)
        {
            if (test.Hidden) hiddenOrdinal++;
            cases.Add(Judge(test, parsed?.Tests ?? [], EvaluatorSupport.DisplayTitle(test, hiddenOrdinal), result.Status, program.WorkingDirectory));
        }

        var allPassed = cases.Count > 0 && cases.All(c => c.Passed);
        var status = allPassed && result.Status == ExecutionStatus.Success ? Verdict.Passed : Verdict.Failed;
        string? overall = result.Status switch
        {
            ExecutionStatus.Timeout => $"Time limit exceeded ({seconds} s)",
            ExecutionStatus.MemoryLimitExceeded => "Memory limit exceeded",
            ExecutionStatus.OutputLimitExceeded => "Output limit exceeded",
            ExecutionStatus.RuntimeError when allPassed => "All declared tests passed, but the test command exited with an error (a test outside the declared list failed?)",
            _ => null,
        };
        return new EvaluationReport(status, cases, result.Duration, result.AppliedIsolation, overall, exercise.LanguageId);
    }

    private static CaseResult Judge(TestCase test, IReadOnlyList<ReportedTest> reported, string title, ExecutionStatus run, string workDir)
    {
        var matches = CommandCaseMatcher.Select(reported, test.Match);
        string? why = null;
        if (matches.Count == 0) why = run is ExecutionStatus.Success or ExecutionStatus.RuntimeError ? "тест не был запущен" : $"тест не был запущен ({run})";
        else if (matches.FirstOrDefault(m => m.Outcome == ReportedOutcome.Failed) is { } bad)
            why = test.Hidden ? "Hidden test failed" : bad.Message is { Length: > 0 } m ? EvaluatorSupport.Tail(Sanitize(m, workDir)) : "тест не пройден";
        else if (matches.Any(m => m.Outcome == ReportedOutcome.Skipped)) why = "тест пропущен (skip/todo не засчитывается)";

        if (why is null) return new CaseResult(test.Id, title, test.Hidden, true, null, null);
        var message = test.Hidden
            ? (string.IsNullOrWhiteSpace(test.Hint) ? why : $"{why}. Hint: {test.Hint}")
            : string.Join("\n", new[] { why, test.Hint }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new CaseResult(test.Id, title, test.Hidden, false, message, null);
    }

    private static string BuildOutput(ParsedReport? parsed, ExecutionResult result, string workDir)
    {
        var parts = new List<string>();
        if (parsed?.BuildOutput is { } build) parts.Add(build);
        if (result.Stderr.Trim().Length > 0) parts.Add(result.Stderr);
        // A JUnit report goes to a file, so the error text is only on stdout when the author also added a console reporter.
        if (parsed is null || parsed.BuildOutput is null)
            if (result.Stdout.Trim().Length > 0 && !result.Stdout.TrimStart().StartsWith('{') && !result.Stdout.TrimStart().StartsWith('<')) parts.Add(result.Stdout);
        if (parts.Count == 0 && parsed is not null)
            parts.AddRange(parsed.Tests.Where(t => t.Outcome == ReportedOutcome.Failed && t.Message is not null).Select(t => $"{t.Name}: {t.Message}"));
        if (parts.Count == 0) parts.Add($"The test command failed ({result.Status}, exit code {result.ExitCode?.ToString() ?? "none"}) and produced no output");
        return EvaluatorSupport.Tail(Sanitize(string.Join("\n", parts), workDir));
    }

    /// <summary>Hides the random work directory in messages ("file:///tmp/devacademy-run-.../src/x.js" becomes "src/x.js").</summary>
    private static string Sanitize(string text, string workDir)
    {
        if (workDir.Length == 0) return text;
        var dir = workDir.TrimEnd('/', '\\');
        foreach (var variant in new[] { "file://" + dir + "/", "file://" + "/private" + dir + "/", dir + "/", "/private" + dir + "/" })
            text = text.Replace(variant, "");
        return text;
    }
}

/// <summary>Maps a declared case's <c>match</c> to the reported tests it denotes.</summary>
public static class CommandCaseMatcher
{
    /// <summary>
    /// A reported test matches when its name, its full suite path ("suite &gt; name") or "classname.name" equals the pattern;
    /// a pattern ending in '*' matches by prefix. No pattern matches nothing.
    /// </summary>
    public static IReadOnlyList<ReportedTest> Select(IReadOnlyList<ReportedTest> reported, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return [];
        var prefix = pattern.EndsWith('*');
        var text = prefix ? pattern[..^1] : pattern;
        bool Hit(string candidate) => prefix ? candidate.StartsWith(text, StringComparison.Ordinal) : candidate == text;
        return reported.Where(t => Hit(t.Name) || Hit(t.FullName) || (t.ClassName.Length > 0 && Hit(t.ClassName + "." + t.Name))).ToList();
    }
}

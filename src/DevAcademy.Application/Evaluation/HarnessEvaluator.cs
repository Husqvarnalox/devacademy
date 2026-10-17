using System.Security.Cryptography;
using System.Text.Json;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Evaluation;

public sealed record HarnessCaseReport(bool Passed, string? Message);

/// <summary>Parses lines of the form <c>##DEVACADEMY## &lt;nonce&gt; {json}</c> from harness stdout.</summary>
public static class HarnessOutputParser
{
    public const string Prefix = "##DEVACADEMY##";

    /// <summary>
    /// Lines with a different nonce or malformed JSON are ignored. If a case id is reported more than
    /// once it only passes when every report passed.
    /// </summary>
    public static IReadOnlyDictionary<string, HarnessCaseReport> Parse(string stdout, string nonce)
    {
        var result = new Dictionary<string, HarnessCaseReport>(StringComparer.Ordinal);
        var header = Prefix + " " + nonce + " ";
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (!line.StartsWith(header, StringComparison.Ordinal)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line[header.Length..]);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                    || !root.TryGetProperty("passed", out var passed) || passed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    continue;
                var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                var key = id.GetString()!;
                var report = new HarnessCaseReport(passed.GetBoolean(), message);
                if (result.TryGetValue(key, out var previous) && !previous.Passed) continue; // a failure sticks
                result[key] = report;
            }
            catch (JsonException) { /* malformed line: ignore */ }
        }
        return result;
    }
}

/// <summary>Runs learner files plus the content-supplied harness once and reads per-case results from stdout.</summary>
public sealed class HarnessEvaluator(ICodeRunner runner) : IExerciseEvaluator
{
    public const string NonceVariable = "DEVACADEMY_NONCE";

    public EvaluationKind Kind => EvaluationKind.Harness;

    public async Task<EvaluationReport> EvaluateAsync(Exercise exercise, IReadOnlyList<ExerciseFile> learnerFiles,
        IsolationLevel isolation, CancellationToken ct, PythonRunSelection? python = null, NodeRunSelection? node = null, GoRunSelection? go = null, NuGetRunSelection? nuget = null, MavenRunSelection? maven = null)
    {
        if (string.IsNullOrWhiteSpace(exercise.HarnessEntry))
            return EvaluationReport.Failure(Verdict.Error, $"Content error: exercise '{exercise.Id}' has no harnessEntry", isolation);
        var learnerPaths = learnerFiles.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var collision = exercise.Harness.FirstOrDefault(h => learnerPaths.Contains(h.Path));
        if (collision is not null)
            return EvaluationReport.Failure(Verdict.Error, $"Content error: harness file '{collision.Path}' collides with a learner file", isolation);

        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var files = EvaluatorSupport.ToSource(learnerFiles.Concat(exercise.Harness));
        var request = new ExecutionRequest(exercise.LanguageId, files, exercise.HarnessEntry, isolation, python, Go: go, NuGet: nuget, Maven: maven);

        await using var program = await runner.PrepareAsync(request, ct);
        if (program.PreparationFailure is { } failure) return EvaluatorSupport.FromPreparationFailure(failure);

        var result = await program.RunAsync(null, ExerciseLimits.For(exercise.Requires), ct,
            new Dictionary<string, string> { [NonceVariable] = nonce });
        if (result.Status is ExecutionStatus.RuntimeMissing or ExecutionStatus.InternalError or ExecutionStatus.Cancelled)
            return new EvaluationReport(result.Status == ExecutionStatus.RuntimeMissing ? Verdict.RuntimeMissing : Verdict.Error,
                [], result.Duration, result.AppliedIsolation, result.Message ?? result.Status.ToString());

        var reports = HarnessOutputParser.Parse(result.Stdout, nonce);
        var cases = new List<CaseResult>();
        var hiddenOrdinal = 0;
        foreach (var test in exercise.Tests)
        {
            if (test.Hidden) hiddenOrdinal++;
            var title = EvaluatorSupport.DisplayTitle(test, hiddenOrdinal);
            if (!reports.TryGetValue(test.Id, out var report))
            {
                var why = result.Status == ExecutionStatus.Success ? "test did not report" : $"test did not report ({result.Status})";
                cases.Add(new CaseResult(test.Id, title, test.Hidden, false, test.Hidden ? Hinted(why, test) : Detailed(why, result), null));
                continue;
            }
            var message = report.Passed ? null : test.Hidden ? Hinted("Hidden test failed", test) : report.Message ?? test.Hint;
            cases.Add(new CaseResult(test.Id, title, test.Hidden, report.Passed, message, null));
        }

        var allPassed = cases.Count > 0 && cases.All(c => c.Passed);
        // A passing report from a process that did not exit cleanly does not count.
        var status = allPassed && result.Status == ExecutionStatus.Success ? Verdict.Passed : Verdict.Failed;
        var overall = allPassed && status == Verdict.Failed ? $"All tests reported success but the process ended with {result.Status}" : null;
        return new EvaluationReport(status, cases, result.Duration, result.AppliedIsolation, overall, exercise.LanguageId);
    }

    private static string Hinted(string status, TestCase t) =>
        string.IsNullOrWhiteSpace(t.Hint) ? status : $"{status}. Hint: {t.Hint}";

    private static string Detailed(string status, ExecutionResult r) =>
        r.Stderr.Length > 0 ? status + "\n" + EvaluatorSupport.Tail(r.Stderr) : status;
}

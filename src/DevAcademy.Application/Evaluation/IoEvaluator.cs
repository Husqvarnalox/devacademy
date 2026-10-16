using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Evaluation;

/// <summary>Runs the program once per test case with its stdin and compares stdout.</summary>
public sealed class IoEvaluator(ICodeRunner runner) : IExerciseEvaluator
{
    public EvaluationKind Kind => EvaluationKind.Io;

    public async Task<EvaluationReport> EvaluateAsync(Exercise exercise, IReadOnlyList<ExerciseFile> learnerFiles,
        IsolationLevel isolation, CancellationToken ct, PythonRunSelection? python = null, NodeRunSelection? node = null)
    {
        var request = new ExecutionRequest(exercise.LanguageId, EvaluatorSupport.ToSource(learnerFiles), exercise.EntryFile, isolation, python);
        await using var program = await runner.PrepareAsync(request, ct);
        if (program.PreparationFailure is { } failure) return EvaluatorSupport.FromPreparationFailure(failure);

        var cases = new List<CaseResult>();
        var applied = isolation;
        var total = TimeSpan.Zero;
        Verdict? infrastructure = null;
        string? infrastructureMessage = null;
        var hiddenOrdinal = 0;

        foreach (var test in exercise.Tests)
        {
            if (test.Hidden) hiddenOrdinal++;
            var result = await program.RunAsync(test.Stdin, ResourceLimits.DefaultRun, ct);
            applied = result.AppliedIsolation;
            total += result.Duration;

            if (result.Status is ExecutionStatus.RuntimeMissing or ExecutionStatus.InternalError or ExecutionStatus.Cancelled)
            {
                infrastructure ??= result.Status == ExecutionStatus.RuntimeMissing ? Verdict.RuntimeMissing : Verdict.Error;
                infrastructureMessage ??= result.Message ?? result.Status.ToString();
                continue;
            }
            cases.Add(Judge(test, result, EvaluatorSupport.DisplayTitle(test, hiddenOrdinal)));
        }

        if (infrastructure is { } v) return new EvaluationReport(v, [], total, applied, infrastructureMessage);
        var status = cases.Count > 0 && cases.All(c => c.Passed) ? Verdict.Passed : Verdict.Failed;
        return new EvaluationReport(status, cases, total, applied, LanguageId: exercise.LanguageId);
    }

    private static CaseResult Judge(TestCase test, ExecutionResult result, string title)
    {
        var expected = test.Expected ?? "";
        var passed = result.Status == ExecutionStatus.Success
            && OutputComparer.AreEqual(expected, result.Stdout, test.Compare, test.Tolerance);

        string? message = null;
        if (!passed)
        {
            var status = result.Status switch
            {
                ExecutionStatus.Success => "Output does not match the expected output",
                ExecutionStatus.RuntimeError => "The program crashed or exited with an error",
                ExecutionStatus.Timeout => "Time limit exceeded",
                ExecutionStatus.MemoryLimitExceeded => "Memory limit exceeded",
                ExecutionStatus.OutputLimitExceeded => "Output limit exceeded",
                _ => result.Status.ToString(),
            };
            message = test.Hidden
                ? (string.IsNullOrWhiteSpace(test.Hint) ? status : $"{status}. Hint: {test.Hint}")
                : string.Join("\n", new[] { status, test.Hint, result.Stderr.Length > 0 ? EvaluatorSupport.Tail(result.Stderr) : null }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        var detail = test.Hidden ? null : new CaseDetail(test.Stdin, test.Expected, result.Stdout);
        return new CaseResult(test.Id, title, test.Hidden, passed, message, detail, result.Duration);
    }
}

using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Evaluation;

/// <summary>Stdin/expected/actual of a PUBLIC test. Never created for hidden tests.</summary>
public sealed record CaseDetail(string? Stdin, string? Expected, string? Actual);

/// <summary>
/// Result of one test. For hidden tests Title is "Hidden test N", Message is only the author's hint or a
/// generic status, and Detail is null: hidden inputs/outputs are never carried into the report.
/// </summary>
/// <param name="Duration">
/// Wall time of this test's process, including process start; null when the evaluator cannot measure tests
/// individually (harness evaluation runs all tests in one process).
/// </param>
public sealed record CaseResult(string TestId, string Title, bool Hidden, bool Passed, string? Message, CaseDetail? Detail,
    TimeSpan? Duration = null);

public sealed record EvaluationReport(
    Verdict Status,
    IReadOnlyList<CaseResult> Cases,
    TimeSpan Duration,
    IsolationLevel AppliedIsolation,
    string? Message = null,
    string? LanguageId = null)
{
    public int PassedCount => Cases.Count(c => c.Passed);
    public int TotalCount => Cases.Count;

    public static EvaluationReport Failure(Verdict status, string message, IsolationLevel isolation) =>
        new(status, [], TimeSpan.Zero, isolation, message);
}

public interface IExerciseEvaluator
{
    EvaluationKind Kind { get; }
    Task<EvaluationReport> EvaluateAsync(Exercise exercise, IReadOnlyList<ExerciseFile> learnerFiles,
        IsolationLevel isolation, CancellationToken ct, PythonRunSelection? python = null, NodeRunSelection? node = null, GoRunSelection? go = null, NuGetRunSelection? nuget = null, MavenRunSelection? maven = null);
}

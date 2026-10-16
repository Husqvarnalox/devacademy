using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Evaluation;

internal static class EvaluatorSupport
{
    private const int MaxMessageChars = 2000;

    public static List<SourceFile> ToSource(IEnumerable<ExerciseFile> files) =>
        files.Select(f => new SourceFile(f.Path, f.Content)).ToList();

    public static string Tail(string s)
    {
        s = s.Trim();
        return s.Length <= MaxMessageChars ? s : "..." + s[^MaxMessageChars..];
    }

    /// <summary>Title shown for a test: hidden tests are anonymised.</summary>
    public static string DisplayTitle(TestCase t, int hiddenOrdinal) =>
        t.Hidden ? $"Hidden test {hiddenOrdinal}" : t.Title;

    /// <summary>Maps a failed preparation (compile error, runtime missing, ...) to a report.</summary>
    public static EvaluationReport FromPreparationFailure(ExecutionResult failure)
    {
        var detail = failure.CompileOutput.Length > 0 ? failure.CompileOutput : failure.Message ?? failure.Stderr;
        var verdict = failure.Status switch
        {
            ExecutionStatus.CompileError => Verdict.CompileError,
            ExecutionStatus.RuntimeMissing => Verdict.RuntimeMissing,
            _ => Verdict.Error,
        };
        return new EvaluationReport(verdict, [], failure.Duration, failure.AppliedIsolation, Tail(detail));
    }
}

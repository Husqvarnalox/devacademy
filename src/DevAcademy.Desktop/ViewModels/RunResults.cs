using DevAcademy.Application.Evaluation;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Desktop.ViewModels;

public sealed class ConsoleResultViewModel
{
    public ConsoleResultViewModel(ExecutionResult r)
    {
        Result = r;
        StatusText = Display.Run(r.Status);
        IsSuccess = r.IsSuccess;
        Stdout = r.Stdout;
        Stderr = r.Stderr;
        CompileOutput = r.CompileOutput;
        Message = r.Message is null && r.MessageText is null ? null : Loc.Of(r.MessageText, r.Message);
        DurationText = Display.Duration(r.Duration);
        ExitCodeText = r.ExitCode is { } c ? Loc.F("Run_ExitCode", c) : "";
        IsolationBadge = Loc.F("Isolation_Badge", Display.Isolation(r.AppliedIsolation));
        IsWeakIsolation = r.AppliedIsolation == IsolationLevel.ProcessLimits;
    }

    public ExecutionResult Result { get; }
    public string StatusText { get; }
    public bool IsSuccess { get; }
    public string Stdout { get; }
    public string Stderr { get; }
    public string CompileOutput { get; }
    public string? Message { get; }
    public string DurationText { get; }
    public string ExitCodeText { get; }
    public string IsolationBadge { get; }
    public bool IsWeakIsolation { get; }
    public bool HasStdout => Stdout.Length > 0;
    public bool HasStderr => Stderr.Length > 0;
    public bool HasCompileOutput => CompileOutput.Length > 0;
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
}

public sealed class TestCaseViewModel
{
    public TestCaseViewModel(CaseResult c, int hiddenOrdinal)
    {
        Title = c.Hidden ? Loc.F("Tests_HiddenTitle", hiddenOrdinal) : c.Title;
        Hidden = c.Hidden;
        Passed = c.Passed;
        Message = c.Passed ? null : c.Message is null && c.MessageText is null ? null : Loc.Of(c.MessageText, c.Message);
        DurationText = c.Duration is { } dur ? Display.Duration(dur) : null;
        if (c.Detail is { } d && !c.Hidden)
        {
            Stdin = d.Stdin;
            Expected = d.Expected;
            Actual = d.Actual;
        }
    }

    public string Title { get; }
    public bool Hidden { get; }
    public bool Passed { get; }
    public string? Message { get; }
    public string? Stdin { get; }
    public string? Expected { get; }
    public string? Actual { get; }
    /// <summary>Wall time of this test including process start; null when the evaluator does not time cases.</summary>
    public string? DurationText { get; }
    public bool HasDuration => DurationText is not null;
    public string StatusGlyph => Passed ? "✓" : "✗";
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool HasDetail => !Passed && (Expected is not null || Actual is not null);
}

public sealed class TestReportViewModel
{
    public TestReportViewModel(EvaluationReport r)
    {
        Report = r;
        IsPassed = r.Status == Domain.Verdict.Passed;
        var hidden = 0;
        Cases = r.Cases.Select(c => new TestCaseViewModel(c, c.Hidden ? ++hidden : 0)).ToList();
        Verdict = r.Status switch
        {
            Domain.Verdict.Passed => Loc.F("Tests_AllPassed", r.PassedCount, r.TotalCount),
            Domain.Verdict.Failed => Loc.F("Tests_Failed", r.PassedCount, r.TotalCount),
            var other => Display.Verdict(other),
        };
        Message = r.Message is null && r.MessageText is null ? null : Loc.Of(r.MessageText, r.Message);
        DurationText = Display.Duration(r.Duration);
        IsolationBadge = Loc.F("Isolation_Badge", Display.Isolation(r.AppliedIsolation));
        IsWeakIsolation = r.AppliedIsolation == IsolationLevel.ProcessLimits;
    }

    public EvaluationReport Report { get; }
    public bool IsPassed { get; }
    public string Verdict { get; }
    public string? Message { get; }
    public string DurationText { get; }
    public string IsolationBadge { get; }
    public bool IsWeakIsolation { get; }
    public IReadOnlyList<TestCaseViewModel> Cases { get; }
    public bool HasCaseDurations => Cases.Any(c => c.HasDuration);
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
}

public sealed record HintItemViewModel(int Level, int Total, string Text)
{
    public string Header => Loc.F("Hints_Header", Level, Total);
}

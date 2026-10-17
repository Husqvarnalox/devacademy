using DevAcademy.Domain;

namespace DevAcademy.Application;

/// <summary>
/// Catalog of the user-visible messages of the evaluation layer. Each <c>const</c> is a resource key a UI translates; the factory beside it
/// holds the English template that stays the fallback for the CLI and logs. Stderr, test-tool output and the hints written by course authors
/// are <see cref="LocalizedText.Literal"/> and are never translated. A test enumerates the keys by reflection.
/// </summary>
public static class EngineMessages
{
    public const string OutputMismatchKey = "Engine_Eval_OutputMismatch";
    public const string CrashedKey = "Engine_Eval_Crashed";
    public const string TimeLimitKey = "Engine_Eval_TimeLimit";
    public const string MemoryLimitKey = "Engine_Eval_MemoryLimit";
    public const string OutputLimitKey = "Engine_Eval_OutputLimit";
    public const string HintedKey = "Engine_Eval_Hinted";
    public const string HiddenFailedKey = "Engine_Eval_HiddenFailed";
    public const string NoHarnessEntryKey = "Engine_Eval_NoHarnessEntry";
    public const string HarnessCollisionKey = "Engine_Eval_HarnessCollision";
    public const string NoCommandKey = "Engine_Eval_NoCommand";
    public const string ContentErrorKey = "Engine_Eval_ContentError";
    public const string NotReportedKey = "Engine_Eval_NotReported";
    public const string NotReportedStatusKey = "Engine_Eval_NotReportedStatus";
    public const string ProcessEndedKey = "Engine_Eval_ProcessEnded";
    public const string NoReportKey = "Engine_Eval_NoReport";
    public const string TimeLimitSecondsKey = "Engine_Eval_TimeLimitSeconds";
    public const string DeclaredPassedKey = "Engine_Eval_DeclaredPassedButFailed";
    public const string TestNotRunKey = "Engine_Eval_TestNotRun";
    public const string TestNotRunStatusKey = "Engine_Eval_TestNotRunStatus";
    public const string TestFailedKey = "Engine_Eval_TestFailed";
    public const string TestSkippedKey = "Engine_Eval_TestSkipped";
    public const string CommandFailedKey = "Engine_Eval_CommandFailedNoOutput";

    public static LocalizedText OutputMismatch() => LocalizedText.Create(OutputMismatchKey, "Output does not match the expected output");
    public static LocalizedText Crashed() => LocalizedText.Create(CrashedKey, "The program crashed or exited with an error");
    public static LocalizedText TimeLimit() => LocalizedText.Create(TimeLimitKey, "Time limit exceeded");
    public static LocalizedText MemoryLimit() => LocalizedText.Create(MemoryLimitKey, "Memory limit exceeded");
    public static LocalizedText OutputLimit() => LocalizedText.Create(OutputLimitKey, "Output limit exceeded");

    /// <summary>"status. Hint: hint" for hidden tests; the hint is the course author's text and stays verbatim.</summary>
    public static LocalizedText Hinted(LocalizedText status, string? hint) =>
        string.IsNullOrWhiteSpace(hint) ? status : LocalizedText.Create(HintedKey, "{0}. Hint: {1}", status, LocalizedText.Literal(hint));

    public static LocalizedText HiddenFailed() => LocalizedText.Create(HiddenFailedKey, "Hidden test failed");

    public static LocalizedText NoHarnessEntry(string exerciseId) =>
        LocalizedText.Create(NoHarnessEntryKey, "Content error: exercise '{0}' has no harnessEntry", exerciseId);

    public static LocalizedText HarnessCollision(string path) =>
        LocalizedText.Create(HarnessCollisionKey, "Content error: harness file '{0}' collides with a learner file", path);

    public static LocalizedText NoCommand(string exerciseId) =>
        LocalizedText.Create(NoCommandKey, "Content error: exercise '{0}' has no command", exerciseId);

    /// <summary>A content-authoring problem; <paramref name="problem"/> is the (English) text from the content policy and stays verbatim.</summary>
    public static LocalizedText ContentError(string problem) =>
        LocalizedText.Create(ContentErrorKey, "Content error: {0}", LocalizedText.Literal(problem));

    public static LocalizedText NotReported() => LocalizedText.Create(NotReportedKey, "test did not report");
    public static LocalizedText NotReported(string status) => LocalizedText.Create(NotReportedStatusKey, "test did not report ({0})", status);
    public static LocalizedText ProcessEnded(string status) =>
        LocalizedText.Create(ProcessEndedKey, "All tests reported success but the process ended with {0}", status);
    public static LocalizedText NoReport() => LocalizedText.Create(NoReportKey, "The test tool produced no report");
    public static LocalizedText TimeLimitSeconds(int seconds) => LocalizedText.Create(TimeLimitSecondsKey, "Time limit exceeded ({0} s)", seconds);
    public static LocalizedText DeclaredPassed() => LocalizedText.Create(DeclaredPassedKey,
        "All declared tests passed, but the test command exited with an error (a test outside the declared list failed?)");
    public static LocalizedText TestNotRun() => LocalizedText.Create(TestNotRunKey, "the test was not run");
    public static LocalizedText TestNotRun(string status) => LocalizedText.Create(TestNotRunStatusKey, "the test was not run ({0})", status);
    public static LocalizedText TestFailed() => LocalizedText.Create(TestFailedKey, "the test failed");
    public static LocalizedText TestSkipped() => LocalizedText.Create(TestSkippedKey, "the test was skipped (skip/todo does not count)");
    public static LocalizedText CommandFailed(string status, string exitCode) =>
        LocalizedText.Create(CommandFailedKey, "The test command failed ({0}, exit code {1}) and produced no output", status, exitCode);
}

using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

internal static class TestSupport
{
    // Uses the real default runner cache so that explicitly provisioned @types/node is picked up.
    public static readonly ExecutionOptions Options = new();

    public static readonly ICodeRunner Runner = ExecutionEngine.CreateDefault(Options);

    private static readonly Lazy<Task<IReadOnlyList<RuntimeStatus>>> Statuses =
        new(() => Runner.DetectRuntimesAsync(CancellationToken.None));

    public static ResourceLimits RunLimits(double seconds = 10, long memoryMb = 512, int outputBytes = 64 * 1024) =>
        new(TimeSpan.FromSeconds(seconds), memoryMb * 1024 * 1024, outputBytes);

    public static async Task<RuntimeStatus> StatusAsync(string language) =>
        (await Statuses.Value).Single(s => s.LanguageId == language);

    /// <summary>Skips (never passes) the calling test when the language runtime is not installed.</summary>
    public static async Task RequireAsync(string language)
    {
        RuntimeStatus s = await StatusAsync(language);
        if (s.Availability != RuntimeAvailability.Available)
            Assert.Skip($"{language} runtime not available: {s.Notes}");
    }

    public static ExecutionRequest Request(string language, string file, string code, IsolationLevel iso = IsolationLevel.ProcessLimits) =>
        new(language, [new SourceFile(file, code)], file, iso);

    public static Task<ExecutionResult> RunAsync(ExecutionRequest r, string? stdin = null, ResourceLimits? limits = null) =>
        Runner.RunOnceAsync(r, stdin, limits ?? RunLimits(), CancellationToken.None);
}

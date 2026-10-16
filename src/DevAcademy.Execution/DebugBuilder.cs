namespace DevAcademy.Execution;

/// <summary>Result of building a program for debugging. <see cref="Executable"/> is null when the build failed.</summary>
public sealed record DebugBuildResult(bool Success, string Output, string? Executable, string WorkingDirectory);

/// <summary>
/// Builds already-written source files with debug info and without optimisation, ready for a DAP adapter.
/// Debugging is NOT sandboxed (the debugger needs ptrace / task_for_pid), so compile and run happen as plain child processes.
/// </summary>
public interface IDebugBuilder
{
    /// <summary>Language ids this builder can produce debug binaries for.</summary>
    IReadOnlyCollection<string> Languages { get; }
    /// <param name="directory">Working directory and output location (the binary is <c>prog</c> in it).</param>
    /// <param name="sourceDirectory">Where the sources are, when not in <paramref name="directory"/> (builds a project in place without writing next to it).</param>
    Task<DebugBuildResult> BuildAsync(string languageId, string directory, string entryFile, CancellationToken ct, string? sourceDirectory = null);
}

public static class DebugBuilderFactory
{
    public static IDebugBuilder Create(ExecutionOptions? options = null) =>
        new DebugBuilder(RuntimeFactory.CreateAll((options ?? new ExecutionOptions()).CacheDirectory));
}

internal sealed class DebugBuilder(IReadOnlyList<ILanguageRuntime> runtimes) : IDebugBuilder
{
    private readonly LocalProcessSandbox _sandbox = new();

    public IReadOnlyCollection<string> Languages => runtimes.OfType<IDebugBuildRuntime>().Select(r => r.LanguageId).ToList();

    public async Task<DebugBuildResult> BuildAsync(string languageId, string directory, string entryFile, CancellationToken ct, string? sourceDirectory = null)
    {
        if (runtimes.OfType<IDebugBuildRuntime>().FirstOrDefault(r => r.LanguageId == languageId) is not { } runtime)
            return new DebugBuildResult(false, $"No debug build for '{languageId}'.", null, directory);
        RuntimeStatus status = await runtime.DetectAsync(ct).ConfigureAwait(false);
        if (status.Availability != RuntimeAvailability.Available)
            return new DebugBuildResult(false, (status.Notes + " " + status.InstallHint).Trim(), null, directory);

        Directory.CreateDirectory(Path.Combine(directory, "tmp"));   // the local sandbox points HOME/TMPDIR here
        BuildPlan plan = runtime.CreateDebugPlan(directory, entryFile, status, sourceDirectory);
        var output = new System.Text.StringBuilder();
        foreach (ProcessSpec step in plan.CompileSteps)
        {
            ProcessOutcome o = await _sandbox.RunAsync(step, ResourceLimits.DefaultCompile, null, ct).ConfigureAwait(false);
            output.Append(o.Stdout).Append(o.Stderr);
            if (o.ExitCode != 0 || o.TimedOut)
                return new DebugBuildResult(false, output.Length > 0 ? output.ToString() : "The compiler failed.", null, directory);
        }
        return new DebugBuildResult(true, output.ToString(), plan.Run.FileName, directory);
    }
}

using DevAcademy.Domain;

namespace DevAcademy.Execution;

/// <summary>
/// Result of building a program for debugging. <see cref="Executable"/> is null when the build failed. Native languages produce a binary
/// (<see cref="RunArguments"/> null); runtime languages return the runtime as <see cref="Executable"/> and the rest of the run command
/// (<c>node main.js</c>, <c>java -cp out Main</c>) in <see cref="RunArguments"/>.
/// </summary>
public sealed record DebugBuildResult(bool Success, string Output, string? Executable, string WorkingDirectory, IReadOnlyList<string>? RunArguments = null,
    /// <summary>Translatable form of <see cref="Output"/> when the build failed for a reason of our own (compiler output stays verbatim); null otherwise.</summary>
    LocalizedText? OutputText = null)
{
    public static DebugBuildResult Failure(LocalizedText message, string workingDirectory) => new(false, message.Fallback, null, workingDirectory, null, message);
}

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
            return DebugBuildResult.Failure(EngineMessages.NoDebugBuild(languageId), directory);
        RuntimeStatus status = await runtime.DetectAsync(ct).ConfigureAwait(false);
        if (status.Availability != RuntimeAvailability.Available)
            return DebugBuildResult.Failure(LocalizedText.Join(" ", status.NotesMessage, status.InstallHintMessage), directory);

        Directory.CreateDirectory(Path.Combine(directory, "tmp"));   // the local sandbox points HOME/TMPDIR here
        BuildPlan plan = runtime.CreateDebugPlan(directory, entryFile, status, sourceDirectory);
        var output = new System.Text.StringBuilder();
        foreach (ProcessSpec step in plan.CompileSteps)
        {
            ProcessOutcome o = await _sandbox.RunAsync(step, ResourceLimits.DefaultCompile, null, ct).ConfigureAwait(false);
            output.Append(o.Stdout).Append(o.Stderr);
            if (o.ExitCode != 0 || o.TimedOut)
                return output.Length > 0
                    ? new DebugBuildResult(false, output.ToString(), null, directory)
                    : DebugBuildResult.Failure(EngineMessages.CompilerFailed(), directory);
        }
        return new DebugBuildResult(true, output.ToString(), plan.Run.FileName, directory, runtime is INativeDebugBuild ? null : plan.Run.Args);
    }
}

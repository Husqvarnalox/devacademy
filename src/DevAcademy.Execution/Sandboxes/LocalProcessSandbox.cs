namespace DevAcademy.Execution;

/// <summary>
/// Plain child process with wall timeout, output cap, RSS polling of the whole tree and tree kill.
/// On Unix the command is wrapped in <c>sh -c 'ulimit ...; exec "$@"'</c> for CPU-time, file-size and core-dump limits.
/// This is not a security boundary.
/// </summary>
public class LocalProcessSandbox : IProcessSandbox
{
    private const int MaxFileSizeMegabytes = 256;

    public virtual IsolationLevel Level => IsolationLevel.ProcessLimits;

    public virtual Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

    public Task<ProcessOutcome> RunAsync(ProcessSpec spec, ResourceLimits limits, string? stdin, CancellationToken ct)
    {
        (string file, IReadOnlyList<string> args) = ApplyUnixLimits(spec.FileName, spec.Args, limits);
        (file, args) = Wrap(file, args, spec, limits);
        var launch = new ProcessLaunch(file, args, spec.WorkingDirectory, BuildEnvironment(spec), MonitorMemory: true);
        return ProcessRunner.RunAsync(launch, limits, stdin, ct);
    }

    /// <summary>Hook for derived sandboxes to prefix the (already ulimit-wrapped) command.</summary>
    protected virtual (string File, IReadOnlyList<string> Args) Wrap(
        string file, IReadOnlyList<string> args, ProcessSpec spec, ResourceLimits limits) => (file, args);

    internal static (string File, IReadOnlyList<string> Args) ApplyUnixLimits(
        string file, IReadOnlyList<string> args, ResourceLimits limits)
    {
        if (OperatingSystem.IsWindows()) return (file, args);
        long fileBlocks = MaxFileSizeMegabytes * 1024L * 2; // ulimit -f counts 512-byte blocks
        long cpuSeconds = (long)Math.Ceiling(limits.WallTimeout.TotalSeconds) + 1;
        string script = $"ulimit -c 0; ulimit -f {fileBlocks}; ulimit -t {cpuSeconds}; if command -v perl >/dev/null 2>&1; then exec perl -e 'setpgrp(0,0); exec @ARGV or die \"exec: $!\"' \"$@\"; else exec \"$@\"; fi";
        var wrapped = new List<string> { "-c", script, "sh", file };
        wrapped.AddRange(args);
        return ("/bin/sh", wrapped);
    }

    /// <summary>Minimal environment: inherited PATH, HOME and TMPDIR inside the workdir, UTF-8 locale, plus the spec's variables.</summary>
    internal static Dictionary<string, string> BuildEnvironment(ProcessSpec spec)
    {
        string tmp = Path.Combine(spec.WorkingDirectory, "tmp");
        var env = new Dictionary<string, string>
        {
            ["PATH"] = ToolLocator.SearchPath(),
            ["HOME"] = spec.WorkingDirectory,
            ["TMPDIR"] = tmp,
            ["LANG"] = "C.UTF-8",
            ["LC_ALL"] = "C.UTF-8",
        };
        if (OperatingSystem.IsWindows())
        {
            env["TEMP"] = tmp;
            env["TMP"] = tmp;
            foreach (string name in new[] { "SystemRoot", "ComSpec", "PATHEXT", "windir" })
                if (Environment.GetEnvironmentVariable(name) is { } v) env[name] = v;
        }
        if (spec.Environment is not null)
            foreach (var (k, v) in spec.Environment) env[k] = v;
        return env;
    }
}

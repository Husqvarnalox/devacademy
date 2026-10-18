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
        EnsureScratch(spec.WorkingDirectory);
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
        // CPU time is summed over all threads, so a multi-threaded runtime (JIT and GC threads of a JVM, the .NET tiered compiler) legitimately
        // spends several CPU seconds per wall second. A limit of one CPU second per wall second would kill it with SIGXCPU - no output, no
        // message, just "crashed" - long before the wall timeout, and the slower the machine the sooner. The wall timeout stays the real
        // limit; this one is only the backstop for a child that escapes it, so it scales with the cores the run can use.
        long cpuSeconds = (long)Math.Ceiling(limits.WallTimeout.TotalSeconds) * Math.Clamp(Environment.ProcessorCount, 1, 8) + 1;
        string script = $"ulimit -c 0; ulimit -f {fileBlocks}; ulimit -t {cpuSeconds}; if command -v perl >/dev/null 2>&1; then exec perl -e 'setpgrp(0,0); exec @ARGV or die \"exec: $!\"' \"$@\"; else exec \"$@\"; fi";
        var wrapped = new List<string> { "-c", script, "sh", file };
        wrapped.AddRange(args);
        return ("/bin/sh", wrapped);
    }

    /// <summary>
    /// The per-run scratch tree under <c>tmp</c> that HOME/TMPDIR/XDG_* point at. The directories must exist before the child starts:
    /// the .NET SDK resolves its file-based-app cache through <c>$XDG_DATA_HOME</c> (else <c>$HOME/.local/share</c>) and, when that
    /// folder is missing, fails with "Unable to determine a temporary directory path" (Linux; a fresh HOME per run exposes it).
    /// </summary>
    internal static void EnsureScratch(string workDir, UnixFileMode? mode = null)
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (string dir in new[] { "tmp", Path.Combine("tmp", "appdata"), Path.Combine("tmp", "localappdata") })
                Directory.CreateDirectory(Path.Combine(workDir, dir));
            return;
        }
        foreach (string dir in new[] { "tmp", Path.Combine("tmp", "xdg-data"), Path.Combine("tmp", "xdg-cache"), Path.Combine("tmp", "xdg-config") })
        {
            string full = Path.Combine(workDir, dir);
            if (mode is { } m) { Directory.CreateDirectory(full, m); File.SetUnixFileMode(full, m); }
            else Directory.CreateDirectory(full);
        }
    }

    /// <summary>Minimal environment: inherited PATH, HOME and TMPDIR inside the workdir, UTF-8 locale, plus the spec's variables.</summary>
    internal static Dictionary<string, string> BuildEnvironment(ProcessSpec spec)
    {
        string tmp = Path.Combine(spec.WorkingDirectory, "tmp");
        // Windows variable names are case-insensitive (PATH, Path): a spec's "Path" must replace ours, not sit beside it.
        var env = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        {
            ["PATH"] = ToolLocator.SearchPath(),
            ["HOME"] = spec.WorkingDirectory,
            ["TMPDIR"] = tmp,
            ["LANG"] = "C.UTF-8",
            ["LC_ALL"] = "C.UTF-8",
        };
        if (!OperatingSystem.IsWindows())
        {
            env["XDG_DATA_HOME"] = Path.Combine(tmp, "xdg-data");
            env["XDG_CACHE_HOME"] = Path.Combine(tmp, "xdg-cache");
            env["XDG_CONFIG_HOME"] = Path.Combine(tmp, "xdg-config");
        }
        if (OperatingSystem.IsWindows())
        {
            env["TEMP"] = tmp;
            env["TMP"] = tmp;
            // The Windows counterpart of HOME and XDG_*: tools (NuGet, rustup, node) resolve their per-user folders from these and fail on null paths without them.
            env["USERPROFILE"] = spec.WorkingDirectory;
            env["APPDATA"] = Path.Combine(tmp, "appdata");
            env["LOCALAPPDATA"] = Path.Combine(tmp, "localappdata");
            // Machine-level, non-secret variables: Windows itself, and toolchains that locate Visual Studio / the SDKs through them (rustc and clang find link.exe and the libraries this way).
            foreach (string name in new[]
            {
                "SystemRoot", "SystemDrive", "ComSpec", "PATHEXT", "windir", "OS", "NUMBER_OF_PROCESSORS", "PROCESSOR_ARCHITECTURE",
                "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "ProgramData", "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432", "ALLUSERSPROFILE",
            })
                if (Environment.GetEnvironmentVariable(name) is { } v) env[name] = v;
        }
        if (spec.Environment is not null)
            foreach (var (k, v) in spec.Environment) env[k] = v;
        return env;
    }
}

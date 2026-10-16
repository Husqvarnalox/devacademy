using Avalonia.Threading;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;

namespace DevAcademy.Desktop.Services;

/// <summary>
/// App-facing side of the debugger layer: finds (never installs on its own) the debug adapter for a language, builds debug binaries
/// and owns the per-run scratch directories. Debug runs are NOT sandboxed (the debugger needs ptrace / task_for_pid); the UI says so
/// and asks once per app run. Installing an adapter happens only through <see cref="InstallAsync"/>, which a confirmed user action calls.
/// </summary>
public sealed class DebugService
{
    private readonly AdapterResolver _resolver;

    public DebugService(AppPaths paths, ExecutionOptions execution, IDebugBuilder? builder = null, IAdapterLauncher? launcher = null, AdapterResolver? resolver = null)
    {
        RunsDirectory = Path.Combine(paths.DataDir, "debug-runs");
        _resolver = resolver ?? new AdapterResolver(execution.CacheDirectory);
        Builder = builder ?? DebugBuilderFactory.Create(execution);
        Launcher = launcher ?? new ProcessAdapterLauncher();
    }

    public string RunsDirectory { get; }
    public IDebugBuilder Builder { get; }

    /// <summary>How adapters are started (tests substitute an in-process fake).</summary>
    public IAdapterLauncher Launcher { get; set; }

    /// <summary>Runs <c>action</c> on the UI thread (debug events arrive on worker threads). Tests replace it with an inline call.</summary>
    public Action<Action> Invoke { get; set; } = a => { if (Dispatcher.UIThread.CheckAccess()) a(); else Dispatcher.UIThread.Post(a); };

    /// <summary>The "debugging runs the program without a sandbox" confirmation was accepted during this app run.</summary>
    public bool SandboxAcknowledged { get; set; }

    public bool Supports(string? languageId) => languageId is not null && DebugAdapterRegistry.ForLanguage(languageId) is not null;

    /// <summary>Why a language has no debugger (planned adapters get a specific note).</summary>
    public static string NoAdapterMessage(string? languageId) =>
        languageId is not null && DebugAdapterRegistry.Planned.TryGetValue(languageId, out var note)
            ? note
            : "Для этого языка отладчик пока не поддерживается.";

    public async Task<AdapterStatus> GetStatusAsync(string? languageId, string? pythonInterpreter, CancellationToken ct = default)
    {
        var def = languageId is null ? null : DebugAdapterRegistry.ForLanguage(languageId);
        if (def is null) return new AdapterStatus(AdapterState.Unavailable, NoAdapterMessage(languageId));
        return await _resolver.GetStatusAsync(def, new DebugEnvironment(pythonInterpreter), ct).ConfigureAwait(false);
    }

    /// <summary>Runs the exact command of <paramref name="plan"/> (the caller has shown it and got a yes).</summary>
    public Task<(bool Success, string Output)> InstallAsync(ProvisionCommand plan, Func<bool> isInstalled, CancellationToken ct = default) =>
        AdapterResolver.ProvisionAsync(plan, isInstalled, ct);

    /// <summary>A fresh scratch directory for one debug run (lesson files, debug binary).</summary>
    public string NewRunDirectory()
    {
        Directory.CreateDirectory(RunsDirectory);
        // Debuggers match breakpoints against the real path recorded in debug info; /var is a symlink to /private/var on macOS.
        string root = RunsDirectory;
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        string dir = Path.Combine(root, Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void CleanupRunDirectory(string? dir)
    {
        if (dir is null || !dir.StartsWith(RunsDirectory, StringComparison.Ordinal) && !dir.StartsWith("/private" + RunsDirectory, StringComparison.Ordinal)) return;
        try { Directory.Delete(dir, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* best effort; the next app start can clear debug-runs */ }
    }

    /// <summary>Removes leftovers of crashed runs. Called at most once per app run, before the first debug session.</summary>
    public void PurgeStaleRuns()
    {
        try
        {
            if (!Directory.Exists(RunsDirectory)) return;
            foreach (string d in Directory.EnumerateDirectories(RunsDirectory))
                if (Directory.GetLastWriteTimeUtc(d) < DateTime.UtcNow.AddHours(-12)) Directory.Delete(d, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}

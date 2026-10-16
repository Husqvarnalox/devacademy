using System.Diagnostics;

namespace DevAcademy.Execution;

/// <summary>
/// Linux bubblewrap: read-only root, private /tmp, no network, writes only to the workdir and the runner cache.
/// UNVERIFIED on real Linux: only the argument construction is unit-tested; it has never been executed.
/// </summary>
public sealed class LinuxBubblewrapSandbox(string cacheDirectory) : LocalProcessSandbox
{
    private Task<bool>? _available;

    public override IsolationLevel Level => IsolationLevel.OsSandbox;

    public override Task<bool> IsAvailableAsync(CancellationToken ct) => _available ??= ProbeAsync();

    private static async Task<bool> ProbeAsync()
    {
        if (!OperatingSystem.IsLinux() || ToolLocator.Find("bwrap") is not { } bwrap) return false;
        try
        {
            var psi = new ProcessStartInfo(bwrap) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (string a in new[] { "--ro-bind", "/", "/", "--unshare-all", "true" }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return p.ExitCode == 0; // fails when unprivileged user namespaces are disabled
        }
        catch (Exception ex) when (ex is OperationCanceledException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    protected override (string File, IReadOnlyList<string> Args) Wrap(
        string file, IReadOnlyList<string> args, ProcessSpec spec, ResourceLimits limits) =>
        ("bwrap", BuildArguments(spec.WorkingDirectory, cacheDirectory, file, args, limits.AllowNetwork, spec.ReadOnlyPaths));

    internal static IReadOnlyList<string> BuildArguments(
        string workDir, string cacheDir, string file, IReadOnlyList<string> args, bool allowNetwork, IReadOnlyList<string>? readOnlyPaths = null)
    {
        var result = new List<string>
        {
            "--ro-bind", "/", "/",
            "--dev", "/dev",
            "--proc", "/proc",
            "--tmpfs", "/tmp",
            "--bind", workDir, workDir,
            "--bind", cacheDir, cacheDir,
        };
        if (readOnlyPaths is not null)
            foreach (string ro in readOnlyPaths) result.AddRange(["--ro-bind", ro, ro]);
        result.Add("--unshare-all");
        if (allowNetwork) result.Add("--share-net");
        result.AddRange(["--die-with-parent", "--new-session", "--chdir", workDir, "--", file]);
        result.AddRange(args);
        return result;
    }
}

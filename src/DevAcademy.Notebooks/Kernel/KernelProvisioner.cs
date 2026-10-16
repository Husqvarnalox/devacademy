using System.Diagnostics;
using System.Text;

namespace DevAcademy.Notebooks.Kernel;

/// <summary>The exact command that installing the kernel would run; shown to the user before anything is executed.</summary>
public sealed record KernelInstallPlan(string FileName, IReadOnlyList<string> Arguments, string CommandLine, string EnvironmentDirectory);

public sealed record KernelInstallResult(bool Ok, string? Error);

/// <summary>
/// Checks for and installs <c>ipykernel</c> in a Python virtual environment. Installation happens only when <see cref="InstallAsync"/> is
/// called (the UI calls it after the user confirmed the exact command) and only into a virtual environment, never a system interpreter.
/// </summary>
public static class KernelProvisioner
{
    /// <summary>The ipykernel release this version of the app was tested with.</summary>
    public const string PinnedRequirement = "ipykernel==7.4.0";

    private static readonly string[] HostileEnvironment =
    [
        "PIP_TARGET", "PIP_PREFIX", "PIP_USER", "PIP_REQUIRE_VIRTUALENV", "PIP_INDEX_URL", "PIP_EXTRA_INDEX_URL", "PIP_CONFIG_FILE",
        "PYTHONHOME", "PYTHONPATH", "PYTHONUSERBASE", "PYTHONSTARTUP", "VIRTUAL_ENV",
    ];

    /// <summary>True when the interpreter belongs to a virtual environment (a <c>pyvenv.cfg</c> beside its bin/Scripts directory).</summary>
    public static bool IsVirtualEnvironment(string interpreter)
    {
        var bin = Path.GetDirectoryName(interpreter);
        var root = bin is null ? null : Path.GetDirectoryName(bin);
        return root is not null && File.Exists(Path.Combine(root, "pyvenv.cfg"));
    }

    /// <summary>The installed ipykernel version, or null when it cannot be imported.</summary>
    public static async Task<string?> GetInstalledVersionAsync(string interpreter, CancellationToken ct)
    {
        var r = await RunAsync(interpreter, ["-c", "import ipykernel; print(ipykernel.__version__)"], TimeSpan.FromSeconds(60), null, ct).ConfigureAwait(false);
        return r.ExitCode == 0 && r.Stdout.Trim() is { Length: > 0 } v ? v.Split('\n')[^1].Trim() : null;
    }

    /// <summary>The install command for <paramref name="interpreter"/>, or null when it is not a virtual environment (installing is refused).</summary>
    public static KernelInstallPlan? PlanInstall(string interpreter)
    {
        if (!IsVirtualEnvironment(interpreter)) return null;
        string[] args = ["-m", "pip", "install", "--disable-pip-version-check", PinnedRequirement];
        var line = Quote(interpreter) + " " + string.Join(' ', args);
        return new KernelInstallPlan(interpreter, args, line, Path.GetDirectoryName(Path.GetDirectoryName(interpreter))!);
    }

    public static async Task<KernelInstallResult> InstallAsync(KernelInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        if (!IsVirtualEnvironment(plan.FileName)) return new(false, "Установка допускается только в виртуальное окружение.");
        var r = await RunAsync(plan.FileName, plan.Arguments, TimeSpan.FromMinutes(10), onLine, ct).ConfigureAwait(false);
        return r.ExitCode == 0 ? new(true, null) : new(false, r.TimedOut ? "Установка не завершилась за 10 минут." : $"pip завершился с кодом {r.ExitCode}.\n{Tail(r.Stderr.Length > 0 ? r.Stderr : r.Stdout)}");
    }

    private static string Tail(string s) => s.Length > 1500 ? s[^1500..] : s;

    private static string Quote(string s) => s.Contains(' ') ? "\"" + s + "\"" : s;

    private sealed record Output(int ExitCode, string Stdout, string Stderr, bool TimedOut);

    private static async Task<Output> RunAsync(string file, IReadOnlyList<string> args, TimeSpan timeout, Action<string>? onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var h in HostileEnvironment) psi.Environment.Remove(h);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (stdout) stdout.AppendLine(e.Data); onLine?.Invoke(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (stderr) stderr.AppendLine(e.Data); onLine?.Invoke(e.Data); } };
        try { process.Start(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return new(-1, "", ex.Message, false); }
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            process.WaitForExit(); // flush the async readers
            return new(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            return new(-1, stdout.ToString(), stderr.ToString(), true);
        }
    }
}

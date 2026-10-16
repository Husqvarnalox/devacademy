using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.Debugging.Adapters;

public enum AdapterState { Ready, NotInstalled, Unavailable }

/// <summary>
/// Whether a language can be debugged right now. <see cref="Plan"/> is set only for <see cref="AdapterState.NotInstalled"/> when an
/// install is possible; it is shown to the user and run only after confirmation.
/// </summary>
public sealed record AdapterStatus(AdapterState State, string Message, ResolvedAdapter? Adapter = null, ProvisionCommand? Plan = null)
{
    public bool IsReady => State == AdapterState.Ready;
    public static AdapterStatus Ready(ResolvedAdapter a) => new(AdapterState.Ready, a.Definition.DisplayName + " ✓", a);
}

/// <summary>The exact command that would install an adapter, shown BEFORE anything is downloaded.</summary>
public sealed record ProvisionCommand(
    string Executable, IReadOnlyList<string> Args, string WorkingDirectory, IReadOnlyDictionary<string, string> Environment)
{
    public string Display =>
        string.Join(' ', Environment.Select(kv => $"{kv.Key}={Quote(kv.Value)}").Concat([Quote(Executable)]).Concat(Args.Select(Quote)));

    private static string Quote(string s) => s.Length > 0 && s.All(c => char.IsLetterOrDigit(c) || c is '/' or '.' or '_' or '-' or ':' or '@' or '=' or '\\' or '+')
        ? s : "\"" + s.Replace("\"", "\\\"") + "\"";
}

/// <summary>What the adapter needs to know about the machine: the interpreter of the selected Python environment.</summary>
public sealed record DebugEnvironment(string? PythonInterpreter = null);

public sealed record ProbeResult(int ExitCode, string Output);

/// <summary>Runs a short helper process. A seam so tests do not need real tools.</summary>
public delegate Task<ProbeResult> ProbeRunner(string executable, IReadOnlyList<string> args, CancellationToken ct);

/// <summary>
/// Finds (and, on explicit request, plans the installation of) debug adapters. Adapters installed by the app live below
/// <c>&lt;runner cache&gt;/debug-adapters</c>; nothing is installed into the system Python or anywhere else.
/// </summary>
public sealed class AdapterResolver
{
    private readonly string _cacheDirectory;
    private readonly ServerLocator _locator;
    private readonly ProbeRunner _probe;
    private readonly IReadOnlyList<string> _extraDirs;

    public AdapterResolver(string cacheDirectory, ServerLocator? locator = null, ProbeRunner? probe = null, IReadOnlyList<string>? extraSearchDirs = null)
    {
        _cacheDirectory = cacheDirectory;
        _locator = locator ?? new ServerLocator(null);
        _probe = probe ?? DefaultProbe;
        _extraDirs = extraSearchDirs ?? WellKnownLldbDirs();
    }

    public static string AdaptersDirectory(string cacheDirectory) => Path.Combine(cacheDirectory, "debug-adapters");
    /// <summary>GOBIN used by the explicit Delve install.</summary>
    public static string DelveBinDirectory(string cacheDirectory) => Path.Combine(AdaptersDirectory(cacheDirectory), "go", "bin");

    private static IReadOnlyList<string> WellKnownLldbDirs()
    {
        if (!OperatingSystem.IsMacOS()) return [];
        return
        [
            "/Applications/Xcode.app/Contents/Developer/usr/bin",
            "/Library/Developer/CommandLineTools/usr/bin",
            "/opt/homebrew/opt/llvm/bin",
            "/usr/local/opt/llvm/bin",
        ];
    }

    public async Task<AdapterStatus> GetStatusAsync(DebugAdapterDefinition def, DebugEnvironment env, CancellationToken ct)
    {
        try
        {
            return def.Kind switch
            {
                AdapterKind.DebugPy => await PythonStatusAsync(def, env, ct).ConfigureAwait(false),
                AdapterKind.LldbDap => await LldbStatusAsync(def, ct).ConfigureAwait(false),
                AdapterKind.Delve => DelveStatus(def),
                _ => new AdapterStatus(AdapterState.Unavailable, "Unknown adapter."),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AdapterStatus(AdapterState.Unavailable, $"{def.DisplayName}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- python / debugpy

    private async Task<AdapterStatus> PythonStatusAsync(DebugAdapterDefinition def, DebugEnvironment env, CancellationToken ct)
    {
        if (env.PythonInterpreter is not { Length: > 0 } python || !File.Exists(python))
            return new AdapterStatus(AdapterState.Unavailable, "Выберите окружение Python (виртуальное окружение проекта или управляемое окружение): отладчик debugpy ставится только в него.");
        var probe = await _probe(python, ["-c", "import debugpy"], ct).ConfigureAwait(false);
        if (probe.ExitCode == 0)
            return AdapterStatus.Ready(new ResolvedAdapter(def, python, ["-m", "debugpy.adapter"], new Dictionary<string, string>
            {
                ["PYTHONIOENCODING"] = "utf-8",
            }));
        var venv = await _probe(python, ["-c", "import sys; raise SystemExit(0 if sys.prefix != getattr(sys, 'base_prefix', sys.prefix) else 1)"], ct).ConfigureAwait(false);
        if (venv.ExitCode != 0)
            return new AdapterStatus(AdapterState.Unavailable,
                "debugpy не установлен, а выбранный интерпретатор не относится к виртуальному окружению. В системный Python приложение ничего не устанавливает: создайте .venv или управляемое окружение.");
        return new AdapterStatus(AdapterState.NotInstalled, "debugpy не установлен в выбранном окружении.", Plan: PlanPip(python));
    }

    /// <summary><c>python -m pip install debugpy</c>; pip itself refuses to run outside a virtual environment (PIP_REQUIRE_VIRTUALENV).</summary>
    public ProvisionCommand PlanPip(string python) => new(python, ["-m", "pip", "install", "debugpy"], Path.GetDirectoryName(python) ?? ".",
        new Dictionary<string, string> { ["PIP_REQUIRE_VIRTUALENV"] = "1", ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1" });

    // ---------------------------------------------------------------- lldb-dap

    private async Task<AdapterStatus> LldbStatusAsync(DebugAdapterDefinition def, CancellationToken ct)
    {
        string? exe = null;
        if (OperatingSystem.IsMacOS() && File.Exists("/usr/bin/xcrun"))
        {
            var r = await _probe("/usr/bin/xcrun", ["-f", "lldb-dap"], ct).ConfigureAwait(false);
            string path = r.Output.Trim().Split('\n').LastOrDefault()?.Trim() ?? "";
            if (r.ExitCode == 0 && File.Exists(path)) exe = path;
        }
        exe ??= FindExecutable(def.ExecutableNames, _extraDirs);
        return exe is null
            ? new AdapterStatus(AdapterState.Unavailable, $"lldb-dap не найден. {def.InstallHint}")
            : AdapterStatus.Ready(new ResolvedAdapter(def, exe, [], new Dictionary<string, string>()));
    }

    // ---------------------------------------------------------------- delve

    private AdapterStatus DelveStatus(DebugAdapterDefinition def)
    {
        string? dlv = FindExecutable(def.ExecutableNames, [DelveBinDirectory(_cacheDirectory)]);
        if (dlv is not null)
            return AdapterStatus.Ready(new ResolvedAdapter(def, dlv, ["dap", "--listen=127.0.0.1:0"], DelveRuntimeEnvironment(),
                new Regex(@"listening at:\s*(\S+)", RegexOptions.CultureInvariant)));
        string? go = _locator.FindExecutable("go", includeCache: false);
        if (go is null)
            return new AdapterStatus(AdapterState.Unavailable, "dlv не найден, и Go не установлен: установите Go (https://go.dev/dl/), затем Delve.");
        return new AdapterStatus(AdapterState.NotInstalled, "Delve (dlv) не установлен.", Plan: PlanDelve(go));
    }

    private static Dictionary<string, string> DelveRuntimeEnvironment() => new() { ["GOTOOLCHAIN"] = "local" };

    /// <summary>
    /// <c>go install github.com/go-delve/delve/cmd/dlv@&lt;pinned&gt;</c> into the runner cache (GOBIN), run from a scratch
    /// directory so no project go.mod is touched. Downloads from the Go module proxy; explicit user action only.
    /// </summary>
    public ProvisionCommand PlanDelve(string go) => new(go,
        ["install", $"{DebugAdapterDefinition.DelvePackage}@{DebugAdapterDefinition.DelvePinnedVersion}"],
        Path.Combine(AdaptersDirectory(_cacheDirectory), "go", "work"),
        new Dictionary<string, string> { ["GOBIN"] = DelveBinDirectory(_cacheDirectory), ["GOTOOLCHAIN"] = "local", ["GOFLAGS"] = "-mod=mod" });

    // ---------------------------------------------------------------- helpers

    private string? FindExecutable(IReadOnlyList<string> names, IReadOnlyList<string> firstDirs)
    {
        string[] exts = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat", ""] : [""];
        foreach (string name in names)
        {
            foreach (string dir in firstDirs.Concat(_locator.SystemDirs()))
                foreach (string ext in exts)
                {
                    string candidate = Path.Combine(dir, name + ext);
                    if (File.Exists(candidate)) return candidate;
                }
        }
        return null;
    }

    private static async Task<ProbeResult> DefaultProbe(string executable, IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetTempPath(),
        };
        foreach (string a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Close();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                Task<string> o = p.StandardOutput.ReadToEndAsync(cts.Token), e = p.StandardError.ReadToEndAsync(cts.Token);
                await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                return new ProbeResult(p.ExitCode, await o.ConfigureAwait(false) + await e.ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch (InvalidOperationException) { }
                ct.ThrowIfCancellationRequested();
                return new ProbeResult(-1, "timed out");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ProbeResult(-1, ex.Message);
        }
    }

    /// <summary>Runs an install command that the user has seen and confirmed.</summary>
    public static async Task<(bool Success, string Output)> ProvisionAsync(ProvisionCommand plan, Func<bool> isInstalled, CancellationToken ct)
    {
        Directory.CreateDirectory(plan.WorkingDirectory);
        var psi = new ProcessStartInfo(plan.Executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            WorkingDirectory = plan.WorkingDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in plan.Args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in plan.Environment) psi.Environment[k] = v;
        using var p = Process.Start(psi) ?? throw new InvalidOperationException(plan.Executable + " did not start");
        p.StandardInput.Close();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            Task<string> o = p.StandardOutput.ReadToEndAsync(timeout.Token), e = p.StandardError.ReadToEndAsync(timeout.Token);
            await p.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string output = (await o.ConfigureAwait(false) + await e.ConfigureAwait(false)).Trim();
            return (p.ExitCode == 0 && isInstalled(), output);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch (InvalidOperationException) { }
            ct.ThrowIfCancellationRequested();
            return (false, "The install timed out.");
        }
    }
}

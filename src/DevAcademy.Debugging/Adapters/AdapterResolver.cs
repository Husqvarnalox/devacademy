using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DevAcademy.Domain;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.Debugging.Adapters;

public enum AdapterState { Ready, NotInstalled, Unavailable }

/// <summary>
/// Whether a language can be debugged right now. <see cref="Plan"/> is set only for <see cref="AdapterState.NotInstalled"/> when an
/// install is possible; it is shown to the user and run only after confirmation.
/// </summary>
public sealed record AdapterStatus(AdapterState State, string Message, ResolvedAdapter? Adapter = null, ProvisionCommand? Plan = null,
    LocalizedText? MessageText = null)
{
    public bool IsReady => State == AdapterState.Ready;

    /// <summary>The message as translatable text (<see cref="Message"/> stays the English fallback).</summary>
    public LocalizedText Text => MessageText ?? LocalizedText.Literal(Message);

    public static AdapterStatus Of(AdapterState state, LocalizedText message, ProvisionCommand? plan = null) =>
        new(state, message.Fallback, Plan: plan, MessageText: message);

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
                AdapterKind.JsDebug => JsDebugStatus(def),
                AdapterKind.JavaJdi => await JavaStatusAsync(def, ct).ConfigureAwait(false),
                AdapterKind.NetCoreDbg => NetCoreDbgStatus(def),
                _ => AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.UnknownAdapter()),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.AdapterFailed(def.DisplayName, ex));
        }
    }

    // ---------------------------------------------------------------- python / debugpy

    private async Task<AdapterStatus> PythonStatusAsync(DebugAdapterDefinition def, DebugEnvironment env, CancellationToken ct)
    {
        if (env.PythonInterpreter is not { Length: > 0 } python || !File.Exists(python))
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.NoPythonEnvironment());
        var probe = await _probe(python, ["-c", "import debugpy"], ct).ConfigureAwait(false);
        if (probe.ExitCode == 0)
            return AdapterStatus.Ready(new ResolvedAdapter(def, python, ["-m", "debugpy.adapter"], new Dictionary<string, string>
            {
                ["PYTHONIOENCODING"] = "utf-8",
            }));
        var venv = await _probe(python, ["-c", "import sys; raise SystemExit(0 if sys.prefix != getattr(sys, 'base_prefix', sys.prefix) else 1)"], ct).ConfigureAwait(false);
        if (venv.ExitCode != 0)
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.DebugpyNoVenv());
        return AdapterStatus.Of(AdapterState.NotInstalled, EngineMessages.DebugpyNotInstalled(), PlanPip(python));
    }

    /// <summary><c>python -m pip install debugpy</c>; pip itself refuses to run outside a virtual environment (PIP_REQUIRE_VIRTUALENV).</summary>
    public ProvisionCommand PlanPip(string python) => new(python, ["-m", "pip", "install", "debugpy"], Path.GetDirectoryName(python) ?? ".",
        new Dictionary<string, string> { ["PIP_REQUIRE_VIRTUALENV"] = "1", ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1" });

    // ---------------------------------------------------------------- lldb-dap

    private async Task<AdapterStatus> LldbStatusAsync(DebugAdapterDefinition def, CancellationToken ct)
    {
        // lldb-dap on Windows (MinGW/MSVC builds) closes the session at startup in CI and is untested; say so instead of offering a broken debugger.
        if (OperatingSystem.IsWindows()) return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.LldbWindowsUnsupported());
        string? exe = null;
        if (OperatingSystem.IsMacOS() && File.Exists("/usr/bin/xcrun"))
        {
            var r = await _probe("/usr/bin/xcrun", ["-f", "lldb-dap"], ct).ConfigureAwait(false);
            string path = r.Output.Trim().Split('\n').LastOrDefault()?.Trim() ?? "";
            if (r.ExitCode == 0 && File.Exists(path)) exe = path;
        }
        exe ??= FindExecutable(def.ExecutableNames, _extraDirs);
        return exe is null
            ? AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.LldbNotFound(EngineMessages.InstallHint(def)))
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
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.DelveNoGo());
        return AdapterStatus.Of(AdapterState.NotInstalled, EngineMessages.DelveNotInstalled(), PlanDelve(go));
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

    // ---------------------------------------------------------------- js-debug

    /// <summary>Where the pinned vscode-js-debug release is unpacked: <c>&lt;cache&gt;/debug-adapters/js-debug/&lt;version&gt;/js-debug/src/dapDebugServer.js</c>.</summary>
    public static string JsDebugDirectory(string cacheDirectory) =>
        Path.Combine(AdaptersDirectory(cacheDirectory), "js-debug", DebugAdapterDefinition.JsDebugPinnedVersion);

    public static string JsDebugServerScript(string cacheDirectory) => Path.Combine(JsDebugDirectory(cacheDirectory), "js-debug", "src", "dapDebugServer.js");

    private AdapterStatus JsDebugStatus(DebugAdapterDefinition def)
    {
        string? node = _locator.FindExecutable("node", includeCache: false);
        if (node is null)
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.NodeMissing());
        string script = JsDebugServerScript(_cacheDirectory);
        if (File.Exists(script))
            // Port 0: the server picks a free port and prints "Debug server listening at 127.0.0.1:<port>"; it is bound to loopback only.
            return AdapterStatus.Ready(new ResolvedAdapter(def, node, [script, "0", "127.0.0.1"], new Dictionary<string, string>(),
                new Regex(@"listening at\s+(\S+)", RegexOptions.CultureInvariant)));
        if (OperatingSystem.IsWindows())
            return AdapterStatus.Of(AdapterState.Unavailable,
                EngineMessages.JsDebugWindows(DebugAdapterDefinition.JsDebugUrl, DebugAdapterDefinition.JsDebugSha256, JsDebugDirectory(_cacheDirectory)));
        return AdapterStatus.Of(AdapterState.NotInstalled, EngineMessages.JsDebugNotInstalled(DebugAdapterDefinition.JsDebugPinnedVersion), PlanJsDebug());
    }

    /// <summary>
    /// Downloads the pinned release tarball with curl, verifies its SHA-256 and only then unpacks it into the runner cache (set -e: a failed
    /// check leaves nothing behind but the tarball, which is removed). The whole script is shown to the user before it runs.
    /// </summary>
    public ProvisionCommand PlanJsDebug()
    {
        string dir = JsDebugDirectory(_cacheDirectory);
        const string Archive = "js-debug-dap.tar.gz";
        string script = string.Join(" && ",
            $"mkdir -p {QuoteSh(dir)}",
            $"cd {QuoteSh(dir)}",
            $"curl -fsSL -o {Archive} {QuoteSh(DebugAdapterDefinition.JsDebugUrl)}",
            $"echo '{DebugAdapterDefinition.JsDebugSha256}  {Archive}' | {{ shasum -a 256 -c - || sha256sum -c -; }}",
            $"tar xzf {Archive}",
            $"rm -f {Archive}");
        return new ProvisionCommand("/bin/sh", ["-c", script], AdaptersDirectory(_cacheDirectory), new Dictionary<string, string>());
    }

    private static string QuoteSh(string s) => "'" + s.Replace("'", "'\\''") + "'";

    // ---------------------------------------------------------------- C# / netcoredbg

    public static string NetCoreDbgDirectory(string cacheDirectory) =>
        Path.Combine(AdaptersDirectory(cacheDirectory), "netcoredbg", DebugAdapterDefinition.NetCoreDbgVersion);

    public static string NetCoreDbgExecutable(string cacheDirectory) =>
        Path.Combine(NetCoreDbgDirectory(cacheDirectory), "netcoredbg", OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg");

    private AdapterStatus NetCoreDbgStatus(DebugAdapterDefinition def)
    {
        string exe = NetCoreDbgExecutable(_cacheDirectory);
        if (File.Exists(exe))
            return AdapterStatus.Ready(new ResolvedAdapter(def, exe, ["--interpreter=vscode"], new Dictionary<string, string>()));
        if (_locator.FindExecutable("dotnet", includeCache: false) is null)
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.DotNetMissing());
        if (OperatingSystem.IsMacOS() && System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
            return AdapterStatus.Of(AdapterState.NotInstalled, EngineMessages.NetCoreDbgNotInstalled(DebugAdapterDefinition.NetCoreDbgVersion), PlanNetCoreDbg());
        return AdapterStatus.Of(AdapterState.Unavailable,
            EngineMessages.NetCoreDbgManual(DebugAdapterDefinition.NetCoreDbgVersion, NetCoreDbgDirectory(_cacheDirectory), NetCoreDbgExecutable(_cacheDirectory)));
    }

    /// <summary>Downloads the pinned zip with curl, verifies its SHA-256 and only then unpacks it into the runner cache. Shown to the user before it runs.</summary>
    public ProvisionCommand PlanNetCoreDbg()
    {
        string dir = NetCoreDbgDirectory(_cacheDirectory);
        const string Archive = "netcoredbg.zip";
        string script = string.Join(" && ",
            $"mkdir -p {QuoteSh(dir)}",
            $"cd {QuoteSh(dir)}",
            $"curl -fsSL -o {Archive} {QuoteSh(DebugAdapterDefinition.NetCoreDbgOsxArm64Url)}",
            $"echo '{DebugAdapterDefinition.NetCoreDbgOsxArm64Sha256}  {Archive}' | shasum -a 256 -c -",
            $"unzip -q -o {Archive}",
            $"rm -f {Archive}");
        return new ProvisionCommand("/bin/sh", ["-c", script], AdaptersDirectory(_cacheDirectory), new Dictionary<string, string>());
    }

    // ---------------------------------------------------------------- java / JDI

    private const string JavaAdapterClass = "DevAcademyJdiAdapter";

    private static readonly Lazy<string> JavaSource = new(() =>
    {
        using var s = typeof(AdapterResolver).Assembly.GetManifestResourceStream("DevAcademyJdiAdapter.java")
            ?? throw new InvalidOperationException("The embedded Java adapter source is missing.");
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    });

    /// <summary>Directory of the compiled adapter; named after a hash of its source, so a new app version compiles its own copy.</summary>
    public static string JavaAdapterDirectory(string cacheDirectory)
    {
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(JavaSource.Value)))[..12].ToLowerInvariant();
        return Path.Combine(AdaptersDirectory(cacheDirectory), "java", hash);
    }

    public static string JavaAdapterClasses(string cacheDirectory) => Path.Combine(JavaAdapterDirectory(cacheDirectory), "classes");

    private async Task<AdapterStatus> JavaStatusAsync(DebugAdapterDefinition def, CancellationToken ct)
    {
        string? javac = _locator.FindExecutable("javac", includeCache: false), java = null;
        if (javac is not null)
        {
            // The adapter must run on the JDK that compiled the program: the java next to javac.
            string sibling = Path.Combine(Path.GetDirectoryName(javac)!, OperatingSystem.IsWindows() ? "java.exe" : "java");
            java = File.Exists(sibling) ? sibling : _locator.FindExecutable("java", includeCache: false);
        }
        // /usr/bin/java and /usr/bin/javac are shims on macOS that fail without a JDK.
        if (javac is null || java is null || (await _probe(javac, ["-version"], ct).ConfigureAwait(false)).ExitCode != 0)
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.JdkNotFound(EngineMessages.InstallHint(def)));
        string classes = JavaAdapterClasses(_cacheDirectory);
        if (File.Exists(Path.Combine(classes, JavaAdapterClass + ".class")))
            return AdapterStatus.Ready(new ResolvedAdapter(def, java, ["-cp", classes, JavaAdapterClass], new Dictionary<string, string>()));
        var modules = await _probe(java, ["--list-modules"], ct).ConfigureAwait(false);
        if (modules.ExitCode != 0 || !modules.Output.Contains("jdk.jdi", StringComparison.Ordinal))
            return AdapterStatus.Of(AdapterState.Unavailable, EngineMessages.NoJdi());
        return AdapterStatus.Of(AdapterState.NotInstalled, EngineMessages.JavaNotBuilt(), PlanJava(javac));
    }

    /// <summary>
    /// <c>javac -d classes DevAcademyJdiAdapter.java</c> in the runner cache: the adapter's source ships inside the app and is written next to
    /// its output, then compiled with the learner's own JDK. Nothing is downloaded.
    /// </summary>
    public ProvisionCommand PlanJava(string javac)
    {
        string dir = JavaAdapterDirectory(_cacheDirectory);
        string source = Path.Combine(dir, "src", JavaAdapterClass + ".java");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, JavaSource.Value, new UTF8Encoding(false));
        return new ProvisionCommand(javac, ["-nowarn", "-encoding", "UTF-8", "-d", JavaAdapterClasses(_cacheDirectory), source], dir, new Dictionary<string, string>());
    }

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

using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevAcademy.Execution;

/// <summary>A Python interpreter found on this machine, as reported by the interpreter itself (not guessed from its path).</summary>
/// <param name="Implementation">sys.implementation.name: "cpython", "pypy", ...</param>
/// <param name="FreeThreaded">sysconfig Py_GIL_DISABLED (PEP 703 free-threaded build).</param>
/// <param name="Architecture">platform.machine(): "arm64", "x86_64", "AMD64"...</param>
/// <param name="Source">Where it was found: PATH, Homebrew, pyenv, uv, py launcher, ...</param>
public sealed record PythonInterpreter(
    string Path,
    string Version,
    string Implementation,
    bool FreeThreaded,
    string Architecture,
    string Source)
{
    public string DisplayName => $"{Implementation} {Version}" + (FreeThreaded ? " (free-threaded)" : "");

    public Version? ParsedVersion => System.Version.TryParse(Version, out var v) ? v : null;
}

/// <summary>Where the detector looks. Defaults describe the real machine; tests narrow them.</summary>
public sealed record PythonDetectionOptions
{
    /// <summary>Directories scanned for python / python3 / python3.N. Null = PATH plus the usual extra directories.</summary>
    public IReadOnlyList<string>? SearchDirectories { get; init; }
    public string? HomeDirectory { get; init; }
    /// <summary>Path of uv, or null to look it up. An empty string means "uv is not installed".</summary>
    public string? UvExecutable { get; init; }
    /// <summary>Path of the Windows "py" launcher, or null to look it up on Windows only.</summary>
    public string? PyLauncher { get; init; }
    public bool? IsWindows { get; init; }
    public int MaxParallelProbes { get; init; } = 4;
}

/// <summary>
/// Finds Python interpreters: PATH, Homebrew / system directories, pyenv, uv-managed, and the Windows py launcher. Every candidate is
/// probed by running it once with <c>-I -c</c>; candidates that do not run (shims, the Windows Store stub, Python 2) are dropped.
/// Probing executes only the interpreters themselves with a fixed read-only script.
/// </summary>
public sealed class PythonInterpreterDetector(IToolRunner runner, PythonDetectionOptions? options = null)
{
    private readonly PythonDetectionOptions _options = options ?? new PythonDetectionOptions();

    internal const string ProbeScript =
        "import sys,sysconfig,platform,json\n" +
        "print(json.dumps({'version':'.'.join(map(str,sys.version_info[:3])),'impl':sys.implementation.name," +
        "'gil_disabled':bool(sysconfig.get_config_var('Py_GIL_DISABLED')),'machine':platform.machine()," +
        "'in_venv':sys.prefix!=getattr(sys,'base_prefix',sys.prefix)}))";

    private static readonly Regex UnixName = new(@"^python(3(\.\d+)?)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionedName = new(@"^python3\.\d+t?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private bool IsWindows => _options.IsWindows ?? OperatingSystem.IsWindows();
    private string Home => _options.HomeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public async Task<IReadOnlyList<PythonInterpreter>> DetectAsync(CancellationToken ct)
    {
        var candidates = new List<(string Path, string Source)>();
        AddDirectoryCandidates(candidates);
        AddPyenvCandidates(candidates);
        await AddUvCandidatesAsync(candidates, ct).ConfigureAwait(false);
        await AddPyLauncherCandidatesAsync(candidates, ct).ConfigureAwait(false);

        // The same interpreter shows up as python, python3 and python3.14 (symlinks): keep the first spelling per real file.
        var unique = new List<(string Path, string Source)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in candidates)
        {
            if (!File.Exists(c.Path)) continue;
            string real = MacSandboxExecSandbox.RealPath(c.Path);
            if (seen.Add(real)) unique.Add(c);
        }

        using var gate = new SemaphoreSlim(Math.Max(1, _options.MaxParallelProbes));
        var tasks = unique.Select(async c =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try { return await ProbeAsync(c.Path, c.Source, ct).ConfigureAwait(false); }
            finally { gate.Release(); }
        }).ToList();
        PythonInterpreter?[] probed = await Task.WhenAll(tasks).ConfigureAwait(false);

        return probed.OfType<PythonInterpreter>()
            .OrderByDescending(p => p.ParsedVersion)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Runs the interpreter once; null when it does not behave like a usable Python 3 (and when it is a venv's own python).</summary>
    public async Task<PythonInterpreter?> ProbeAsync(string path, string source, CancellationToken ct)
    {
        ToolOutput o = await runner.RunAsync(path, ["-I", "-c", ProbeScript], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(10)), ct).ConfigureAwait(false);
        if (!o.Succeeded) return null;
        return ParseProbe(path, source, o.Stdout);
    }

    internal static PythonInterpreter? ParseProbe(string path, string source, string stdout)
    {
        string? line = stdout.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith('{'));
        if (line is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            JsonElement r = doc.RootElement;
            if (r.TryGetProperty("in_venv", out var venv) && venv.ValueKind == JsonValueKind.True) return null;
            string? version = r.GetProperty("version").GetString();
            if (version is null || !version.StartsWith("3.", StringComparison.Ordinal)) return null;
            return new PythonInterpreter(path, version, r.GetProperty("impl").GetString() ?? "unknown",
                r.TryGetProperty("gil_disabled", out var g) && g.ValueKind == JsonValueKind.True,
                r.TryGetProperty("machine", out var m) ? m.GetString() ?? "unknown" : "unknown", source);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private IEnumerable<string> SearchDirs()
    {
        if (_options.SearchDirectories is { } forced) return forced;
        return ToolLocator.SearchPath().Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
    }

    private void AddDirectoryCandidates(List<(string Path, string Source)> into)
    {
        foreach (string dir in SearchDirs().Distinct())
        {
            if (!Directory.Exists(dir)) continue;
            string source = dir switch
            {
                "/opt/homebrew/bin" => "Homebrew",
                "/usr/local/bin" => "/usr/local",
                _ => "PATH",
            };
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir).ToList(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (string f in files.Order(StringComparer.Ordinal))
            {
                string name = System.IO.Path.GetFileName(f);
                if (IsWindows) name = Regex.Replace(name, @"\.exe$", "", RegexOptions.IgnoreCase);
                if (UnixName.IsMatch(name) || (source != "PATH" && VersionedName.IsMatch(name)))
                    into.Add((f, source));
            }
        }
    }

    private void AddPyenvCandidates(List<(string Path, string Source)> into)
    {
        string root = Environment.GetEnvironmentVariable("PYENV_ROOT") is { Length: > 0 } r ? r : System.IO.Path.Combine(Home, ".pyenv");
        string versions = IsWindows ? System.IO.Path.Combine(root, "pyenv-win", "versions") : System.IO.Path.Combine(root, "versions");
        if (!Directory.Exists(versions)) return;
        foreach (string v in Directory.EnumerateDirectories(versions).Order(StringComparer.Ordinal))
        {
            string exe = IsWindows ? System.IO.Path.Combine(v, "python.exe") : System.IO.Path.Combine(v, "bin", "python");
            if (File.Exists(exe)) into.Add((exe, "pyenv"));
        }
    }

    private async Task AddUvCandidatesAsync(List<(string Path, string Source)> into, CancellationToken ct)
    {
        string? uv = _options.UvExecutable ?? runner.Find("uv");
        if (string.IsNullOrEmpty(uv)) return;
        ToolOutput o = await runner.RunAsync(uv, ["python", "list", "--only-installed", "--output-format", "json"],
            new ToolRunOptions(Timeout: TimeSpan.FromSeconds(20)), ct).ConfigureAwait(false);
        if (!o.Succeeded) return;
        foreach (string p in ParseUvList(o.Stdout)) into.Add((p, "uv"));
    }

    /// <summary>Paths from <c>uv python list --only-installed --output-format json</c>.</summary>
    public static IReadOnlyList<string> ParseUvList(string json)
    {
        var result = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (JsonElement e in doc.RootElement.EnumerateArray())
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } s)
                    result.Add(s);
        }
        catch (JsonException) { }
        return result;
    }

    private async Task AddPyLauncherCandidatesAsync(List<(string Path, string Source)> into, CancellationToken ct)
    {
        if (!IsWindows) return;
        string? py = _options.PyLauncher ?? runner.Find("py");
        if (string.IsNullOrEmpty(py)) return;
        ToolOutput o = await runner.RunAsync(py, ["-0p"], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(10)), ct).ConfigureAwait(false);
        if (!o.Succeeded) return;
        foreach (string p in ParsePyLauncher(o.Stdout)) into.Add((p, "py launcher"));
    }

    /// <summary>Paths from <c>py -0p</c> (lines like <c> -V:3.12 *   C:\Python312\python.exe</c> or the older <c> -3.12-64   C:\...\python.exe</c>).</summary>
    public static IReadOnlyList<string> ParsePyLauncher(string output)
    {
        var result = new List<string>();
        foreach (string raw in output.Split('\n'))
        {
            string line = raw.Trim().TrimEnd('*').Trim();
            int idx = line.IndexOf(":\\", StringComparison.Ordinal);
            if (idx < 1) continue;
            string path = line[(idx - 1)..].Trim();
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) result.Add(path);
        }
        return result;
    }
}

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevAcademy.Execution;

public enum PythonEnvTool { Venv, Uv }

/// <summary>A managed virtual environment: a directory directly under the managed root that contains a pyvenv.cfg.</summary>
public sealed record ManagedPythonEnvironment(string Name, string Path, string InterpreterPath, string? Version, bool CreatedWithUv);

public sealed record PythonPackage(string Name, string Version);

public sealed record PythonEnvironmentValidation(bool Ok, string? Version, string? Implementation, string? Error);

public sealed record PythonEnvResult(bool Ok, string? Error, ManagedPythonEnvironment? Environment = null)
{
    public static PythonEnvResult Fail(string error) => new(false, error);
}

public enum InstallSource { Packages, RequirementsFile, Pyproject }

/// <summary>What the user asked to install. Never executed implicitly.</summary>
public sealed record PythonInstallRequest(InstallSource Source, IReadOnlyList<string> Packages, string? FilePath = null)
{
    public static PythonInstallRequest FromPackages(IEnumerable<string> specs) => new(InstallSource.Packages, specs.ToList());
    public static PythonInstallRequest FromRequirements(string file) => new(InstallSource.RequirementsFile, [], file);
    public static PythonInstallRequest FromPyproject(string file) => new(InstallSource.Pyproject, [], file);
}

/// <summary>The exact command that would run, shown to the user before consent. <see cref="Downloads"/> is always true for installs.</summary>
public sealed record PythonInstallPlan(
    string Tool,
    IReadOnlyList<string> Arguments,
    string CommandLine,
    bool Downloads,
    IReadOnlyList<string> PackageNames,
    string EnvironmentPath);

public sealed record PythonInstallResult(bool Ok, string? Error, bool Cancelled = false);

/// <summary>Result of the importlib.metadata probe: which distributions are installed in the environment, and their versions.</summary>
public sealed record PythonProbeResult(bool Ok, string? Error, string? Version, string? Implementation, IReadOnlyDictionary<string, string> Installed);

/// <summary>
/// Python Environment Manager. Owns virtual environments under <c>&lt;root&gt;/&lt;name&gt;</c> (the app's <c>python-envs</c> directory) and nothing else:
/// it never touches a system interpreter, never installs globally, and installs only through an explicit method called on user action.
/// </summary>
public sealed class PythonEnvironmentManager
{
    private static readonly Regex NamePattern = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] HostileEnvironment =
    [
        "PIP_TARGET", "PIP_PREFIX", "PIP_USER", "PIP_REQUIRE_VIRTUALENV", "PIP_INDEX_URL", "PIP_EXTRA_INDEX_URL", "PIP_CONFIG_FILE",
        "PYTHONHOME", "PYTHONPATH", "PYTHONUSERBASE", "PYTHONSTARTUP", "VIRTUAL_ENV", "UV_PYTHON", "UV_SYSTEM_PYTHON", "UV_PROJECT_ENVIRONMENT",
    ];

    private readonly IToolRunner _runner;
    private readonly bool _isWindows;

    public PythonEnvironmentManager(string root, IToolRunner? runner = null, bool? isWindows = null)
    {
        _runner = runner ?? new SystemToolRunner();
        _isWindows = isWindows ?? OperatingSystem.IsWindows();
        Root = System.IO.Path.GetFullPath(root);
    }

    public string Root { get; }

    public static bool IsValidName(string? name) => name is not null && NamePattern.IsMatch(name) && !name.EndsWith('.');

    public string EnvironmentPath(string name) => System.IO.Path.Combine(Root, name);

    public string InterpreterPath(string envPath) => _isWindows
        ? System.IO.Path.Combine(envPath, "Scripts", "python.exe")
        : System.IO.Path.Combine(envPath, "bin", "python");

    /// <summary>
    /// Finds the interpreter of an arbitrary virtual-environment directory (not necessarily one this manager owns), e.g. a venv created
    /// with <c>python -m venv</c>. The host's layout is tried first (<c>Scripts/python.exe</c> on Windows, <c>bin/python</c> elsewhere),
    /// then the other one. Null when the directory holds no interpreter.
    /// </summary>
    public string? ResolveInterpreter(string envDir)
    {
        string dir = System.IO.Path.GetFullPath(envDir);
        string[] unix = [System.IO.Path.Combine(dir, "bin", "python"), System.IO.Path.Combine(dir, "bin", "python3")];
        string[] windows = [System.IO.Path.Combine(dir, "Scripts", "python.exe")];
        IEnumerable<string> candidates = _isWindows ? windows.Concat(unix) : unix.Concat(windows);
        return candidates.FirstOrDefault(File.Exists);
    }

    public bool IsUvAvailable => _runner.Find("uv") is not null;

    // ---------------------------------------------------------------- list / create / delete

    public IReadOnlyList<ManagedPythonEnvironment> List()
    {
        var result = new List<ManagedPythonEnvironment>();
        if (!Directory.Exists(Root)) return result;
        foreach (string dir in Directory.EnumerateDirectories(Root).Order(StringComparer.Ordinal))
        {
            string name = System.IO.Path.GetFileName(dir);
            if (!IsValidName(name)) continue;
            if (new DirectoryInfo(dir).LinkTarget is not null) continue; // a link planted in the root is not one of ours
            string cfg = System.IO.Path.Combine(dir, "pyvenv.cfg");
            if (!File.Exists(cfg)) continue;
            result.Add(Describe(name, dir, ReadConfig(cfg)));
        }
        return result;
    }

    public ManagedPythonEnvironment? Get(string name) => IsValidName(name) ? List().FirstOrDefault(e => e.Name == name) : null;

    private ManagedPythonEnvironment Describe(string name, string dir, Dictionary<string, string> cfg)
    {
        string? version = cfg.GetValueOrDefault("version_info") ?? cfg.GetValueOrDefault("version");
        return new ManagedPythonEnvironment(name, dir, InterpreterPath(dir), version, cfg.ContainsKey("uv"));
    }

    private static Dictionary<string, string> ReadConfig(string cfgPath)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string line in File.ReadLines(cfgPath))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) d[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }
        catch (IOException) { }
        return d;
    }

    public async Task<PythonEnvResult> CreateAsync(string name, string interpreterPath, PythonEnvTool tool, CancellationToken ct)
    {
        if (!IsValidName(name))
            return PythonEnvResult.Fail("Недопустимое имя окружения: допустимы латинские буквы, цифры, '.', '_' и '-' (до 64 символов, начинается с буквы или цифры).");
        if (!File.Exists(interpreterPath))
            return PythonEnvResult.Fail($"Интерпретатор не найден: {interpreterPath}");
        string target = EnvironmentPath(name);
        if (Directory.Exists(target) || File.Exists(target))
            return PythonEnvResult.Fail($"Окружение «{name}» уже существует.");

        Directory.CreateDirectory(Root);
        string fileName;
        List<string> args;
        if (tool == PythonEnvTool.Uv)
        {
            string? uv = _runner.Find("uv");
            if (uv is null) return PythonEnvResult.Fail("Утилита uv не найдена в PATH.");
            fileName = uv;
            args = ["venv", "--python", interpreterPath, target];
        }
        else
        {
            fileName = interpreterPath;
            args = ["-I", "-m", "venv", target];
        }

        ToolOutput o = await _runner.RunAsync(fileName, args, new ToolRunOptions(Timeout: TimeSpan.FromMinutes(3), RemoveEnvironment: HostileEnvironment), ct).ConfigureAwait(false);
        if (!o.Succeeded)
        {
            TryDeleteDirectory(target); // never leave a half-made environment behind
            return PythonEnvResult.Fail($"Не удалось создать окружение: {o.FailureText}");
        }
        ManagedPythonEnvironment? env = Get(name);
        return env is null ? PythonEnvResult.Fail("Окружение создано, но не распознано (нет pyvenv.cfg).") : new PythonEnvResult(true, null, env);
    }

    /// <summary>
    /// Deletes a managed environment. Refuses anything that is not a direct child of the managed root (names with separators or "..",
    /// symlinks, directories without pyvenv.cfg), so a crafted name or path can never remove files elsewhere.
    /// </summary>
    public PythonEnvResult Delete(string nameOrPath)
    {
        string? error = CheckDeletable(nameOrPath, out string full);
        if (error is not null) return PythonEnvResult.Fail(error);
        try
        {
            Directory.Delete(full, recursive: true);
            return new PythonEnvResult(true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PythonEnvResult.Fail($"Не удалось удалить окружение: {ex.Message}");
        }
    }

    private string? CheckDeletable(string nameOrPath, out string full)
    {
        full = "";
        if (string.IsNullOrWhiteSpace(nameOrPath)) return "Пустое имя окружения.";
        string candidate = System.IO.Path.IsPathRooted(nameOrPath) ? nameOrPath : System.IO.Path.Combine(Root, nameOrPath);
        full = System.IO.Path.GetFullPath(candidate).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

        string? parent = System.IO.Path.GetDirectoryName(full);
        if (parent is null || !string.Equals(parent, Root.TrimEnd(System.IO.Path.DirectorySeparatorChar), PathComparison))
            return "Удаление отклонено: путь находится вне каталога управляемых окружений.";
        if (!IsValidName(System.IO.Path.GetFileName(full))) return "Удаление отклонено: недопустимое имя окружения.";
        if (!Directory.Exists(full)) return "Окружение не найдено.";
        if (new DirectoryInfo(full).LinkTarget is not null) return "Удаление отклонено: путь является символической ссылкой.";
        // Resolve links in the root itself too (macOS /var -> /private/var): the real directory must still sit in the real root.
        string realRoot = MacSandboxExecSandbox.RealPath(Root);
        string realFull = MacSandboxExecSandbox.RealPath(full);
        if (!string.Equals(System.IO.Path.GetDirectoryName(realFull), realRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar), PathComparison))
            return "Удаление отклонено: реальный путь находится вне каталога управляемых окружений.";
        if (!File.Exists(System.IO.Path.Combine(full, "pyvenv.cfg"))) return "Удаление отклонено: это не виртуальное окружение (нет pyvenv.cfg).";
        return null;
    }

    private StringComparison PathComparison => _isWindows || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // ---------------------------------------------------------------- validate / probe / packages

    internal const string InfoScript =
        "import sys,json\nprint(json.dumps({'version':'.'.join(map(str,sys.version_info[:3])),'impl':sys.implementation.name}))";

    internal const string MetadataScript =
        "import sys,json,importlib.metadata as m\n" +
        "names=json.load(sys.stdin)\n" +
        "out={}\n" +
        "for n in names:\n" +
        "    try: out[n]=m.version(n)\n" +
        "    except m.PackageNotFoundError: pass\n" +
        "    except Exception: pass\n" +
        "print(json.dumps({'version':'.'.join(map(str,sys.version_info[:3])),'impl':sys.implementation.name,'installed':out}))";

    public async Task<PythonEnvironmentValidation> ValidateAsync(string envPath, CancellationToken ct)
    {
        string python = InterpreterPath(envPath);
        if (!File.Exists(python)) return new(false, null, null, "В окружении нет интерпретатора.");
        ToolOutput o = await _runner.RunAsync(python, ["-I", "-c", InfoScript], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(15)), ct).ConfigureAwait(false);
        if (!o.Succeeded) return new(false, null, null, o.FailureText);
        try
        {
            using var doc = JsonDocument.Parse(LastJsonLine(o.Stdout));
            return new(true, doc.RootElement.GetProperty("version").GetString(), doc.RootElement.GetProperty("impl").GetString(), null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new(false, null, null, "Интерпретатор вернул непонятный ответ.");
        }
    }

    /// <summary>
    /// Reads installed distribution versions through <c>importlib.metadata</c> (nothing is imported, so a package with side effects or a
    /// heavy import never runs). Package names are passed on stdin as JSON, never on the command line.
    /// </summary>
    public async Task<PythonProbeResult> ProbeAsync(string interpreterPath, IReadOnlyCollection<string> distributionNames, CancellationToken ct)
    {
        string stdin = JsonSerializer.Serialize(distributionNames);
        ToolOutput o = await _runner.RunAsync(interpreterPath, ["-I", "-c", MetadataScript],
            new ToolRunOptions(Timeout: TimeSpan.FromSeconds(30), Stdin: stdin), ct).ConfigureAwait(false);
        if (!o.Succeeded) return new(false, o.FailureText, null, null, new Dictionary<string, string>());
        try
        {
            using var doc = JsonDocument.Parse(LastJsonLine(o.Stdout));
            var installed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.GetProperty("installed").EnumerateObject())
                installed[p.Name] = p.Value.GetString() ?? "";
            return new(true, null, doc.RootElement.GetProperty("version").GetString(), doc.RootElement.GetProperty("impl").GetString(), installed);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new(false, "Интерпретатор вернул непонятный ответ.", null, null, new Dictionary<string, string>());
        }
    }

    private static string LastJsonLine(string stdout) =>
        stdout.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith('{')) ?? "";

    public async Task<(IReadOnlyList<PythonPackage> Packages, string? Error)> ListPackagesAsync(string envPath, CancellationToken ct)
    {
        string python = InterpreterPath(envPath);
        if (!File.Exists(python)) return ([], "В окружении нет интерпретатора.");
        (string tool, List<string> args)? cmd = ResolvePackageTool(envPath, python, forList: true);
        if (cmd is null) return ([], "Не найден ни pip в окружении, ни uv.");
        ToolOutput o = await _runner.RunAsync(cmd.Value.tool, cmd.Value.args, new ToolRunOptions(Timeout: TimeSpan.FromMinutes(1), RemoveEnvironment: HostileEnvironment), ct).ConfigureAwait(false);
        if (!o.Succeeded) return ([], o.FailureText);
        return (ParsePipList(o.Stdout), null);
    }

    public static IReadOnlyList<PythonPackage> ParsePipList(string json)
    {
        var result = new List<PythonPackage>();
        try
        {
            int start = json.IndexOf('[');
            if (start < 0) return result;
            using var doc = JsonDocument.Parse(json[start..]);
            foreach (JsonElement e in doc.RootElement.EnumerateArray())
                if (e.TryGetProperty("name", out var n) && e.TryGetProperty("version", out var v))
                    result.Add(new PythonPackage(n.GetString() ?? "", v.GetString() ?? ""));
        }
        catch (JsonException) { }
        return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>pip inside the env when it has one; otherwise uv (uv-made venvs ship without pip) targeting the env's interpreter.</summary>
    private (string tool, List<string> args)? ResolvePackageTool(string envPath, string python, bool forList)
    {
        var cfg = ReadConfig(System.IO.Path.Combine(envPath, "pyvenv.cfg"));
        bool hasPip = Directory.Exists(System.IO.Path.Combine(envPath, _isWindows ? "Lib" : "lib")) && HasPip(envPath);
        string? uv = _runner.Find("uv");
        bool preferUv = cfg.ContainsKey("uv") || !hasPip;
        if (preferUv && uv is not null)
            return (uv, forList ? ["pip", "list", "--format", "json", "--python", python] : ["pip", "install", "--python", python]);
        if (hasPip)
            return (python, forList ? ["-I", "-m", "pip", "list", "--format=json", "--disable-pip-version-check"] : ["-I", "-m", "pip", "install", "--disable-pip-version-check"]);
        return null;
    }

    private bool HasPip(string envPath)
    {
        try
        {
            string lib = System.IO.Path.Combine(envPath, _isWindows ? "Lib" : "lib");
            return Directory.EnumerateDirectories(lib, "site-packages", SearchOption.AllDirectories).Take(4)
                .Any(sp => Directory.Exists(System.IO.Path.Combine(sp, "pip")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // ---------------------------------------------------------------- install

    /// <summary>
    /// Builds the install command without running anything. The caller shows <see cref="PythonInstallPlan.CommandLine"/> and the PyPI notice,
    /// and calls <see cref="InstallAsync"/> only after explicit confirmation.
    /// </summary>
    public async Task<(PythonInstallPlan? Plan, string? Error)> BuildInstallPlanAsync(string envName, PythonInstallRequest request, CancellationToken ct)
    {
        ManagedPythonEnvironment? env = Get(envName);
        if (env is null) return (null, $"Окружение «{envName}» не найдено среди управляемых.");
        if (!File.Exists(env.InterpreterPath)) return (null, "В окружении нет интерпретатора.");

        var tail = new List<string>();
        var names = new List<string>();
        switch (request.Source)
        {
            case InstallSource.Packages:
                if (request.Packages.Count == 0) return (null, "Список пакетов пуст.");
                foreach (string spec in request.Packages)
                {
                    if (!TryPackageName(spec, out string? pname))
                        return (null, $"Недопустимая спецификация пакета «{spec}».");
                    tail.Add(spec.Trim());
                    names.Add(pname!);
                }
                break;

            case InstallSource.RequirementsFile:
                if (request.FilePath is null || !File.Exists(request.FilePath)) return (null, "Файл requirements не найден.");
                tail.AddRange(["-r", System.IO.Path.GetFullPath(request.FilePath)]);
                foreach (string line in File.ReadLines(request.FilePath))
                    if (TryPackageName(line.Split('#')[0].Trim(), out string? rn)) names.Add(rn!);
                break;

            case InstallSource.Pyproject:
                if (request.FilePath is null || !File.Exists(request.FilePath)) return (null, "Файл pyproject.toml не найден.");
                (IReadOnlyList<string>? deps, string? depErr) = await ReadPyprojectDependenciesAsync(env.InterpreterPath, request.FilePath, ct).ConfigureAwait(false);
                if (deps is null) return (null, depErr);
                if (deps.Count == 0) return (null, "В pyproject.toml нет [project].dependencies.");
                foreach (string spec in deps)
                {
                    if (!TryPackageName(spec, out string? pname))
                        return (null, $"Зависимость «{spec}» из pyproject не поддерживается.");
                    tail.Add(spec.Trim());
                    names.Add(pname!);
                }
                break;
        }

        (string tool, List<string> args)? baseCmd = ResolvePackageTool(env.Path, env.InterpreterPath, forList: false);
        if (baseCmd is null) return (null, "Не найден ни pip в окружении, ни uv.");
        var all = new List<string>(baseCmd.Value.args);
        // "--" guards against a spec ever being read as an option; -r keeps its flag form.
        if (request.Source == InstallSource.RequirementsFile) all.AddRange(tail);
        else { all.Add("--"); all.AddRange(tail); }

        string display = DisplayName(baseCmd.Value.tool) + " " + string.Join(' ', all.Select(Quote));
        return (new PythonInstallPlan(baseCmd.Value.tool, all, display, Downloads: true, names, env.Path), null);
    }

    private static readonly Regex PackageNameStart = new(@"^([A-Za-z0-9][A-Za-z0-9._-]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Conservative guard (the full PEP 508 check lives in the Domain): a requirement must begin with a distribution name, so it can never be
    /// an option ("--index-url"), a URL, a local path or a direct reference; and it carries no control characters or environment markers.
    /// </summary>
    internal static bool TryPackageName(string? spec, out string? name)
    {
        name = null;
        if (string.IsNullOrWhiteSpace(spec)) return false;
        string t = spec.Trim();
        if (t.Any(char.IsControl) || t.Contains('@') || t.Contains("://") || t.Contains(';')) return false;
        Match m = PackageNameStart.Match(t);
        if (!m.Success) return false;
        name = m.Groups[1].Value;
        return true;
    }

    private static string DisplayName(string tool) => System.IO.Path.GetFileName(tool) switch { "uv" or "uv.exe" => "uv", _ => tool };

    private static string Quote(string a) => a.Length > 0 && a.All(c => char.IsLetterOrDigit(c) || "-_./:=<>!~,*+@\\".Contains(c)) ? a : "\"" + a.Replace("\"", "\\\"") + "\"";

    private async Task<(IReadOnlyList<string>? Deps, string? Error)> ReadPyprojectDependenciesAsync(string python, string file, CancellationToken ct)
    {
        const string script = "import sys,json\ntry:\n import tomllib\nexcept ImportError:\n print(json.dumps({'error':'tomllib недоступен (нужен Python 3.11+)'})); sys.exit(0)\n" +
            "with open(sys.argv[1],'rb') as f: d=tomllib.load(f)\nprint(json.dumps({'deps':d.get('project',{}).get('dependencies',[])}))";
        ToolOutput o = await _runner.RunAsync(python, ["-I", "-c", script, file], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(15)), ct).ConfigureAwait(false);
        if (!o.Succeeded) return (null, $"Не удалось прочитать pyproject.toml: {o.FailureText}");
        try
        {
            using var doc = JsonDocument.Parse(LastJsonLine(o.Stdout));
            if (doc.RootElement.TryGetProperty("error", out var e)) return (null, e.GetString());
            return (doc.RootElement.GetProperty("deps").EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList(), null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return (null, "Не удалось разобрать pyproject.toml.");
        }
    }

    /// <summary>
    /// Runs a previously built plan. Output is streamed line by line; cancelling kills the process tree. Only call after the user confirmed the
    /// plan. Refuses a plan whose environment is not a managed one, so this can never target the system interpreter.
    /// </summary>
    public async Task<PythonInstallResult> InstallAsync(PythonInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        string full = System.IO.Path.GetFullPath(plan.EnvironmentPath).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        if (!string.Equals(System.IO.Path.GetDirectoryName(full), Root.TrimEnd(System.IO.Path.DirectorySeparatorChar), PathComparison)
            || !File.Exists(System.IO.Path.Combine(full, "pyvenv.cfg")))
            return new PythonInstallResult(false, "Установка отклонена: окружение не управляется приложением.");

        var env = new Dictionary<string, string> { ["VIRTUAL_ENV"] = full, ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1" };
        try
        {
            ToolOutput o = await _runner.RunStreamingAsync(plan.Tool, plan.Arguments,
                new ToolRunOptions(Environment: env, Timeout: TimeSpan.FromMinutes(30), RemoveEnvironment: HostileEnvironment.Where(n => n != "VIRTUAL_ENV").ToList()),
                onLine, ct).ConfigureAwait(false);
            return o.Succeeded ? new PythonInstallResult(true, null) : new PythonInstallResult(false, o.TimedOut ? "Превышено время установки." : $"Установка завершилась с ошибкой (код {o.ExitCode?.ToString() ?? "?"}). {o.StartError}".Trim());
        }
        catch (OperationCanceledException)
        {
            return new PythonInstallResult(false, "Установка отменена.", Cancelled: true);
        }
    }
}

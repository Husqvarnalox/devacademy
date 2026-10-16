using System.Text.Json;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Node;

/// <summary>What may happen to an exercise run given the node content environment. <see cref="BlockedMessage"/> non-null means "do not run".</summary>
public sealed record NodeRunPlan(NodeRunSelection? Node, string? BlockedMessage = null)
{
    public bool IsBlocked => BlockedMessage is not null;
    public static NodeRunPlan Default { get; } = new((NodeRunSelection?)null);
}

/// <summary>Decides whether an exercise that needs npm packages (Vitest, Testing Library...) can run. Reads only; never installs.</summary>
public interface INodeEnvironmentResolver
{
    Task<NodeRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct);
}

public sealed record NodePackageInfo(string Name, string? Version);

public sealed record NodeEnvironmentStatus(string Path, bool Installed, string? NodeVersion, IReadOnlyList<NodePackageInfo> Packages, string? ManifestDirectory)
{
    public bool HasManifest => ManifestDirectory is not null;
}

/// <summary>The exact command of an explicit install; shown to the learner before it runs.</summary>
public sealed record NodeInstallPlan(string Npm, string ManifestDirectory, string EnvironmentPath, IReadOnlyList<string> Args)
{
    public string CommandLine => $"cd {EnvironmentPath} && npm {string.Join(' ', Args)}";
}

public sealed record NodeInstallResult(bool Ok, bool Cancelled, string? Error);

/// <summary>
/// The node content environment: a directory with <c>node_modules</c> installed by <c>npm ci</c> from the committed
/// <c>tools/node-content/package-lock.json</c>. It is the Node counterpart of the Python content venv: exercises declare
/// <c>requires: { node, npmPackages }</c>, the check reads each package's <c>package.json</c> (nothing is imported or run), and installation
/// is an explicit user action that needs the network. At grading time the directory is only exposed read-only (see <see cref="NodeRunSelection"/>).
/// </summary>
public sealed class NodeEnvironmentService(IToolRunner tools, string environmentPath, string? manifestDirectory = null) : INodeEnvironmentResolver
{
    public const string ManifestFile = "package.json";
    public const string LockFile = "package-lock.json";
    private static readonly string[] InstallArgs = ["ci", "--ignore-scripts", "--no-audit", "--no-fund"];

    public string EnvironmentPath { get; } = Path.GetFullPath(environmentPath);
    public string? ManifestDirectory { get; } = manifestDirectory is not null && File.Exists(Path.Combine(manifestDirectory, LockFile)) ? Path.GetFullPath(manifestDirectory) : null;

    public NodeRunSelection Selection => new(EnvironmentPath);

    /// <summary>
    /// Where the pinned manifest lives: <c>node-content</c> next to the executable (published builds), else <c>tools/node-content</c> in a
    /// parent directory (a repository checkout). Null when neither exists.
    /// </summary>
    public static string? FindManifestDirectory(string? startDirectory = null)
    {
        var start = startDirectory ?? AppContext.BaseDirectory;
        var shipped = Path.Combine(start, "node-content");
        if (File.Exists(Path.Combine(shipped, LockFile))) return shipped;
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "node-content");
            if (File.Exists(Path.Combine(candidate, LockFile))) return candidate;
        }
        return null;
    }

    /// <summary>A developer checkout's <c>.node-content</c> (made by <c>make node-env</c>), found next to <c>tools/node-content</c>.</summary>
    public static string? FindRepositoryEnvironment(string? startDirectory = null)
    {
        var manifest = FindManifestDirectory(startDirectory);
        if (manifest is null) return null;
        var root = Directory.GetParent(Directory.GetParent(manifest)?.FullName ?? "")?.FullName;
        var env = root is null ? null : Path.Combine(root, ".node-content");
        return env is not null && Directory.Exists(Path.Combine(env, "node_modules")) ? env : null;
    }

    // ---------------------------------------------------------------- inspection

    public async Task<NodeEnvironmentStatus> InspectAsync(CancellationToken ct)
    {
        var modules = Path.Combine(EnvironmentPath, "node_modules");
        var installed = Directory.Exists(modules);
        var packages = new List<NodePackageInfo>();
        if (installed)
            foreach (var name in DeclaredPackages())
                packages.Add(new NodePackageInfo(name, ReadVersion(name)));
        return new NodeEnvironmentStatus(EnvironmentPath, installed, await NodeVersionAsync(ct).ConfigureAwait(false), packages, ManifestDirectory);
    }

    /// <summary>Top-level packages of the manifest (the environment's own package.json, else the shipped one).</summary>
    private IEnumerable<string> DeclaredPackages()
    {
        foreach (var file in new[] { Path.Combine(EnvironmentPath, ManifestFile), ManifestDirectory is null ? null : Path.Combine(ManifestDirectory, ManifestFile) })
        {
            if (file is null || !File.Exists(file)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var names = new List<string>();
                foreach (var section in new[] { "dependencies", "devDependencies" })
                    if (doc.RootElement.TryGetProperty(section, out var deps) && deps.ValueKind == JsonValueKind.Object)
                        names.AddRange(deps.EnumerateObject().Select(p => p.Name));
                return names.Order(StringComparer.Ordinal).ToList();
            }
            catch (Exception ex) when (ex is JsonException or IOException) { }
        }
        return [];
    }

    private string? ReadVersion(string package)
    {
        var file = Path.Combine(EnvironmentPath, "node_modules", package.Replace('/', Path.DirectorySeparatorChar), ManifestFile);
        try
        {
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
        }
        catch (Exception ex) when (ex is JsonException or IOException) { return null; }
    }

    private async Task<string?> NodeVersionAsync(CancellationToken ct)
    {
        if (tools.Find("node") is not { } node) return null;
        var output = await tools.RunAsync(node, ["--version"], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(10)), ct).ConfigureAwait(false);
        return output.Succeeded ? output.Stdout.Trim().TrimStart('v') : null;
    }

    // ---------------------------------------------------------------- requirement check

    /// <summary>Null when the environment satisfies <paramref name="requires"/>, otherwise a one-line explanation in Russian with the fix.</summary>
    public async Task<string?> CheckAsync(ExerciseRequirements? requires, IReadOnlyList<string> extraPackages, CancellationToken ct)
    {
        var packages = (requires?.Npm ?? []).Concat(extraPackages).Distinct(StringComparer.Ordinal).ToList();
        var missing = new List<string>();
        if (!Directory.Exists(Path.Combine(EnvironmentPath, "node_modules")))
            return $"Для упражнения нужны npm-пакеты ({string.Join(", ", packages)}), но окружение курсов не установлено ({EnvironmentPath}). " + Fix;
        foreach (var package in packages)
            if (ReadVersion(package) is null) missing.Add(package);
        if (missing.Count > 0)
            return $"В окружении курсов нет пакетов: {string.Join(", ", missing)}. " + Fix;
        return await CheckNodeVersionAsync(requires?.Node, ct).ConfigureAwait(false);
    }

    /// <summary>Checks only the installed Node.js against a <c>requires.node</c> specifier; needs no node environment.</summary>
    public async Task<string?> CheckNodeVersionAsync(string? spec, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        if (!PythonSpecifierSet.TryParse(spec, out var set, out _)) return $"Некорректное требование к Node.js: {spec}";
        var version = await NodeVersionAsync(ct).ConfigureAwait(false);
        if (version is null) return "Node.js не найден или не запускается; установите Node.js 22 или новее с https://nodejs.org/.";
        if (!PythonVersion.TryParse(version, out var installed) || !set.Satisfies(installed))
            return $"Нужен Node.js {spec}, а установлен {version}.";
        return null;
    }

    private const string Fix = "Настройки → Среды выполнения → «Установить пакеты для курсов (npm ci, нужна сеть)» (из репозитория: make node-env).";

    public async Task<NodeRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct)
    {
        if (!exercise.HasNodeRequirements) return NodeRunPlan.Default;
        if (!exercise.NeedsNodeEnvironment)
        {
            // Only a Node.js version is required: plain built-in modules, no node_modules to expose.
            var versionProblem = await CheckNodeVersionAsync(exercise.Requires?.Node, ct).ConfigureAwait(false);
            return versionProblem is null ? NodeRunPlan.Default : new NodeRunPlan(null, versionProblem);
        }
        var tool = exercise.Command?.Tool is { } t ? CommandPolicy.FindNodeTool(exercise.LanguageId, t)?.Package : null;
        var problem = await CheckAsync(exercise.Requires, tool is null ? [] : [tool], ct).ConfigureAwait(false);
        return problem is null ? new NodeRunPlan(Selection) : new NodeRunPlan(null, problem);
    }

    // ---------------------------------------------------------------- explicit install

    /// <summary>The install command, or an explanation why it cannot be built. Nothing runs.</summary>
    public (NodeInstallPlan? Plan, string? Error) PlanInstall()
    {
        if (ManifestDirectory is null) return (null, "Не найден каталог node-content с package-lock.json (он поставляется вместе с приложением и лежит в tools/node-content репозитория).");
        if (tools.Find("npm") is not { } npm) return (null, "npm не найден. Установите Node.js 22 или новее (он включает npm) с https://nodejs.org/.");
        return (new NodeInstallPlan(npm, ManifestDirectory, EnvironmentPath, InstallArgs), null);
    }

    /// <summary>Copies the pinned manifest and lock into the environment directory and runs <c>npm ci</c> there. This is the only place that uses the network.</summary>
    public async Task<NodeInstallResult> InstallAsync(NodeInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(plan.EnvironmentPath);
            foreach (var file in new[] { ManifestFile, LockFile })
                File.Copy(Path.Combine(plan.ManifestDirectory, file), Path.Combine(plan.EnvironmentPath, file), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new NodeInstallResult(false, false, $"Не удалось подготовить каталог {plan.EnvironmentPath}: {ex.Message}");
        }
        try
        {
            var options = new ToolRunOptions(plan.EnvironmentPath,
                new Dictionary<string, string> { ["npm_config_update_notifier"] = "false", ["npm_config_progress"] = "false" },
                TimeSpan.FromMinutes(10));
            var result = await tools.RunStreamingAsync(plan.Npm, plan.Args, options, onLine, ct).ConfigureAwait(false);
            return result.Succeeded ? new NodeInstallResult(true, false, null) : new NodeInstallResult(false, false, "npm ci завершился с ошибкой: " + result.FailureText);
        }
        catch (OperationCanceledException)
        {
            return new NodeInstallResult(false, true, null);
        }
    }
}

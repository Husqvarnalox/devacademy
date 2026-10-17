using System.Text.Json;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Node;

/// <summary>
/// What may happen to an exercise run given the node content environment. <see cref="BlockedMessage"/> non-null means "do not run";
/// <see cref="InstallSet"/> is the package set whose (explicit, confirmed) installation would unblock it.
/// </summary>
public sealed record NodeRunPlan(NodeRunSelection? Node, string? BlockedMessage = null, ContentPackageSet? InstallSet = null)
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

/// <summary>One package set in the node environment root: installed or not, with the versions found in its <c>node_modules</c>.</summary>
public sealed record NodeSetStatus(ContentPackageSet Set, string Path, bool Installed, IReadOnlyList<NodePackageInfo> Packages, bool HasManifest);

public sealed record NodeEnvironmentStatus(string Root, string? NodeVersion, IReadOnlyList<NodeSetStatus> Sets);

/// <summary>The exact command of an explicit install of ONE set; shown to the learner (with the pinned packages) before it runs.</summary>
public sealed record NodeInstallPlan(string Npm, ContentPackageSet Set, string ManifestDirectory, string EnvironmentPath, IReadOnlyList<string> Args, IReadOnlyList<PinnedPackage> Packages)
{
    public string CommandLine => $"cd {EnvironmentPath} && npm {string.Join(' ', Args)}";
}

public sealed record NodeInstallResult(bool Ok, bool Cancelled, string? Error);

/// <summary><see cref="Problem"/> null = satisfied; <see cref="Set"/> is the set to install (or repair) when the problem is a missing/incomplete set.</summary>
public sealed record NodeCheck(string? Problem, ContentPackageSet? Set = null);

/// <summary>
/// The node content environment: a root directory with one subdirectory per package set, each holding the <c>node_modules</c> that
/// <c>npm ci</c> installed from that set's committed <c>package-lock.json</c> (<c>tools/content-packages/node/&lt;id&gt;</c>). Exercises declare
/// <c>requires: { node, npmPackages }</c>; the check picks the ONE set that covers them, reads that set's package.json files (nothing is imported or
/// run) and a run mounts only that set's <c>node_modules</c>, read-only (see <see cref="NodeRunSelection"/>). Installing or removing a set is an
/// explicit user action; the install needs the network and nothing else ever downloads a package.
/// </summary>
public sealed class NodeEnvironmentService(IToolRunner tools, string environmentRoot, ContentPackageIndex? index = null) : INodeEnvironmentResolver
{
    public const string ManifestFile = "package.json";
    public const string LockFile = "package-lock.json";
    private static readonly string[] InstallArgs = ["ci", "--ignore-scripts", "--no-audit", "--no-fund"];

    public string EnvironmentRoot { get; } = Path.GetFullPath(environmentRoot);
    public ContentPackageIndex? Index { get; } = index;

    public IReadOnlyList<ContentPackageSet> Sets => Index?.For(PackageSetLanguage.Node).ToList() ?? [];

    public string SetPath(ContentPackageSet set) => Path.Combine(EnvironmentRoot, set.Id);

    public bool IsInstalled(ContentPackageSet set) => Directory.Exists(Path.Combine(SetPath(set), "node_modules"));

    private string? ManifestDirectory(ContentPackageSet set) =>
        Index?.SourcePath(set) is { } dir && File.Exists(Path.Combine(dir, LockFile)) ? dir : null;

    /// <summary>A developer checkout's <c>.node-content</c> (made by <c>make node-env</c>), found next to <c>tools/content-packages</c>.</summary>
    public static string? FindRepositoryEnvironment(string? startDirectory = null)
    {
        var packages = ContentPackageIndex.FindDirectory(startDirectory);
        if (packages is null) return null;
        var root = Directory.GetParent(Directory.GetParent(packages)?.FullName ?? "")?.FullName;
        var env = root is null ? null : Path.Combine(root, ".node-content");
        return env is not null && HasInstalledSet(env) ? env : null;
    }

    /// <summary>True when any subdirectory of <paramref name="root"/> holds a <c>node_modules</c> (some set is installed there).</summary>
    public static bool HasInstalledSet(string root)
    {
        try { return Directory.Exists(root) && Directory.EnumerateDirectories(root).Any(d => Directory.Exists(Path.Combine(d, "node_modules"))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // ---------------------------------------------------------------- inspection

    public async Task<NodeEnvironmentStatus> InspectAsync(CancellationToken ct)
    {
        var sets = new List<NodeSetStatus>();
        foreach (var set in Sets)
        {
            var installed = IsInstalled(set);
            var names = Index!.ReadPins(set).Select(p => p.Name).ToList();
            if (names.Count == 0) names = [.. set.Packages];
            var packages = installed ? names.Select(name => new NodePackageInfo(name, ReadVersion(set, name))).ToList() : [];
            sets.Add(new NodeSetStatus(set, SetPath(set), installed, packages, ManifestDirectory(set) is not null));
        }
        return new NodeEnvironmentStatus(EnvironmentRoot, await NodeVersionAsync(ct).ConfigureAwait(false), sets);
    }

    private string? ReadVersion(ContentPackageSet set, string package)
    {
        var file = Path.Combine(SetPath(set), "node_modules", package.Replace('/', Path.DirectorySeparatorChar), ManifestFile);
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

    private const string Fix = "Установите набор: кнопка в уроке или Настройки → Среды выполнения → Node (нужна сеть; приложение ничего не устанавливает само; из репозитория: make node-env).";

    /// <summary>The set that covers <paramref name="packages"/>, or a one-line explanation in Russian.</summary>
    public (ContentPackageSet? Set, string? Error) ResolveSet(IReadOnlyCollection<string> packages)
    {
        if (Index is not { Sets.Count: > 0 }) return (null, "не найден каталог content-packages с описанием наборов пакетов (он поставляется вместе с приложением и лежит в tools/content-packages репозитория)");
        var resolution = Index.Resolve(PackageSetLanguage.Node, packages);
        return (resolution.Set, resolution.Error);
    }

    /// <summary>Satisfied (<see cref="NodeCheck.Problem"/> null) when the exercise's set is installed with the needed packages and Node.js fits.</summary>
    public async Task<NodeCheck> CheckAsync(ExerciseRequirements? requires, IReadOnlyList<string> extraPackages, CancellationToken ct)
    {
        var packages = (requires?.Npm ?? []).Concat(extraPackages).Distinct(StringComparer.Ordinal).ToList();
        ContentPackageSet? set = null;
        if (packages.Count > 0)
        {
            var (chosen, error) = ResolveSet(packages);
            if (chosen is null) return new NodeCheck($"Для упражнения нужны npm-пакеты ({string.Join(", ", packages)}), но {error}.");
            set = chosen;
            if (!IsInstalled(set))
                return new NodeCheck($"Для упражнения нужен набор пакетов «{set.Title}» ({string.Join(", ", set.Packages)}), он не установлен ({SetPath(set)}). " + Fix, set);
            var missing = packages.Where(p => ReadVersion(set, p) is null).ToList();
            if (missing.Count > 0)
                return new NodeCheck($"В наборе «{set.Title}» нет пакетов: {string.Join(", ", missing)}. " + Fix, set);
        }
        return new NodeCheck(await CheckNodeVersionAsync(requires?.Node, ct).ConfigureAwait(false), set);
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
        var check = await CheckAsync(exercise.Requires, tool is null ? [] : [tool], ct).ConfigureAwait(false);
        return check.Problem is null ? new NodeRunPlan(new NodeRunSelection(SetPath(check.Set!))) : new NodeRunPlan(null, check.Problem, check.Set);
    }

    // ---------------------------------------------------------------- explicit install and removal

    /// <summary>The install command of one set, or an explanation why it cannot be built. Nothing runs.</summary>
    public (NodeInstallPlan? Plan, string? Error) PlanInstall(ContentPackageSet set)
    {
        if (ManifestDirectory(set) is not { } manifest) return (null, $"Не найден каталог content-packages/node/{set.Id} с package-lock.json (он поставляется вместе с приложением и лежит в tools/content-packages репозитория).");
        if (tools.Find("npm") is not { } npm) return (null, "npm не найден. Установите Node.js 22 или новее (он включает npm) с https://nodejs.org/.");
        return (new NodeInstallPlan(npm, set, manifest, SetPath(set), InstallArgs, Index!.ReadPins(set)), null);
    }

    /// <summary>Copies the set's pinned manifest and lock into its directory and runs <c>npm ci</c> there. This is the only place that uses the network.</summary>
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

    /// <summary>Deletes ONE set's directory (its node_modules and manifest copy) and nothing else. The caller confirms first.</summary>
    public NodeInstallResult RemoveSet(ContentPackageSet set)
    {
        var path = SetPath(set);
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            return new NodeInstallResult(true, false, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new NodeInstallResult(false, false, $"Не удалось удалить {path}: {ex.Message}");
        }
    }
}

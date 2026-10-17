using System.Text.RegularExpressions;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Go;

/// <summary>
/// What may happen to an exercise run given the Go content environment. <see cref="BlockedMessage"/> non-null means "do not run";
/// <see cref="InstallSet"/> is the package set whose (explicit, confirmed) installation would unblock it.
/// </summary>
public sealed record GoRunPlan(GoRunSelection? Go, string? BlockedMessage = null, ContentPackageSet? InstallSet = null)
{
    public bool IsBlocked => BlockedMessage is not null;
    public static GoRunPlan Default { get; } = new((GoRunSelection?)null);
}

/// <summary>Decides whether an exercise that needs Go modules (GoMLX...) can run. Reads only; never downloads.</summary>
public interface IGoEnvironmentResolver
{
    Task<GoRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct);
}

public sealed record GoModuleInfo(string Path, string? Version);

/// <summary>One package set in the Go environment root: installed or not, with the module versions found in its module cache.</summary>
public sealed record GoSetStatus(ContentPackageSet Set, string Path, bool Installed, IReadOnlyList<GoModuleInfo> Modules, bool HasManifest);

public sealed record GoEnvironmentStatus(string Root, string? GoVersion, IReadOnlyList<GoSetStatus> Sets);

/// <summary>The exact command of an explicit install of ONE set; shown to the learner (with the pinned modules) before it runs.</summary>
public sealed record GoInstallPlan(string Go, ContentPackageSet Set, string ManifestDirectory, string EnvironmentPath, IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Environment, IReadOnlyList<PinnedPackage> Packages)
{
    public string CommandLine =>
        $"cd {EnvironmentPath} && {string.Join(' ', Environment.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"))} go {string.Join(' ', Args)}";
}

public sealed record GoInstallResult(bool Ok, bool Cancelled, string? Error);

/// <summary><see cref="Problem"/> null = satisfied; <see cref="Set"/> is the set to install (or repair) when the problem is a missing/incomplete set.</summary>
public sealed record GoCheck(string? Problem, ContentPackageSet? Set = null);

/// <summary>
/// The Go content environment: a root directory with one subdirectory per package set, each holding the module cache (<c>pkg/mod</c>) that
/// <c>go mod download</c> filled from that set's committed <c>go.mod</c>/<c>go.sum</c> (<c>tools/content-packages/go/&lt;id&gt;</c>). Exercises declare
/// <c>requires: { go, goModules }</c>; the check picks the ONE set that covers the modules, looks for each module's pinned zip in the cache (nothing is
/// built or run) and a run mounts only that set's module cache, read-only, with <c>GOPROXY=off</c> (see <see cref="GoRunSelection"/>). Installing or
/// removing a set is an explicit user action; the install needs the network and nothing else ever downloads a module.
/// </summary>
public sealed partial class GoEnvironmentService(IToolRunner tools, string environmentRoot, ContentPackageIndex? index = null) : IGoEnvironmentResolver
{
    public const string ModFile = "go.mod";
    public const string SumFile = "go.sum";
    public const string Proxy = "https://proxy.golang.org";
    private static readonly string[] InstallArgs = ["mod", "download"];

    [GeneratedRegex(@"go version go(\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    public string EnvironmentRoot { get; } = Path.GetFullPath(environmentRoot);
    public ContentPackageIndex? Index { get; } = index;

    public IReadOnlyList<ContentPackageSet> Sets => Index?.For(PackageSetLanguage.Go).ToList() ?? [];

    public string SetPath(ContentPackageSet set) => Path.Combine(EnvironmentRoot, set.Id);

    /// <summary>The set's module cache (<c>GOMODCACHE</c>).</summary>
    public string ModCachePath(ContentPackageSet set) => Path.Combine(SetPath(set), "pkg", "mod");

    public bool IsInstalled(ContentPackageSet set) => Directory.Exists(Path.Combine(ModCachePath(set), "cache", "download"));

    private string? ManifestDirectory(ContentPackageSet set) =>
        Index?.SourcePath(set) is { } dir && File.Exists(Path.Combine(dir, ModFile)) && File.Exists(Path.Combine(dir, SumFile)) ? dir : null;

    /// <summary>A developer checkout's <c>.go-content</c> (made by <c>make go-env</c>), found next to <c>tools/content-packages</c>.</summary>
    public static string? FindRepositoryEnvironment(string? startDirectory = null)
    {
        var packages = ContentPackageIndex.FindDirectory(startDirectory);
        if (packages is null) return null;
        var root = Directory.GetParent(Directory.GetParent(packages)?.FullName ?? "")?.FullName;
        var env = root is null ? null : Path.Combine(root, ".go-content");
        return env is not null && HasInstalledSet(env) ? env : null;
    }

    /// <summary>True when any subdirectory of <paramref name="root"/> holds a downloaded module cache (some set is installed there).</summary>
    public static bool HasInstalledSet(string root)
    {
        try { return Directory.Exists(root) && Directory.EnumerateDirectories(root).Any(d => Directory.Exists(Path.Combine(d, "pkg", "mod", "cache", "download"))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // ---------------------------------------------------------------- inspection

    public async Task<GoEnvironmentStatus> InspectAsync(CancellationToken ct)
    {
        var sets = new List<GoSetStatus>();
        foreach (var set in Sets)
        {
            var installed = IsInstalled(set);
            var names = Index!.ReadPins(set).Select(p => p.Name).ToList();
            if (names.Count == 0) names = [.. set.Packages];
            var modules = installed ? names.Select(name => new GoModuleInfo(name, ReadVersion(set, name))).ToList() : [];
            sets.Add(new GoSetStatus(set, SetPath(set), installed, modules, ManifestDirectory(set) is not null));
        }
        return new GoEnvironmentStatus(EnvironmentRoot, await GoVersionAsync(ct).ConfigureAwait(false), sets);
    }

    /// <summary>The version of <paramref name="module"/> found in the set's module cache (its pinned one when downloaded), or null when absent.</summary>
    private string? ReadVersion(ContentPackageSet set, string module)
    {
        var versions = Path.Combine(ModCachePath(set), "cache", "download", EscapeModulePath(module).Replace('/', Path.DirectorySeparatorChar), "@v");
        try
        {
            if (!Directory.Exists(versions)) return null;
            var pinned = Index?.ReadPins(set).FirstOrDefault(p => p.Name == module)?.Version;
            if (pinned is not null) return File.Exists(Path.Combine(versions, pinned + ".zip")) ? pinned : null;
            var zip = Directory.EnumerateFiles(versions, "*.zip").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
            return zip is null ? null : Path.GetFileNameWithoutExtension(zip);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Go's case-encoding of module paths in the cache: an upper-case letter becomes '!' plus its lower-case form.</summary>
    internal static string EscapeModulePath(string path) =>
        string.Concat(path.Select(c => char.IsUpper(c) ? "!" + char.ToLowerInvariant(c) : c.ToString()));

    private async Task<string?> GoVersionAsync(CancellationToken ct)
    {
        if (tools.Find("go") is not { } go) return null;
        var output = await tools.RunAsync(go, ["version"], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(10)), ct).ConfigureAwait(false);
        return output.Succeeded && VersionPattern().Match(output.Stdout) is { Success: true } m ? m.Groups[1].Value : null;
    }

    // ---------------------------------------------------------------- requirement check

    private const string Fix = "Установите набор: кнопка в уроке или Настройки → Среды выполнения → Go (нужна сеть; приложение ничего не скачивает само; из репозитория: make go-env).";

    /// <summary>The set that covers <paramref name="modules"/>, or a one-line explanation in Russian.</summary>
    public (ContentPackageSet? Set, string? Error) ResolveSet(IReadOnlyCollection<string> modules)
    {
        if (Index is not { Sets.Count: > 0 }) return (null, "не найден каталог content-packages с описанием наборов пакетов (он поставляется вместе с приложением и лежит в tools/content-packages репозитория)");
        var resolution = Index.Resolve(PackageSetLanguage.Go, modules);
        return (resolution.Set, resolution.Error);
    }

    /// <summary>Satisfied (<see cref="GoCheck.Problem"/> null) when the exercise's set is installed with the needed modules and Go fits.</summary>
    public async Task<GoCheck> CheckAsync(ExerciseRequirements? requires, CancellationToken ct)
    {
        var modules = (requires?.Modules ?? []).Distinct(StringComparer.Ordinal).ToList();
        ContentPackageSet? set = null;
        if (modules.Count > 0)
        {
            var (chosen, error) = ResolveSet(modules);
            if (chosen is null) return new GoCheck($"Для упражнения нужны модули Go ({string.Join(", ", modules)}), но {error}.");
            set = chosen;
            if (!IsInstalled(set))
                return new GoCheck($"Для упражнения нужен набор пакетов «{set.Title}» ({string.Join(", ", set.Packages)}), он не установлен ({SetPath(set)}). " + Fix, set);
            var missing = modules.Where(m => ReadVersion(set, m) is null).ToList();
            if (missing.Count > 0)
                return new GoCheck($"В наборе «{set.Title}» нет модулей: {string.Join(", ", missing)}. " + Fix, set);
        }
        return new GoCheck(await CheckGoVersionAsync(requires?.Go, ct).ConfigureAwait(false), set);
    }

    /// <summary>Checks only the installed Go toolchain against a <c>requires.go</c> specifier; needs no module cache.</summary>
    public async Task<string?> CheckGoVersionAsync(string? spec, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        if (!PythonSpecifierSet.TryParse(spec, out var set, out _)) return $"Некорректное требование к Go: {spec}";
        var version = await GoVersionAsync(ct).ConfigureAwait(false);
        if (version is null) return "Go не найден или не запускается; установите Go 1.26.5 или новее с https://go.dev/dl/.";
        if (!PythonVersion.TryParse(version, out var installed) || !set.Satisfies(installed))
            return $"Нужен Go {spec}, а установлен {version}.";
        return null;
    }

    public async Task<GoRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct)
    {
        if (!exercise.HasGoRequirements) return GoRunPlan.Default;
        var check = await CheckAsync(exercise.Requires, ct).ConfigureAwait(false);
        if (check.Problem is not null) return new GoRunPlan(null, check.Problem, check.Set);
        return check.Set is null ? GoRunPlan.Default : new GoRunPlan(new GoRunSelection(ModCachePath(check.Set)));
    }

    // ---------------------------------------------------------------- explicit install and removal

    /// <summary>The install command of one set, or an explanation why it cannot be built. Nothing runs.</summary>
    public (GoInstallPlan? Plan, string? Error) PlanInstall(ContentPackageSet set)
    {
        if (ManifestDirectory(set) is not { } manifest) return (null, $"Не найден каталог content-packages/go/{set.Id} с go.mod и go.sum (он поставляется вместе с приложением и лежит в tools/content-packages репозитория).");
        if (tools.Find("go") is not { } go) return (null, "Go не найден. Установите Go 1.26.5 или новее с https://go.dev/dl/.");
        var environment = new Dictionary<string, string>
        {
            ["GOMODCACHE"] = ModCachePath(set),
            ["GOPATH"] = Path.Combine(SetPath(set), "gopath"),
            ["GOFLAGS"] = "-mod=mod",
            ["GOPROXY"] = Proxy,
            ["GOTOOLCHAIN"] = "local",
        };
        return (new GoInstallPlan(go, set, manifest, SetPath(set), InstallArgs, environment, Index!.ReadPins(set)), null);
    }

    /// <summary>Copies the set's pinned go.mod and go.sum into its directory and runs <c>go mod download</c> there. This is the only place that uses the network.</summary>
    public async Task<GoInstallResult> InstallAsync(GoInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(plan.EnvironmentPath);
            foreach (var file in new[] { ModFile, SumFile })
                File.Copy(Path.Combine(plan.ManifestDirectory, file), Path.Combine(plan.EnvironmentPath, file), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GoInstallResult(false, false, $"Не удалось подготовить каталог {plan.EnvironmentPath}: {ex.Message}");
        }
        try
        {
            var options = new ToolRunOptions(plan.EnvironmentPath, plan.Environment, TimeSpan.FromMinutes(10));
            var result = await tools.RunStreamingAsync(plan.Go, plan.Args, options, onLine, ct).ConfigureAwait(false);
            return result.Succeeded ? new GoInstallResult(true, false, null) : new GoInstallResult(false, false, "go mod download завершился с ошибкой: " + result.FailureText);
        }
        catch (OperationCanceledException)
        {
            return new GoInstallResult(false, true, null);
        }
    }

    /// <summary>
    /// Deletes ONE set's directory (module cache, build cache and manifest copy) and nothing else. Go makes its module cache read-only, so the
    /// write bit is restored first (<c>go clean -modcache</c> does the same). The caller confirms first.
    /// </summary>
    public GoInstallResult RemoveSet(ContentPackageSet set)
    {
        var path = SetPath(set);
        try
        {
            if (Directory.Exists(path))
            {
                MakeWritable(path);
                Directory.Delete(path, recursive: true);
            }
            return new GoInstallResult(true, false, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GoInstallResult(false, false, $"Не удалось удалить {path}: {ex.Message}");
        }
    }

    private static void MakeWritable(string root)
    {
        void Fix(string entry)
        {
            if (new FileInfo(entry).LinkTarget is not null) return;
            if (OperatingSystem.IsWindows()) File.SetAttributes(entry, File.GetAttributes(entry) & ~FileAttributes.ReadOnly);
            else File.SetUnixFileMode(entry, File.GetUnixFileMode(entry) | UnixFileMode.UserWrite | UnixFileMode.UserRead | (Directory.Exists(entry) ? UnixFileMode.UserExecute : 0));
        }
        Fix(root);
        foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            Fix(entry);
    }
}

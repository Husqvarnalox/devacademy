using System.Text.RegularExpressions;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.NuGet;

/// <summary>
/// What may happen to an exercise run given the NuGet content environment. <see cref="BlockedMessage"/> non-null means "do not run";
/// <see cref="InstallSet"/> is the package set whose (explicit, confirmed) installation would unblock it.
/// </summary>
public sealed record NuGetRunPlan(NuGetRunSelection? NuGet, string? BlockedMessage = null, ContentPackageSet? InstallSet = null)
{
    public bool IsBlocked => BlockedMessage is not null;
    public static NuGetRunPlan Default { get; } = new((NuGetRunSelection?)null);
}

/// <summary>Decides whether an exercise that needs NuGet packages (ASP.NET Core TestHost...) can run. Reads only; never downloads.</summary>
public interface INuGetEnvironmentResolver
{
    Task<NuGetRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct);
}

public sealed record NuGetPackageInfo(string Id, string? Version);

/// <summary>One package set in the NuGet environment root: installed or not, with the package versions found in its packages folder.</summary>
public sealed record NuGetSetStatus(ContentPackageSet Set, string Path, bool Installed, IReadOnlyList<NuGetPackageInfo> Packages, bool HasManifest);

public sealed record NuGetEnvironmentStatus(string Root, string? DotnetVersion, IReadOnlyList<NuGetSetStatus> Sets);

/// <summary>The exact command of an explicit install of ONE set; shown to the learner (with the pinned packages) before it runs.</summary>
public sealed record NuGetInstallPlan(string Dotnet, ContentPackageSet Set, string ManifestDirectory, string EnvironmentPath, string PackagesPath, IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Environment, IReadOnlyList<PinnedPackage> Packages)
{
    public string CommandLine =>
        $"cd {EnvironmentPath} && {string.Join(' ', Environment.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"))} dotnet {string.Join(' ', Args)}";
}

public sealed record NuGetInstallResult(bool Ok, bool Cancelled, string? Error);

/// <summary><see cref="Problem"/> null = satisfied; <see cref="Set"/> is the set to install (or repair) when the problem is a missing/incomplete set.</summary>
public sealed record NuGetCheck(string? Problem, ContentPackageSet? Set = null);

/// <summary>
/// The NuGet content environment: a root directory with one subdirectory per package set, each holding the packages folder that
/// <c>dotnet restore</c> filled from that set's committed <c>restore.csproj</c> (<c>tools/content-packages/nuget/&lt;id&gt;</c>). Exercises declare
/// <c>requires.nugetPackages</c>; the check picks the ONE set that covers the packages, looks for each package's pinned version in the folder (nothing is
/// built or run) and a run mounts only that set's packages folder, read-only, as the only package source (see <see cref="NuGetRunSelection"/>). Installing
/// or removing a set is an explicit user action; the install needs the network and nothing else ever downloads a package.
/// </summary>
public sealed partial class NuGetEnvironmentService(IToolRunner tools, string environmentRoot, ContentPackageIndex? index = null) : INuGetEnvironmentResolver
{
    public const string RestoreProject = "restore.csproj";
    public const string Source = "https://api.nuget.org/v3/index.json";

    [GeneratedRegex(@"^\s*(\d+\.\d+\.\d+\S*)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    public string EnvironmentRoot { get; } = Path.GetFullPath(environmentRoot);
    public ContentPackageIndex? Index { get; } = index;

    public IReadOnlyList<ContentPackageSet> Sets => Index?.For(PackageSetLanguage.NuGet).ToList() ?? [];

    public string SetPath(ContentPackageSet set) => Path.Combine(EnvironmentRoot, set.Id);

    /// <summary>The set's packages folder (<c>NUGET_PACKAGES</c>).</summary>
    public string PackagesPath(ContentPackageSet set) => Path.Combine(SetPath(set), "packages");

    public bool IsInstalled(ContentPackageSet set) => HasPackages(PackagesPath(set));

    private string? ManifestDirectory(ContentPackageSet set) =>
        Index?.SourcePath(set) is { } dir && File.Exists(Path.Combine(dir, RestoreProject)) ? dir : null;

    /// <summary>A developer checkout's <c>.nuget-content</c> (made by <c>make nuget-env</c>), found next to <c>tools/content-packages</c>.</summary>
    public static string? FindRepositoryEnvironment(string? startDirectory = null)
    {
        var packages = ContentPackageIndex.FindDirectory(startDirectory);
        if (packages is null) return null;
        var root = Directory.GetParent(Directory.GetParent(packages)?.FullName ?? "")?.FullName;
        var env = root is null ? null : Path.Combine(root, ".nuget-content");
        return env is not null && HasInstalledSet(env) ? env : null;
    }

    /// <summary>True when any subdirectory of <paramref name="root"/> holds a restored packages folder (some set is installed there).</summary>
    public static bool HasInstalledSet(string root)
    {
        try { return Directory.Exists(root) && Directory.EnumerateDirectories(root).Any(d => HasPackages(Path.Combine(d, "packages"))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool HasPackages(string packages)
    {
        try { return Directory.Exists(packages) && Directory.EnumerateDirectories(packages).Any(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // ---------------------------------------------------------------- inspection

    public async Task<NuGetEnvironmentStatus> InspectAsync(CancellationToken ct)
    {
        var sets = new List<NuGetSetStatus>();
        foreach (var set in Sets)
        {
            var installed = IsInstalled(set);
            var names = Index!.ReadPins(set).Select(p => p.Name).ToList();
            if (names.Count == 0) names = [.. set.Packages];
            var packages = installed ? names.Select(name => new NuGetPackageInfo(name, ReadVersion(set, name))).ToList() : [];
            sets.Add(new NuGetSetStatus(set, SetPath(set), installed, packages, ManifestDirectory(set) is not null));
        }
        return new NuGetEnvironmentStatus(EnvironmentRoot, await DotnetVersionAsync(ct).ConfigureAwait(false), sets);
    }

    /// <summary>The version of <paramref name="package"/> found in the set's packages folder (its pinned one when restored), or null when absent.</summary>
    private string? ReadVersion(ContentPackageSet set, string package)
    {
        var versions = Path.Combine(PackagesPath(set), package.ToLowerInvariant());
        try
        {
            if (!Directory.Exists(versions)) return null;
            var pinned = Index?.ReadPins(set).FirstOrDefault(p => string.Equals(p.Name, package, StringComparison.OrdinalIgnoreCase))?.Version;
            bool Restored(string version) => File.Exists(Path.Combine(versions, version, ".nupkg.metadata"));
            if (pinned is not null) return Restored(pinned) ? pinned : null;
            return Directory.EnumerateDirectories(versions).Select(Path.GetFileName).Where(v => v is not null && Restored(v)).OrderBy(v => v, StringComparer.Ordinal).FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private async Task<string?> DotnetVersionAsync(CancellationToken ct)
    {
        if (tools.Find("dotnet") is not { } dotnet) return null;
        var output = await tools.RunAsync(dotnet, ["--version"], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(10)), ct).ConfigureAwait(false);
        return output.Succeeded && VersionPattern().Match(output.Stdout) is { Success: true } m ? m.Groups[1].Value : null;
    }

    // ---------------------------------------------------------------- requirement check

    private const string Fix = "Установите набор: кнопка в уроке или Настройки → Среды выполнения → NuGet (нужна сеть; приложение ничего не скачивает само; из репозитория: make nuget-env).";

    /// <summary>The set that covers <paramref name="packages"/>, or a one-line explanation in Russian.</summary>
    public (ContentPackageSet? Set, string? Error) ResolveSet(IReadOnlyCollection<string> packages)
    {
        if (Index is not { Sets.Count: > 0 }) return (null, "не найден каталог content-packages с описанием наборов пакетов (он поставляется вместе с приложением и лежит в tools/content-packages репозитория)");
        var resolution = Index.Resolve(PackageSetLanguage.NuGet, packages);
        return (resolution.Set, resolution.Error);
    }

    /// <summary>Satisfied (<see cref="NuGetCheck.Problem"/> null) when the exercise's set is installed with the needed packages.</summary>
    public NuGetCheck Check(ExerciseRequirements? requires)
    {
        var packages = (requires?.NuGet ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (packages.Count == 0) return new NuGetCheck(null);
        var (set, error) = ResolveSet(packages);
        if (set is null) return new NuGetCheck($"Для упражнения нужны пакеты NuGet ({string.Join(", ", packages)}), но {error}.");
        if (!IsInstalled(set))
            return new NuGetCheck($"Для упражнения нужен набор пакетов «{set.Title}» ({string.Join(", ", set.Packages)}), он не установлен ({SetPath(set)}). " + Fix, set);
        var missing = packages.Where(p => ReadVersion(set, p) is null).ToList();
        return missing.Count > 0
            ? new NuGetCheck($"В наборе «{set.Title}» нет пакетов: {string.Join(", ", missing)}. " + Fix, set)
            : new NuGetCheck(null, set);
    }

    public Task<NuGetRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct)
    {
        if (!exercise.NeedsNuGetEnvironment) return Task.FromResult(NuGetRunPlan.Default);
        var check = Check(exercise.Requires);
        if (check.Problem is not null) return Task.FromResult(new NuGetRunPlan(null, check.Problem, check.Set));
        return Task.FromResult(check.Set is null ? NuGetRunPlan.Default : new NuGetRunPlan(new NuGetRunSelection(PackagesPath(check.Set))));
    }

    // ---------------------------------------------------------------- explicit install and removal

    /// <summary>The install command of one set, or an explanation why it cannot be built. Nothing runs.</summary>
    public (NuGetInstallPlan? Plan, string? Error) PlanInstall(ContentPackageSet set)
    {
        if (ManifestDirectory(set) is not { } manifest) return (null, $"Не найден каталог content-packages/nuget/{set.Id} с restore.csproj (он поставляется вместе с приложением и лежит в tools/content-packages репозитория).");
        if (tools.Find("dotnet") is not { } dotnet) return (null, "dotnet не найден. Установите .NET SDK 10 или новее с https://dotnet.microsoft.com/download.");
        var packages = PackagesPath(set);
        // An explicit --source keeps user and machine NuGet.Config files out of it; the packages folder and the CLI home stay inside the set's directory.
        string[] args = ["restore", RestoreProject, "--packages", packages, "--source", Source];
        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_HOME"] = Path.Combine(SetPath(set), "cli"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(SetPath(set), "http-cache"),
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
        };
        return (new NuGetInstallPlan(dotnet, set, manifest, SetPath(set), packages, args, environment, Index!.ReadPins(set)), null);
    }

    /// <summary>Copies the set's pinned restore.csproj into its directory and runs <c>dotnet restore</c> there. This is the only place that uses the network.</summary>
    public async Task<NuGetInstallResult> InstallAsync(NuGetInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(plan.EnvironmentPath);
            File.Copy(Path.Combine(plan.ManifestDirectory, RestoreProject), Path.Combine(plan.EnvironmentPath, RestoreProject), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new NuGetInstallResult(false, false, $"Не удалось подготовить каталог {plan.EnvironmentPath}: {ex.Message}");
        }
        try
        {
            var options = new ToolRunOptions(plan.EnvironmentPath, plan.Environment, TimeSpan.FromMinutes(10));
            var result = await tools.RunStreamingAsync(plan.Dotnet, plan.Args, options, onLine, ct).ConfigureAwait(false);
            return result.Succeeded ? new NuGetInstallResult(true, false, null) : new NuGetInstallResult(false, false, "dotnet restore завершился с ошибкой: " + result.FailureText);
        }
        catch (OperationCanceledException)
        {
            return new NuGetInstallResult(false, true, null);
        }
    }

    /// <summary>Deletes ONE set's directory (packages folder, CLI home, restore project copy and obj) and nothing else. The caller confirms first.</summary>
    public NuGetInstallResult RemoveSet(ContentPackageSet set)
    {
        var path = SetPath(set);
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            return new NuGetInstallResult(true, false, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new NuGetInstallResult(false, false, $"Не удалось удалить {path}: {ex.Message}");
        }
    }
}

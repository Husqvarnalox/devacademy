using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DevAcademy.Domain;

public enum PackageSetLanguage { Python, Node, Go, NuGet, Maven }

/// <summary>
/// A per-course package set: the pinned packages of ONE library (or a small family) that a learner can choose to install. Sets are the unit of
/// installation, so a machine only ever downloads what the learner explicitly asked to study. <see cref="Packages"/> are the names an exercise may
/// require from this set (the set's pinned files may carry more, such as dependencies).
/// </summary>
public sealed record ContentPackageSet(string Id, string Title, PackageSetLanguage Language, string Description, IReadOnlyList<string> Packages, IReadOnlyList<string>? Platforms = null)
{
    public string Key => $"{Language.ToString().ToLowerInvariant()}/{Id}";

    /// <summary>True when the set can be installed on <paramref name="rid"/>; a set without <see cref="Platforms"/> (<c>platforms</c> in index.json) supports every platform.</summary>
    public bool IsSupportedOn(string rid) => Platforms is null || Platforms.Contains(rid, StringComparer.OrdinalIgnoreCase);

    /// <summary>The Russian explanation shown instead of an Install button; null when the set is supported on <paramref name="rid"/>.</summary>
    public string? UnsupportedReason(string rid) => IsSupportedOn(rid)
        ? null
        : $"Набор пакетов «{Title}» недоступен на этой платформе ({PlatformRid.Describe(rid)}): для неё нет сборок пакетов этого набора.";

    /// <summary>True when every required package name is offered by this set.</summary>
    public bool Covers(IEnumerable<string> required)
    {
        var have = Packages.Select(p => ContentPackageIndex.NormalizeName(Language, p)).ToHashSet(StringComparer.Ordinal);
        return required.All(r => have.Contains(ContentPackageIndex.NormalizeName(Language, r)));
    }
}

/// <summary>A package pinned by a set (<c>name==version</c> or a top-level exact pin in an npm manifest).</summary>
public sealed record PinnedPackage(string Name, string Version);

/// <summary>
/// The outcome of choosing the set for an exercise: exactly one of <see cref="Set"/> and <see cref="Error"/> is set. <see cref="Unsupported"/> is
/// non-null when a set covers the packages but cannot be installed on this platform (then <see cref="Set"/> is null and <see cref="Error"/> says why).
/// </summary>
public sealed record PackageSetResolution(ContentPackageSet? Set, string? Error, ContentPackageSet? Unsupported = null);

/// <summary>The .NET runtime identifier (RID) of the machine, as used in <c>platforms</c> of index.json: osx-arm64, osx-x64, linux-x64, linux-arm64, win-x64, win-arm64.</summary>
public static class PlatformRid
{
    /// <summary>The RID of this machine; <c>unknown</c> for an operating system or architecture that is not mapped.</summary>
    public static string Current { get; } = Map(
        OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsWindows() ? "win" : "",
        RuntimeInformation.OSArchitecture);

    public static string Map(string os, Architecture architecture) => os.Length == 0
        ? "unknown"
        : architecture switch
        {
            Architecture.X64 => os + "-x64",
            Architecture.Arm64 => os + "-arm64",
            _ => "unknown",
        };

    /// <summary>A short human name of a RID for messages, e.g. <c>osx-x64</c> becomes "macOS Intel".</summary>
    public static string Describe(string rid) => rid switch
    {
        "osx-x64" => "macOS Intel",
        "osx-arm64" => "macOS Apple Silicon",
        "linux-x64" => "Linux x64",
        "linux-arm64" => "Linux arm64",
        "win-x64" => "Windows x64",
        "win-arm64" => "Windows arm64",
        _ => rid,
    };
}

/// <summary>
/// The shipped <c>content-packages/index.json</c> (a repository checkout keeps it in <c>tools/content-packages</c>) and the choice of a set for an
/// exercise: one set must cover all the packages the exercise needs; the smallest covering set wins, ties go to the earlier index entry.
/// Reading is all this does; nothing is installed here.
/// </summary>
public sealed partial class ContentPackageIndex
{
    public const string IndexFile = "index.json";
    /// <summary>The RIDs a set's <c>platforms</c> may list.</summary>
    public static readonly IReadOnlyList<string> KnownRids = ["osx-arm64", "osx-x64", "linux-x64", "linux-arm64", "win-x64", "win-arm64"];

    public const string ShippedDirectoryName = "content-packages";

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    // `\tgithub.com/gomlx/gomlx v0.28.8` inside a go.mod require block, or `require path version`; indirect requirements are not top-level pins.
    [GeneratedRegex(@"^\s*(?:require\s+)?([A-Za-z0-9][A-Za-z0-9._~/-]*\.[A-Za-z0-9._~/-]+)\s+(v[0-9][^\s]*)\s*(//.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex GoRequirePattern();

    // `<PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.10" />` in a nuget set's restore.csproj (exact pins only).
    [GeneratedRegex(@"<PackageReference\s+Include=""([A-Za-z0-9][A-Za-z0-9._-]*)""\s+Version=""([0-9][^""\s]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex PackageReferencePattern();

    [GeneratedRegex(@"^\s*([A-Za-z0-9][A-Za-z0-9._-]*)\s*==\s*([^\s;#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex PinPattern();

    public ContentPackageIndex(IReadOnlyList<ContentPackageSet> sets, string? directory = null, string? rid = null)
    {
        Sets = sets;
        Rid = rid ?? PlatformRid.Current;
        Directory = directory is null ? null : Path.GetFullPath(directory);
    }

    public IReadOnlyList<ContentPackageSet> Sets { get; }

    /// <summary>The platform (RID) the sets are offered for; this machine unless an index is built for tests.</summary>
    public string Rid { get; }

    /// <summary>The directory that holds <c>index.json</c> and the manifests; null for an index built in memory.</summary>
    public string? Directory { get; }

    public IEnumerable<ContentPackageSet> For(PackageSetLanguage language) => Sets.Where(s => s.Language == language);

    public ContentPackageSet? Find(PackageSetLanguage language, string id) =>
        Sets.FirstOrDefault(s => s.Language == language && string.Equals(s.Id, id, StringComparison.Ordinal));

    public static string NormalizeName(PackageSetLanguage language, string name) =>
        language switch
        {
            PackageSetLanguage.Python => PythonPackageSpec.Normalize(name),
            PackageSetLanguage.NuGet => name.ToLowerInvariant(), // NuGet package ids are case-insensitive
            // Maven coordinates (group:artifact) are case-sensitive on Maven Central.
            _ => name,
        };

    public static string Describe(PackageSetLanguage language) => language switch
    {
        PackageSetLanguage.Python => "Python",
        PackageSetLanguage.Go => "Go",
        PackageSetLanguage.NuGet => ".NET (NuGet)",
        PackageSetLanguage.Maven => "Java (Maven)",
        _ => "Node.js",
    };

    // ---------------------------------------------------------------- resolution

    /// <summary>The npm packages an exercise needs: <c>requires.npmPackages</c> plus the package of its command tool. Empty = no node set needed.</summary>
    public static IReadOnlyList<string> RequiredNodePackages(Exercise exercise)
    {
        if (!exercise.NeedsNodeEnvironment) return [];
        var names = new List<string>(exercise.Requires?.Npm ?? []);
        if (exercise.Command?.Tool is { } tool && CommandPolicy.FindNodeTool(exercise.LanguageId, tool)?.Package is { } package) names.Add(package);
        return names.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>The Go module paths an exercise needs (<c>requires.goModules</c>). Empty = no go set needed.</summary>
    public static IReadOnlyList<string> RequiredGoModules(Exercise exercise) =>
        (exercise.Requires?.GoModules ?? []).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The NuGet package ids an exercise needs (<c>requires.nugetPackages</c>). Empty = no nuget set needed.</summary>
    public static IReadOnlyList<string> RequiredNuGetPackages(Exercise exercise) =>
        (exercise.Requires?.NugetPackages ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The Maven artifacts (<c>group:artifact</c>) an exercise needs (<c>requires.mavenArtifacts</c>). Empty = no maven set needed.</summary>
    public static IReadOnlyList<string> RequiredMavenArtifacts(Exercise exercise) =>
        (exercise.Requires?.MavenArtifacts ?? []).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The PyPI distributions an exercise needs (names only, normalised). Empty = no python set needed.</summary>
    public static IReadOnlyList<string> RequiredPythonPackages(Exercise exercise)
    {
        var names = new List<string>();
        foreach (var text in exercise.Requires?.Packages ?? [])
            if (PythonPackageSpec.TryParse(text, out var spec, out _)) names.Add(spec.NormalizedName);
        return names.Distinct(StringComparer.Ordinal).ToList();
    }

    public PackageSetResolution Resolve(PackageSetLanguage language, IReadOnlyCollection<string> required)
    {
        if (required.Count == 0) return new(null, "нет требуемых пакетов");
        ContentPackageSet? best = null, unsupported = null;
        foreach (var set in For(language))
        {
            if (!set.Covers(required)) continue;
            if (!set.IsSupportedOn(Rid)) { if (unsupported is null || set.Packages.Count < unsupported.Packages.Count) unsupported = set; }
            else if (best is null || set.Packages.Count < best.Packages.Count) best = set;
        }
        if (best is null && unsupported is not null)
            return new(null, unsupported.UnsupportedReason(Rid), unsupported);
        return best is not null
            ? new(best, null)
            : new(null, $"ни один набор пакетов не покрывает {string.Join(", ", required)} одновременно (наборы {Describe(language)} в {IndexFile})");
    }

    /// <summary>
    /// The content-authoring question "does some shipped set offer all of these packages?", independent of the platform the check runs on:
    /// a set covers a requirement wherever it is supported. Whether it can be installed here is a runtime concern, answered by <see cref="Resolve"/>.
    /// </summary>
    public string? CoverageError(PackageSetLanguage language, IReadOnlyCollection<string> required) =>
        required.Count == 0 || For(language).Any(set => set.Covers(required))
            ? null
            : $"ни один набор пакетов не покрывает {string.Join(", ", required)} одновременно (наборы {Describe(language)} в {IndexFile})";

    // ---------------------------------------------------------------- files

    /// <summary>The set's npm directory (package.json + package-lock.json), Go module directory (go.mod + go.sum), NuGet restore directory (restore.csproj), Maven set directory (lock.json) or Python requirements file; null when the index is in memory.</summary>
    public string? SourcePath(ContentPackageSet set) => Directory is null ? null : set.Language switch
    {
        PackageSetLanguage.Node => Path.Combine(Directory, "node", set.Id),
        PackageSetLanguage.Go => Path.Combine(Directory, "go", set.Id),
        PackageSetLanguage.NuGet => Path.Combine(Directory, "nuget", set.Id),
        PackageSetLanguage.Maven => Path.Combine(Directory, "maven", set.Id),
        _ => Path.Combine(Directory, "python", set.Id + ".txt"),
    };

    /// <summary>The pinned top-level packages of a set, read from its manifest; empty when unreadable.</summary>
    public IReadOnlyList<PinnedPackage> ReadPins(ContentPackageSet set)
    {
        if (SourcePath(set) is not { } path) return [];
        var pins = new List<PinnedPackage>();
        try
        {
            if (set.Language == PackageSetLanguage.Python)
            {
                foreach (var line in File.ReadLines(path))
                    if (PinPattern().Match(line) is { Success: true } m) pins.Add(new(m.Groups[1].Value, m.Groups[2].Value));
            }
            else if (set.Language == PackageSetLanguage.Go)
            {
                foreach (var line in File.ReadLines(Path.Combine(path, "go.mod")))
                    if (GoRequirePattern().Match(line) is { Success: true } m && !m.Groups[3].Value.Contains("indirect", StringComparison.Ordinal))
                        pins.Add(new(m.Groups[1].Value, m.Groups[2].Value));
            }
            else if (set.Language == PackageSetLanguage.Maven)
            {
                // The artifacts a set offers (not every transitive jar), each with its locked version.
                if (MavenLock.TryParse(File.ReadAllText(Path.Combine(path, MavenLock.FileName)), out var lockFile, out _))
                    foreach (var jar in lockFile.Jars.Where(j => set.Packages.Contains(j.Coordinates, StringComparer.Ordinal)))
                        pins.Add(new(jar.Coordinates, jar.Version));
            }
            else if (set.Language == PackageSetLanguage.NuGet)
            {
                foreach (var line in File.ReadLines(Path.Combine(path, "restore.csproj")))
                    if (PackageReferencePattern().Match(line) is { Success: true } m) pins.Add(new(m.Groups[1].Value, m.Groups[2].Value));
            }
            else
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "package.json")));
                foreach (var section in new[] { "dependencies", "devDependencies" })
                    if (doc.RootElement.TryGetProperty(section, out var deps) && deps.ValueKind == JsonValueKind.Object)
                        pins.AddRange(deps.EnumerateObject().Select(p => new PinnedPackage(p.Name, p.Value.GetString() ?? "")));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return []; }
        return pins.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
    }

    // ---------------------------------------------------------------- loading

    /// <summary>
    /// Where the index lives: <c>content-packages</c> next to the executable (published builds), else <c>tools/content-packages</c> in a parent
    /// directory (a repository checkout). Null when neither exists.
    /// </summary>
    public static string? FindDirectory(string? startDirectory = null)
    {
        var start = startDirectory ?? AppContext.BaseDirectory;
        var shipped = Path.Combine(start, ShippedDirectoryName);
        if (File.Exists(Path.Combine(shipped, IndexFile))) return shipped;
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tools", ShippedDirectoryName);
            if (File.Exists(Path.Combine(candidate, IndexFile))) return candidate;
        }
        return null;
    }

    /// <summary>Finds and loads the index; null when it is missing or malformed.</summary>
    public static ContentPackageIndex? TryLoadDefault(string? startDirectory = null, string? rid = null) =>
        FindDirectory(startDirectory) is { } dir && TryLoad(dir, out var index, out _, rid) ? index : null;

    public static bool TryLoad(string directory, out ContentPackageIndex index, out string? error, string? rid = null)
    {
        index = new ContentPackageIndex([], directory, rid);
        try
        {
            return TryParse(File.ReadAllText(Path.Combine(directory, IndexFile)), directory, out index, out error, rid);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryParse(string json, string? directory, out ContentPackageIndex index, out string? error, string? rid = null)
    {
        index = new ContentPackageIndex([], directory, rid);
        var sets = new List<ContentPackageSet>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) { error = "index.json must be a list of sets"; return false; }
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string? Str(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var id = Str("id");
                if (id is null || !IdPattern().IsMatch(id)) { error = $"set id '{id}' must be lower-case letters, digits and '-'"; return false; }
                var language = Str("language") switch { "python" => PackageSetLanguage.Python, "node" => PackageSetLanguage.Node, "go" => PackageSetLanguage.Go, "nuget" => PackageSetLanguage.NuGet, "maven" => PackageSetLanguage.Maven, _ => (PackageSetLanguage?)null };
                if (language is null) { error = $"set '{id}': language must be 'python', 'node', 'go', 'nuget' or 'maven'"; return false; }
                if (string.IsNullOrWhiteSpace(Str("title"))) { error = $"set '{id}': title is missing"; return false; }
                var packages = new List<string>();
                if (item.TryGetProperty("packages", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    packages.AddRange(arr.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!));
                if (packages.Count == 0) { error = $"set '{id}': packages is empty"; return false; }
                IReadOnlyList<string>? platforms = null;
                if (item.TryGetProperty("platforms", out var plat))
                {
                    if (plat.ValueKind != JsonValueKind.Array || plat.GetArrayLength() == 0 || plat.EnumerateArray().Any(p => p.ValueKind != JsonValueKind.String || !KnownRids.Contains(p.GetString()!)))
                    { error = $"set '{id}': platforms must be a non-empty list of {string.Join(", ", KnownRids)}"; return false; }
                    platforms = plat.EnumerateArray().Select(p => p.GetString()!).Distinct(StringComparer.Ordinal).ToList();
                }
                if (sets.Any(s => s.Language == language && s.Id == id)) { error = $"duplicate set '{language.ToString()!.ToLowerInvariant()}/{id}'"; return false; }
                sets.Add(new ContentPackageSet(id, Str("title")!, language.Value, Str("description") ?? "", packages, platforms));
            }
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
        index = new ContentPackageIndex(sets, directory, rid);
        error = null;
        return true;
    }
}

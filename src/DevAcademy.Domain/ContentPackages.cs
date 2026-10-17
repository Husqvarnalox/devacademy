using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevAcademy.Domain;

public enum PackageSetLanguage { Python, Node }

/// <summary>
/// A per-course package set: the pinned packages of ONE library (or a small family) that a learner can choose to install. Sets are the unit of
/// installation, so a machine only ever downloads what the learner explicitly asked to study. <see cref="Packages"/> are the names an exercise may
/// require from this set (the set's pinned files may carry more, such as dependencies).
/// </summary>
public sealed record ContentPackageSet(string Id, string Title, PackageSetLanguage Language, string Description, IReadOnlyList<string> Packages)
{
    public string Key => $"{Language.ToString().ToLowerInvariant()}/{Id}";

    /// <summary>True when every required package name is offered by this set.</summary>
    public bool Covers(IEnumerable<string> required)
    {
        var have = Packages.Select(p => ContentPackageIndex.NormalizeName(Language, p)).ToHashSet(StringComparer.Ordinal);
        return required.All(r => have.Contains(ContentPackageIndex.NormalizeName(Language, r)));
    }
}

/// <summary>A package pinned by a set (<c>name==version</c> or a top-level exact pin in an npm manifest).</summary>
public sealed record PinnedPackage(string Name, string Version);

/// <summary>The outcome of choosing the set for an exercise: exactly one of <see cref="Set"/> and <see cref="Error"/> is set.</summary>
public sealed record PackageSetResolution(ContentPackageSet? Set, string? Error);

/// <summary>
/// The shipped <c>content-packages/index.json</c> (a repository checkout keeps it in <c>tools/content-packages</c>) and the choice of a set for an
/// exercise: one set must cover all the packages the exercise needs; the smallest covering set wins, ties go to the earlier index entry.
/// Reading is all this does; nothing is installed here.
/// </summary>
public sealed partial class ContentPackageIndex
{
    public const string IndexFile = "index.json";
    public const string ShippedDirectoryName = "content-packages";

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^\s*([A-Za-z0-9][A-Za-z0-9._-]*)\s*==\s*([^\s;#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex PinPattern();

    public ContentPackageIndex(IReadOnlyList<ContentPackageSet> sets, string? directory = null)
    {
        Sets = sets;
        Directory = directory is null ? null : Path.GetFullPath(directory);
    }

    public IReadOnlyList<ContentPackageSet> Sets { get; }

    /// <summary>The directory that holds <c>index.json</c> and the manifests; null for an index built in memory.</summary>
    public string? Directory { get; }

    public IEnumerable<ContentPackageSet> For(PackageSetLanguage language) => Sets.Where(s => s.Language == language);

    public ContentPackageSet? Find(PackageSetLanguage language, string id) =>
        Sets.FirstOrDefault(s => s.Language == language && string.Equals(s.Id, id, StringComparison.Ordinal));

    public static string NormalizeName(PackageSetLanguage language, string name) =>
        language == PackageSetLanguage.Python ? PythonPackageSpec.Normalize(name) : name;

    // ---------------------------------------------------------------- resolution

    /// <summary>The npm packages an exercise needs: <c>requires.npmPackages</c> plus the package of its command tool. Empty = no node set needed.</summary>
    public static IReadOnlyList<string> RequiredNodePackages(Exercise exercise)
    {
        if (!exercise.NeedsNodeEnvironment) return [];
        var names = new List<string>(exercise.Requires?.Npm ?? []);
        if (exercise.Command?.Tool is { } tool && CommandPolicy.FindNodeTool(exercise.LanguageId, tool)?.Package is { } package) names.Add(package);
        return names.Distinct(StringComparer.Ordinal).ToList();
    }

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
        ContentPackageSet? best = null;
        foreach (var set in For(language))
            if (set.Covers(required) && (best is null || set.Packages.Count < best.Packages.Count)) best = set;
        return best is not null
            ? new(best, null)
            : new(null, $"ни один набор пакетов не покрывает {string.Join(", ", required)} одновременно (наборы {(language == PackageSetLanguage.Python ? "Python" : "Node.js")} в {IndexFile})");
    }

    // ---------------------------------------------------------------- files

    /// <summary>The set's npm directory (package.json + package-lock.json) or Python requirements file; null when the index is in memory.</summary>
    public string? SourcePath(ContentPackageSet set) => Directory is null ? null
        : set.Language == PackageSetLanguage.Node ? Path.Combine(Directory, "node", set.Id) : Path.Combine(Directory, "python", set.Id + ".txt");

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
    public static ContentPackageIndex? TryLoadDefault(string? startDirectory = null) =>
        FindDirectory(startDirectory) is { } dir && TryLoad(dir, out var index, out _) ? index : null;

    public static bool TryLoad(string directory, out ContentPackageIndex index, out string? error)
    {
        index = new ContentPackageIndex([], directory);
        try
        {
            return TryParse(File.ReadAllText(Path.Combine(directory, IndexFile)), directory, out index, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryParse(string json, string? directory, out ContentPackageIndex index, out string? error)
    {
        index = new ContentPackageIndex([], directory);
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
                var language = Str("language") switch { "python" => PackageSetLanguage.Python, "node" => PackageSetLanguage.Node, _ => (PackageSetLanguage?)null };
                if (language is null) { error = $"set '{id}': language must be 'python' or 'node'"; return false; }
                if (string.IsNullOrWhiteSpace(Str("title"))) { error = $"set '{id}': title is missing"; return false; }
                var packages = new List<string>();
                if (item.TryGetProperty("packages", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    packages.AddRange(arr.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!));
                if (packages.Count == 0) { error = $"set '{id}': packages is empty"; return false; }
                if (sets.Any(s => s.Language == language && s.Id == id)) { error = $"duplicate set '{language.ToString()!.ToLowerInvariant()}/{id}'"; return false; }
                sets.Add(new ContentPackageSet(id, Str("title")!, language.Value, Str("description") ?? "", packages));
            }
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
        index = new ContentPackageIndex(sets, directory);
        error = null;
        return true;
    }
}

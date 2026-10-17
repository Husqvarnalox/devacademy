using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevAcademy.Domain;

/// <summary>One jar of a maven package set's runtime classpath, pinned by version and sha256.</summary>
public sealed record MavenJar(string GroupId, string ArtifactId, string Version, string File, string Sha256, long Size)
{
    /// <summary><c>group:artifact</c>, the name an exercise requires.</summary>
    public string Coordinates => $"{GroupId}:{ArtifactId}";

    /// <summary>Path of the jar below the repository root: <c>group/path/artifact/version/file</c>.</summary>
    public string RepositoryPath => $"{GroupId.Replace('.', '/')}/{ArtifactId}/{Version}/{File}";
}

/// <summary>
/// The committed <c>tools/content-packages/maven/&lt;set&gt;/lock.json</c>: every jar of the set's resolved runtime classpath with its sha256 and size.
/// The app never resolves anything: it downloads exactly these jars from <see cref="Source"/> and refuses any file whose hash differs.
/// </summary>
public sealed partial record MavenLock(string Set, string Source, IReadOnlyList<MavenJar> Jars)
{
    public const string FileName = "lock.json";

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_-]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex GroupPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    public long TotalBytes => Jars.Sum(j => j.Size);

    /// <summary>Host of <see cref="Source"/> (shown to the learner before anything is downloaded).</summary>
    public string Host => new Uri(Source).Host;

    /// <summary>Download address of one jar.</summary>
    public Uri UrlOf(MavenJar jar) => new($"{Source.TrimEnd('/')}/{jar.RepositoryPath}");

    public static bool TryParse(string json, out MavenLock lockFile, out string? error)
    {
        lockFile = new MavenLock("", "", []);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "lock.json must be an object"; return false; }
            string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var set = Str(root, "set");
            var source = Str(root, "source");
            if (string.IsNullOrWhiteSpace(set)) { error = "lock.json: 'set' is missing"; return false; }
            if (source is null || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            { error = "lock.json: 'source' must be an https URL"; return false; }
            if (!root.TryGetProperty("jars", out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            { error = "lock.json: 'jars' must be a non-empty list"; return false; }
            var jars = new List<MavenJar>();
            var files = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in arr.EnumerateArray())
            {
                var group = Str(item, "groupId");
                var artifact = Str(item, "artifactId");
                var version = Str(item, "version");
                var file = Str(item, "file");
                var sha = Str(item, "sha256");
                var size = item.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number && sz.TryGetInt64(out var n) ? n : -1;
                if (group is null || !GroupPattern().IsMatch(group)) { error = $"lock.json: bad groupId '{group}'"; return false; }
                if (artifact is null || !NamePattern().IsMatch(artifact)) { error = $"lock.json: bad artifactId '{artifact}'"; return false; }
                if (version is null || !NamePattern().IsMatch(version)) { error = $"lock.json: bad version '{version}' for {group}:{artifact}"; return false; }
                if (file != $"{artifact}-{version}.jar") { error = $"lock.json: file '{file}' of {group}:{artifact} must be '{artifact}-{version}.jar'"; return false; }
                if (sha is null || !Sha256Pattern().IsMatch(sha)) { error = $"lock.json: sha256 of {file} must be 64 lower-case hex digits"; return false; }
                if (size <= 0) { error = $"lock.json: size of {file} must be positive"; return false; }
                if (!files.Add(file)) { error = $"lock.json: {file} is listed twice"; return false; }
                jars.Add(new MavenJar(group, artifact, version, file, sha, size));
            }
            lockFile = new MavenLock(set, source, jars);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Reads <c>lock.json</c> from a set's source directory; false (with the reason) when missing or malformed.</summary>
    public static bool TryLoad(string setDirectory, out MavenLock lockFile, out string? error)
    {
        lockFile = new MavenLock("", "", []);
        try { return TryParse(File.ReadAllText(Path.Combine(setDirectory, FileName)), out lockFile, out error); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = ex.Message; return false; }
    }
}

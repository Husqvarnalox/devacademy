using System.Text.Json;

namespace DevAcademy.Application.Workspace;

/// <summary>A project directory the user opened before. Only the path and a timestamp are stored, never file contents.</summary>
public sealed record RecentProject(string Path, DateTimeOffset LastOpenedAt)
{
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) is { Length: > 0 } n ? n : Path;
}

public interface IRecentProjectsRepository
{
    Task<IReadOnlyList<RecentProject>> GetRecentAsync(int limit, CancellationToken ct = default);
    /// <summary>Adds the path or moves it to the top.</summary>
    Task TouchAsync(string path, DateTimeOffset when, CancellationToken ct = default);
    Task RemoveAsync(string path, CancellationToken ct = default);
}

/// <summary>
/// Editor layout restored when a project is reopened: relative file paths per group, the active tab of each group and the selected Python
/// environment. Paths only; text never leaves the files themselves.
/// </summary>
public sealed record ProjectSession(
    IReadOnlyList<IReadOnlyList<string>> Groups,
    IReadOnlyList<string?> ActiveTabs,
    int ActiveGroup = 0,
    string? PythonEnvironment = null)
{
    public static ProjectSession Empty { get; } = new([[]], [null]);

    public string ToJson() => JsonSerializer.Serialize(this);

    public static ProjectSession FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            var s = JsonSerializer.Deserialize<ProjectSession>(json);
            return s is { Groups.Count: > 0 } ? s : Empty;
        }
        catch (JsonException) { return Empty; }
    }
}

public interface IProjectSessionStore
{
    Task<ProjectSession> LoadAsync(string projectPath, CancellationToken ct = default);
    Task SaveAsync(string projectPath, ProjectSession session, CancellationToken ct = default);
}

/// <summary>The commands the Run and Test buttons type into a terminal. They are shown as visible text; nothing runs behind the user's back.</summary>
public sealed record RunProfile(string LanguageId, string RunCommand, string TestCommand);

public static class ProjectRunProfiles
{
    /// <summary>Project language from marker files; null when nothing is recognised.</summary>
    public static string? DetectLanguage(string projectDir)
    {
        if (!Directory.Exists(projectDir)) return null;
        bool Has(string n) => File.Exists(System.IO.Path.Combine(projectDir, n));
        if (Has("go.mod")) return "go";
        if (Has("Cargo.toml")) return "rust";
        if (Has("package.json")) return "javascript";
        if (Has("pyproject.toml") || Has("requirements.txt") || Has("main.py") || Has("setup.py")) return "python";
        try
        {
            if (Directory.EnumerateFiles(projectDir, "*.py").Any()) return "python";
            if (Directory.EnumerateFiles(projectDir, "*.go").Any()) return "go";
            if (Directory.EnumerateFiles(projectDir, "*.js").Any()) return "javascript";
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    /// <param name="pythonExecutable">Interpreter of the selected environment; null means plain <c>python</c> from PATH.</param>
    public static RunProfile? For(string? languageId, string? pythonExecutable = null)
    {
        switch (languageId)
        {
            case "python":
                var py = pythonExecutable is null ? "python" : Quote(pythonExecutable);
                return new RunProfile("python", $"{py} main.py", $"{py} -m pytest");
            case "javascript":
            case "node":
                return new RunProfile("javascript", "node main.js", "node --test");
            case "go":
                return new RunProfile("go", "go run .", "go test ./...");
            case "rust":
                return new RunProfile("rust", "cargo run", "cargo test");
            default:
                return null;
        }
    }

    /// <summary>Quotes a path for a POSIX shell or Windows shells only when it contains characters that need it.</summary>
    public static string Quote(string value)
    {
        if (value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || c is '/' or '.' or '_' or '-' or ':' or '\\' or '+' or '@' or '~'))
            return value;
        if (OperatingSystem.IsWindows()) return "& \"" + value.Replace("\"", "\"\"") + "\"";
        return "'" + value.Replace("'", "'\\''") + "'";
    }
}

namespace DevAcademy.Infrastructure;

/// <summary>Per-OS application data location. Override with the constructor argument or DEVACADEMY_DATA_DIR.</summary>
public sealed class AppPaths
{
    public const string DataDirVariable = "DEVACADEMY_DATA_DIR";

    public AppPaths(string? dataDir = null)
    {
        var env = Environment.GetEnvironmentVariable(DataDirVariable);
        DataDir = Path.GetFullPath(dataDir ?? (string.IsNullOrWhiteSpace(env) ? DefaultDataDir() : env));
    }

    public string DataDir { get; }
    public string DatabasePath => Path.Combine(DataDir, "devacademy.db");
    public string BackupsDir => Path.Combine(DataDir, "backups");
    /// <summary>Managed Python virtual environments: <c>python-envs/&lt;name&gt;</c>.</summary>
    public string PythonEnvsDir => Path.Combine(DataDir, "python-envs");
    /// <summary>The node content environment (<c>package.json</c>, lock and <c>node_modules</c> for React/Vitest exercises), created only by an explicit install.</summary>
    public string NodeContentDir => Path.Combine(DataDir, "node-content");
    public string GoContentDir => Path.Combine(DataDir, "go-content");
    public string NuGetContentDir => Path.Combine(DataDir, "nuget-content");
    public string MavenContentDir => Path.Combine(DataDir, "maven-content");

    private static string DefaultDataDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS()) return Path.Combine(home, "Library", "Application Support", "DevAcademy");
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetEnvironmentVariable("APPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevAcademy");
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(string.IsNullOrWhiteSpace(xdg) ? Path.Combine(home, ".local", "share") : xdg, "devacademy");
    }
}

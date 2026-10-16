namespace DevAcademy.Execution;

public sealed record ExecutionOptions
{
    /// <summary>Maximum number of simultaneously executing processes (compile steps and program runs) across the runner.</summary>
    public int MaxConcurrentRuns { get; init; } = 2;

    /// <summary>Persistent build caches (GOCACHE, GOPATH, DOTNET_CLI_HOME, NuGet). Writable by learner code under OsSandbox.</summary>
    public string CacheDirectory { get; init; } = DefaultCacheDirectory();

    /// <summary>When the requested isolation is unavailable, run with the next weaker one and say so in the result instead of failing.</summary>
    public bool AllowIsolationFallback { get; init; } = true;

    /// <summary>Language id to Docker image, used by IsolationLevel.Container. Languages missing here do not support Container.</summary>
    public IReadOnlyDictionary<string, string> DockerImages { get; init; } = new Dictionary<string, string>
    {
        ["python"] = "python:3.13-slim",
        ["javascript"] = "node:22-slim",
    };

    public static string DefaultCacheDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string cacheRoot;
        if (OperatingSystem.IsWindows())
            cacheRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        else if (OperatingSystem.IsMacOS())
            cacheRoot = Path.Combine(home, "Library", "Caches");
        else
            cacheRoot = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(home, ".cache");
        return Path.Combine(cacheRoot, "DevAcademy", "runner-cache");
    }
}

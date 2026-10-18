using System.Text;

namespace DevAcademy.LanguageServices.Servers;

public enum ServerSource { UserOverride, RunnerCache, System }

/// <summary>A command that can be started: executable (absolute path) + arguments.</summary>
public sealed record ResolvedServer(LanguageServerDefinition Definition, string Executable, IReadOnlyList<string> Args, ServerSource Source);

/// <summary>
/// Finds language server executables: the user's override, the runner cache (<c>language-servers/node_modules/.bin</c>, where
/// explicitly requested npm installs go), then PATH plus the directories GUI apps lack (Homebrew, ~/go/bin, ~/.cargo/bin ...).
/// </summary>
public sealed class ServerLocator
{
    private readonly string? _cacheDirectory;
    private readonly IReadOnlyList<string>? _dirsOverride;

    /// <param name="cacheDirectory">Runner cache root (ExecutionOptions.CacheDirectory).</param>
    /// <param name="searchDirsOverride">Tests: replaces the PATH-derived directories.</param>
    public ServerLocator(string? cacheDirectory, IReadOnlyList<string>? searchDirsOverride = null)
    {
        _cacheDirectory = cacheDirectory;
        _dirsOverride = searchDirsOverride;
    }

    public static string InstallDirectory(string cacheDirectory) => Path.Combine(cacheDirectory, "language-servers");
    public static string NpmBinDirectory(string cacheDirectory) => Path.Combine(InstallDirectory(cacheDirectory), "node_modules", ".bin");

    public IEnumerable<string> SystemDirs()
    {
        if (_dirsOverride is not null) return _dirsOverride;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (Environment.GetEnvironmentVariable("GOBIN") is { Length: > 0 } gobin) dirs.Add(gobin);
        if (Environment.GetEnvironmentVariable("GOPATH") is { Length: > 0 } gopath)
            dirs.AddRange(gopath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(p => Path.Combine(p, "bin")));
        dirs.Add(Path.Combine(home, "go", "bin"));
        dirs.Add(Path.Combine(home, ".cargo", "bin"));
        dirs.Add(Path.Combine(home, ".dotnet", "tools"));
        dirs.Add(Path.Combine(home, ".local", "bin"));
        if (!OperatingSystem.IsWindows())
        {
            dirs.AddRange(["/opt/homebrew/bin", "/opt/homebrew/opt/llvm/bin", "/usr/local/bin", "/usr/local/opt/llvm/bin"]);
            dirs.Add(Path.Combine(home, ".npm-global", "bin"));
        }
        return dirs.Distinct(StringComparer.Ordinal);
    }

    /// <summary>PATH for child processes (so node-based servers find node).</summary>
    public string ChildPath() => string.Join(Path.PathSeparator, SystemDirs());

    public string? FindExecutable(string nameOrPath, bool includeCache = true)
    {
        if (nameOrPath.Contains(Path.DirectorySeparatorChar) || nameOrPath.Contains('/'))
            return File.Exists(nameOrPath) ? Path.GetFullPath(nameOrPath) : null;
        var dirs = new List<string>();
        if (includeCache && _cacheDirectory is not null) dirs.Add(NpmBinDirectory(_cacheDirectory));
        dirs.AddRange(SystemDirs());
        string[] exts = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        // "cmd.exe" already carries a PATHEXT extension: try it as written before appending another one.
        if (OperatingSystem.IsWindows() && exts.Any(e => nameOrPath.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            exts = ["", .. exts];
        foreach (string dir in dirs)
            foreach (string ext in exts)
            {
                string candidate = Path.Combine(dir, nameOrPath + ext);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    public ResolvedServer? Resolve(LanguageServerDefinition def, string? overrideCommandLine = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideCommandLine))
        {
            var parts = CommandLine.Split(overrideCommandLine);
            if (parts.Count == 0) return null;
            string? exe = FindExecutable(parts[0]);
            return exe is null ? null : new ResolvedServer(def, exe, parts.Skip(1).ToList(), ServerSource.UserOverride);
        }
        foreach (var c in def.Candidates)
        {
            string? exe = FindExecutable(c.Executable);
            if (exe is null) continue;
            bool cache = _cacheDirectory is not null && exe.StartsWith(InstallDirectory(_cacheDirectory), StringComparison.Ordinal);
            return new ResolvedServer(def, exe, c.Args, cache ? ServerSource.RunnerCache : ServerSource.System);
        }
        return null;
    }
}

/// <summary>Minimal shell-like splitting for the user's override command (quotes group, no expansion).</summary>
public static class CommandLine
{
    public static List<string> Split(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        char? quote = null;
        bool any = false;
        foreach (char ch in line)
        {
            if (quote is { } q)
            {
                if (ch == q) quote = null; else sb.Append(ch);
            }
            else if (ch is '"' or '\'') { quote = ch; any = true; }
            else if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 || any) { result.Add(sb.ToString()); sb.Clear(); any = false; }
            }
            else sb.Append(ch);
        }
        if (sb.Length > 0 || any) result.Add(sb.ToString());
        return result;
    }
}

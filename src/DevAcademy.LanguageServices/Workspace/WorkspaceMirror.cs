using System.Text;

namespace DevAcademy.LanguageServices.Workspace;

/// <summary>
/// Gives lesson buffers (which have no files) a real directory for the language server: <c>lsp-workspaces/&lt;exerciseId&gt;/&lt;lang&gt;</c>
/// containing the editor files plus the minimal project scaffolding the server needs (go.mod, compile_flags.txt ...).
/// Content is written atomically and debounced; <see cref="FlushAsync"/> forces pending writes.
/// </summary>
public sealed class WorkspaceMirror : IAsyncDisposable
{
    private readonly Dictionary<string, string> _pending = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;
    private Task _writer = Task.CompletedTask;

    public WorkspaceMirror(string baseDirectory, string exerciseId, string languageId, TimeSpan? debounce = null)
    {
        Root = Path.GetFullPath(Path.Combine(baseDirectory, Sanitize(exerciseId), Sanitize(languageId)));
        LanguageId = languageId;
        _delay = debounce ?? TimeSpan.FromMilliseconds(400);
    }

    public string Root { get; }
    public string LanguageId { get; }

    public string PathFor(string relativePath) => Path.GetFullPath(Path.Combine(Root, relativePath));

    /// <summary>The mirrored relative path of a full path inside this root, or null.</summary>
    public string? RelativeOf(string fullPath)
    {
        string rel = Path.GetRelativePath(Root, fullPath);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel.Replace('\\', '/');
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        string r = sb.ToString().Trim('.');
        return r.Length == 0 ? "_" : r;
    }

    /// <summary>Scaffolding files for a language, as (relative path, content). Skipped when the exercise ships a file of that name.</summary>
    public static IReadOnlyList<(string Path, string Content)> Scaffolding(string languageId) => languageId switch
    {
        "go" => [("go.mod", "module lesson\n\ngo 1.22\n")],
        "cpp" => [("compile_flags.txt", "-std=c++20\n-Wall\n-Wextra\n")],
        "python" => [("pyrightconfig.json", "{\n  \"typeCheckingMode\": \"basic\",\n  \"reportMissingModuleSource\": false\n}\n")],
        "typescript" or "javascript" => [("tsconfig.json",
            "{\n  \"compilerOptions\": {\n    \"target\": \"ES2022\",\n    \"module\": \"commonjs\",\n    \"strict\": true,\n    \"allowJs\": true,\n    \"checkJs\": false,\n    \"skipLibCheck\": true\n  }\n}\n")],
        "rust" => [("Cargo.toml", "[package]\nname = \"lesson\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[[bin]]\nname = \"lesson\"\npath = \"main.rs\"\n")],
        "csharp" => [("lesson.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <OutputType>Exe</OutputType>\n    <TargetFramework>net10.0</TargetFramework>\n    <ImplicitUsings>enable</ImplicitUsings>\n    <Nullable>enable</Nullable>\n  </PropertyGroup>\n</Project>\n")],
        _ => [],
    };

    /// <summary>Writes all files and scaffolding now and removes stale files from earlier states of the same exercise.</summary>
    public async Task PrepareAsync(IEnumerable<(string Path, string Content)> files, CancellationToken ct = default)
    {
        var list = files.ToList();
        Directory.CreateDirectory(Root);
        var keep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, content) in list)
        {
            keep.Add(Normalize(path));
            await WriteAsync(path, content, ct).ConfigureAwait(false);
        }
        foreach (var (path, content) in Scaffolding(LanguageId))
        {
            keep.Add(Normalize(path));
            if (list.Any(f => Normalize(f.Path) == Normalize(path))) continue;
            await WriteAsync(path, content, ct).ConfigureAwait(false);
        }
        foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            if (!keep.Contains(Normalize(Path.GetRelativePath(Root, file))))
                try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Normalize(string p) => p.Replace('\\', '/');

    private async Task WriteAsync(string relative, string content, CancellationToken ct)
    {
        string full = PathFor(relative);
        if (RelativeOf(full) is null) return; // never write outside the root
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string tmp = full + ".tmp";
        await File.WriteAllTextAsync(tmp, content, new UTF8Encoding(false), ct).ConfigureAwait(false);
        File.Move(tmp, full, overwrite: true);
    }

    /// <summary>Queues a changed file; written after the debounce delay.</summary>
    public void Schedule(string relativePath, string content)
    {
        lock (_gate)
        {
            _pending[relativePath] = content;
            _cts?.Cancel();
            var cts = _cts = new CancellationTokenSource();
            _writer = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_delay, cts.Token).ConfigureAwait(false);
                    await DrainAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            });
        }
    }

    public async Task FlushAsync()
    {
        lock (_gate) _cts?.Cancel();
        await DrainAsync().ConfigureAwait(false);
    }

    private async Task DrainAsync()
    {
        List<KeyValuePair<string, string>> batch;
        lock (_gate)
        {
            batch = _pending.ToList();
            _pending.Clear();
        }
        foreach (var kv in batch)
        {
            try { await WriteAsync(kv.Key, kv.Value, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the next change retries */ }
        }
    }

    public async ValueTask DisposeAsync() => await FlushAsync().ConfigureAwait(false);
}

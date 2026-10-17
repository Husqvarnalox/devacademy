namespace DevAcademy.LanguageServices.Servers;

public sealed record ServerCommand(string Executable, IReadOnlyList<string> Args);

/// <summary>
/// One language server as data: which app languages it serves, how to start it (first candidate that exists wins), what marks a
/// project root for it, and which npm packages provide it (null = not npm-distributed, the user installs it themselves).
/// </summary>
public sealed record LanguageServerDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Languages,
    IReadOnlyList<ServerCommand> Candidates,
    IReadOnlyList<string> RootMarkers,
    object? InitializationOptions,
    IReadOnlyList<string> NpmPackages,
    string InstallHint)
{
    public bool IsNpmInstallable => NpmPackages.Count > 0;

    /// <summary><see cref="InstallHint"/> as translatable text.</summary>
    public DevAcademy.Domain.LocalizedText InstallHintText => EngineMessages.InstallHint(Id, InstallHint);
}

public static class LanguageServerRegistry
{
    public const string ClangdId = "clangd", GoplsId = "gopls", PyrightId = "pyright";

    public static IReadOnlyList<LanguageServerDefinition> Defaults { get; } =
    [
        new("clangd", "clangd", ["cpp", "c"],
            [new("clangd", ["--background-index=false", "--log=error"])],
            ["compile_commands.json", "compile_flags.txt", ".clangd", ".git"], null, [],
            "macOS: xcode-select --install (or brew install llvm). Linux: apt install clangd. Windows: install LLVM."),
        new("gopls", "gopls", ["go"],
            [new("gopls", [])],
            ["go.work", "go.mod", ".git"], null, [],
            "go install golang.org/x/tools/gopls@latest"),
        new("pyright", "Pyright", ["python"],
            [new("pyright-langserver", ["--stdio"]), new("basedpyright-langserver", ["--stdio"])],
            ["pyrightconfig.json", "pyproject.toml", "setup.py", "requirements.txt", ".git"], null, ["pyright"],
            "npm install pyright (or pip install basedpyright)"),
        new("typescript-language-server", "typescript-language-server", ["typescript", "javascript"],
            [new("typescript-language-server", ["--stdio"])],
            ["tsconfig.json", "jsconfig.json", "package.json", ".git"], null, ["typescript-language-server", "typescript"],
            "npm install typescript-language-server typescript"),
        new("rust-analyzer", "rust-analyzer", ["rust"],
            [new("rust-analyzer", [])],
            ["Cargo.toml", "rust-project.json", ".git"], null, [],
            "rustup component add rust-analyzer"),
        new("jdtls", "Eclipse JDT LS", ["java"],
            [new("jdtls", [])],
            ["pom.xml", "build.gradle", "build.gradle.kts", ".git"], null, [],
            "brew install jdtls (or download Eclipse JDT Language Server)"),
        new("csharp-ls", "csharp-ls", ["csharp"],
            [new("csharp-ls", [])],
            ["*.sln", "*.slnx", "*.csproj", ".git"], null, [],
            "dotnet tool install --global csharp-ls"),
        new("intelephense", "Intelephense", ["php"],
            [new("intelephense", ["--stdio"])],
            ["composer.json", ".git"], null, ["intelephense"],
            "npm install intelephense"),
        new("kotlin-language-server", "kotlin-language-server", ["kotlin"],
            [new("kotlin-language-server", [])],
            ["settings.gradle", "settings.gradle.kts", "build.gradle", "build.gradle.kts", "pom.xml", ".git"], null, [],
            "brew install kotlin-language-server"),
    ];

    public static LanguageServerDefinition? ForLanguage(string languageId, IReadOnlyList<LanguageServerDefinition>? defs = null) =>
        (defs ?? Defaults).FirstOrDefault(d => d.Languages.Contains(languageId, StringComparer.Ordinal));

    /// <summary>App language of a file (the lessons' language ids) from its extension; null for non-code files.</summary>
    public static string? LanguageForExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".go" => "go",
        ".py" or ".pyi" => "python",
        ".ts" or ".tsx" or ".mts" or ".cts" => "typescript",
        ".js" or ".jsx" or ".mjs" or ".cjs" => "javascript",
        ".cpp" or ".cc" or ".cxx" or ".hpp" or ".hh" or ".hxx" or ".h" or ".c" => "cpp",
        ".rs" => "rust",
        ".java" => "java",
        ".cs" => "csharp",
        ".php" => "php",
        ".kt" or ".kts" => "kotlin",
        _ => null,
    };

    /// <summary>The textDocument/didOpen languageId (more specific than the app language: tsx, c, jsx).</summary>
    public static string LspLanguageId(string appLanguage, string path) => (appLanguage, Path.GetExtension(path).ToLowerInvariant()) switch
    {
        ("typescript", ".tsx") => "typescriptreact",
        ("javascript", ".jsx") => "javascriptreact",
        ("cpp", ".c") => "c",
        _ => appLanguage,
    };

    /// <summary>Nearest directory at or above the file (not above <paramref name="boundary"/>) holding one of the root markers; otherwise the boundary.</summary>
    public static string FindRoot(string filePath, string boundary, LanguageServerDefinition def)
    {
        string full = Path.GetFullPath(boundary);
        string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        while (dir is not null && IsWithin(full, dir))
        {
            foreach (string marker in def.RootMarkers)
            {
                if (marker == ".git") continue; // the boundary is the project root already
                try
                {
                    if (marker.Contains('*') ? Directory.EnumerateFileSystemEntries(dir, marker).Any() : File.Exists(Path.Combine(dir, marker)))
                        return dir;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (string.Equals(dir.TrimEnd(Path.DirectorySeparatorChar), full.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)) break;
            dir = Path.GetDirectoryName(dir);
        }
        return full;
    }

    private static bool IsWithin(string root, string dir) =>
        dir.TrimEnd(Path.DirectorySeparatorChar).StartsWith(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal);
}

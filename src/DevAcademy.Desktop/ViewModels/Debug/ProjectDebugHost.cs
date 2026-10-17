using DevAcademy.Debugging.Adapters;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>Debugging inside an open project: files are debugged where they are, compiled languages are built into a scratch directory.</summary>
internal sealed class ProjectDebugHost(ProjectWorkspaceViewModel workspace, DebugService service) : IDebugHost
{
    public BreakpointStore Breakpoints { get; } = new();

    /// <summary>Debuggable language of the active file (by extension), else the project's language.</summary>
    public string? LanguageId =>
        LanguageForFile(workspace.ActiveGroup?.ActiveTab?.FullPath) is { } l && service.Supports(l) ? l : workspace.Language;

    public string? DisabledReason => LanguageId is null ? Loc.T("Dbg_NoLanguage") : null;

    public static string? LanguageForFile(string? path) => Path.GetExtension(path ?? "").ToLowerInvariant() switch
    {
        ".py" => "python",
        ".go" => "go",
        ".rs" => "rust",
        ".cpp" or ".cc" or ".cxx" => "cpp",
        ".js" or ".mjs" or ".cjs" => "javascript",
        ".ts" => "typescript",
        ".java" => "java",
        ".cs" => "csharp",
        _ => null,
    };

    public async Task<string?> GetPythonInterpreterAsync(CancellationToken ct)
    {
        if (workspace.SelectedPython?.InterpreterForCommand is { } p)
            return Path.IsPathRooted(p) ? p : Path.GetFullPath(p, workspace.Root);
        var all = await workspace.ServicesPython.GetInterpretersAsync(false, ct).ConfigureAwait(false);
        return all.FirstOrDefault()?.Path;
    }

    public async Task<DebugPreparation> PrepareAsync(CancellationToken ct)
    {
        string? lang = LanguageId;
        if (lang is null) return new DebugPreparation(null, Loc.T("Dbg_NoLanguage"), null, new Dictionary<string, string>());
        if (!await workspace.SaveAllAsync()) return new DebugPreparation(null, workspace.Status, null, new Dictionary<string, string>());

        var paths = Breakpoints.All.ToDictionary(kv => kv.Key, kv => Path.GetFullPath(Path.Combine(workspace.Root, kv.Key)), StringComparer.Ordinal);
        string? active = workspace.ActiveGroup?.ActiveTab?.FullPath;
        bool activeMatches = active is not null && LanguageForFile(active) == lang;
        // Make sure the file shown in the editor is mapped even when it has no breakpoints yet (stack frames resolve to it).
        foreach (var tab in workspace.AllTabs) paths[tab.RelativePath] = tab.FullPath;

        if (lang == "python")
        {
            string entry = activeMatches ? active! : Path.Combine(workspace.Root, "main.py");
            if (!File.Exists(entry)) return new DebugPreparation(null, Loc.F("Dbg_NoEntry", "main.py"), null, paths);
            string? py = await GetPythonInterpreterAsync(ct).ConfigureAwait(false);
            return new DebugPreparation(new DebugLaunch(entry, workspace.Root, [], py), null, null, paths);
        }

        string runDir = service.NewRunDirectory();
        string? entryRel = activeMatches ? Path.GetRelativePath(workspace.Root, active!) : DefaultEntry(lang);
        if (entryRel is null || !File.Exists(Path.Combine(workspace.Root, entryRel)))
        {
            service.CleanupRunDirectory(runDir);
            return new DebugPreparation(null, Loc.F("Dbg_NoEntry", entryRel ?? "main"), null, paths);
        }
        var built = await service.Builder.BuildAsync(lang, runDir, entryRel, ct, workspace.Root).ConfigureAwait(false);
        if (!built.Success || built.Executable is null)
        {
            service.CleanupRunDirectory(runDir);
            return new DebugPreparation(null, Loc.T("Dbg_BuildFailed") + "\n" + Loc.Of(built.OutputText, built.Output), null, paths);
        }
        return new DebugPreparation(new DebugLaunch(built.Executable, workspace.Root, [], RunArguments: built.RunArguments), null, runDir, paths);
    }

    private string? DefaultEntry(string lang)
    {
        string[] candidates = lang switch
        {
            "go" => ["main.go", "cmd/main.go"],
            "rust" => ["src/main.rs", "main.rs"],
            "cpp" => ["main.cpp", "src/main.cpp"],
            "javascript" => ["main.js", "index.js", "src/main.js"],
            "typescript" => ["main.ts", "src/main.ts"],
            "java" => ["Main.java"],
            "csharp" => ["Program.cs"],
            _ => [],
        };
        return candidates.FirstOrDefault(c => File.Exists(Path.Combine(workspace.Root, c)));
    }

    public async Task RevealAsync(string key, int line) =>
        await workspace.OpenFileAsync(Path.GetFullPath(Path.Combine(workspace.Root, key)), line).ConfigureAwait(true);
}

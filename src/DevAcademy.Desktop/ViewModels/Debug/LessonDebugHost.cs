using System.Text.Json;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>
/// Debugging a lesson's Practice / Challenge / scratch editor. The files are written into a throw-away directory next to nothing else of the
/// learner's, run there and removed afterwards. Never used for Submit: grading stays sandboxed and separate.
/// </summary>
internal sealed class LessonDebugHost(ExerciseSessionViewModel session, AppServices services) : IDebugHost
{
    public BreakpointStore Breakpoints { get; } = new();

    public string? LanguageId => session.LanguageId;

    public string? DisabledReason =>
        session.IsCommandGraded ? Loc.T("Dbg_CommandGraded")
        : session.IsRuntimeMissing ? Loc.T("Dbg_RuntimeMissing")
        : null;

    public async Task<string?> GetPythonInterpreterAsync(CancellationToken ct)
    {
        Execution.PythonRunSelection? sel = null;
        if (session.Exercise is { } ex) sel = (await services.Python.ResolveAsync(ex.ForVariant(session.LanguageId), ct).ConfigureAwait(false)).Python;
        else sel = await services.Python.GetActiveSelectionAsync(ct).ConfigureAwait(false);
        if (sel is not null) return sel.InterpreterPath;
        var all = await services.Python.GetInterpretersAsync(false, ct).ConfigureAwait(false);
        return all.FirstOrDefault()?.Path;
    }

    public async Task<DebugPreparation> PrepareAsync(CancellationToken ct)
    {
        await session.FlushDraftsAsync();
        var files = session.SnapshotFiles();
        string lang = session.LanguageId, entry = session.EntryFile;
        var keys = Breakpoints.All.Select(kv => kv.Key).Concat(files.Select(f => f.Path)).Distinct(StringComparer.Ordinal).ToList();

        string dir = services.Debug.NewRunDirectory();
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var f in files)
            {
                string full = Path.GetFullPath(Path.Combine(dir, f.Path));
                if (!full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;   // never write outside the run directory
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                await File.WriteAllTextAsync(full, f.Content, ct).ConfigureAwait(false);
                paths[f.Path] = full;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            services.Debug.CleanupRunDirectory(dir);
            return new DebugPreparation(null, ex.Message, null, paths);
        }
        foreach (var k in keys) paths.TryAdd(k, Path.Combine(dir, k));

        if (lang == "python")
        {
            string? py = await GetPythonInterpreterAsync(ct).ConfigureAwait(false);
            return new DebugPreparation(new DebugLaunch(Path.Combine(dir, entry), dir, [], py), null, dir, paths);
        }
        var built = await services.Debug.Builder.BuildAsync(lang, dir, entry, ct).ConfigureAwait(false);
        if (!built.Success || built.Executable is null)
        {
            services.Debug.CleanupRunDirectory(dir);
            return new DebugPreparation(null, Loc.T("Dbg_BuildFailed") + "\n" + Loc.Of(built.OutputText, built.Output), null, paths);
        }
        return new DebugPreparation(new DebugLaunch(built.Executable, dir, [], RunArguments: built.RunArguments), null, dir, paths);
    }

    public Task RevealAsync(string key, int line)
    {
        session.ShowFileAt(key, line);
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- persistence (per exercise and language)

    private string? SettingKey => session.Exercise is { } ex ? $"debug.breakpoints.{ex.Id}.{session.LanguageId}" : null;

    public async Task LoadAsync()
    {
        if (SettingKey is not { } key) return;
        string? json;
        try { json = await services.Settings.GetAsync(key).ConfigureAwait(true); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return; }
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json);
            if (saved is null) return;
            Breakpoints.Load(saved.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)kv.Value));
        }
        catch (JsonException) { /* ignore a damaged value */ }
    }

    public async Task SaveAsync()
    {
        if (SettingKey is not { } key) return;
        try { await services.Settings.SetAsync(key, JsonSerializer.Serialize(Breakpoints.Snapshot())).ConfigureAwait(true); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* best effort */ }
    }
}

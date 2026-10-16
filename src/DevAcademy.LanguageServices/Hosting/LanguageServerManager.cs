using System.Collections.Concurrent;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.LanguageServices.Hosting;

public sealed class LanguageServerManagerOptions
{
    public IReadOnlyList<LanguageServerDefinition> Definitions { get; init; } = LanguageServerRegistry.Defaults;
    public required ServerLocator Locator { get; init; }
    public IServerLauncher Launcher { get; init; } = new ProcessServerLauncher();
    /// <summary>languageId -> user's override command line (settings), or null.</summary>
    public Func<string, string?> OverrideFor { get; init; } = _ => null;
    public Func<bool> IsEnabled { get; init; } = () => true;
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan KillTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan InitializeTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public int MaxRestarts { get; init; } = 3;
    public IReadOnlyList<TimeSpan> RestartDelays { get; init; } = [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];
    public Action<string>? Log { get; init; }

    /// <summary>
    /// Runs an action on the thread that owns editor text (the UI thread in the app) and waits for it. The default runs inline,
    /// which is right for tests and for text owned by no particular thread.
    /// </summary>
    public Action<Action> Invoke { get; init; } = a => a();
}

/// <summary>Inventory row for settings: one server definition with where it was found and its last known state.</summary>
public sealed record ServerInventoryItem(LanguageServerDefinition Definition, string? ExecutablePath, ServerSource? Source, string? OverrideCommand, ServerState State, string? Version, string? Message);

/// <summary>
/// Owns all server sessions: one per (server definition, root), started lazily by the first document, shared by every tab of
/// that root, stopped when idle. Nothing here installs anything.
/// </summary>
public sealed class LanguageServerManager : IAsyncDisposable
{
    private readonly LanguageServerManagerOptions _o;
    private readonly ConcurrentDictionary<(string DefId, string Root), LanguageServerSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _knownVersions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public LanguageServerManager(LanguageServerManagerOptions options) => _o = options;

    public ServerLocator Locator => _o.Locator;
    public IReadOnlyList<LanguageServerDefinition> Definitions => _o.Definitions;
    public event Action<ServerStatus>? StatusChanged;

    public LanguageServerDefinition? DefinitionFor(string appLanguage) => LanguageServerRegistry.ForLanguage(appLanguage, _o.Definitions);

    /// <summary>Current status for a language and root: the live session's, or why none can start.</summary>
    public ServerStatus GetStatus(string appLanguage, string root)
    {
        var def = DefinitionFor(appLanguage);
        if (def is null)
            return new ServerStatus("", "", appLanguage, root, ServerState.NotInstalled, null, null, "no language server is defined", 0, "");
        if (!_o.IsEnabled())
            return new ServerStatus(def.Id, def.DisplayName, appLanguage, root, ServerState.Disabled, null, null, null, 0, def.InstallHint);
        if (_sessions.TryGetValue((def.Id, Path.GetFullPath(root)), out var s)) return s.Status;
        var resolved = _o.Locator.Resolve(def, _o.OverrideFor(appLanguage));
        return resolved is null
            ? new ServerStatus(def.Id, def.DisplayName, appLanguage, root, ServerState.NotInstalled, null, null, null, 0, def.InstallHint)
            : new ServerStatus(def.Id, def.DisplayName, appLanguage, root, ServerState.Stopped, _knownVersions.GetValueOrDefault(def.Id), resolved.Executable, null, 0, def.InstallHint);
    }

    /// <summary>
    /// Opens a buffer on the right server, starting it when needed (in the background: this returns immediately). Null when there
    /// is no usable server; <see cref="GetStatus"/> says why.
    /// </summary>
    public LspDocument? OpenDocument(string appLanguage, string filePath, string root, Func<string> getText)
    {
        var def = DefinitionFor(appLanguage);
        if (def is null || !_o.IsEnabled()) return null;
        root = Path.GetFullPath(root);
        LanguageServerSession session;
        lock (_gate)
        {
            if (!_sessions.TryGetValue((def.Id, root), out session!))
            {
                var resolved = _o.Locator.Resolve(def, _o.OverrideFor(appLanguage));
                if (resolved is null) return null;
                session = new LanguageServerSession(_o, resolved, appLanguage, root);
                session.StatusChanged += OnSessionStatus;
                session.Idle += OnIdle;
                _sessions[(def.Id, root)] = session;
                session.Start();
            }
        }
        return session.OpenDocument(Path.GetFullPath(filePath), LanguageServerRegistry.LspLanguageId(appLanguage, filePath), getText);
    }

    private void OnSessionStatus(LanguageServerSession s)
    {
        var st = s.Status;
        if (st.Version is not null) _knownVersions[st.DefinitionId] = st.Version;
        StatusChanged?.Invoke(st);
    }

    private void OnIdle(LanguageServerSession s)
    {
        lock (_gate)
        {
            if (s.DocumentCount != 0) return;
            _sessions.TryRemove((s.Definition.Id, s.Root), out _);
        }
        _ = s.StopAsync();
    }

    public IReadOnlyList<LanguageServerSession> ActiveSessions => _sessions.Values.ToList();

    public IReadOnlyList<ServerInventoryItem> Inventory()
    {
        var rows = new List<ServerInventoryItem>();
        foreach (var def in _o.Definitions)
        {
            string lang = def.Languages[0];
            string? ovr = _o.OverrideFor(lang);
            var resolved = _o.Locator.Resolve(def, ovr);
            var live = _sessions.Values.FirstOrDefault(s => s.Definition.Id == def.Id);
            ServerState state = live?.Status.State ?? (resolved is null ? ServerState.NotInstalled : ServerState.Stopped);
            rows.Add(new ServerInventoryItem(def, resolved?.Executable, resolved?.Source, ovr, state,
                live?.Status.Version ?? _knownVersions.GetValueOrDefault(def.Id), live?.Status.Message));
        }
        return rows;
    }

    public async Task StopAllAsync()
    {
        var all = _sessions.Values.ToList();
        _sessions.Clear();
        await Task.WhenAll(all.Select(s => s.StopAsync())).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await StopAllAsync().ConfigureAwait(false);
}

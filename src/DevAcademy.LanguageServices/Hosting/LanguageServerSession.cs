using System.Collections.Concurrent;
using DevAcademy.LanguageServices.JsonRpc;
using DevAcademy.LanguageServices.Protocol;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.LanguageServices.Hosting;

public enum ServerState { NotInstalled, Disabled, Starting, Ready, Restarting, Error, Stopped }

public sealed record ServerStatus(
    string DefinitionId,
    string DisplayName,
    string LanguageId,
    string Root,
    ServerState State,
    string? Version,
    string? Executable,
    string? Message,
    int RestartCount,
    string InstallHint)
{
    public bool IsUsable => State == ServerState.Ready;
}

/// <summary>
/// One server process for one (definition, root). Starts lazily, re-opens its documents after a restart, restarts after a crash
/// with growing delays (at most <see cref="LanguageServerManagerOptions.MaxRestarts"/> times in a row), and shuts down gracefully
/// (shutdown/exit, then kill after a timeout).
/// </summary>
public sealed class LanguageServerSession : IAsyncDisposable
{
    private readonly LanguageServerManagerOptions _o;
    private readonly ResolvedServer _server;
    private readonly string _languageId;
    private readonly ConcurrentDictionary<string, LspDocument> _docs = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private IServerProcess? _process;
    private LspClient? _client;
    private Task _supervisor = Task.CompletedTask;
    private ServerStatus _status;
    private bool _stopping;
    private CancellationTokenSource? _idleCts;

    internal LanguageServerSession(LanguageServerManagerOptions options, ResolvedServer server, string languageId, string root)
    {
        _o = options;
        _server = server;
        _languageId = languageId;
        Root = root;
        _status = new ServerStatus(server.Definition.Id, server.Definition.DisplayName, languageId, root, ServerState.Starting, null,
            server.Executable, null, 0, server.Definition.InstallHint);
    }

    public string Root { get; }
    public LanguageServerDefinition Definition => _server.Definition;
    public ServerStatus Status { get { lock (_gate) return _status; } }
    public ServerCapabilitiesInfo Capabilities => _client?.Capabilities ?? ServerCapabilitiesInfo.None;
    public int? ProcessId => _process?.ProcessId;
    public int DocumentCount => _docs.Count;
    public event Action<LanguageServerSession>? StatusChanged;
    internal event Action<LanguageServerSession>? Idle;

    private void SetStatus(ServerState state, string? message = null, int? restarts = null, string? version = null)
    {
        lock (_gate)
            _status = _status with { State = state, Message = message, RestartCount = restarts ?? _status.RestartCount, Version = version ?? _status.Version };
        StatusChanged?.Invoke(this);
    }

    internal void Start() => _supervisor = Task.Run(SuperviseAsync);

    public LspDocument OpenDocument(string path, string lspLanguageId, Func<string> getText)
    {
        _idleCts?.Cancel();
        var doc = new LspDocument(this, path, lspLanguageId, getText);
        _docs[FileUri.Key(doc.Uri)] = doc;
        if (_client is { } c && Status.State == ServerState.Ready) OpenInvoked(doc, c);
        return doc;
    }

    private void OpenInvoked(LspDocument doc, LspClient client)
    {
        // The text must be read on the thread that owns it; the invoker (UI thread in the app) also serialises with edits.
        _o.Invoke(() => doc.OpenOn(client));
    }

    internal void Release(LspDocument doc)
    {
        _docs.TryRemove(FileUri.Key(doc.Uri), out _);
        if (!_docs.IsEmpty || _stopping) return;
        _idleCts?.Cancel();
        var cts = _idleCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_o.IdleTimeout, cts.Token).ConfigureAwait(false);
                if (_docs.IsEmpty) Idle?.Invoke(this);
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task SuperviseAsync()
    {
        int failures = 0;
        while (!_stopping)
        {
            string? failure;
            var startedAt = DateTime.UtcNow;
            try { failure = await RunOnceAsync().ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stopping) { break; }
            catch (Exception ex) { failure = ex.Message; }
            if (_stopping) break;
            if (DateTime.UtcNow - startedAt > TimeSpan.FromSeconds(30)) failures = 0; // it ran fine for a while: not a crash loop

            foreach (var d in _docs.Values) { d.Detach(); d.ClearDiagnostics(); }
            string tail = _process?.StderrTail.Trim() ?? "";
            string detail = failure ?? "the server process exited";
            if (tail.Length > 0) detail += ": " + tail[^Math.Min(300, tail.Length)..];
            if (failures >= _o.MaxRestarts)
            {
                SetStatus(ServerState.Error, $"crashed {failures + 1} times; {detail}");
                return;
            }
            TimeSpan delay = _o.RestartDelays[Math.Min(failures, _o.RestartDelays.Count - 1)];
            failures++;
            SetStatus(ServerState.Restarting, detail, failures);
            try { await Task.Delay(delay, _cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Runs one server process to its end. Returns the failure reason, or null when it ended without an error to report.</summary>
    private async Task<string?> RunOnceAsync()
    {
        if (Status.State != ServerState.Restarting) SetStatus(ServerState.Starting);
        IServerProcess proc = await _o.Launcher.LaunchAsync(_server, Root, _o.Locator.ChildPath(), _cts.Token).ConfigureAwait(false);
        _process = proc;
        var rpc = new JsonRpcConnection(proc.Output, proc.Input, m => _o.Log?.Invoke($"[{Definition.Id}] {m}"));
        var client = new LspClient(rpc, FileUri.FromPath(Root), Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar)));
        client.ServerLog += m => _o.Log?.Invoke($"[{Definition.Id}] {m}");
        client.Diagnostics += OnDiagnostics;
        try
        {
            using (var init = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
            {
                init.CancelAfter(_o.InitializeTimeout);
                try { await client.InitializeAsync(Definition.InitializationOptions, init.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!_cts.IsCancellationRequested) { return "initialize timed out"; }
                catch (IOException) { return "the server exited during initialize"; }
                catch (RpcException ex) { return "initialize failed: " + ex.Message; }
            }
            _client = client;
            SetStatus(ServerState.Ready, null, version: client.Capabilities.ServerVersion);
            foreach (var d in _docs.Values) OpenInvoked(d, client);

            await Task.WhenAny(client.Closed, proc.Exited).ConfigureAwait(false);
            return _stopping ? null : "the server process exited unexpectedly";
        }
        finally
        {
            _client = null;
            await client.DisposeAsync().ConfigureAwait(false);
            await proc.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void OnDiagnostics(PublishDiagnosticsParams p)
    {
        if (_docs.TryGetValue(FileUri.Key(p.Uri), out var doc)) doc.SetDiagnostics(p);
    }

    /// <summary>Graceful stop: shutdown/exit, then kill if the process lingers beyond <see cref="LanguageServerManagerOptions.KillTimeout"/>.</summary>
    public async Task StopAsync()
    {
        if (_stopping) { await _supervisor.ConfigureAwait(false); return; }
        _stopping = true;
        _idleCts?.Cancel();
        LspClient? client = _client;
        IServerProcess? proc = _process;
        foreach (var d in _docs.Values) { d.Detach(); d.ClearDiagnostics(); }
        if (client is not null && proc is not null)
        {
            bool ok = await client.ShutdownAsync(_o.KillTimeout).ConfigureAwait(false);
            if (ok) await Task.WhenAny(proc.Exited, Task.Delay(_o.KillTimeout)).ConfigureAwait(false);
        }
        proc?.Kill();
        _cts.Cancel();
        try { await _supervisor.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
        SetStatus(ServerState.Stopped);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;

namespace DevAcademy.Debugging.Session;

public enum DebugState { NotStarted, Starting, Running, Stopped, Terminated }

/// <summary>A stop: why, which thread and the call stack of that thread (innermost first).</summary>
public sealed record DebugStop(string Reason, string? Description, int ThreadId, IReadOnlyList<DapStackFrame> Frames);

public sealed record DebugTermination(int? ExitCode, string? Error);

public sealed record DebugSessionOptions(DebugAdapterDefinition Adapter, ResolvedAdapter Resolved, JsonObject Launch, string WorkingDirectory);

/// <summary>
/// One debugging run, UI-free: starts the adapter, performs the DAP handshake (initialize, launch, initialized, setBreakpoints,
/// configurationDone), keeps the stopped/running state and exposes the stop data. Events are raised from a single worker loop in
/// order, never from the connection's reader, so handlers may call back into the session. The UI marshals them itself.
/// </summary>
public sealed class DebugSession : IAsyncDisposable
{
    private readonly DebugSessionOptions _options;
    private readonly IAdapterLauncher _launcher;
    private readonly Channel<Func<Task>> _work = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, IReadOnlyList<SourceBreakpoint>> _breakpoints = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _gate = new();
    private IAdapterProcess? _process;
    private DapConnection? _conn;
    private DapClient? _client;
    private DapClient? _root;
    private readonly List<DapConnection> _children = [];
    private Task? _worker;
    private int? _exitCode;
    private int _terminatedRaised;
    private volatile bool _stopping;

    public DebugSession(DebugSessionOptions options, IAdapterLauncher launcher)
    {
        _options = options;
        _launcher = launcher;
    }

    public DebugState State { get; private set; } = DebugState.NotStarted;
    public Capabilities Capabilities => _client?.Capabilities ?? new Capabilities();
    public int? CurrentThreadId { get; private set; }
    public IReadOnlyList<DapStackFrame> Frames { get; private set; } = [];
    public string AdapterLog => _process?.LogTail ?? "";

    public event Action<DebugState>? StateChanged;
    public event Action<DebugStop>? Stopped;
    public event Action? Resumed;
    public event Action<OutputEventArgs>? Output;
    public event Action<DebugTermination>? Terminated;
    public event Action<string, DapBreakpoint>? BreakpointChanged;

    /// <summary>Breakpoints to send during the handshake and the current set (the session remembers the last list per file).</summary>
    public void SeedBreakpoints(IReadOnlyDictionary<string, IReadOnlyList<SourceBreakpoint>> byFile)
    {
        lock (_gate) foreach (var (path, list) in byFile) _breakpoints[path] = list;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        SetState(DebugState.Starting);
        _worker = Task.Run(WorkerAsync, CancellationToken.None);
        try
        {
            _process = await _launcher.LaunchAsync(_options.Resolved, _options.WorkingDirectory, ct).ConfigureAwait(false);
            _conn = new DapConnection(_process.FromAdapter, _process.ToAdapter);
            _client = new DapClient(_conn);
            _conn.Start();
            Wire(_client);   // after Start: Closed is only the read loop once started
            _root = _client;
            bool childSessions = _options.Adapter.Kind == AdapterKind.JsDebug;
            if (childSessions) _conn.ReverseRequestHandler = HandleReverseRequestAsync;

            await _client.InitializeAsync(_options.Adapter.Id, ct, childSessions).ConfigureAwait(false);
            var launch = _client.LaunchAsync(_options.Launch, ct);
            // Adapters differ: some send `initialized` right after initialize, some after launch. A launch that already answered is also fine.
            var first = await Task.WhenAny(_initialized.Task, launch, _conn.Closed, _process.Exited, Task.Delay(TimeSpan.FromSeconds(30), ct)).ConfigureAwait(false);
            if (first == _conn.Closed || first == _process.Exited) throw new DapException(EngineMessages.AdapterStopped(_process.LogTail.Trim()));
            if (launch.IsFaulted) await launch.ConfigureAwait(false);

            foreach (var (path, list) in SnapshotBreakpoints())
                await SendBreakpointsAsync(path, list, ct).ConfigureAwait(false);
            if (_client.Capabilities.SupportsConfigurationDoneRequest)
                await _client.ConfigurationDoneAsync(ct).ConfigureAwait(false);
            if (State == DebugState.Starting) SetState(DebugState.Running);
            await launch.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string log = AdapterLog.Trim();
            await DisposeAsync().ConfigureAwait(false);
            throw new DapException(log.Length > 0 && !ex.Message.Contains(log, StringComparison.Ordinal) ? ex.Message + "\n" + log : ex.Message);
        }
    }

    // ---------------------------------------------------------------- child sessions (vscode-js-debug)

    /// <summary>
    /// The adapter's launcher session asks the client to start the real debug session: a second connection to the same adapter,
    /// initialised and attached with the configuration it sends. From then on that session is the one that is stepped and inspected.
    /// </summary>
    private Task<ReverseReply> HandleReverseRequestAsync(string command, JsonElement args)
    {
        if (command != "startDebugging" || args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("configuration", out var cfg) || JsonNode.Parse(cfg.GetRawText()) is not JsonObject config)
            return Task.FromResult(ReverseReply.NotSupported(command));
        bool attach = !(args.TryGetProperty("request", out var rq) && rq.GetString() == "launch");
        _ = Task.Run(async () =>
        {
            try { await StartChildAsync(config, attach).ConfigureAwait(false); }
            catch (Exception ex) when (ex is DapException or IOException or System.Net.Sockets.SocketException or InvalidOperationException or ObjectDisposedException)
            {
                Post(() => { Output?.Invoke(new OutputEventArgs("important", "debugger: " + ex.Message + "\n")); return Task.CompletedTask; });
                Post(() => RaiseTerminatedAsync("The debug session could not be started: " + ex.Message));
            }
        });
        return Task.FromResult(new ReverseReply(true));
    }

    private async Task StartChildAsync(JsonObject config, bool attach)
    {
        var process = _process ?? throw new InvalidOperationException("The debug adapter is gone.");
        var (from, to) = await process.OpenAdditionalConnectionAsync(CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The debug adapter cannot serve a second session.");
        var conn = new DapConnection(from, to);
        var client = new DapClient(conn);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Initialized += () => ready.TrySetResult();
        lock (_gate) _children.Add(conn);
        conn.Start();
        Wire(client);
        _client = client;   // from here on this session is the debuggee's
        await client.InitializeAsync(_options.Adapter.Id, CancellationToken.None, true).ConfigureAwait(false);
        var request = attach ? client.AttachAsync(config) : client.LaunchAsync(config);
        var first = await Task.WhenAny(ready.Task, request, conn.Closed, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (first == conn.Closed) throw new DapException(EngineMessages.SessionClosed());
        if (request.IsFaulted) await request.ConfigureAwait(false);
        foreach (var (path, list) in SnapshotBreakpoints())
            await SendBreakpointsAsync(path, list, CancellationToken.None).ConfigureAwait(false);
        if (client.Capabilities.SupportsConfigurationDoneRequest)
            await client.ConfigurationDoneAsync().ConfigureAwait(false);
        await request.ConfigureAwait(false);
    }

    private void Wire(DapClient c)
    {
        c.Initialized += () => _initialized.TrySetResult();
        c.Output += o => Post(() => { Output?.Invoke(o); return Task.CompletedTask; });
        // netcoredbg reports exitCode 0 whatever the program returned (verified against `return 7`), so its value is not shown rather than shown wrong.
        c.Exited += code => _exitCode = _options.Adapter.Kind == AdapterKind.NetCoreDbg ? null : code;
        c.Continued += _ => Post(() =>
        {
            if (State == DebugState.Stopped) { SetState(DebugState.Running); Resumed?.Invoke(); }
            return Task.CompletedTask;
        });
        c.Stopped += e => Post(() => HandleStoppedAsync(e));
        c.Terminated += () => Post(() => RaiseTerminatedAsync(null));
        c.BreakpointChanged += e => Post(() => { BreakpointChanged?.Invoke(e.Reason, e.Breakpoint); return Task.CompletedTask; });
        c.Connection.Closed.ContinueWith(_ => Post(() => RaiseTerminatedAsync(_stopping ? null : "The debug adapter exited unexpectedly. " + AdapterLog.Trim())), TaskScheduler.Default);
    }

    private void Post(Func<Task> work) => _work.Writer.TryWrite(work);

    private async Task WorkerAsync()
    {
        await foreach (var job in _work.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try { await job().ConfigureAwait(false); }
            catch (Exception ex) when (ex is DapException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                Output?.Invoke(new OutputEventArgs("important", "debugger: " + ex.Message + "\n"));
            }
        }
    }

    private async Task HandleStoppedAsync(StoppedEventArgs e)
    {
        if (State == DebugState.Terminated || _client is null) return;
        int thread;
        if (e.ThreadId is { } t) thread = t;
        else
        {
            var threads = await _client.ThreadsAsync().ConfigureAwait(false);
            if (threads.Count == 0) return;
            thread = threads[0].Id;
        }
        IReadOnlyList<DapStackFrame> frames = [];
        try { frames = await _client.StackTraceAsync(thread).ConfigureAwait(false); }
        catch (DapException) { /* a thread that already resumed has no stack */ }
        if (State == DebugState.Terminated) return;
        CurrentThreadId = thread;
        Frames = frames;
        SetState(DebugState.Stopped);
        Stopped?.Invoke(new DebugStop(e.Reason, e.Description ?? e.Text, thread, frames));
    }

    private async Task RaiseTerminatedAsync(string? error)
    {
        if (Interlocked.Exchange(ref _terminatedRaised, 1) == 1) return;
        SetState(DebugState.Terminated);
        Terminated?.Invoke(new DebugTermination(_exitCode, error));
        // The debuggee is gone; ask the adapter to leave, then make sure it did.
        _ = Task.Run(async () =>
        {
            try { if (_client is not null && !_client.Connection.Closed.IsCompleted) await _client.DisconnectAsync(false).ConfigureAwait(false); }
            catch (DapException) { }
            try { if (_root is not null && _root != _client && !_root.Connection.Closed.IsCompleted) await _root.DisconnectAsync(false).ConfigureAwait(false); }
            catch (DapException) { }
            await Task.Delay(500).ConfigureAwait(false);
            _process?.Kill();
        });
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void SetState(DebugState s)
    {
        if (State == s) return;
        State = s;
        StateChanged?.Invoke(s);
    }

    // ---------------------------------------------------------------- breakpoints

    private List<KeyValuePair<string, IReadOnlyList<SourceBreakpoint>>> SnapshotBreakpoints()
    {
        lock (_gate) return [.. _breakpoints];
    }

    /// <summary>Replaces the breakpoints of one file (sent at once when the session is live). Returns the adapter's verdicts, empty before start.</summary>
    public async Task<IReadOnlyList<DapBreakpoint>> SetBreakpointsAsync(string path, IReadOnlyList<SourceBreakpoint> breakpoints, CancellationToken ct = default)
    {
        lock (_gate) _breakpoints[path] = breakpoints;
        if (_client is null || State is DebugState.NotStarted or DebugState.Terminated) return [];
        if (State == DebugState.Starting && !_initialized.Task.IsCompleted) return []; // sent by the handshake
        return await SendBreakpointsAsync(path, breakpoints, ct).ConfigureAwait(false);
    }

    private Task<IReadOnlyList<DapBreakpoint>> SendBreakpointsAsync(string path, IReadOnlyList<SourceBreakpoint> list, CancellationToken ct)
    {
        var caps = _client!.Capabilities;
        // Conditions the adapter cannot evaluate are dropped rather than sent: the verdict then says the line stops unconditionally.
        var usable = list.Select(b => b with
        {
            Condition = caps.SupportsConditionalBreakpoints ? b.Condition : null,
            HitCondition = caps.SupportsHitConditionalBreakpoints ? b.HitCondition : null,
            LogMessage = caps.SupportsLogPoints ? b.LogMessage : null,
        }).ToList();
        return _client.SetBreakpointsAsync(path, usable, ct);
    }

    // ---------------------------------------------------------------- execution control

    private int Thread => CurrentThreadId ?? throw new InvalidOperationException("The program is not stopped.");

    private async Task ResumeAsync(Func<int, Task> request)
    {
        if (State != DebugState.Stopped) return;
        int thread = Thread;
        Frames = [];
        SetState(DebugState.Running);
        Resumed?.Invoke();
        await request(thread).ConfigureAwait(false);
    }

    public Task ContinueAsync() => ResumeAsync(t => _client!.ContinueAsync(t));
    public Task StepOverAsync() => ResumeAsync(t => _client!.NextAsync(t));
    public Task StepIntoAsync() => ResumeAsync(t => _client!.StepInAsync(t));
    public Task StepOutAsync() => ResumeAsync(t => _client!.StepOutAsync(t));

    public async Task PauseAsync()
    {
        if (State != DebugState.Running || _client is null) return;
        var threads = await _client.ThreadsAsync().ConfigureAwait(false);
        if (threads.Count > 0) await _client.PauseAsync(CurrentThreadId ?? threads[0].Id).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- inspection (only meaningful while stopped)

    public Task<IReadOnlyList<DapScope>> GetScopesAsync(int frameId, CancellationToken ct = default) => _client!.ScopesAsync(frameId, ct);
    public Task<IReadOnlyList<DapVariable>> GetVariablesAsync(int variablesReference, CancellationToken ct = default) => _client!.VariablesAsync(variablesReference, ct);

    /// <param name="context"><c>watch</c>, <c>hover</c> or <c>repl</c>.</param>
    public Task<DapEvaluateResult> EvaluateAsync(string expression, int? frameId, string context, CancellationToken ct = default) =>
        _client!.EvaluateAsync(expression, frameId, context, ct);

    // ---------------------------------------------------------------- end

    /// <summary>Ends the run: disconnect (terminating the debuggee), then kill the adapter if it lingers.</summary>
    public async Task StopAsync()
    {
        _stopping = true;
        if (_client is not null && State != DebugState.Terminated)
        {
            try
            {
                if (Capabilities.SupportsTerminateRequest && State == DebugState.Running) await _client.TerminateAsync().ConfigureAwait(false);
            }
            catch (DapException) { }
            try { await _client.DisconnectAsync(terminateDebuggee: true).ConfigureAwait(false); }
            catch (DapException) { }
            if (_root is not null && _root != _client)
            {
                try { await _root.DisconnectAsync(terminateDebuggee: true).ConfigureAwait(false); }
                catch (DapException) { }
            }
        }
        await RaiseTerminatedAsync(null).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _stopping = true;
        _process?.Kill();
        _work.Writer.TryComplete();
        if (_conn is not null) await _conn.DisposeAsync().ConfigureAwait(false);
        DapConnection[] children;
        lock (_gate) { children = [.. _children]; _children.Clear(); }
        foreach (var child in children) await child.DisposeAsync().ConfigureAwait(false);
        if (_process is not null) await _process.DisposeAsync().ConfigureAwait(false);
        if (_worker is not null) await Task.WhenAny(_worker, Task.Delay(2000)).ConfigureAwait(false);
        _conn = null;
        _process = null;
        if (State != DebugState.Terminated) SetState(DebugState.Terminated);
    }
}

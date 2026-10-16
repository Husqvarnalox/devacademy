using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using DevAcademy.Notebooks.Nbformat;
using DevAcademy.Notebooks.Protocol;
using DevAcademy.Notebooks.Transport;

namespace DevAcademy.Notebooks.Kernel;

public sealed record KernelSessionOptions
{
    /// <summary>How long the kernel may take to bind its ports and answer kernel_info (Python + ipykernel import is slow on a cold disk).</summary>
    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(3);
    /// <summary>Consecutive missed heartbeats after which the kernel is declared dead.</summary>
    public int MaxMissedHeartbeats { get; init; } = 3;
    /// <summary>How long a kernel gets to exit after shutdown_request before it is killed.</summary>
    public TimeSpan ShutdownGrace { get; init; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// The Jupyter messaging protocol client: one kernel process, five ZeroMQ channels (shell, iopub, stdin, control, heartbeat),
/// message signing, request/reply correlation by <c>parent_header.msg_id</c>, interrupt, restart, shutdown and dead-kernel detection.
/// </summary>
public sealed class KernelSession(IKernelHost host, KernelSessionOptions? options = null) : IKernelClient
{
    private static readonly byte[] Ping = "ping"u8.ToArray();
    private readonly KernelSessionOptions _options = options ?? new();
    private readonly object _gate = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private Instance? _current;
    private KernelState _state = KernelState.NotStarted;
    private string? _deadReason;
    private int _dropped;
    private bool _disposed;

    public KernelState State { get { lock (_gate) return _state; } }
    public string? DeadReason { get { lock (_gate) return _deadReason; } }
    public event Action<KernelState>? StateChanged;

    /// <summary>Messages dropped because their signature was wrong or they were malformed.</summary>
    public int DroppedMessages => Volatile.Read(ref _dropped);
    public int? ProcessId => _current?.Process.ProcessId;
    public ConnectionInfo? Connection => _current?.Connection;
    /// <summary>The kernel_info_reply content of the running kernel (implementation, language_info, protocol_version).</summary>
    public JsonObject? KernelInfo => _current?.Info;

    private void SetState(KernelState state, string? deadReason = null)
    {
        lock (_gate)
        {
            if (_state == state && deadReason is null) return;
            if (_disposed && state != KernelState.Stopped) return;
            _state = state;
            _deadReason = state == KernelState.Dead ? deadReason : null;
        }
        StateChanged?.Invoke(state);
    }

    // ------------------------------------------------------------------ start / restart / shutdown

    public async Task StartAsync(CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_current is not null) throw new InvalidOperationException("The kernel is already started.");
            await StartInstanceAsync(KernelState.Starting, ct).ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task StartInstanceAsync(KernelState during, CancellationToken ct)
    {
        SetState(during);
        var connection = ConnectionInfo.CreateRandom();
        var process = await host.StartAsync(connection, ct).ConfigureAwait(false);
        var instance = new Instance(process, connection);
        _current = instance;
        process.Exited += () => OnProcessExited(instance);
        try
        {
            using var startCts = CancellationTokenSource.CreateLinkedTokenSource(ct, instance.ProcessGone.Token);
            startCts.CancelAfter(_options.StartTimeout);
            await ConnectAsync(instance, startCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ZmtpException or IOException)
        {
            var reason = DescribeFailure(instance, ct.IsCancellationRequested);
            await DisposeInstanceAsync(instance, kill: true).ConfigureAwait(false);
            _current = null;
            SetState(ct.IsCancellationRequested ? KernelState.NotStarted : KernelState.Dead, reason);
            if (ct.IsCancellationRequested) throw;
            throw new KernelDeadException(reason);
        }
        SetState(KernelState.Idle);
    }

    private static string DescribeFailure(Instance i, bool cancelled)
    {
        if (cancelled) return "cancelled";
        var sb = new StringBuilder();
        if (i.Process.HasExited) sb.Append($"Процесс ядра завершился при запуске (код {i.Process.ExitCode}).");
        else sb.Append("Ядро не ответило вовремя.");
        var tail = i.Process.StderrTail.Trim();
        if (tail.Length > 0) sb.Append('\n').Append(tail.Length > 1500 ? tail[^1500..] : tail);
        return sb.ToString();
    }

    private async Task ConnectAsync(Instance i, CancellationToken ct)
    {
        var c = i.Connection;
        var timeout = _options.StartTimeout;
        i.Shell = await ZmtpSocket.ConnectAsync(c.Ip, c.ShellPort, "DEALER", timeout, ct).ConfigureAwait(false);
        i.Iopub = await ZmtpSocket.ConnectAsync(c.Ip, c.IopubPort, "SUB", timeout, ct).ConfigureAwait(false);
        i.Control = await ZmtpSocket.ConnectAsync(c.Ip, c.ControlPort, "DEALER", timeout, ct).ConfigureAwait(false);
        i.Stdin = await ZmtpSocket.ConnectAsync(c.Ip, c.StdinPort, "DEALER", timeout, ct).ConfigureAwait(false);
        await i.Iopub.SubscribeAsync([], ct).ConfigureAwait(false);

        i.Loops.Add(Task.Run(() => ShellLoopAsync(i)));
        i.Loops.Add(Task.Run(() => IopubLoopAsync(i)));
        i.Loops.Add(Task.Run(() => ControlLoopAsync(i)));
        i.Loops.Add(Task.Run(() => StdinLoopAsync(i)));

        // The kernel may still be importing; ask until it answers. The idle status of our own request proves IOPub is subscribed
        // (messages published before a SUB socket is attached would be lost).
        while (!i.InfoReceived.Task.IsCompleted)
        {
            ct.ThrowIfCancellationRequested();
            var request = JupyterMessage.KernelInfoRequest(i.SessionId);
            i.InfoIds.TryAdd(request.MsgId, 0);
            await SendAsync(i.Shell, request, i, ct).ConfigureAwait(false);
            await Task.WhenAny(i.InfoReceived.Task, Task.Delay(1000, ct)).ConfigureAwait(false);
        }
        await Task.WhenAny(i.InfoIdle.Task, Task.Delay(3000, ct)).ConfigureAwait(false);
        i.Loops.Add(Task.Run(() => HeartbeatLoopAsync(i)));
    }

    public async Task RestartAsync(CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            SetState(KernelState.Restarting);
            if (_current is { } old)
            {
                await StopInstanceAsync(old).ConfigureAwait(false);
                _current = null;
            }
            await StartInstanceAsync(KernelState.Restarting, ct).ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }

    public async Task ShutdownAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_current is { } inst)
            {
                await StopInstanceAsync(inst).ConfigureAwait(false);
                _current = null;
            }
            SetState(KernelState.Stopped);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task StopInstanceAsync(Instance i)
    {
        i.Stopping = true;
        if (!i.Process.HasExited && i.Control is not null)
        {
            try
            {
                var request = JupyterMessage.ShutdownRequest(i.SessionId, restart: false);
                var reply = i.Expect(request.MsgId);
                await SendAsync(i.Control, request, i, CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAny(reply.Task, Task.Delay(2000)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ZmtpException or IOException or ObjectDisposedException) { }
        }
        var deadline = DateTime.UtcNow + _options.ShutdownGrace;
        while (!i.Process.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25).ConfigureAwait(false);
        await DisposeInstanceAsync(i, kill: true).ConfigureAwait(false);
    }

    private async Task DisposeInstanceAsync(Instance i, bool kill)
    {
        i.Stopping = true;
        i.FailAll(new KernelDeadException("Ядро остановлено."));
        i.Cts.Cancel();
        if (kill && !i.Process.HasExited)
        {
            i.Process.Kill();
            var until = DateTime.UtcNow.AddSeconds(3);
            while (!i.Process.HasExited && DateTime.UtcNow < until) await Task.Delay(25).ConfigureAwait(false);
        }
        foreach (var s in new[] { i.Shell, i.Iopub, i.Control, i.Stdin, i.Hb })
            if (s is not null) await s.DisposeAsync().ConfigureAwait(false);
        try { await Task.WhenAll(i.Loops).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or ZmtpException or IOException or ObjectDisposedException) { }
        i.Process.Dispose();
        i.ProcessGone.Dispose();
    }

    private void OnProcessExited(Instance i)
    {
        try { i.ProcessGone.Cancel(); }
        catch (ObjectDisposedException) { return; }
        if (i.Stopping) return;
        MarkDead(i, $"Процесс ядра завершился (код {i.Process.ExitCode})." + (i.Process.StderrTail.Trim() is { Length: > 0 } t ? "\n" + (t.Length > 1500 ? t[^1500..] : t) : ""));
    }

    private void MarkDead(Instance i, string reason)
    {
        if (i.Stopping || !ReferenceEquals(_current, i)) return;
        if (State is KernelState.Dead or KernelState.Stopped or KernelState.Restarting) return;
        i.FailAll(new KernelDeadException(reason));
        SetState(KernelState.Dead, reason);
    }

    // ------------------------------------------------------------------ requests

    public async Task<ExecuteResult> ExecuteAsync(string code, ExecuteOptions? options, Action<KernelEvent>? onEvent, CancellationToken ct)
    {
        var i = _current ?? throw new InvalidOperationException("The kernel is not started.");
        if (State is KernelState.Dead) throw new KernelDeadException(DeadReason ?? "Ядро остановлено.");
        options ??= new ExecuteOptions();
        var request = JupyterMessage.ExecuteRequest(i.SessionId, code, options.Silent, options.StoreHistory, options.AllowStdin, options.UserExpressions);
        var pending = new PendingExecute(onEvent);
        i.Pending[request.MsgId] = pending;
        try
        {
            await SendAsync(i.Shell!, request, i, ct).ConfigureAwait(false);
            return await pending.Completion.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally { i.Pending.TryRemove(request.MsgId, out _); }
    }

    public async Task InterruptAsync()
    {
        var i = _current;
        if (i is null || State is KernelState.Dead or KernelState.Stopped) return;
        if (i.Process.TrySignalInterrupt()) return;
        var request = JupyterMessage.InterruptRequest(i.SessionId);
        var reply = i.Expect(request.MsgId);
        await SendAsync(i.Control!, request, i, CancellationToken.None).ConfigureAwait(false);
        await Task.WhenAny(reply.Task, Task.Delay(5000)).ConfigureAwait(false);
    }

    private Task SendAsync(ZmtpSocket? socket, JupyterMessage message, Instance i, CancellationToken ct) =>
        (socket ?? throw new InvalidOperationException("Channel not connected.")).SendAsync(WireCodec.Encode(message, i.Signer), ct);

    // ------------------------------------------------------------------ receive loops

    private async Task ShellLoopAsync(Instance i) => await ReadLoopAsync(i, i.Shell!, m =>
    {
        switch (m.MsgType)
        {
            case "execute_reply":
                if (i.Pending.TryGetValue(m.ParentMsgId, out var p)) p.OnReply(m.Content);
                break;
            case "kernel_info_reply":
                i.Info = m.Content;
                i.InfoReceived.TrySetResult();
                break;
        }
        i.Complete(m);
    }, "shell");

    private async Task ControlLoopAsync(Instance i) => await ReadLoopAsync(i, i.Control!, i.Complete, "control");

    private async Task StdinLoopAsync(Instance i) => await ReadLoopAsync(i, i.Stdin!, m =>
    {
        // stdin is not supported (allow_stdin is false); a kernel that asks anyway gets an empty answer instead of hanging.
        if (m.MsgType != "input_request") return;
        var reply = JupyterMessage.InputReply(i.SessionId, "", m.Header, m.Identities);
        _ = SendAsync(i.Stdin, reply, i, i.Cts.Token).ContinueWith(_ => { }, TaskScheduler.Default);
    }, "stdin");

    private async Task IopubLoopAsync(Instance i) => await ReadLoopAsync(i, i.Iopub!, m =>
    {
        switch (m.MsgType)
        {
            case "status":
                var st = m.Content["execution_state"]?.GetValue<string>();
                if (st == "busy") TransitionTo(KernelState.Busy);
                else if (st == "idle")
                {
                    TransitionTo(KernelState.Idle);
                    if (i.Pending.TryGetValue(m.ParentMsgId, out var p)) p.OnIdle();
                    if (i.InfoIds.ContainsKey(m.ParentMsgId)) i.InfoIdle.TrySetResult();
                }
                break;
            case "stream":
                Emit(m, new OutputEvent(NotebookOutput.StreamOutput(m.Content["name"]?.GetValue<string>() ?? "stdout", m.Content["text"]?.GetValue<string>() ?? "")));
                break;
            case "display_data":
            case "update_display_data":
                Emit(m, new OutputEvent(new NotebookOutput
                {
                    OutputType = NotebookOutput.DisplayData,
                    Data = Clone(m.Content["data"]),
                    Metadata = Clone(m.Content["metadata"]),
                }));
                break;
            case "execute_result":
                Emit(m, new OutputEvent(new NotebookOutput
                {
                    OutputType = NotebookOutput.ExecuteResult,
                    Data = Clone(m.Content["data"]),
                    Metadata = Clone(m.Content["metadata"]),
                    ExecutionCount = m.Content["execution_count"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null,
                }));
                break;
            case "error":
                Emit(m, new OutputEvent(new NotebookOutput
                {
                    OutputType = NotebookOutput.Error,
                    ErrorName = m.Content["ename"]?.GetValue<string>() ?? "",
                    ErrorValue = m.Content["evalue"]?.GetValue<string>() ?? "",
                    Traceback = (m.Content["traceback"] as JsonArray)?.Select(t => t?.GetValue<string>() ?? "").ToList() ?? [],
                }));
                break;
            case "clear_output":
                Emit(m, new ClearOutputEvent(m.Content["wait"] is JsonValue w && w.TryGetValue<bool>(out var wait) && wait));
                break;
        }

        void Emit(JupyterMessage msg, KernelEvent ev)
        {
            if (i.Pending.TryGetValue(msg.ParentMsgId, out var pending)) pending.OnEvent?.Invoke(ev);
        }
    }, "iopub");

    private static JsonObject Clone(JsonNode? n) => (n as JsonObject)?.DeepClone().AsObject() ?? new JsonObject();

    private void TransitionTo(KernelState state)
    {
        lock (_gate)
        {
            if (_state is not (KernelState.Idle or KernelState.Busy or KernelState.Starting)) return;
        }
        SetState(state);
    }

    private async Task ReadLoopAsync(Instance i, ZmtpSocket socket, Action<JupyterMessage> handle, string channel)
    {
        try
        {
            while (!i.Cts.IsCancellationRequested)
            {
                var frames = await socket.ReceiveAsync(i.Cts.Token).ConfigureAwait(false);
                JupyterMessage message;
                try { message = WireCodec.Decode(frames, i.Signer); }
                catch (Exception ex) when (ex is InvalidSignatureException or MalformedMessageException)
                {
                    Interlocked.Increment(ref _dropped);
                    continue;
                }
                try { handle(message); }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException or InvalidCastException) { Interlocked.Increment(ref _dropped); }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
        catch (Exception ex) when (ex is ZmtpException or IOException)
        {
            if (!i.Stopping) MarkDead(i, $"Соединение с ядром потеряно ({channel}): {ex.Message}");
        }
    }

    private async Task HeartbeatLoopAsync(Instance i)
    {
        var misses = 0;
        try
        {
            while (!i.Cts.IsCancellationRequested)
            {
                await Task.Delay(_options.HeartbeatInterval, i.Cts.Token).ConfigureAwait(false);
                try
                {
                    i.Hb ??= await ZmtpSocket.ConnectAsync(i.Connection.Ip, i.Connection.HbPort, "REQ", _options.HeartbeatTimeout, i.Cts.Token).ConfigureAwait(false);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(i.Cts.Token);
                    timeout.CancelAfter(_options.HeartbeatTimeout);
                    // REQ semantics: an empty delimiter frame, then the payload; REP echoes both.
                    await i.Hb.SendAsync([[], Ping], timeout.Token).ConfigureAwait(false);
                    await i.Hb.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                    misses = 0;
                }
                catch (Exception ex) when (ex is OperationCanceledException or ZmtpException or IOException)
                {
                    if (i.Cts.IsCancellationRequested) return;
                    misses++;
                    if (i.Hb is not null) { await i.Hb.DisposeAsync().ConfigureAwait(false); i.Hb = null; } // a REQ socket cannot resend: start over
                    if (misses >= _options.MaxMissedHeartbeats || i.Process.HasExited)
                    {
                        MarkDead(i, i.Process.HasExited ? "Процесс ядра завершился." : "Ядро не отвечает (потеряны сигналы heartbeat).");
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_current is { } inst) { await StopInstanceAsync(inst).ConfigureAwait(false); _current = null; }
            SetState(KernelState.Stopped);
            _disposed = true;
        }
        finally { _lifecycle.Release(); }
    }

    // ------------------------------------------------------------------ per-kernel state

    private sealed class PendingExecute(Action<KernelEvent>? onEvent)
    {
        private readonly object _lock = new();
        private ExecuteResult? _result;
        private bool _idle;
        public Action<KernelEvent>? OnEvent { get; } = onEvent;
        public TaskCompletionSource<ExecuteResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnReply(JsonObject c)
        {
            lock (_lock)
            {
                _result = new ExecuteResult(
                    c["status"]?.GetValue<string>() ?? "ok",
                    c["execution_count"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null,
                    c["ename"]?.GetValue<string>(), c["evalue"]?.GetValue<string>(),
                    (c["user_expressions"] as JsonObject)?.DeepClone().AsObject());
                TryComplete();
            }
        }

        public void OnIdle()
        {
            lock (_lock) { _idle = true; TryComplete(); }
        }

        private void TryComplete()
        {
            if (_result is not null && _idle) Completion.TrySetResult(_result);
        }
    }

    private sealed class Instance(IKernelProcess process, ConnectionInfo connection)
    {
        public IKernelProcess Process { get; } = process;
        public ConnectionInfo Connection { get; } = connection;
        public MessageSigner Signer { get; } = new(connection.Key);
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public CancellationTokenSource Cts { get; } = new();
        public CancellationTokenSource ProcessGone { get; } = new();
        public bool Stopping { get; set; }
        public ZmtpSocket? Shell, Iopub, Control, Stdin, Hb;
        public List<Task> Loops { get; } = [];
        public ConcurrentDictionary<string, PendingExecute> Pending { get; } = new();
        public ConcurrentDictionary<string, TaskCompletionSource<JupyterMessage>> Waiters { get; } = new();
        public ConcurrentDictionary<string, byte> InfoIds { get; } = new();
        public TaskCompletionSource InfoReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource InfoIdle { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public JsonObject? Info { get; set; }

        public TaskCompletionSource<JupyterMessage> Expect(string msgId)
        {
            var tcs = new TaskCompletionSource<JupyterMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            Waiters[msgId] = tcs;
            return tcs;
        }

        public void Complete(JupyterMessage m)
        {
            if (Waiters.TryRemove(m.ParentMsgId, out var tcs)) tcs.TrySetResult(m);
        }

        public void FailAll(Exception ex)
        {
            foreach (var p in Pending.Values) p.Completion.TrySetException(ex);
            foreach (var w in Waiters.Values) w.TrySetCanceled();
        }
    }
}

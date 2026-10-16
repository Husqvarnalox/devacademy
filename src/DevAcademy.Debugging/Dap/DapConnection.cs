using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using DevAcademy.LanguageServices.JsonRpc;

namespace DevAcademy.Debugging.Dap;

/// <summary>
/// The DAP wire layer over two byte streams: Content-Length framing (shared with the LSP client), one reader loop, one ordered writer
/// loop, request/response correlation by <c>seq</c> / <c>request_seq</c>, events and requests sent by the adapter (reverse requests).
/// Events are raised synchronously and in order on the reader loop; handlers must not block (the session queues them).
/// </summary>
public sealed class DapConnection : IAsyncDisposable
{
    private readonly MessageReader _reader;
    private readonly Stream _output;
    private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<int, (string Command, TaskCompletionSource<JsonElement?> Tcs)> _pending = [];
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private int _seq;
    private Task? _readLoop, _writeLoop;
    private bool _started;

    public DapConnection(Stream fromAdapter, Stream toAdapter)
    {
        _reader = new MessageReader(fromAdapter);
        _output = toAdapter;
    }

    /// <summary>Raised for every adapter event, in arrival order.</summary>
    public event Action<DapEvent>? EventReceived;

    /// <summary>Answers requests sent by the adapter. Default: every one is answered "not supported" (no runInTerminal).</summary>
    public Func<string, JsonElement, Task<ReverseReply>>? ReverseRequestHandler { get; set; }

    /// <summary>Protocol problems and other diagnostics (never throws into the loops).</summary>
    public event Action<string>? Log;

    /// <summary>Completes when the adapter closed the stream or the connection failed; pending requests are failed first.</summary>
    public Task Closed { get; private set; } = Task.CompletedTask;

    public void Start()
    {
        if (_started) throw new InvalidOperationException("already started");
        _started = true;
        _readLoop = Task.Run(ReadLoopAsync);
        _writeLoop = Task.Run(WriteLoopAsync);
        Closed = _readLoop;
    }

    /// <summary>Sends a request and returns the response body (null when the adapter sent none). Throws <see cref="DapException"/> on success=false.</summary>
    public async Task<JsonElement?> RequestAsync(string command, object? arguments, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int seq;
        lock (_gate)
        {
            if (_closedError is not null) throw new DapException(_closedError, command);
            seq = ++_seq;
            _pending[seq] = (command, tcs);
        }
        var message = new JsonObject { ["seq"] = seq, ["type"] = "request", ["command"] = command };
        if (arguments is not null) message["arguments"] = JsonSerializer.SerializeToNode(arguments, DapJson.Options);
        Enqueue(message);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) linked.CancelAfter(t);
        using var reg = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));
        try { return await tcs.Task.ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DapException($"'{command}' timed out", command);
        }
        finally { lock (_gate) _pending.Remove(seq); }
    }

    private string? _closedError;

    private void Enqueue(JsonObject message) =>
        _outbox.Writer.TryWrite(MessageWriter.Frame(System.Text.Encoding.UTF8.GetBytes(message.ToJsonString())));

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var frame in _outbox.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                await _output.WriteAsync(frame, _cts.Token).ConfigureAwait(false);
                await _output.FlushAsync(_cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            FailAll("Connection to the debug adapter was lost while writing.");
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                byte[]? body = await _reader.ReadAsync(_cts.Token).ConfigureAwait(false);
                if (body is null) break;
                try { Dispatch(body); }
                catch (JsonException ex) { Log?.Invoke("Malformed DAP message: " + ex.Message); }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidDataException or EndOfStreamException)
        {
            Log?.Invoke("DAP read loop ended: " + ex.Message);
        }
        FailAll("The debug adapter closed the connection.");
    }

    private void Dispatch(byte[] body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        switch (type)
        {
            case "response":
                HandleResponse(root);
                break;
            case "event":
                {
                    string name = root.GetProperty("event").GetString() ?? "";
                    JsonElement ebody = root.TryGetProperty("body", out var b) ? b.Clone() : default;
                    try { EventReceived?.Invoke(new DapEvent(name, ebody)); }
                    catch (Exception ex) { Log?.Invoke($"Event handler for '{name}' threw: {ex.Message}"); }
                    break;
                }
            case "request":
                {
                    int seq = root.GetProperty("seq").GetInt32();
                    string command = root.GetProperty("command").GetString() ?? "";
                    JsonElement args = root.TryGetProperty("arguments", out var a) ? a.Clone() : default;
                    _ = AnswerReverseAsync(seq, command, args);
                    break;
                }
            default:
                Log?.Invoke("DAP message of unknown type: " + type);
                break;
        }
    }

    private void HandleResponse(JsonElement root)
    {
        int requestSeq = root.TryGetProperty("request_seq", out var rs) ? rs.GetInt32() : -1;
        (string Command, TaskCompletionSource<JsonElement?> Tcs) entry;
        lock (_gate)
        {
            if (!_pending.TryGetValue(requestSeq, out entry)) { Log?.Invoke($"Response for unknown request_seq {requestSeq}"); return; }
        }
        bool success = root.TryGetProperty("success", out var s) && s.GetBoolean();
        if (success)
            entry.Tcs.TrySetResult(root.TryGetProperty("body", out var b) ? b.Clone() : null);
        else
        {
            string message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "request failed" : "request failed";
            // Adapters often put the human-readable text in body.error.format.
            if (root.TryGetProperty("body", out var eb) && eb.ValueKind == JsonValueKind.Object
                && eb.TryGetProperty("error", out var er) && er.ValueKind == JsonValueKind.Object
                && er.TryGetProperty("format", out var f) && f.GetString() is { Length: > 0 } fmt)
                message = fmt;
            entry.Tcs.TrySetException(new DapException(message, entry.Command));
        }
    }

    private async Task AnswerReverseAsync(int requestSeq, string command, JsonElement args)
    {
        ReverseReply reply;
        try
        {
            reply = ReverseRequestHandler is { } h ? await h(command, args).ConfigureAwait(false) : ReverseReply.NotSupported(command);
        }
        catch (Exception ex) { reply = new ReverseReply(false, ex.Message); }
        var message = new JsonObject
        {
            ["seq"] = Interlocked.Increment(ref _seq),
            ["type"] = "response",
            ["request_seq"] = requestSeq,
            ["success"] = reply.Success,
            ["command"] = command,
        };
        if (reply.Message is not null) message["message"] = reply.Message;
        if (reply.Body is not null) message["body"] = JsonSerializer.SerializeToNode(reply.Body, DapJson.Options);
        Enqueue(message);
    }

    private void FailAll(string reason)
    {
        List<(string Command, TaskCompletionSource<JsonElement?> Tcs)> all;
        lock (_gate)
        {
            _closedError ??= reason;
            all = [.. _pending.Values];
            _pending.Clear();
        }
        foreach (var (command, tcs) in all) tcs.TrySetException(new DapException(reason, command));
    }

    public async ValueTask DisposeAsync()
    {
        _outbox.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);
        FailAll("The connection was disposed.");
        foreach (var loop in new[] { _readLoop, _writeLoop })
            if (loop is not null) try { await Task.WhenAny(loop, Task.Delay(1000)).ConfigureAwait(false); } catch { /* loops swallow their own failures */ }
        _cts.Dispose();
    }
}

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace DevAcademy.LanguageServices.JsonRpc;

/// <summary>JSON-RPC error object returned by the peer.</summary>
public sealed class RpcException(int code, string message, JsonElement? data = null) : Exception(message)
{
    public const int MethodNotFound = -32601, InternalError = -32603, InvalidParams = -32602, RequestCancelled = -32800, ContentModified = -32801, ServerCancelled = -32802;
    public int Code { get; } = code;
    public JsonElement? ErrorData { get; } = data;
}

/// <summary>
/// JSON-RPC 2.0 over a byte stream pair with LSP framing. One reader loop dispatches responses (by id), notifications and
/// peer-initiated requests; one writer loop serialises all outgoing messages in the order they were issued. Never throws from the
/// loops: protocol problems go to <see cref="Log"/>.
/// </summary>
public sealed class JsonRpcConnection : IAsyncDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly MessageReader _reader;
    private readonly Stream _output;
    private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly ConcurrentDictionary<string, Func<JsonElement?, CancellationToken, Task<object?>>> _requestHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Action<JsonElement?>> _notificationHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _incoming = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readLoop, _writeLoop;
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _nextId;
    private int _disposed;

    public JsonRpcConnection(Stream input, Stream output, Action<string>? log = null)
    {
        _reader = new MessageReader(input);
        _output = output;
        Log = log;
        _readLoop = Task.Run(ReadLoopAsync);
        _writeLoop = Task.Run(WriteLoopAsync);
    }

    /// <summary>Protocol-level diagnostics (malformed frames, handler failures, unknown messages).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Completes when the input stream ended or the connection was disposed.</summary>
    public Task Closed => _closed.Task;
    public bool IsClosed => _closed.Task.IsCompleted;

    public void OnRequest(string method, Func<JsonElement?, CancellationToken, Task<object?>> handler) => _requestHandlers[method] = handler;
    public void OnNotification(string method, Action<JsonElement?> handler) => _notificationHandlers[method] = handler;

    public async Task<JsonElement> RequestAsync(string method, object? parameters, CancellationToken ct = default)
    {
        if (IsClosed) throw new IOException("The connection is closed.");
        long id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        // If the connection dies the pending request is failed by the read loop's cleanup.
        using CancellationTokenRegistration reg = ct.Register(() =>
        {
            if (_pending.TryRemove(id, out var t))
            {
                t.TrySetCanceled(ct);
                try { Send(Envelope("$/cancelRequest", new { id }, null)); } catch (ObjectDisposedException) { }
            }
        });
        Send(Envelope(method, parameters, id));
        return await tcs.Task.ConfigureAwait(false);
    }

    public async Task<T?> RequestAsync<T>(string method, object? parameters, CancellationToken ct = default)
    {
        JsonElement r = await RequestAsync(method, parameters, ct).ConfigureAwait(false);
        return r.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? default : r.Deserialize<T>(Json);
    }

    public Task NotifyAsync(string method, object? parameters)
    {
        if (IsClosed) return Task.CompletedTask;
        Send(Envelope(method, parameters, null));
        return Task.CompletedTask;
    }

    private void Send(byte[] framed)
    {
        if (!_outbox.Writer.TryWrite(framed)) throw new ObjectDisposedException(nameof(JsonRpcConnection));
    }

    private static byte[] Envelope(string method, object? parameters, long? id)
    {
        var dict = new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["method"] = method };
        if (id is { } i) dict["id"] = i;
        if (parameters is not null) dict["params"] = parameters;
        return MessageWriter.Frame(JsonSerializer.SerializeToUtf8Bytes(dict, Json));
    }

    private void SendResponse(JsonElement id, object? result, RpcException? error)
    {
        var dict = new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = id };
        if (error is not null) dict["error"] = new { code = error.Code, message = error.Message };
        else dict["result"] = result; // null must be serialised as null, not omitted
        try
        {
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions(Json) { DefaultIgnoreCondition = JsonIgnoreCondition.Never });
            Send(MessageWriter.Frame(body));
        }
        catch (ObjectDisposedException) { }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (byte[] framed in _outbox.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await _output.WriteAsync(framed).ConfigureAwait(false);
                await _output.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Log?.Invoke($"write loop ended: {ex.Message}");
            Fail();
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (true)
            {
                byte[]? body;
                try { body = await _reader.ReadAsync(_cts.Token).ConfigureAwait(false); }
                catch (InvalidDataException ex)
                {
                    Log?.Invoke($"protocol error: {ex.Message}");
                    break; // framing is lost, nothing can be recovered
                }
                if (body is null) break;
                Dispatch(body);
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ObjectDisposedException or OperationCanceledException)
        {
            Log?.Invoke($"read loop ended: {ex.Message}");
        }
        Fail();
    }

    private void Fail()
    {
        _closed.TrySetResult();
        _outbox.Writer.TryComplete();
        foreach (var kv in _pending)
            if (_pending.TryRemove(kv.Key, out var t)) t.TrySetException(new IOException("The language server connection was closed."));
        foreach (var c in _incoming.Values) { try { c.Cancel(); } catch (ObjectDisposedException) { } }
    }

    private void Dispatch(byte[] body)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException ex) { Log?.Invoke($"invalid JSON ignored: {ex.Message}"); return; }
        using (doc)
        {
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { Log?.Invoke("non-object message ignored"); return; }
            bool hasId = root.TryGetProperty("id", out JsonElement id) && id.ValueKind != JsonValueKind.Null;
            bool hasMethod = root.TryGetProperty("method", out JsonElement m) && m.ValueKind == JsonValueKind.String;
            JsonElement? parms = root.TryGetProperty("params", out JsonElement p) ? p.Clone() : null;

            if (hasMethod)
            {
                string method = m.GetString()!;
                if (hasId) HandleRequest(method, id.Clone(), parms);
                else HandleNotification(method, parms);
            }
            else if (hasId) HandleResponse(id, root);
            else Log?.Invoke("message without id or method ignored");
        }
    }

    private void HandleResponse(JsonElement id, JsonElement root)
    {
        if (!(id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out long key)) &&
            !(id.ValueKind == JsonValueKind.String && long.TryParse(id.GetString(), out key)))
        {
            Log?.Invoke($"response with unknown id {id}");
            return;
        }
        if (!_pending.TryRemove(key, out var tcs)) return; // cancelled or duplicate
        if (root.TryGetProperty("error", out JsonElement err) && err.ValueKind == JsonValueKind.Object)
        {
            int code = err.TryGetProperty("code", out var c) && c.TryGetInt32(out int ci) ? ci : RpcException.InternalError;
            string msg = err.TryGetProperty("message", out var mm) && mm.ValueKind == JsonValueKind.String ? mm.GetString()! : "error";
            tcs.TrySetException(new RpcException(code, msg, err.TryGetProperty("data", out var d) ? d.Clone() : null));
        }
        else tcs.TrySetResult(root.TryGetProperty("result", out var r) ? r.Clone() : default);
    }

    private void HandleNotification(string method, JsonElement? parms)
    {
        if (method == "$/cancelRequest" && parms is { ValueKind: JsonValueKind.Object } cp && cp.TryGetProperty("id", out var cid))
        {
            if (_incoming.TryGetValue(cid.ToString(), out var cts)) { try { cts.Cancel(); } catch (ObjectDisposedException) { } }
            return;
        }
        if (!_notificationHandlers.TryGetValue(method, out var handler)) return;
        try { handler(parms); }
        catch (Exception ex) { Log?.Invoke($"notification handler '{method}' failed: {ex.Message}"); }
    }

    private void HandleRequest(string method, JsonElement id, JsonElement? parms)
    {
        if (!_requestHandlers.TryGetValue(method, out var handler))
        {
            Log?.Invoke($"unhandled server request '{method}'");
            SendResponse(id, null, new RpcException(RpcException.MethodNotFound, $"Method not found: {method}"));
            return;
        }
        string key = id.ToString();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _incoming[key] = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                object? result = await handler(parms, cts.Token).ConfigureAwait(false);
                SendResponse(id, result, null);
            }
            catch (OperationCanceledException) { SendResponse(id, null, new RpcException(RpcException.RequestCancelled, "cancelled")); }
            catch (RpcException rex) { SendResponse(id, null, rex); }
            catch (Exception ex)
            {
                Log?.Invoke($"request handler '{method}' failed: {ex.Message}");
                SendResponse(id, null, new RpcException(RpcException.InternalError, ex.Message));
            }
            finally
            {
                _incoming.TryRemove(key, out _);
                cts.Dispose();
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _outbox.Writer.TryComplete();
        // Let queued messages (e.g. "exit") flush, but never wait forever for a stuck peer.
        await Task.WhenAny(_writeLoop, Task.Delay(500)).ConfigureAwait(false);
        _cts.Cancel();
        Fail();
        await Task.WhenAny(_readLoop, Task.Delay(500)).ConfigureAwait(false);
    }
}

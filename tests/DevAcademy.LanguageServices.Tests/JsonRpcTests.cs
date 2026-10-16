using System.Text.Json;
using DevAcademy.LanguageServices.JsonRpc;

namespace DevAcademy.LanguageServices.Tests;

public class JsonRpcTests
{
    /// <summary>Client connection plus the raw server side (reader of what the client sends, writer of what it receives).</summary>
    private sealed class Harness : IAsyncDisposable
    {
        public PipeEnd ToServer = new(), ToClient = new();
        public JsonRpcConnection Client;
        public MessageReader ServerReader;
        public List<string> Logs = [];

        public Harness()
        {
            Client = new JsonRpcConnection(ToClient.Reader, ToServer.Writer, m => { lock (Logs) Logs.Add(m); });
            ServerReader = new MessageReader(ToServer.Reader);
        }

        public void ServerSend(string json) => ToClient.Writer.Write(Raw.Frame(json));
        public ValueTask DisposeAsync() => Client.DisposeAsync();
    }

    [Fact]
    public async Task Out_of_order_responses_are_matched_by_id()
    {
        await using var h = new Harness();
        var a = h.Client.RequestAsync("a", null);
        var b = h.Client.RequestAsync("b", null);
        using var m1 = await Raw.ReadAsync(h.ServerReader);
        using var m2 = await Raw.ReadAsync(h.ServerReader);
        long idA = m1.RootElement.GetProperty("id").GetInt64(), idB = m2.RootElement.GetProperty("id").GetInt64();
        Assert.Equal("a", m1.RootElement.GetProperty("method").GetString());
        // answer b first, in two frames glued together with a notification in between
        h.ToClient.Writer.Write(Raw.Frame($"{{\"jsonrpc\":\"2.0\",\"id\":{idB},\"result\":\"B\"}}").Concat(Raw.Frame($"{{\"jsonrpc\":\"2.0\",\"id\":{idA},\"result\":\"A\"}}")).ToArray());
        Assert.Equal("B", (await b).GetString());
        Assert.Equal("A", (await a).GetString());
    }

    [Fact]
    public async Task Error_response_becomes_RpcException()
    {
        await using var h = new Harness();
        var t = h.Client.RequestAsync("x", null);
        using var m = await Raw.ReadAsync(h.ServerReader);
        h.ServerSend($"{{\"jsonrpc\":\"2.0\",\"id\":{m.RootElement.GetProperty("id").GetInt64()},\"error\":{{\"code\":-32801,\"message\":\"content modified\"}}}}");
        var ex = await Assert.ThrowsAsync<RpcException>(() => t);
        Assert.Equal(RpcException.ContentModified, ex.Code);
        Assert.Equal("content modified", ex.Message);
    }

    [Fact]
    public async Task Cancelling_a_request_sends_cancelRequest_and_ignores_the_late_response()
    {
        await using var h = new Harness();
        using var cts = new CancellationTokenSource();
        var t = h.Client.RequestAsync("slow", null, cts.Token);
        using var m = await Raw.ReadAsync(h.ServerReader);
        long id = m.RootElement.GetProperty("id").GetInt64();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
        using var c = await Raw.ReadAsync(h.ServerReader);
        Assert.Equal("$/cancelRequest", c.RootElement.GetProperty("method").GetString());
        Assert.Equal(id, c.RootElement.GetProperty("params").GetProperty("id").GetInt64());
        h.ServerSend($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":1}}"); // must not throw or log an error
        var again = h.Client.RequestAsync("next", null);
        using var n = await Raw.ReadAsync(h.ServerReader);
        h.ServerSend($"{{\"jsonrpc\":\"2.0\",\"id\":{n.RootElement.GetProperty("id").GetInt64()},\"result\":2}}");
        Assert.Equal(2, (await again).GetInt32());
    }

    [Fact]
    public async Task Notifications_reach_handlers_and_handler_failures_are_logged_not_fatal()
    {
        await using var h = new Harness();
        var got = new TaskCompletionSource<string>();
        h.Client.OnNotification("boom", _ => throw new InvalidOperationException("bad handler"));
        h.Client.OnNotification("note", p => got.TrySetResult(p!.Value.GetProperty("v").GetString()!));
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"method\":\"boom\",\"params\":{}}");
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"method\":\"note\",\"params\":{\"v\":\"привет\"}}");
        Assert.Equal("привет", await got.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains(h.Logs, l => l.Contains("boom") && l.Contains("bad handler"));
    }

    [Fact]
    public async Task Server_request_is_answered_with_the_handler_result_including_null()
    {
        await using var h = new Harness();
        h.Client.OnRequest("workspace/configuration", (_, _) => Task.FromResult<object?>(new object?[] { null, new { a = 1 } }));
        h.Client.OnRequest("window/workDoneProgress/create", (_, _) => Task.FromResult<object?>(null));
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"id\":\"cfg-1\",\"method\":\"workspace/configuration\",\"params\":{\"items\":[{},{}]}}");
        using var r1 = await Raw.ReadAsync(h.ServerReader);
        Assert.Equal("cfg-1", r1.RootElement.GetProperty("id").GetString());
        Assert.Equal(2, r1.RootElement.GetProperty("result").GetArrayLength());
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"window/workDoneProgress/create\",\"params\":{\"token\":1}}");
        using var r2 = await Raw.ReadAsync(h.ServerReader);
        Assert.Equal(7, r2.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, r2.RootElement.GetProperty("result").ValueKind);   // "result": null must be present
    }

    [Fact]
    public async Task Unknown_server_request_gets_method_not_found_and_failing_handler_gets_internal_error()
    {
        await using var h = new Harness();
        h.Client.OnRequest("fails", (_, _) => throw new InvalidOperationException("nope"));
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"mystery\",\"params\":{}}");
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"fails\"}");
        var codes = new Dictionary<int, int>();
        for (int i = 0; i < 2; i++)
        {
            using var r = await Raw.ReadAsync(h.ServerReader);
            codes[r.RootElement.GetProperty("id").GetInt32()] = r.RootElement.GetProperty("error").GetProperty("code").GetInt32();
        }
        Assert.Equal(RpcException.MethodNotFound, codes[1]);
        Assert.Equal(RpcException.InternalError, codes[2]);
    }

    [Fact]
    public async Task Server_cancelRequest_cancels_the_running_handler()
    {
        await using var h = new Harness();
        var started = new TaskCompletionSource();
        h.Client.OnRequest("long", async (_, ct) => { started.SetResult(); await Task.Delay(Timeout.Infinite, ct); return null; });
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"long\"}");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.ServerSend("{\"jsonrpc\":\"2.0\",\"method\":\"$/cancelRequest\",\"params\":{\"id\":5}}");
        using var r = await Raw.ReadAsync(h.ServerReader);
        Assert.Equal(RpcException.RequestCancelled, r.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Malformed_json_is_logged_and_the_stream_continues()
    {
        await using var h = new Harness();
        h.ServerSend("{ this is not json");
        h.ServerSend("[1,2,3]");
        var t = h.Client.RequestAsync("ping", null);
        using var m = await Raw.ReadAsync(h.ServerReader);
        h.ServerSend($"{{\"jsonrpc\":\"2.0\",\"id\":{m.RootElement.GetProperty("id").GetInt64()},\"result\":true}}");
        Assert.True((await t).GetBoolean());
        Assert.Contains(h.Logs, l => l.Contains("invalid JSON"));
    }

    [Fact]
    public async Task Closing_the_stream_fails_pending_requests_and_completes_Closed()
    {
        await using var h = new Harness();
        var t = h.Client.RequestAsync("never", null);
        await Raw.ReadAsync(h.ServerReader);
        h.ToClient.Complete();
        await Assert.ThrowsAsync<IOException>(() => t);
        await h.Client.Closed.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<IOException>(() => h.Client.RequestAsync("late", null));
    }

    [Fact]
    public async Task Messages_are_written_in_the_order_they_were_issued_with_utf8_lengths()
    {
        await using var h = new Harness();
        for (int i = 0; i < 50; i++) _ = h.Client.NotifyAsync("n", new { i, text = "жжж😀" });
        for (int i = 0; i < 50; i++)
        {
            using var m = await Raw.ReadAsync(h.ServerReader);
            Assert.Equal(i, m.RootElement.GetProperty("params").GetProperty("i").GetInt32());
            Assert.Equal("жжж😀", m.RootElement.GetProperty("params").GetProperty("text").GetString());
        }
    }
}

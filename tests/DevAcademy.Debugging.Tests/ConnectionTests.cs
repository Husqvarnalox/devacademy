using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevAcademy.Debugging.Dap;
using DevAcademy.LanguageServices.JsonRpc;

namespace DevAcademy.Debugging.Tests;

/// <summary>The DAP wire layer: framing, seq / request_seq correlation, events, reverse requests.</summary>
public class ConnectionTests
{
    private sealed class Harness : IAsyncDisposable
    {
        public PipeEnd ToAdapter = new(), FromAdapter = new();
        public DapConnection Conn;
        public MessageReader AdapterSide;
        public Harness()
        {
            Conn = new DapConnection(FromAdapter.Reader, ToAdapter.Writer);
            AdapterSide = new MessageReader(ToAdapter.Reader);
            Conn.Start();
        }
        public async Task<JsonNode> ReadRequestAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return JsonNode.Parse((await AdapterSide.ReadAsync(cts.Token))!)!;
        }
        public void Reply(JsonNode request, JsonObject? body = null, bool ok = true, string? message = null)
        {
            var r = new JsonObject { ["seq"] = 100, ["type"] = "response", ["request_seq"] = (int)request["seq"]!, ["success"] = ok, ["command"] = (string)request["command"]! };
            if (body is not null) r["body"] = body;
            if (message is not null) r["message"] = message;
            FromAdapter.Writer.Write(MessageWriter.Frame(Encoding.UTF8.GetBytes(r.ToJsonString())));
        }
        public ValueTask DisposeAsync() => Conn.DisposeAsync();
    }

    [Fact]
    public async Task Request_gets_increasing_seq_and_matching_response_body()
    {
        await using var h = new Harness();
        var t1 = h.Conn.RequestAsync("threads", null);
        var t2 = h.Conn.RequestAsync("scopes", new { frameId = 3 });
        var r1 = await h.ReadRequestAsync();
        var r2 = await h.ReadRequestAsync();
        Assert.Equal("request", (string?)r1["type"]);
        Assert.Equal(1, (int)r1["seq"]!);
        Assert.Equal(2, (int)r2["seq"]!);
        Assert.Equal(3, (int)r2["arguments"]!["frameId"]!);

        // Out-of-order answers still reach the right caller.
        h.Reply(r2, new JsonObject { ["marker"] = "second" });
        h.Reply(r1, new JsonObject { ["marker"] = "first" });
        Assert.Equal("first", (await t1)!.Value.GetProperty("marker").GetString());
        Assert.Equal("second", (await t2)!.Value.GetProperty("marker").GetString());
    }

    [Fact]
    public async Task Failed_response_throws_with_the_adapters_message()
    {
        await using var h = new Harness();
        var t = h.Conn.RequestAsync("evaluate", new { expression = "boom" });
        h.Reply(await h.ReadRequestAsync(), ok: false, message: "name 'boom' is not defined");
        var ex = await Assert.ThrowsAsync<DapException>(() => t);
        Assert.Equal("name 'boom' is not defined", ex.Message);
        Assert.Equal("evaluate", ex.Command);
    }

    [Fact]
    public async Task Error_body_format_is_preferred_over_the_short_message()
    {
        await using var h = new Harness();
        var t = h.Conn.RequestAsync("launch", new { });
        var req = await h.ReadRequestAsync();
        var body = new JsonObject { ["error"] = new JsonObject { ["id"] = 1, ["format"] = "Program 'x' does not exist" } };
        h.Reply(req, body, ok: false, message: "Failed");
        var ex = await Assert.ThrowsAsync<DapException>(() => t);
        Assert.Equal("Program 'x' does not exist", ex.Message);
    }

    [Fact]
    public async Task Events_arrive_in_order_even_when_several_share_one_read_and_headers_are_split()
    {
        await using var h = new Harness();
        var names = new List<string>();
        h.Conn.EventReceived += e => { lock (names) names.Add(e.Name); };
        byte[] Ev(string n, string body = "{}") => MessageWriter.Frame(Encoding.UTF8.GetBytes($"{{\"seq\":1,\"type\":\"event\",\"event\":\"{n}\",\"body\":{body}}}"));
        var all = Ev("initialized").Concat(Ev("output", "{\"output\":\"é€\"}")).Concat(Ev("terminated")).ToArray();
        // One byte at a time: headers and bodies split at every possible position.
        foreach (byte b in all) h.FromAdapter.Writer.Write([b]);
        await Util.UntilAsync(() => { lock (names) return names.Count == 3; }, "three events");
        Assert.Equal(["initialized", "output", "terminated"], names);
    }

    [Fact]
    public async Task Multibyte_body_length_is_counted_in_bytes()
    {
        await using var h = new Harness();
        var t = h.Conn.RequestAsync("evaluate", new { expression = "'Привет €'" });
        var req = await h.ReadRequestAsync();
        Assert.Equal("'Привет €'", (string?)req["arguments"]!["expression"]);
        h.Reply(req, new JsonObject { ["result"] = "'Привет €'" });
        Assert.Equal("'Привет €'", (await t)!.Value.GetProperty("result").GetString());
    }

    [Fact]
    public async Task Requests_from_the_adapter_are_answered_not_supported_by_default()
    {
        await using var h = new Harness();
        var rt = Encoding.UTF8.GetBytes("""{"seq":9,"type":"request","command":"runInTerminal","arguments":{"args":["a"]}}""");
        h.FromAdapter.Writer.Write(MessageWriter.Frame(rt));
        var reply = await h.ReadRequestAsync();
        Assert.Equal("response", (string?)reply["type"]);
        Assert.Equal(9, (int)reply["request_seq"]!);
        Assert.False((bool)reply["success"]!);
        Assert.Equal("runInTerminal", (string?)reply["command"]);
        Assert.Contains("not supported", (string?)reply["message"]);
    }

    [Fact]
    public async Task A_custom_reverse_handler_can_answer()
    {
        await using var h = new Harness();
        h.Conn.ReverseRequestHandler = (cmd, args) => Task.FromResult(new ReverseReply(true, Body: new { processId = 42 }));
        h.FromAdapter.Writer.Write(MessageWriter.Frame(Encoding.UTF8.GetBytes("""{"seq":3,"type":"request","command":"runInTerminal"}""")));
        var reply = await h.ReadRequestAsync();
        Assert.True((bool)reply["success"]!);
        Assert.Equal(42, (int)reply["body"]!["processId"]!);
    }

    [Fact]
    public async Task Pending_requests_fail_when_the_adapter_closes_the_stream()
    {
        var h = new Harness();
        var t = h.Conn.RequestAsync("threads", null);
        await h.ReadRequestAsync();
        h.FromAdapter.Complete();
        var ex = await Assert.ThrowsAsync<DapException>(() => t);
        Assert.Contains("closed", ex.Message);
        await h.Conn.Closed;
        // A request after the close fails immediately instead of hanging.
        await Assert.ThrowsAsync<DapException>(() => h.Conn.RequestAsync("threads", null));
        await h.DisposeAsync();
    }

    [Fact]
    public async Task A_request_times_out_with_a_clear_error_and_does_not_leak()
    {
        await using var h = new Harness();
        var ex = await Assert.ThrowsAsync<DapException>(() => h.Conn.RequestAsync("threads", null, timeout: TimeSpan.FromMilliseconds(80)));
        Assert.Contains("timed out", ex.Message);
    }

    [Fact]
    public async Task A_response_for_an_unknown_request_is_logged_and_ignored()
    {
        await using var h = new Harness();
        var log = new List<string>();
        h.Conn.Log += log.Add;
        h.FromAdapter.Writer.Write(MessageWriter.Frame(Encoding.UTF8.GetBytes("""{"seq":1,"type":"response","request_seq":77,"success":true,"command":"x"}""")));
        await Util.UntilAsync(() => log.Count > 0, "log line");
        Assert.Contains("77", log[0]);
        // The connection still works.
        var t = h.Conn.RequestAsync("threads", null);
        h.Reply(await h.ReadRequestAsync(), new JsonObject());
        await t;
    }

    [Fact]
    public async Task Typed_client_parses_stopped_output_and_exit_events()
    {
        await using var h = new Harness();
        var client = new DapClient(h.Conn);
        StoppedEventArgs? stopped = null; OutputEventArgs? output = null; int? exit = null; bool terminated = false;
        client.Stopped += e => stopped = e;
        client.Output += e => output = e;
        client.Exited += c => exit = c;
        client.Terminated += () => terminated = true;
        void Send(string json) => h.FromAdapter.Writer.Write(MessageWriter.Frame(Encoding.UTF8.GetBytes(json)));
        Send("""{"seq":1,"type":"event","event":"stopped","body":{"reason":"breakpoint","threadId":4,"allThreadsStopped":true,"hitBreakpointIds":[2]}}""");
        Send("""{"seq":2,"type":"event","event":"output","body":{"category":"stderr","output":"oops\n"}}""");
        Send("""{"seq":3,"type":"event","event":"exited","body":{"exitCode":3}}""");
        Send("""{"seq":4,"type":"event","event":"terminated"}""");
        await Util.UntilAsync(() => terminated, "terminated");
        Assert.Equal("breakpoint", stopped!.Reason);
        Assert.Equal(4, stopped.ThreadId);
        Assert.True(stopped.AllThreadsStopped);
        Assert.Equal([2], stopped.HitBreakpointIds);
        Assert.Equal(("stderr", "oops\n"), (output!.Category, output.Text));
        Assert.Equal(3, exit);
    }

    [Fact]
    public async Task Typed_requests_serialize_dap_argument_names()
    {
        await using var h = new Harness();
        var client = new DapClient(h.Conn);
        var t = client.SetBreakpointsAsync("/p/a.py", [new SourceBreakpoint(7, Condition: "i == 2")]);
        var req = await h.ReadRequestAsync();
        Assert.Equal("setBreakpoints", (string?)req["command"]);
        Assert.Equal("/p/a.py", (string?)req["arguments"]!["source"]!["path"]);
        Assert.Equal(7, (int)req["arguments"]!["breakpoints"]![0]!["line"]!);
        Assert.Equal("i == 2", (string?)req["arguments"]!["breakpoints"]![0]!["condition"]);
        h.Reply(req, new JsonObject { ["breakpoints"] = new JsonArray(new JsonObject { ["verified"] = true, ["line"] = 8, ["id"] = 1 }) });
        var result = await t;
        Assert.Equal(8, result[0].Line);
        Assert.True(result[0].Verified);

        var ev = client.EvaluateAsync("x", 5, "hover");
        var er = await h.ReadRequestAsync();
        Assert.Equal("hover", (string?)er["arguments"]!["context"]);
        Assert.Equal(5, (int)er["arguments"]!["frameId"]!);
        h.Reply(er, new JsonObject { ["result"] = "42", ["type"] = "int" });
        Assert.Equal("42", (await ev).Result);
    }
}

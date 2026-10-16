using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevAcademy.Debugging.Adapters;
using DevAcademy.LanguageServices.JsonRpc;

namespace DevAcademy.Debugging.Tests;

/// <summary>
/// An in-process debug adapter that plays a tiny program: a loop whose body is at <see cref="BodyLine"/>. Each `continue` runs to the
/// next iteration's breakpoint; after <see cref="Iterations"/> iterations the program exits with <see cref="ExitCode"/>.
/// </summary>
public sealed class FakeAdapter : IAdapterProcess
{
    private readonly PipeEnd _toAdapter = new(), _fromAdapter = new();
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MessageReader _reader;
    private int _seq;
    private int _iteration;
    private int _line;
    private readonly Task _loop;

    public int BodyLine { get; set; } = 5;
    public int Iterations { get; set; } = 3;
    public int ExitCode { get; set; } = 7;
    public bool InitializedBeforeLaunch { get; set; }
    public bool LaunchRespondsAfterConfigDone { get; set; } = true;
    public bool SendRunInTerminalRequest { get; set; }
    public List<string> Commands { get; } = [];
    public Dictionary<string, List<int>> Breakpoints { get; } = [];
    public JsonObject? LaunchArguments { get; private set; }
    public string? ReverseReplyMessage { get; private set; }
    private int? _pendingLaunchSeq;

    public FakeAdapter()
    {
        _reader = new MessageReader(_toAdapter.Reader);
        _loop = Task.Run(RunAsync);
    }

    public Stream FromAdapter => _fromAdapter.Reader;
    public Stream ToAdapter => _toAdapter.Writer;
    public Task Exited => _exited.Task;
    public string LogTail => "";
    public void Kill() { _exited.TrySetResult(); _fromAdapter.Complete(); _toAdapter.Complete(); }
    public ValueTask DisposeAsync() { Kill(); return ValueTask.CompletedTask; }

    private void Send(JsonObject m)
    {
        m["seq"] = Interlocked.Increment(ref _seq);
        _fromAdapter.Writer.Write(MessageWriter.Frame(Encoding.UTF8.GetBytes(m.ToJsonString())));
    }

    private void Event(string name, JsonObject? body = null) => Send(new JsonObject { ["type"] = "event", ["event"] = name, ["body"] = body ?? new JsonObject() });

    private void Respond(JsonNode req, JsonNode? body = null, bool ok = true, string? message = null)
    {
        var r = new JsonObject { ["type"] = "response", ["request_seq"] = (int)req["seq"]!, ["success"] = ok, ["command"] = (string)req["command"]! };
        if (message is not null) r["message"] = message;
        if (body is not null) r["body"] = body;
        Send(r);
    }

    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                var bytes = await _reader.ReadAsync(CancellationToken.None);
                if (bytes is null) return;
                var msg = JsonNode.Parse(bytes)!;
                if ((string?)msg["type"] == "response") { ReverseReplyMessage = (string?)msg["message"] ?? "ok"; continue; }
                string cmd = (string)msg["command"]!;
                lock (Commands) Commands.Add(cmd);
                Handle(cmd, msg);
                if (cmd == "disconnect") { await Task.Delay(20); Kill(); return; }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException) { }
    }

    private JsonObject Frame(int id, string name, int line) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["line"] = line,
        ["column"] = 1,
        ["source"] = new JsonObject { ["name"] = "main.fake", ["path"] = SourcePath ?? (string?)LaunchArguments?["program"] ?? "/work/main.fake" },
    };

    /// <summary>Path reported for frames; defaults to the launch request's <c>program</c>.</summary>
    public string? SourcePath { get; set; }

    private void StopAt(string reason, int line)
    {
        _line = line;
        Event("stopped", new JsonObject { ["reason"] = reason, ["threadId"] = 1, ["allThreadsStopped"] = true });
    }

    private void Handle(string cmd, JsonNode req)
    {
        var args = req["arguments"];
        switch (cmd)
        {
            case "initialize":
                Respond(req, new JsonObject
                {
                    ["supportsConfigurationDoneRequest"] = true,
                    ["supportsConditionalBreakpoints"] = true,
                    ["supportsTerminateRequest"] = true,
                    ["supportsEvaluateForHovers"] = true,
                });
                if (InitializedBeforeLaunch) Event("initialized");
                if (SendRunInTerminalRequest)
                    Send(new JsonObject { ["type"] = "request", ["command"] = "runInTerminal", ["arguments"] = new JsonObject { ["args"] = new JsonArray("x") } });
                break;
            case "launch":
                LaunchArguments = args!.AsObject();
                if (LaunchRespondsAfterConfigDone) _pendingLaunchSeq = (int)req["seq"]!;
                else Respond(req);
                if (!InitializedBeforeLaunch) Event("initialized");
                _launchReq = req;
                break;
            case "setBreakpoints":
                {
                    string path = (string)args!["source"]!["path"]!;
                    var lines = args["breakpoints"]!.AsArray().Select(b => (int)b!["line"]!).ToList();
                    Breakpoints[path] = lines;
                    var result = new JsonArray(lines.Select(l => (JsonNode)new JsonObject { ["verified"] = true, ["line"] = l }).ToArray());
                    Respond(req, new JsonObject { ["breakpoints"] = result });
                    break;
                }
            case "configurationDone":
                Respond(req);
                if (_pendingLaunchSeq is not null && _launchReq is not null) Respond(_launchReq);
                Event("process", new JsonObject { ["name"] = "fake" });
                Event("output", new JsonObject { ["category"] = "stdout", ["output"] = "start\n" });
                if (Breakpoints.Values.Any(v => v.Contains(BodyLine))) StopAt("breakpoint", BodyLine); else Finish();
                break;
            case "threads":
                Respond(req, new JsonObject { ["threads"] = new JsonArray(new JsonObject { ["id"] = 1, ["name"] = "main" }) });
                break;
            case "stackTrace":
                Respond(req, new JsonObject { ["stackFrames"] = new JsonArray(Frame(1000, "body", _line), Frame(1001, "main", 12)), ["totalFrames"] = 2 });
                break;
            case "scopes":
                Respond(req, new JsonObject { ["scopes"] = new JsonArray(new JsonObject { ["name"] = "Locals", ["variablesReference"] = 10, ["expensive"] = false }) });
                break;
            case "variables":
                {
                    int r = (int)args!["variablesReference"]!;
                    JsonArray vars = r == 10
                        ? new JsonArray(
                            new JsonObject { ["name"] = "i", ["value"] = _iteration.ToString(), ["type"] = "int", ["variablesReference"] = 0 },
                            new JsonObject { ["name"] = "items", ["value"] = "list(2)", ["type"] = "list", ["variablesReference"] = 11, ["indexedVariables"] = 2 })
                        : new JsonArray(
                            new JsonObject { ["name"] = "[0]", ["value"] = "'a'", ["type"] = "str", ["variablesReference"] = 0 },
                            new JsonObject { ["name"] = "[1]", ["value"] = "'b'", ["type"] = "str", ["variablesReference"] = 0 });
                    Respond(req, new JsonObject { ["variables"] = vars });
                    break;
                }
            case "evaluate":
                {
                    string e = (string)args!["expression"]!;
                    string ctx = (string)args["context"]!;
                    if (e == "boom") Respond(req, null, false, "name 'boom' is not defined");
                    else if (e == "i") Respond(req, new JsonObject { ["result"] = _iteration.ToString(), ["type"] = "int", ["variablesReference"] = 0 });
                    else Respond(req, new JsonObject { ["result"] = $"{e}@{ctx}", ["variablesReference"] = 0 });
                    break;
                }
            case "next":
            case "stepIn":
            case "stepOut":
                Respond(req);
                _line++;
                Event("continued", new JsonObject { ["threadId"] = 1 });
                StopAt("step", _line);
                break;
            case "continue":
                Respond(req, new JsonObject { ["allThreadsContinued"] = true });
                Event("continued", new JsonObject { ["threadId"] = 1 });
                _iteration++;
                if (_iteration < Iterations) StopAt("breakpoint", BodyLine); else Finish();
                break;
            case "pause":
                Respond(req);
                StopAt("pause", _line);
                break;
            case "terminate":
                Respond(req);
                Finish();
                break;
            case "disconnect":
                Respond(req);
                break;
            default:
                Respond(req, null, false, "unsupported: " + cmd);
                break;
        }
    }

    private JsonNode? _launchReq;

    private void Finish()
    {
        Event("output", new JsonObject { ["category"] = "stdout", ["output"] = "done\n" });
        Event("exited", new JsonObject { ["exitCode"] = ExitCode });
        Event("terminated");
    }

    /// <summary>Raw bytes to the client (framing tests).</summary>
    public void WriteRaw(byte[] bytes) => _fromAdapter.Writer.Write(bytes);
}

public sealed class FakeAdapterLauncher(FakeAdapter adapter) : IAdapterLauncher
{
    public FakeAdapter Adapter { get; } = adapter;
    public Task<IAdapterProcess> LaunchAsync(ResolvedAdapter a, string wd, CancellationToken ct) => Task.FromResult<IAdapterProcess>(Adapter);
}

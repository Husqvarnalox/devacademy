using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevAcademy.Debugging.Dap;

/// <summary>Typed requests and events over a <see cref="DapConnection"/>. Holds no debugging state (see DebugSession).</summary>
public sealed class DapClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private readonly DapConnection _conn;

    public DapClient(DapConnection connection)
    {
        _conn = connection;
        connection.EventReceived += OnEvent;
    }

    public DapConnection Connection => _conn;
    public Capabilities Capabilities { get; private set; } = new();

    public event Action? Initialized;
    public event Action<StoppedEventArgs>? Stopped;
    public event Action<int?>? Continued;
    public event Action<OutputEventArgs>? Output;
    public event Action? Terminated;
    public event Action<int>? Exited;
    public event Action<BreakpointEventArgs>? BreakpointChanged;

    private void OnEvent(DapEvent e)
    {
        switch (e.Name)
        {
            case "initialized": Initialized?.Invoke(); break;
            case "stopped":
                {
                    var b = e.Body;
                    int? thread = b.TryGetProperty("threadId", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : null;
                    var hit = b.TryGetProperty("hitBreakpointIds", out var h) && h.ValueKind == JsonValueKind.Array
                        ? h.EnumerateArray().Select(x => x.GetInt32()).ToList() : null;
                    Stopped?.Invoke(new StoppedEventArgs(Str(b, "reason") ?? "pause", thread,
                        b.TryGetProperty("allThreadsStopped", out var a) && a.ValueKind == JsonValueKind.True,
                        Str(b, "description"), Str(b, "text"), hit));
                    break;
                }
            case "continued":
                Continued?.Invoke(e.Body.ValueKind == JsonValueKind.Object && e.Body.TryGetProperty("threadId", out var ct) && ct.ValueKind == JsonValueKind.Number ? ct.GetInt32() : null);
                break;
            case "output":
                if (e.Body.ValueKind == JsonValueKind.Object)
                    Output?.Invoke(new OutputEventArgs(Str(e.Body, "category") ?? "console", Str(e.Body, "output") ?? ""));
                break;
            case "terminated": Terminated?.Invoke(); break;
            case "exited":
                Exited?.Invoke(e.Body.ValueKind == JsonValueKind.Object && e.Body.TryGetProperty("exitCode", out var ec) && ec.ValueKind == JsonValueKind.Number ? ec.GetInt32() : 0);
                break;
            case "breakpoint":
                if (DapJson.Read<DapBreakpoint>(e.Body.ValueKind == JsonValueKind.Object && e.Body.TryGetProperty("breakpoint", out var bp) ? bp : null) is { } parsed)
                    BreakpointChanged?.Invoke(new BreakpointEventArgs(Str(e.Body, "reason") ?? "changed", parsed));
                break;
        }
    }

    private static string? Str(JsonElement body, string name) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public async Task<Capabilities> InitializeAsync(string adapterId, CancellationToken ct = default)
    {
        var body = await _conn.RequestAsync("initialize", new
        {
            clientID = "devacademy",
            clientName = "DevAcademy",
            adapterID = adapterId,
            linesStartAt1 = true,
            columnsStartAt1 = true,
            pathFormat = "path",
            supportsVariableType = true,
            supportsVariablePaging = false,
            supportsRunInTerminalRequest = false,
            supportsStartDebuggingRequest = false,
            supportsProgressReporting = false,
            locale = "en-US",
        }, ct, DefaultTimeout).ConfigureAwait(false);
        Capabilities = DapJson.Read<Capabilities>(body) ?? new Capabilities();
        return Capabilities;
    }

    /// <summary>Sends launch; many adapters answer only after configurationDone, so the caller awaits the task later.</summary>
    public Task<JsonElement?> LaunchAsync(JsonObject configuration, CancellationToken ct = default) =>
        _conn.RequestAsync("launch", configuration, ct, TimeSpan.FromMinutes(2));

    public Task ConfigurationDoneAsync(CancellationToken ct = default) => _conn.RequestAsync("configurationDone", null, ct, DefaultTimeout);

    public async Task<IReadOnlyList<DapBreakpoint>> SetBreakpointsAsync(string path, IReadOnlyList<SourceBreakpoint> breakpoints, CancellationToken ct = default)
    {
        var body = await _conn.RequestAsync("setBreakpoints", new
        {
            source = new { path, name = Path.GetFileName(path) },
            breakpoints,
            lines = breakpoints.Select(b => b.Line).ToArray(),
            sourceModified = false,
        }, ct, DefaultTimeout).ConfigureAwait(false);
        return Sub<List<DapBreakpoint>>(body, "breakpoints") ?? [];
    }

    public async Task<IReadOnlyList<DapThread>> ThreadsAsync(CancellationToken ct = default) =>
        Sub<List<DapThread>>(await _conn.RequestAsync("threads", null, ct, DefaultTimeout).ConfigureAwait(false), "threads") ?? [];

    public async Task<IReadOnlyList<DapStackFrame>> StackTraceAsync(int threadId, int levels = 50, CancellationToken ct = default) =>
        Sub<List<DapStackFrame>>(await _conn.RequestAsync("stackTrace", new { threadId, startFrame = 0, levels }, ct, DefaultTimeout).ConfigureAwait(false), "stackFrames") ?? [];

    public async Task<IReadOnlyList<DapScope>> ScopesAsync(int frameId, CancellationToken ct = default) =>
        Sub<List<DapScope>>(await _conn.RequestAsync("scopes", new { frameId }, ct, DefaultTimeout).ConfigureAwait(false), "scopes") ?? [];

    public async Task<IReadOnlyList<DapVariable>> VariablesAsync(int variablesReference, CancellationToken ct = default) =>
        Sub<List<DapVariable>>(await _conn.RequestAsync("variables", new { variablesReference }, ct, DefaultTimeout).ConfigureAwait(false), "variables") ?? [];

    /// <param name="context"><c>watch</c>, <c>hover</c> or <c>repl</c>.</param>
    public async Task<DapEvaluateResult> EvaluateAsync(string expression, int? frameId, string context, CancellationToken ct = default) =>
        DapJson.Read<DapEvaluateResult>(await _conn.RequestAsync("evaluate", new { expression, frameId, context }, ct, DefaultTimeout).ConfigureAwait(false))
        ?? new DapEvaluateResult("");

    public Task ContinueAsync(int threadId, CancellationToken ct = default) => _conn.RequestAsync("continue", new { threadId }, ct, DefaultTimeout);
    public Task NextAsync(int threadId, CancellationToken ct = default) => _conn.RequestAsync("next", new { threadId }, ct, DefaultTimeout);
    public Task StepInAsync(int threadId, CancellationToken ct = default) => _conn.RequestAsync("stepIn", new { threadId }, ct, DefaultTimeout);
    public Task StepOutAsync(int threadId, CancellationToken ct = default) => _conn.RequestAsync("stepOut", new { threadId }, ct, DefaultTimeout);
    public Task PauseAsync(int threadId, CancellationToken ct = default) => _conn.RequestAsync("pause", new { threadId }, ct, DefaultTimeout);
    public Task TerminateAsync(CancellationToken ct = default) => _conn.RequestAsync("terminate", new { }, ct, TimeSpan.FromSeconds(5));
    public Task DisconnectAsync(bool terminateDebuggee, CancellationToken ct = default) =>
        _conn.RequestAsync("disconnect", new { restart = false, terminateDebuggee }, ct, TimeSpan.FromSeconds(5));

    private static T? Sub<T>(JsonElement? body, string name) =>
        body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty(name, out var v) ? v.Deserialize<T>(DapJson.Options) : default;
}

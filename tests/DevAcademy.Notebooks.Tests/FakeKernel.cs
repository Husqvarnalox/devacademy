using System.Text.Json.Nodes;
using DevAcademy.Notebooks.Kernel;
using DevAcademy.Notebooks.Protocol;
using DevAcademy.Notebooks.Transport;

namespace DevAcademy.Notebooks.Tests;

/// <summary>
/// An in-process kernel that speaks real ZMTP + the Jupyter wire protocol on the ports the client chose. "Code" is a tiny script:
/// <c>print:TEXT</c>, <c>result:TEXT</c>, <c>error:NAME</c>, <c>png</c>, <c>clear</c>, <c>loop</c> (runs until interrupted), <c>vars</c> (user_expressions reply).
/// It uses the server side of <see cref="ZmtpSocket"/>, so these tests exercise the framing from both ends.
/// </summary>
internal sealed class FakeKernelHost : IKernelHost
{
    public FakeKernelProcess? Last { get; private set; }
    public int Starts { get; private set; }
    public bool SignalInterrupts { get; set; } = true;
    public bool IgnoreShutdown { get; set; }

    public Task<IKernelProcess> StartAsync(ConnectionInfo connection, CancellationToken ct)
    {
        Starts++;
        Last = new FakeKernelProcess(connection, SignalInterrupts, IgnoreShutdown);
        return Task.FromResult<IKernelProcess>(Last);
    }
}

internal sealed class FakeKernelProcess : IKernelProcess
{
    private readonly ConnectionInfo _c;
    private readonly MessageSigner _signer;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _session = Guid.NewGuid().ToString("N");
    private readonly List<ZmtpListener> _listeners = [];
    private readonly bool _signalInterrupts, _ignoreShutdown;
    private volatile bool _interrupt;
    private volatile bool _hbEnabled = true;
    private ZmtpSocket? _iopub;
    private int _count;
    private bool _exited;

    public FakeKernelProcess(ConnectionInfo c, bool signalInterrupts, bool ignoreShutdown)
    {
        _c = c;
        _signer = new MessageSigner(c.Key);
        _signalInterrupts = signalInterrupts;
        _ignoreShutdown = ignoreShutdown;
        _ = Serve(c.IopubPort, "PUB", async s => _iopub = s);
        _ = Serve(c.ShellPort, "ROUTER", ShellLoop);
        _ = Serve(c.ControlPort, "ROUTER", ControlLoop);
        _ = Serve(c.StdinPort, "ROUTER", async s => await Task.Delay(Timeout.Infinite, _cts.Token));
        _ = Serve(c.HbPort, "REP", HbLoop);
    }

    public int? ProcessId => null;
    public bool HasExited => _exited;
    public int? ExitCode => _exited ? 0 : null;
    public event Action? Exited;
    public string StderrTail => "";
    public int InterruptSignals { get; private set; }
    public int InterruptMessages { get; private set; }
    public int ShutdownRequests { get; private set; }
    public string? ExpectedKey { get; set; }

    public void StopHeartbeat() => _hbEnabled = false;
    public void Crash() => Terminate();

    public bool TrySignalInterrupt()
    {
        if (!_signalInterrupts) return false;
        InterruptSignals++;
        _interrupt = true;
        return true;
    }

    public void Kill() => Terminate();

    public void Dispose() => Terminate();

    private void Terminate()
    {
        if (_exited) return;
        _exited = true;
        _cts.Cancel();
        foreach (var l in _listeners) l.Dispose();
        Exited?.Invoke();
    }

    private async Task Serve(int port, string type, Func<ZmtpSocket, Task> run)
    {
        try
        {
            var listener = new ZmtpListener(port);
            lock (_listeners) _listeners.Add(listener);
            var socket = await listener.AcceptAsync(type, _cts.Token);
            await run(socket);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException or System.Net.Sockets.SocketException) { }
    }

    private async Task HbLoop(ZmtpSocket s)
    {
        while (!_cts.IsCancellationRequested)
        {
            var frames = await s.ReceiveAsync(_cts.Token);
            if (_hbEnabled) await s.SendAsync(frames, _cts.Token);
        }
    }

    private Task Publish(string type, JsonObject content, JsonObject parent) =>
        _iopub is null ? Task.CompletedTask : _iopub.SendAsync(WireCodec.Encode(
            new JupyterMessage { Header = JupyterMessage.NewHeader(type, _session), ParentHeader = parent, Content = content }, _signer), _cts.Token);

    private Task Reply(ZmtpSocket s, string type, JsonObject content, JupyterMessage request) =>
        s.SendAsync(WireCodec.Encode(new JupyterMessage { Header = JupyterMessage.NewHeader(type, _session), ParentHeader = request.Header, Content = content }, _signer), _cts.Token);

    private async Task ControlLoop(ZmtpSocket s)
    {
        while (!_cts.IsCancellationRequested)
        {
            var m = WireCodec.Decode(await s.ReceiveAsync(_cts.Token), _signer);
            if (m.MsgType == "shutdown_request")
            {
                ShutdownRequests++;
                if (_ignoreShutdown) continue;
                await Reply(s, "shutdown_reply", new JsonObject { ["status"] = "ok", ["restart"] = false }, m);
                _ = Task.Run(async () => { await Task.Delay(50); Terminate(); });
            }
            else if (m.MsgType == "interrupt_request")
            {
                InterruptMessages++;
                _interrupt = true;
                await Reply(s, "interrupt_reply", new JsonObject { ["status"] = "ok" }, m);
            }
        }
    }

    private async Task ShellLoop(ZmtpSocket s)
    {
        while (!_cts.IsCancellationRequested)
        {
            var m = WireCodec.Decode(await s.ReceiveAsync(_cts.Token), _signer);
            await Publish("status", new JsonObject { ["execution_state"] = "busy" }, m.Header);
            if (m.MsgType == "kernel_info_request")
            {
                await Reply(s, "kernel_info_reply", new JsonObject { ["status"] = "ok", ["protocol_version"] = "5.3", ["implementation"] = "fake" }, m);
            }
            else if (m.MsgType == "execute_request")
            {
                await Execute(s, m);
            }
            await Publish("status", new JsonObject { ["execution_state"] = "idle" }, m.Header);
        }
    }

    private async Task Execute(ZmtpSocket s, JupyterMessage m)
    {
        var code = m.Content["code"]!.GetValue<string>();
        var silent = m.Content["silent"]!.GetValue<bool>();
        if (!silent) _count++;
        var status = "ok";
        JsonObject reply = new() { ["status"] = "ok", ["execution_count"] = _count, ["user_expressions"] = new JsonObject() };
        foreach (var line in code.Split('\n'))
        {
            if (line.StartsWith("print:")) await Publish("stream", new JsonObject { ["name"] = "stdout", ["text"] = line[6..] + "\n" }, m.Header);
            else if (line.StartsWith("result:"))
                await Publish("execute_result", new JsonObject { ["execution_count"] = _count, ["data"] = new JsonObject { ["text/plain"] = line[7..] }, ["metadata"] = new JsonObject() }, m.Header);
            else if (line == "png")
                await Publish("display_data", new JsonObject { ["data"] = new JsonObject { ["image/png"] = "iVBORw0KGgo=", ["text/plain"] = "<Figure>" }, ["metadata"] = new JsonObject() }, m.Header);
            else if (line == "clear") await Publish("clear_output", new JsonObject { ["wait"] = false }, m.Header);
            else if (line.StartsWith("error:"))
            {
                var name = line[6..];
                await Publish("error", new JsonObject { ["ename"] = name, ["evalue"] = "boom", ["traceback"] = new JsonArray("Traceback", name + ": boom") }, m.Header);
                status = "error";
                reply = new JsonObject { ["status"] = "error", ["execution_count"] = _count, ["ename"] = name, ["evalue"] = "boom", ["traceback"] = new JsonArray() };
                break;
            }
            else if (line == "loop")
            {
                _interrupt = false;
                while (!_interrupt && !_cts.IsCancellationRequested) await Task.Delay(10);
                await Publish("error", new JsonObject { ["ename"] = "KeyboardInterrupt", ["evalue"] = "", ["traceback"] = new JsonArray("KeyboardInterrupt") }, m.Header);
                status = "error";
                reply = new JsonObject { ["status"] = "error", ["execution_count"] = _count, ["ename"] = "KeyboardInterrupt", ["evalue"] = "" };
                break;
            }
        }
        if (status == "ok" && m.Content["user_expressions"] is JsonObject ue && ue.Count > 0)
        {
            reply["user_expressions"] = new JsonObject
            {
                ["vars"] = new JsonObject
                {
                    ["status"] = "ok",
                    ["data"] = new JsonObject
                    {
                        ["application/json"] = new JsonArray(new JsonObject { ["name"] = "x", ["type"] = "int", ["summary"] = "42", ["shape"] = null }),
                        ["text/plain"] = "variables",
                    },
                    ["metadata"] = new JsonObject(),
                },
            };
        }
        await Reply(s, "execute_reply", reply, m);
    }
}

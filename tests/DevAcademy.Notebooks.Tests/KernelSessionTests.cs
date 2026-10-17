using DevAcademy.Notebooks.Kernel;
using DevAcademy.Notebooks.Nbformat;

namespace DevAcademy.Notebooks.Tests;

/// <summary>The Jupyter client against an in-process kernel that speaks the real ZMTP and wire protocol.</summary>
public class KernelSessionTests
{
    private static readonly KernelSessionOptions Fast = new()
    {
        StartTimeout = TimeSpan.FromSeconds(30), // generous: CI runners and loaded machines start kernels slowly
        HeartbeatInterval = TimeSpan.FromMilliseconds(100),
        HeartbeatTimeout = TimeSpan.FromMilliseconds(300),
        MaxMissedHeartbeats = 2,
        ShutdownGrace = TimeSpan.FromMilliseconds(500),
    };

    private static async Task<(KernelSession Session, FakeKernelHost Host)> StartAsync(bool signal = true)
    {
        var host = new FakeKernelHost { SignalInterrupts = signal };
        var session = new KernelSession(host, Fast);
        await session.StartAsync(CancellationToken.None);
        return (session, host);
    }

    private static async Task<(ExecuteResult Result, List<KernelEvent> Events)> RunAsync(KernelSession s, string code, ExecuteOptions? o = null)
    {
        var events = new List<KernelEvent>();
        var r = await s.ExecuteAsync(code, o, e => { lock (events) events.Add(e); }, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
        return (r, events);
    }

    [Fact]
    public async Task Start_handshakes_gets_kernel_info_and_reaches_idle()
    {
        var (s, host) = await StartAsync();
        await using var _ = s;
        Assert.Equal(KernelState.Idle, s.State);
        Assert.Equal("fake", s.KernelInfo!["implementation"]!.GetValue<string>());
        Assert.Equal(1, host.Starts);
        Assert.Equal(0, s.DroppedMessages);
    }

    [Fact]
    public async Task Execute_collects_stream_result_display_and_clear_events_in_order()
    {
        var (s, _) = await StartAsync();
        await using var _ = s;
        var (r, events) = await RunAsync(s, "print:hi\npng\nclear\nresult:42");
        Assert.True(r.IsOk);
        Assert.Equal(1, r.ExecutionCount);
        Assert.Equal(4, events.Count);
        var stream = ((OutputEvent)events[0]).Output;
        Assert.Equal((NotebookOutput.Stream, "stdout", "hi\n"), (stream.OutputType, stream.Name, stream.Text));
        Assert.Equal("iVBORw0KGgo=", ((OutputEvent)events[1]).Output.GetText("image/png"));
        Assert.IsType<ClearOutputEvent>(events[2]);
        var result = ((OutputEvent)events[3]).Output;
        Assert.Equal(NotebookOutput.ExecuteResult, result.OutputType);
        Assert.Equal("42", result.GetText("text/plain"));
        Assert.Equal(1, result.ExecutionCount);
        Assert.Equal(KernelState.Idle, s.State);
    }

    [Fact]
    public async Task An_error_is_reported_with_its_traceback()
    {
        var (s, _) = await StartAsync();
        await using var _ = s;
        var (r, events) = await RunAsync(s, "error:ZeroDivisionError");
        Assert.Equal("error", r.Status);
        Assert.Equal("ZeroDivisionError", r.ErrorName);
        var err = ((OutputEvent)events.Single()).Output;
        Assert.Equal(NotebookOutput.Error, err.OutputType);
        Assert.Equal(["Traceback", "ZeroDivisionError: boom"], err.Traceback);
    }

    [Fact]
    public async Task Requests_run_in_order_and_execution_counts_increase()
    {
        var (s, _) = await StartAsync();
        await using var _ = s;
        var a = RunAsync(s, "print:a");
        var b = RunAsync(s, "print:b");
        var c = RunAsync(s, "print:c");
        var results = await Task.WhenAll(a, b, c);
        Assert.Equal([1, 2, 3], results.Select(r => r.Result.ExecutionCount!.Value));
        Assert.Equal("a\n", ((OutputEvent)results[0].Events.Single()).Output.Text);
        Assert.Equal("c\n", ((OutputEvent)results[2].Events.Single()).Output.Text);
    }

    [Fact]
    public async Task Silent_requests_do_not_count_and_return_user_expressions_which_the_inspector_parses()
    {
        var (s, _) = await StartAsync();
        await using var _ = s;
        await RunAsync(s, "print:a");
        var vars = await VariableInspector.QueryAsync(s, CancellationToken.None);
        var v = Assert.Single(vars);
        Assert.Equal(new VariableInfo("x", "int", "42", null), v);
        var (next, _) = await RunAsync(s, "print:b");
        Assert.Equal(2, next.ExecutionCount);
    }

    [Fact]
    public async Task State_goes_busy_while_running_and_back_to_idle()
    {
        var (s, _) = await StartAsync();
        await using var _ = s;
        var states = new List<KernelState>();
        s.StateChanged += st => { lock (states) states.Add(st); };
        await RunAsync(s, "print:a");
        Assert.Contains(KernelState.Busy, states);
        Assert.Equal(KernelState.Idle, states.Last());
    }

    [Fact]
    public async Task Interrupt_by_signal_stops_a_running_cell()
    {
        var (s, host) = await StartAsync(signal: true);
        await using var _ = s;
        var running = RunAsync(s, "loop");
        await WaitAsync(() => s.State == KernelState.Busy);
        await s.InterruptAsync();
        var (r, events) = await running;
        Assert.Equal("error", r.Status);
        Assert.Equal("KeyboardInterrupt", r.ErrorName);
        Assert.Equal(1, host.Last!.InterruptSignals);
        Assert.Equal(0, host.Last.InterruptMessages);
        Assert.Equal(KernelState.Idle, s.State);
    }

    [Fact]
    public async Task Interrupt_falls_back_to_the_interrupt_request_message()
    {
        var (s, host) = await StartAsync(signal: false);
        await using var _ = s;
        var running = RunAsync(s, "loop");
        await WaitAsync(() => s.State == KernelState.Busy);
        await s.InterruptAsync();
        var (r, _) = await running;
        Assert.Equal("KeyboardInterrupt", r.ErrorName);
        Assert.Equal(1, host.Last!.InterruptMessages);
    }

    [Fact]
    public async Task Restart_replaces_the_process_with_new_ports_and_key()
    {
        var (s, host) = await StartAsync();
        await using var _ = s;
        var first = host.Last!;
        var oldConn = s.Connection!;
        await s.RestartAsync(CancellationToken.None);
        Assert.True(first.HasExited);
        Assert.Equal(1, first.ShutdownRequests);
        Assert.Equal(2, host.Starts);
        Assert.NotSame(first, host.Last);
        Assert.NotEqual(oldConn.Key, s.Connection!.Key);
        Assert.Equal(KernelState.Idle, s.State);
        var (r, _) = await RunAsync(s, "print:after");
        Assert.Equal(1, r.ExecutionCount); // a fresh kernel counts from 1 again
    }

    [Fact]
    public async Task Shutdown_asks_politely_then_stops()
    {
        var (s, host) = await StartAsync();
        await s.ShutdownAsync();
        Assert.Equal(KernelState.Stopped, s.State);
        Assert.True(host.Last!.HasExited);
        Assert.Equal(1, host.Last.ShutdownRequests);
        await s.DisposeAsync();
    }

    [Fact]
    public async Task A_kernel_that_ignores_shutdown_is_killed_after_the_grace_period()
    {
        var host = new FakeKernelHost { IgnoreShutdown = true };
        var s = new KernelSession(host, Fast);
        await s.StartAsync(CancellationToken.None);
        await s.ShutdownAsync();
        Assert.True(host.Last!.HasExited);
        Assert.Equal(KernelState.Stopped, s.State);
    }

    [Fact]
    public async Task A_crashed_process_marks_the_kernel_dead_and_fails_the_running_request()
    {
        var (s, host) = await StartAsync();
        await using var _ = s;
        var running = RunAsync(s, "loop");
        await WaitAsync(() => s.State == KernelState.Busy);
        host.Last!.Crash();
        await Assert.ThrowsAsync<KernelDeadException>(() => running);
        Assert.Equal(KernelState.Dead, s.State);
        Assert.Contains("exited", s.DeadReason);
        await Assert.ThrowsAsync<KernelDeadException>(() => RunAsync(s, "print:x"));
    }

    [Fact]
    public async Task Lost_heartbeats_mark_the_kernel_dead()
    {
        var (s, host) = await StartAsync();
        await using var _ = s;
        host.Last!.StopHeartbeat();
        await WaitAsync(() => s.State == KernelState.Dead, 10_000);
        Assert.Contains("heartbeat", s.DeadReason);
    }

    [Fact]
    public async Task Restart_after_death_brings_the_kernel_back()
    {
        var (s, host) = await StartAsync();
        await using var _ = s;
        host.Last!.Crash();
        await WaitAsync(() => s.State == KernelState.Dead);
        await s.RestartAsync(CancellationToken.None);
        Assert.Equal(KernelState.Idle, s.State);
        var (r, _) = await RunAsync(s, "result:1");
        Assert.True(r.IsOk);
    }

    [Fact]
    public async Task Cancelling_the_wait_does_not_hang_and_the_session_stays_usable()
    {
        var (s, _) = await StartAsync();
        await using var _ = s;
        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => s.ExecuteAsync("loop", null, null, cts.Token));
        await s.InterruptAsync();
        await WaitAsync(() => s.State == KernelState.Idle);
        var (r, _) = await RunAsync(s, "print:ok");
        Assert.True(r.IsOk);
    }

    internal static async Task WaitAsync(Func<bool> condition, int ms = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Condition not reached.");
            await Task.Delay(10);
        }
    }
}

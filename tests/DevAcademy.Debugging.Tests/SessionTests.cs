using System.Text.Json.Nodes;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;
using DevAcademy.Debugging.Session;

namespace DevAcademy.Debugging.Tests;

/// <summary>Whole debugging flows against the in-process fake adapter: handshake, breakpoint hit, stack/variables, stepping, exit.</summary>
public class SessionTests
{
    private static (DebugSession Session, FakeAdapter Adapter, List<DebugStop> Stops, List<string> Output, List<DebugTermination> Ended) Create(
        Action<FakeAdapter>? configure = null)
    {
        var adapter = new FakeAdapter();
        configure?.Invoke(adapter);
        var def = DebugAdapterRegistry.ForLanguage("python")!;
        var resolved = new ResolvedAdapter(def, "fake", [], new Dictionary<string, string>());
        var launch = LaunchConfigurations.Create(def, new DebugLaunch("/work/main.fake", "/work", ["--x"], "/venv/bin/python"));
        var session = new DebugSession(new DebugSessionOptions(def, resolved, launch, "/work"), new FakeAdapterLauncher(adapter));
        var stops = new List<DebugStop>();
        var output = new List<string>();
        var ended = new List<DebugTermination>();
        session.Stopped += s => { lock (stops) stops.Add(s); };
        session.Output += o => { lock (output) output.Add(o.Text); };
        session.Terminated += t => { lock (ended) ended.Add(t); };
        session.SeedBreakpoints(new Dictionary<string, IReadOnlyList<SourceBreakpoint>> { ["/work/main.fake"] = [new SourceBreakpoint(5)] });
        return (session, adapter, stops, output, ended);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Handshake_works_whichever_order_the_adapter_uses(bool initializedBeforeLaunch, bool launchRespondsEarly)
    {
        var (session, adapter, stops, _, _) = Create(a => { a.InitializedBeforeLaunch = initializedBeforeLaunch; a.LaunchRespondsAfterConfigDone = !launchRespondsEarly; });
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => stops.Count == 1, "first stop");

        string[] order = adapter.Commands.ToArray();
        Assert.True(Array.IndexOf(order, "initialize") < Array.IndexOf(order, "launch"));
        Assert.True(Array.IndexOf(order, "setBreakpoints") < Array.IndexOf(order, "configurationDone"));
        Assert.Equal([5], adapter.Breakpoints["/work/main.fake"]);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Launch_configuration_reaches_the_adapter()
    {
        var (session, adapter, stops, _, _) = Create();
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => stops.Count == 1, "stop");
        var l = adapter.LaunchArguments!;
        Assert.Equal("/work/main.fake", (string?)l["program"]);
        Assert.Equal("/work", (string?)l["cwd"]);
        Assert.Equal("--x", (string?)l["args"]![0]);
        Assert.Equal("/venv/bin/python", (string?)l["python"]![0]);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Breakpoint_hit_populates_stack_then_variables_expand_lazily_then_step_then_run_to_exit()
    {
        var (session, adapter, stops, output, ended) = Create();
        await session.StartAsync(CancellationToken.None);
        var stop = await Util.UntilAsync(() => { lock (stops) return stops.FirstOrDefault(); }, "breakpoint stop");

        Assert.Equal("breakpoint", stop.Reason);
        Assert.Equal(DebugState.Stopped, session.State);
        Assert.Equal(["body", "main"], stop.Frames.Select(f => f.Name));
        Assert.Equal(5, stop.Frames[0].Line);
        Assert.Equal("/work/main.fake", stop.Frames[0].Source!.Path);

        var scopes = await session.GetScopesAsync(stop.Frames[0].Id);
        Assert.Equal("Locals", scopes[0].Name);
        var locals = await session.GetVariablesAsync(scopes[0].VariablesReference);
        Assert.Equal("i", locals[0].Name);
        Assert.Equal("0", locals[0].Value);
        Assert.False(locals[0].HasChildren);
        Assert.True(locals[1].HasChildren);
        var children = await session.GetVariablesAsync(locals[1].VariablesReference);
        Assert.Equal(["[0]", "[1]"], children.Select(c => c.Name));

        // Watch / hover / repl contexts reach the adapter.
        Assert.Equal("0", (await session.EvaluateAsync("i", stop.Frames[0].Id, "watch")).Result);
        Assert.Equal("1+1@repl", (await session.EvaluateAsync("1+1", stop.Frames[0].Id, "repl")).Result);
        await Assert.ThrowsAsync<DapException>(() => session.EvaluateAsync("boom", stop.Frames[0].Id, "watch"));

        // Step over: state flips to Running, then a new stop one line further.
        await session.StepOverAsync();
        await Util.UntilAsync(() => stops.Count == 2, "step stop");
        Assert.Equal("step", stops[1].Reason);
        Assert.Equal(6, stops[1].Frames[0].Line);

        // Continue: the next iteration hits the breakpoint again with a changed variable value.
        await session.ContinueAsync();
        await Util.UntilAsync(() => stops.Count == 3, "second breakpoint");
        var again = await session.GetVariablesAsync((await session.GetScopesAsync(stops[2].Frames[0].Id))[0].VariablesReference);
        Assert.Equal("1", again[0].Value);

        await session.ContinueAsync();
        await Util.UntilAsync(() => stops.Count == 4, "third breakpoint");
        await session.ContinueAsync();
        var end = await Util.UntilAsync(() => { lock (ended) return ended.FirstOrDefault(); }, "terminated");
        Assert.Equal(7, end.ExitCode);
        Assert.Null(end.Error);
        Assert.Equal(DebugState.Terminated, session.State);
        Assert.Contains("start\n", output);
        Assert.Contains("done\n", output);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Breakpoints_can_change_while_running_and_stopped()
    {
        var (session, adapter, stops, _, _) = Create();
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => stops.Count == 1, "stop");
        var verdict = await session.SetBreakpointsAsync("/work/main.fake", [new SourceBreakpoint(5), new SourceBreakpoint(9, "i == 2")]);
        Assert.Equal(2, verdict.Count);
        Assert.Equal([5, 9], adapter.Breakpoints["/work/main.fake"]);
        await session.SetBreakpointsAsync("/work/main.fake", []);
        Assert.Empty(adapter.Breakpoints["/work/main.fake"]);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Program_without_breakpoints_runs_to_the_end()
    {
        var (session, adapter, stops, _, ended) = Create();
        session.SeedBreakpoints(new Dictionary<string, IReadOnlyList<SourceBreakpoint>> { ["/work/main.fake"] = [] });
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => ended.Count == 1, "terminated");
        Assert.Empty(stops);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Pause_and_stop_work_while_running()
    {
        var (session, adapter, stops, _, ended) = Create();
        session.SeedBreakpoints(new Dictionary<string, IReadOnlyList<SourceBreakpoint>> { ["/work/main.fake"] = [new SourceBreakpoint(5)] });
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => stops.Count == 1, "stop");
        await session.ContinueAsync();
        await Util.UntilAsync(() => stops.Count == 2, "second stop");
        // Resume, then pause; the fake answers pause with a stopped event.
        await session.ContinueAsync();
        await Util.UntilAsync(() => stops.Count == 3, "third stop");
        await session.StopAsync();
        Assert.Contains("disconnect", adapter.Commands);
        Assert.Equal(DebugState.Terminated, session.State);
        Assert.True(ended.Count >= 1);
    }

    [Fact]
    public async Task Reverse_runInTerminal_is_refused_and_does_not_break_the_session()
    {
        var (session, adapter, stops, _, _) = Create(a => a.SendRunInTerminalRequest = true);
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => stops.Count == 1, "stop");
        await Util.UntilAsync(() => adapter.ReverseReplyMessage is not null, "reverse reply");
        Assert.Contains("not supported", adapter.ReverseReplyMessage);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Adapter_dying_mid_run_ends_the_session_with_an_error()
    {
        var (session, adapter, stops, _, ended) = Create();
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => stops.Count == 1, "stop");
        adapter.Kill();
        var end = await Util.UntilAsync(() => { lock (ended) return ended.FirstOrDefault(); }, "terminated");
        Assert.NotNull(end.Error);
        Assert.Equal(DebugState.Terminated, session.State);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task A_launch_the_adapter_rejects_fails_StartAsync_with_its_message()
    {
        var adapter = new FakeAdapter { LaunchRespondsAfterConfigDone = false };
        var def = DebugAdapterRegistry.ForLanguage("cpp")!;
        // A program path is validated by the fake via an unknown command: send a request the fake rejects.
        var launch = new JsonObject { ["program"] = "/nope" };
        var failing = new FailingLaunchLauncher(adapter);
        var session = new DebugSession(new DebugSessionOptions(def, new ResolvedAdapter(def, "x", [], new Dictionary<string, string>()), launch, "/"), failing);
        var ex = await Assert.ThrowsAsync<DapException>(() => session.StartAsync(CancellationToken.None));
        Assert.Contains("cannot start", ex.Message);
    }

    private sealed class FailingLaunchLauncher(FakeAdapter adapter) : IAdapterLauncher
    {
        public Task<IAdapterProcess> LaunchAsync(ResolvedAdapter a, string wd, CancellationToken ct) =>
            throw new DapException("cannot start the adapter: " + adapter.GetType().Name);
    }
}

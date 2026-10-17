using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;
using DevAcademy.Debugging.Session;
using DevAcademy.Execution;

namespace DevAcademy.Debugging.Tests;

/// <summary>A started debug session of a real adapter plus everything it reported, for the end-to-end tests.</summary>
internal sealed class RealRun : IAsyncDisposable
{
    public required DebugSession Session { get; init; }
    public List<DebugStop> Stops { get; } = [];
    public List<DebugTermination> Ended { get; } = [];
    public List<string> Output { get; } = [];

    public static string RunnerCache() => ExecutionOptions.DefaultCacheDirectory();

    /// <summary>Writes the source, builds it for debugging with the real debug builder and starts the real adapter with one breakpoint.</summary>
    public static async Task<(RealRun Run, string File, string Dir)> StartAsync(
        string language, string fileName, string source, int? breakpointLine, AdapterStatus status, int timeoutMs = 60000, bool waitForStop = true)
    {
        string dir = Util.TempDir("real-" + language);
        string file = Path.Combine(dir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, source);
        var built = await DebugBuilderFactory.Create().BuildAsync(language, dir, fileName, CancellationToken.None);
        Assert.True(built.Success, built.Output);
        var def = DebugAdapterRegistry.ForLanguage(language)!;
        var launch = LaunchConfigurations.Create(def, new DebugLaunch(built.Executable!, dir, [], RunArguments: built.RunArguments));
        var session = new DebugSession(new DebugSessionOptions(def, status.Adapter!, launch, dir), new ProcessAdapterLauncher());
        var run = new RealRun { Session = session };
        session.Stopped += s => { lock (run.Stops) run.Stops.Add(s); };
        session.Terminated += t => { lock (run.Ended) run.Ended.Add(t); };
        session.Output += o => { lock (run.Output) run.Output.Add(o.Text); };
        if (breakpointLine is { } line)
            session.SeedBreakpoints(new Dictionary<string, IReadOnlyList<SourceBreakpoint>> { [file] = [new SourceBreakpoint(line)] });
        await session.StartAsync(CancellationToken.None);
        if (waitForStop) await run.WaitForStopAsync(1, "first stop", timeoutMs);
        return (run, file, dir);
    }

    public Task WaitForStopAsync(int count, string what, int ms = 30000) => Util.UntilAsync(() => { lock (Stops) return Stops.Count >= count; }, what, ms);

    public async Task<DebugTermination> WaitForEndAsync(int ms = 30000) =>
        await Util.UntilAsync(() => { lock (Ended) return Ended.FirstOrDefault(); }, "exit", ms);

    public async Task<Dictionary<string, DapVariable>> LocalsAsync(DebugStop stop)
    {
        var all = new Dictionary<string, DapVariable>();
        foreach (var sc in await Session.GetScopesAsync(stop.Frames[0].Id))
            foreach (var v in await Session.GetVariablesAsync(sc.VariablesReference)) all.TryAdd(v.Name, v);
        return all;
    }

    public string OutputText { get { lock (Output) return string.Concat(Output); } }

    public ValueTask DisposeAsync() => Session.DisposeAsync();
}

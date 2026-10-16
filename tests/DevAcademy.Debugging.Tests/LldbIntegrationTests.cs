using System.Diagnostics;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;
using DevAcademy.Debugging.Session;
using DevAcademy.LanguageServices.Hosting;

namespace DevAcademy.Debugging.Tests;

/// <summary>Real lldb-dap against a real C++ program built with -g -O0. Skipped when lldb-dap or a compiler is missing.</summary>
public class LldbIntegrationTests
{
    private const string Source = """
        #include <cstdio>
        int main() {
            int total = 0;
            for (int i = 0; i < 3; ++i) {
                total += i;          // line 5: breakpoint
                std::printf("i=%d\n", i);
            }
            return total + 40;       // exit code 43
        }
        """;

    private static async Task<(string Dir, string Src, string Exe)?> BuildAsync()
    {
        string? cxx = new[] { "clang++", "g++" }.Select(n => new DevAcademy.LanguageServices.Servers.ServerLocator(null).FindExecutable(n, includeCache: false)).FirstOrDefault(p => p is not null);
        if (cxx is null) return null;
        string dir = Util.TempDir("lldb");
        string src = Path.Combine(dir, "main.cpp"), exe = Path.Combine(dir, "prog");
        File.WriteAllText(src, Source);
        var psi = new ProcessStartInfo(cxx) { RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = dir };
        foreach (var a in new[] { "-std=c++20", "-g", "-O0", "-o", exe, src }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
        Assert.True(p.ExitCode == 0, await p.StandardError.ReadToEndAsync());
        return (dir, src, exe);
    }

    [Fact]
    public async Task Breakpoint_in_a_loop_shows_the_variable_steps_and_runs_to_the_exit_code()
    {
        var status = await new AdapterResolver(Util.TempDir("cache")).GetStatusAsync(DebugAdapterRegistry.ForLanguage("cpp")!, new DebugEnvironment(), default);
        Assert.SkipUnless(status.IsReady, "lldb-dap not available: " + status.Message);
        var built = await BuildAsync();
        Assert.SkipWhen(built is null, "no C++ compiler");
        var (dir, src, exe) = built!.Value;

        var def = DebugAdapterRegistry.ForLanguage("cpp")!;
        var launch = LaunchConfigurations.Create(def, new DebugLaunch(exe, dir, []));
        await using var session = new DebugSession(new DebugSessionOptions(def, status.Adapter!, launch, dir), new ProcessAdapterLauncher());
        var stops = new List<DebugStop>();
        var ended = new List<DebugTermination>();
        var output = new List<string>();
        session.Stopped += s => { lock (stops) stops.Add(s); };
        session.Terminated += t => { lock (ended) ended.Add(t); };
        session.Output += o => { lock (output) output.Add(o.Text); };
        session.SeedBreakpoints(new Dictionary<string, IReadOnlyList<SourceBreakpoint>> { [src] = [new SourceBreakpoint(5)] });

        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => { lock (stops) return stops.Count >= 1; }, "first breakpoint hit", 60000);
        var top = stops[0].Frames[0];
        Assert.Equal(5, top.Line);
        Assert.Equal("main", top.Name);

        async Task<Dictionary<string, string>> LocalsAsync(DebugStop s)
        {
            var scopes = await session.GetScopesAsync(s.Frames[0].Id);
            var all = new Dictionary<string, string>();
            foreach (var sc in scopes)
                foreach (var v in await session.GetVariablesAsync(sc.VariablesReference)) all[v.Name] = v.Value;
            return all;
        }
        var locals = await LocalsAsync(stops[0]);
        Assert.Equal("0", locals["i"]);

        await session.StepOverAsync();
        await Util.UntilAsync(() => { lock (stops) return stops.Count >= 2; }, "step", 30000);
        Assert.Equal(6, stops[1].Frames[0].Line);
        Assert.Equal("0", (await LocalsAsync(stops[1]))["total"]);

        await session.ContinueAsync();
        await Util.UntilAsync(() => { lock (stops) return stops.Count >= 3; }, "second iteration", 30000);
        Assert.Equal("1", (await LocalsAsync(stops[2]))["i"]);
        Assert.Equal("1", (await session.EvaluateAsync("i", stops[2].Frames[0].Id, "watch")).Result);

        // Drop the breakpoint and run to the end.
        await session.SetBreakpointsAsync(src, []);
        await session.ContinueAsync();
        var end = await Util.UntilAsync(() => { lock (ended) return ended.FirstOrDefault(); }, "exit", 30000);
        Assert.Equal(43, end.ExitCode);
    }
}

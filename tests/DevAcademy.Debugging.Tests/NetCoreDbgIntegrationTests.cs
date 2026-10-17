using DevAcademy.Debugging.Adapters;

namespace DevAcademy.Debugging.Tests;

/// <summary>
/// Real netcoredbg against a real .NET program (file-based app, debug build). Skipped when netcoredbg or the .NET SDK is not installed
/// in the runner cache (install: the "Install debugger" action of the debug panel).
/// </summary>
public class NetCoreDbgIntegrationTests
{
    private const string Source = """
        static int Sum(int n)
        {
            int total = 0;
            for (int i = 0; i < n; i++)
            {
                total += i * 2;
            }
            return total;
        }

        int result = Sum(3);
        Console.WriteLine("result=" + result);
        return result + 1;
        """;

    [Fact]
    public async Task Breakpoint_in_a_loop_shows_locals_steps_and_runs_to_the_exit_code()
    {
        var status = await new AdapterResolver(RealRun.RunnerCache()).GetStatusAsync(DebugAdapterRegistry.ForLanguage("csharp")!, new DebugEnvironment(), default);
        Assert.SkipUnless(status.IsReady, "netcoredbg not available: " + status.Message);
        var (run, file, _) = await RealRun.StartAsync("csharp", "Program.cs", Source, 6, status, timeoutMs: 120000);
        await using var _ = run;

        var top = run.Stops[0].Frames[0];
        Assert.Equal(6, top.Line);
        Assert.Contains("Sum", top.Name);
        Assert.Equal(file, top.Source?.Path);
        var locals = await run.LocalsAsync(run.Stops[0]);
        Assert.Equal("0", locals["i"].Value);
        Assert.Equal("0", locals["total"].Value);

        await run.Session.StepOverAsync();
        await run.WaitForStopAsync(2, "step");
        Assert.Equal(7, run.Stops[1].Frames[0].Line);   // the loop body's closing brace

        await run.Session.ContinueAsync();
        await run.WaitForStopAsync(3, "second iteration");
        var second = await run.LocalsAsync(run.Stops[2]);
        Assert.Equal("1", second["i"].Value);
        Assert.Equal("1", (await run.Session.EvaluateAsync("i", run.Stops[2].Frames[0].Id, "watch")).Result);

        await run.Session.SetBreakpointsAsync(file, []);
        await run.Session.ContinueAsync();
        var end = await run.WaitForEndAsync();
        Assert.Null(end.Error);
        Assert.Null(end.ExitCode);   // netcoredbg always reports exit code 0, so the session hides it
        Assert.Contains("result=6", run.OutputText);
    }
}

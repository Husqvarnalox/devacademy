using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;

namespace DevAcademy.Debugging.Tests;

/// <summary>
/// Real vscode-js-debug against real Node programs (JavaScript, and TypeScript through Node type stripping). Skipped when Node or the
/// pinned vscode-js-debug release is not installed in the runner cache (install: the "Install debugger" action of the debug panel).
/// </summary>
public class JsDebugIntegrationTests
{
    private const string Js = """
        function sum(n) {
          let total = 0;
          for (let i = 0; i < n; i++) {
            total += i * 2;
          }
          return total;
        }
        const result = sum(3);
        console.log('result=' + result);
        process.exit(result + 1);
        """;

    private const string Ts = """
        function sum(n: number): number {
          let total: number = 0;
          for (let i: number = 0; i < n; i++) {
            total += i * 2;
          }
          return total;
        }
        const result: number = sum(3);
        console.log('result=' + result);
        process.exit(result + 1);
        """;

    private static async Task<AdapterStatus> StatusAsync(string language)
    {
        var status = await new AdapterResolver(RealRun.RunnerCache()).GetStatusAsync(DebugAdapterRegistry.ForLanguage(language)!, new DebugEnvironment(), default);
        Assert.SkipUnless(status.IsReady, "vscode-js-debug not available: " + status.Message);
        return status;
    }

    [Theory]
    [InlineData("javascript", "main.js", Js)]
    [InlineData("typescript", "main.ts", Ts)]
    public async Task Breakpoint_in_a_loop_shows_locals_steps_and_runs_to_the_exit_code(string language, string fileName, string source)
    {
        var status = await StatusAsync(language);
        var (run, file, _) = await RealRun.StartAsync(language, fileName, source, 4, status);
        await using var _ = run;

        var top = run.Stops[0].Frames[0];
        Assert.Equal(4, top.Line);
        Assert.EndsWith("sum", top.Name);   // js-debug names it "global.sum"
        Assert.Equal(file, top.Source?.Path);
        var locals = await run.LocalsAsync(run.Stops[0]);
        Assert.Equal("0", locals["i"].Value);
        Assert.Equal("0", locals["total"].Value);

        await run.Session.StepOverAsync();
        await run.WaitForStopAsync(2, "step");
        Assert.Equal(3, run.Stops[1].Frames[0].Line);   // the for-loop header: i++ / condition

        await run.Session.ContinueAsync();
        await run.WaitForStopAsync(3, "second iteration");
        var second = await run.LocalsAsync(run.Stops[2]);
        Assert.Equal("1", second["i"].Value);
        Assert.Equal("1", (await run.Session.EvaluateAsync("i", run.Stops[2].Frames[0].Id, "watch")).Result);

        await run.Session.SetBreakpointsAsync(file, []);
        await run.Session.ContinueAsync();
        var end = await run.WaitForEndAsync();
        Assert.Null(end.Error);
        Assert.Null(end.ExitCode);   // vscode-js-debug sends `terminated` without an `exited` event: the exit code is not available
        Assert.Contains("result=6", run.OutputText);
    }

    [Fact]
    public async Task Stopping_while_paused_ends_the_debugged_node_process()
    {
        var status = await StatusAsync("javascript");
        string pidFile = Path.Combine(Util.TempDir("pid"), "pid.txt");
        var (run, _, _) = await RealRun.StartAsync("javascript", "main.js",
            $"require('fs').writeFileSync({System.Text.Json.JsonSerializer.Serialize(pidFile)}, String(process.pid));\nlet x = 1;\nsetTimeout(() => {{}}, 60000);\n", 2, status);
        await using var _ = run;
        int pid = int.Parse(File.ReadAllText(pidFile));
        Assert.False(System.Diagnostics.Process.GetProcessById(pid).HasExited);

        await run.Session.StopAsync();
        await Util.UntilAsync(() =>
        {
            try { return System.Diagnostics.Process.GetProcessById(pid).HasExited; }
            catch (ArgumentException) { return true; }
        }, "the debuggee to end", 10000);
    }
}

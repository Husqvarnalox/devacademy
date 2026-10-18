using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;

namespace DevAcademy.Debugging.Tests;

/// <summary>
/// The JDI adapter (tools/debug-adapters/java) against real JVM programs. The adapter is compiled with the local javac into a throwaway
/// cache (no network); skipped when there is no JDK with the jdk.jdi module. With DEVACADEMY_ADAPTER_INSTALL_TO_RUNNER_CACHE=1 the same
/// compile goes into the real runner cache, which is what the in-app "build debugger" action does.
/// </summary>
public class JavaDebugIntegrationTests
{
    // Line numbers matter: they are the breakpoint lines below.
    private const string Source = """
        public class Main {
            static class Point {
                int x; int y; String label;
                Point(int x, int y, String label) { this.x = x; this.y = y; this.label = label; }
            }
            static int sum(int[] values) {
                int total = 0;
                for (int v : values) {
                    total += v;
                }
                return total;
            }
            public static void main(String[] args) {
                int[] numbers = {1, 2, 3};
                Point p = new Point(3, 4, "origin");
                String greeting = "hello";
                int total = sum(numbers);
                Integer boxed = total;
                System.out.println("total=" + total + " " + Helper.twice(total));
                System.exit(total - 1);
            }
        }
        class Helper {
            static int twice(int n) {
                int r = n * 2;
                return r;
            }
        }
        """;

    private static async Task<AdapterStatus> ProvisionedAsync()
    {
        string cache = Environment.GetEnvironmentVariable("DEVACADEMY_ADAPTER_INSTALL_TO_RUNNER_CACHE") == "1" ? RealRun.RunnerCache() : Util.TempDir("java-adapter-cache");
        var resolver = new AdapterResolver(cache);
        var def = DebugAdapterRegistry.ForLanguage("java")!;
        var status = await resolver.GetStatusAsync(def, new DebugEnvironment(), default);
        Assert.SkipWhen(status.State == AdapterState.Unavailable, "Java debugger not available: " + status.Message);
        if (status.State == AdapterState.NotInstalled)
        {
            Assert.DoesNotContain("http", status.Plan!.Display);   // compiled locally, nothing is downloaded
            var (ok, output) = await AdapterResolver.ProvisionAsync(status.Plan, () => true, default);
            Assert.True(ok, output);
            status = await resolver.GetStatusAsync(def, new DebugEnvironment(), default);
        }
        Assert.True(status.IsReady, status.Message);
        return status;
    }

    [Fact]
    public async Task Step_in_out_over_deferred_breakpoint_variables_and_exit_code()
    {
        var status = await ProvisionedAsync();
        var (run, file, _) = await RealRun.StartAsync("java", "Main.java", Source, 17, status);
        await using var _ = run;

        // Stopped in main at the call: parameters-by-value, an array, an object, a string.
        var top = run.Stops[0].Frames[0];
        Assert.Equal(17, top.Line);
        Assert.Equal("Main.main", top.Name);
        Assert.Equal(file, top.Source?.Path);
        var locals = await run.LocalsAsync(run.Stops[0]);
        Assert.Equal("\"hello\"", locals["greeting"].Value);
        Assert.Equal("int[3]", locals["numbers"].Value[..6]);
        Assert.False(locals.ContainsKey("total"));   // not declared yet at this line
        var elements = await run.Session.GetVariablesAsync(locals["numbers"].VariablesReference);
        Assert.Equal(["1", "2", "3"], elements.Select(e => e.Value));
        var fields = (await run.Session.GetVariablesAsync(locals["p"].VariablesReference)).ToDictionary(f => f.Name, f => f.Value);
        Assert.Equal("3", fields["x"]);
        Assert.Equal("4", fields["y"]);
        Assert.Equal("\"origin\"", fields["label"]);

        // Step into sum, out again, over the rest of the line.
        await run.Session.StepIntoAsync();
        await run.WaitForStopAsync(2, "step into");
        Assert.Equal("Main.sum", run.Stops[1].Frames[0].Name);
        Assert.Equal(7, run.Stops[1].Frames[0].Line);
        await run.Session.StepOutAsync();
        await run.WaitForStopAsync(3, "step out");
        Assert.Equal("Main.main", run.Stops[2].Frames[0].Name);
        await run.Session.StepOverAsync();
        await run.WaitForStopAsync(4, "step over");
        Assert.Equal(18, run.Stops[3].Frames[0].Line);
        var after = await run.LocalsAsync(run.Stops[3]);
        Assert.Equal("6", after["total"].Value);
        Assert.Equal("6", (await run.Session.EvaluateAsync("total", run.Stops[3].Frames[0].Id, "watch")).Result);
        Assert.Equal("\"origin\"", (await run.Session.EvaluateAsync("p.label", run.Stops[3].Frames[0].Id, "watch")).Result);
        Assert.Equal("3", (await run.Session.EvaluateAsync("numbers.length", run.Stops[3].Frames[0].Id, "watch")).Result);
        Assert.Equal("2", (await run.Session.EvaluateAsync("numbers[1]", run.Stops[3].Frames[0].Id, "watch")).Result);
        await Assert.ThrowsAsync<DapException>(() => run.Session.EvaluateAsync("total + 1", run.Stops[3].Frames[0].Id, "watch"));

        // A breakpoint in a class that is not loaded yet: it must resolve when the class is prepared.
        await run.Session.SetBreakpointsAsync(file, [new SourceBreakpoint(25)]);
        await run.Session.ContinueAsync();
        await run.WaitForStopAsync(5, "deferred breakpoint");
        var stop = run.Stops[4];
        Assert.Equal("Helper.twice", stop.Frames[0].Name);
        Assert.Equal(25, stop.Frames[0].Line);
        Assert.Equal("Main.main", stop.Frames[1].Name);
        Assert.Equal(19, stop.Frames[1].Line);
        Assert.Equal("6", (await run.LocalsAsync(stop))["n"].Value);

        await run.Session.SetBreakpointsAsync(file, []);
        await run.Session.ContinueAsync();
        var end = await run.WaitForEndAsync();
        Assert.Null(end.Error);
        Assert.Equal(5, end.ExitCode);
        Assert.Contains("total=6 12", run.OutputText);
    }

    [Fact]
    public async Task A_breakpoint_on_a_line_without_code_moves_to_the_next_line()
    {
        var status = await ProvisionedAsync();
        // Line 10 is the closing brace of the loop: no instruction of its own; the next line with code is 11.
        var (run, _, _) = await RealRun.StartAsync("java", "Main.java", Source, 10, status);
        await using var _ = run;
        Assert.Equal(11, run.Stops[0].Frames[0].Line);
    }

    [Fact]
    public async Task A_breakpoint_path_with_windows_separators_and_drive_letter_case_matches_the_class()
    {
        var status = await ProvisionedAsync();
        // What a Windows client sends: backslashes and a drive letter. The debug info only knows "Main.java".
        var (run, _, _) = await RealRun.StartAsync("java", "Main.java", Source, 10, status, breakpointPath: _ => @"C:\Users\Learner\proj\Main.java");
        await using var _ = run;
        Assert.Equal(11, run.Stops[0].Frames[0].Line);
    }

    [Fact]
    public async Task An_uncaught_exception_stops_the_program_and_pause_stops_a_busy_one()
    {
        var status = await ProvisionedAsync();
        var (boom, _, _) = await RealRun.StartAsync("java", "Main.java",
            "public class Main {\n    public static void main(String[] args) {\n        throw new IllegalStateException(\"boom\");\n    }\n}\n", null, status);
        await using (boom)
        {
            Assert.Equal("exception", boom.Stops[0].Reason);
            Assert.Contains("IllegalStateException", boom.Stops[0].Description);
            Assert.Equal(3, boom.Stops[0].Frames[0].Line);
            await boom.Session.ContinueAsync();
            var end = await boom.WaitForEndAsync();
            Assert.Equal(1, end.ExitCode);
            Assert.Contains("boom", boom.OutputText);
        }

        var (busy, _, _) = await RealRun.StartAsync("java", "Main.java",
            "public class Main {\n    public static void main(String[] args) throws Exception {\n        long n = 0;\n        while (true) { n++; if (n % 1000000 == 0) Thread.sleep(1); }\n    }\n}\n",
            null, status, waitForStop: false);
        await using (busy)
        {
            await Task.Delay(500);
            await busy.Session.PauseAsync();
            await busy.WaitForStopAsync(1, "pause");
            Assert.Equal("pause", busy.Stops[0].Reason);
            Assert.Contains(busy.Stops[0].Frames, f => f.Name == "Main.main");   // the top frame may be inside Thread.sleep
            await busy.Session.StopAsync();
        }
    }

    [Fact]
    public async Task A_class_in_a_package_maps_back_to_its_file()
    {
        var status = await ProvisionedAsync();
        var (run, file, _) = await RealRun.StartAsync("java", Path.Combine("app", "Main.java"),
            "package app;\n\npublic class Main {\n    public static void main(String[] args) {\n        String s = \"pkg\";\n        System.out.println(s);\n    }\n}\n", 6, status);
        await using var _ = run;
        Assert.Equal(file, run.Stops[0].Frames[0].Source?.Path);
        Assert.Equal("\"pkg\"", (await run.LocalsAsync(run.Stops[0]))["s"].Value);
        await run.Session.SetBreakpointsAsync(file, []);
        await run.Session.ContinueAsync();
        Assert.Equal(0, (await run.WaitForEndAsync()).ExitCode);
    }
}

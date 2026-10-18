using System.Diagnostics;
using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

public class LimitsTests
{
    private static ExecutionRequest Py(string code, IsolationLevel iso = IsolationLevel.ProcessLimits) =>
        TestSupport.Request("python", "main.py", code, iso);

    [Fact]
    public async Task Infinite_loop_times_out_in_bounded_time()
    {
        await TestSupport.RequireAsync("python");
        var sw = Stopwatch.StartNew();
        ExecutionResult r = await TestSupport.RunAsync(Py("while True: pass\n"), limits: TestSupport.RunLimits(seconds: 1));
        Assert.Equal(ExecutionStatus.Timeout, r.Status);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6), $"took {sw.Elapsed}");
    }

    /// <summary>
    /// CPU time is summed over threads: a JVM (JIT and GC threads) spends 4+ CPU seconds in a 1-second wall run on a loaded machine. A CPU
    /// limit of wall+1 killed such a run with SIGXCPU - no output, an empty "crashed" - before the wall timeout could speak.
    /// </summary>
    [Fact]
    public void Cpu_time_limit_leaves_room_for_the_threads_of_a_multi_threaded_runtime()
    {
        if (OperatingSystem.IsWindows()) return;
        var (_, args) = LocalProcessSandbox.ApplyUnixLimits("/bin/true", [], TestSupport.RunLimits(seconds: 5));
        long cpuSeconds = long.Parse(System.Text.RegularExpressions.Regex.Match(args[1], @"ulimit -t (\d+)").Groups[1].Value);
        Assert.True(cpuSeconds >= 6, $"ulimit -t {cpuSeconds}");
        Assert.True(cpuSeconds >= Math.Min(Environment.ProcessorCount, 8) * 5, $"ulimit -t {cpuSeconds} with {Environment.ProcessorCount} cores");
    }

    [Fact]
    public async Task Memory_hog_is_stopped()
    {
        await TestSupport.RequireAsync("python");
        ExecutionResult r = await TestSupport.RunAsync(
            Py("import time\nx = b'a' * (1024 ** 3)\ntime.sleep(10)\n"),
            limits: TestSupport.RunLimits(seconds: 20, memoryMb: 128));
        Assert.Equal(ExecutionStatus.MemoryLimitExceeded, r.Status);
        Assert.True(r.PeakMemoryBytes > 128L * 1024 * 1024);
    }

    [Fact]
    public async Task Output_flood_is_truncated()
    {
        await TestSupport.RequireAsync("python");
        const int cap = 16 * 1024;
        ExecutionResult r = await TestSupport.RunAsync(
            Py("while True: print('x' * 1000)\n"), limits: TestSupport.RunLimits(seconds: 20, outputBytes: cap));
        Assert.Equal(ExecutionStatus.OutputLimitExceeded, r.Status);
        Assert.True(r.Stdout.Length + r.Stderr.Length <= cap);
        Assert.True(r.Stdout.Length > 0);
    }

    [Fact]
    public async Task Cancellation_kills_the_program()
    {
        await TestSupport.RequireAsync("python");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var sw = Stopwatch.StartNew();
        ExecutionResult r = await TestSupport.Runner.RunOnceAsync(Py("while True: pass\n"), null, TestSupport.RunLimits(seconds: 30), cts.Token);
        Assert.Equal(ExecutionStatus.Cancelled, r.Status);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6));
    }

    [Fact]
    public async Task Timeout_kills_grandchild_processes()
    {
        await TestSupport.RequireAsync("python");
        ExecutionResult r = await TestSupport.RunAsync(
            Py("import subprocess, sys\nc = subprocess.Popen(['sleep', '300'])\nprint(c.pid, flush=True)\nwhile True: pass\n"),
            limits: TestSupport.RunLimits(seconds: 2));
        Assert.Equal(ExecutionStatus.Timeout, r.Status);
        int pid = int.Parse(r.Stdout.Trim());
        Assert.True(await WaitUntilGoneAsync(pid), $"child {pid} is still alive");
    }

    [Fact]
    public async Task Orphaned_grandchild_holding_pipes_does_not_hang_the_run()
    {
        await TestSupport.RequireAsync("python");
        var sw = Stopwatch.StartNew();
        ExecutionResult r = await TestSupport.RunAsync(
            Py("import subprocess\nc = subprocess.Popen(['sleep', '300'])\nprint(c.pid, flush=True)\n"),
            limits: TestSupport.RunLimits(seconds: 20));
        Assert.Equal(ExecutionStatus.Success, r.Status);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8), $"took {sw.Elapsed}");
        Assert.True(await WaitUntilGoneAsync(int.Parse(r.Stdout.Trim())));
    }

    private static async Task<bool> WaitUntilGoneAsync(int pid)
    {
        for (int i = 0; i < 40; i++)
        {
            try { using var p = Process.GetProcessById(pid); if (p.HasExited) return true; }
            catch (ArgumentException) { return true; }
            await Task.Delay(100);
        }
        return false;
    }

    [Fact]
    public async Task Concurrent_runs_never_exceed_the_limit()
    {
        await TestSupport.RequireAsync("python");
        ICodeRunner runner = ExecutionEngine.CreateDefault(new ExecutionOptions { MaxConcurrentRuns = 2, CacheDirectory = TestSupport.Options.CacheDirectory });
        var req = Py("import time\nprint(time.time())\ntime.sleep(0.5)\nprint(time.time())\n");
        var sw = Stopwatch.StartNew();
        ExecutionResult[] results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => runner.RunOnceAsync(req, null, TestSupport.RunLimits(), CancellationToken.None)));
        sw.Stop();

        var events = new List<(double T, int Delta)>();
        foreach (ExecutionResult r in results)
        {
            Assert.True(r.IsSuccess, r.Stderr);
            double[] t = r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            events.Add((t[0], +1));
            events.Add((t[1], -1));
        }
        int current = 0, max = 0;
        foreach (var e in events.OrderBy(e => e.T).ThenBy(e => e.Delta)) { current += e.Delta; max = Math.Max(max, current); }
        Assert.InRange(max, 1, 2);
        Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(1.5), $"3 sequential batches of 0.5 s expected, took {sw.Elapsed}");
    }
}

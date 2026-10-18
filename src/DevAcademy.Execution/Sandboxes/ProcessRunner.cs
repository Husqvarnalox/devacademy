using System.Diagnostics;
using System.Text;

namespace DevAcademy.Execution;

/// <summary>One fully resolved OS process invocation (after sandbox wrapping).</summary>
internal sealed record ProcessLaunch(
    string FileName,
    IReadOnlyList<string> Args,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    bool MonitorMemory,
    Func<Task>? OnKill = null);

/// <summary>Shared process supervision: stdin, capped output, wall timeout, memory polling, tree kill.</summary>
internal static class ProcessRunner
{
    private const int KillNone = 0, KillTimeout = 1, KillMemory = 2, KillOutput = 3, KillCancel = 4;
    private static readonly TimeSpan MemoryPollInterval = TimeSpan.FromMilliseconds(100);

    public static async Task<ProcessOutcome> RunAsync(ProcessLaunch launch, ResourceLimits limits, string? stdin, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(launch.FileName)
        {
            WorkingDirectory = launch.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in launch.Args) psi.ArgumentList.Add(a);
        psi.Environment.Clear();
        foreach (var (k, v) in launch.Environment) psi.Environment[k] = v;

        using var process = new Process { StartInfo = psi };
        var sw = Stopwatch.StartNew();
        process.Start();
        int rootPid = process.Id;

        int killReason = KillNone;
        Task? onKillTask = null;
        var knownPids = new List<int> { process.Id };
        void Kill(int reason)
        {
            if (Interlocked.CompareExchange(ref killReason, reason, KillNone) != KillNone) return;
            KillTree(process, knownPids, rootPid);
            if (launch.OnKill is not null) onKillTask = launch.OnKill();
        }

        using var timeoutCts = new CancellationTokenSource(limits.WallTimeout);
        using var timeoutReg = timeoutCts.Token.Register(() => Kill(KillTimeout));
        using var cancelReg = ct.Register(() => Kill(KillCancel));

        var output = new CappedOutput(limits.MaxOutputBytes, () => Kill(KillOutput));
        using var readCts = new CancellationTokenSource();
        Task outTask = output.PumpAsync(process.StandardOutput.BaseStream, isStdout: true, readCts.Token);
        Task errTask = output.PumpAsync(process.StandardError.BaseStream, isStdout: false, readCts.Token);
        Task stdinTask = WriteStdinAsync(process, stdin);

        long peak = 0;
        bool sampled = false;
        using var monitorCts = new CancellationTokenSource();
        Task monitorTask = launch.MonitorMemory
            ? Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30, monitorCts.Token).ConfigureAwait(false);
                    while (!monitorCts.IsCancellationRequested)
                    {
                        TreeSample? s = await ProcessTreeSampler.SampleAsync(process, monitorCts.Token).ConfigureAwait(false);
                        if (s is not null)
                        {
                            sampled = true;
                            if (s.RssBytes > peak) peak = s.RssBytes;
                            lock (knownPids) { knownPids.Clear(); knownPids.AddRange(s.Pids); }
                            if (s.RssBytes > limits.MaxMemoryBytes) { Kill(KillMemory); return; }
                        }
                        await Task.Delay(MemoryPollInterval, monitorCts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
            })
            : Task.CompletedTask;

        await process.WaitForExitAsync().ConfigureAwait(false);
        int? exitCode = null;
        try { exitCode = process.ExitCode; } catch (InvalidOperationException) { }

        // Pipes can be held open by orphaned grandchildren: give readers a grace period, then kill leftovers.
        Task readers = Task.WhenAll(outTask, errTask);
        if (!await TryWaitAsync(readers, TimeSpan.FromSeconds(1)).ConfigureAwait(false))
        {
            KillLeftovers(knownPids, rootPid);
            KillProcessGroup(rootPid);
            if (!await TryWaitAsync(readers, TimeSpan.FromSeconds(1)).ConfigureAwait(false))
                await readCts.CancelAsync().ConfigureAwait(false);
        }
        await TryWaitAsync(readers, TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        KillLeftovers(knownPids, rootPid);
        KillProcessGroup(rootPid);

        await monitorCts.CancelAsync().ConfigureAwait(false);
        await monitorTask.ConfigureAwait(false);
        await TryWaitAsync(stdinTask, TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        if (onKillTask is not null) await TryWaitAsync(onKillTask, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        sw.Stop();

        int reason = Volatile.Read(ref killReason);
        if (reason != KillNone) exitCode = null;
        (string so, string se) = output.Decode();
        return new ProcessOutcome(
            exitCode, so, se, sw.Elapsed,
            sampled ? peak : null,
            TimedOut: reason == KillTimeout,
            MemoryLimitExceeded: reason == KillMemory,
            OutputTruncated: output.Truncated,
            Cancelled: reason == KillCancel);
    }

    private static async Task WriteStdinAsync(Process process, string? stdin)
    {
        try
        {
            if (!string.IsNullOrEmpty(stdin))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(stdin);
                await process.StandardInput.BaseStream.WriteAsync(bytes).ConfigureAwait(false);
                await process.StandardInput.BaseStream.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (IOException) { } // the program closed stdin without reading it all
        catch (ObjectDisposedException) { }
        finally
        {
            try { process.StandardInput.Close(); } catch (IOException) { }
        }
    }

    private static void KillTree(Process process, List<int> knownPids, int rootPid)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
        KillLeftovers(knownPids, rootPid);
        KillProcessGroup(rootPid);
    }

    /// <summary>
    /// The Unix wrapper (see LocalProcessSandbox) makes the root a process-group leader when perl is available,
    /// so this reaches orphans that were reparented to init and are invisible to a tree walk. No-op otherwise (ESRCH).
    /// Written as <c>kill -s KILL -- -pgid</c> because dash (/bin/sh on Debian and Ubuntu) rejects <c>kill -9 -- -pgid</c> ("Illegal number").
    /// </summary>
    private static void KillProcessGroup(int rootPid)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, CreateNoWindow = true };
            foreach (string a in new[] { "-c", "kill -s KILL -- \"-$0\" 2>/dev/null", rootPid.ToString(System.Globalization.CultureInfo.InvariantCulture) }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            p?.WaitForExit(2000);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    /// <summary>Kills non-root pids seen in the last memory sample (orphaned grandchildren). Racy against pid reuse, so root is skipped.</summary>
    private static void KillLeftovers(List<int> knownPids, int rootPid)
    {
        int[] pids;
        lock (knownPids) pids = knownPids.ToArray();
        foreach (int pid in pids)
        {
            if (pid == rootPid) continue; // root pid may already be recycled
            try
            {
                using var p = Process.GetProcessById(pid);
                if (!p.HasExited) p.Kill();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
    }

    private static async Task<bool> TryWaitAsync(Task task, TimeSpan timeout)
    {
        try
        {
            await task.WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException) { return false; }
        catch (Exception) { return task.IsCompleted; } // faulted/cancelled pumps still count as finished
    }

    /// <summary>Collects stdout/stderr with a combined byte cap; exceeding it requests a kill.</summary>
    private sealed class CappedOutput(int cap, Action onExceeded)
    {
        private readonly MemoryStream _out = new(), _err = new();
        private readonly object _gate = new();
        private long _total;
        private bool _truncated;

        public bool Truncated { get { lock (_gate) return _truncated; } }

        public async Task PumpAsync(Stream stream, bool isStdout, CancellationToken ct)
        {
            byte[] buffer = new byte[8192];
            try
            {
                int n;
                while ((n = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    bool exceeded = false;
                    lock (_gate)
                    {
                        long room = cap - _total;
                        int take = (int)Math.Min(n, Math.Max(room, 0));
                        (isStdout ? _out : _err).Write(buffer, 0, take);
                        _total += take;
                        if (take < n && !_truncated) { _truncated = true; exceeded = true; }
                    }
                    if (exceeded) onExceeded();
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }

        public (string Stdout, string Stderr) Decode()
        {
            lock (_gate) return (Encoding.UTF8.GetString(_out.ToArray()), Encoding.UTF8.GetString(_err.ToArray()));
        }
    }
}

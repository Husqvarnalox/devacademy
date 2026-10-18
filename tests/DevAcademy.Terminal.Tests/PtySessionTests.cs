using System.Diagnostics;
using DevAcademy.Terminal.Vt;

namespace DevAcademy.Terminal.Tests;

/// <summary>Real pseudo-terminal sessions against /bin/sh (Unix only; the Windows ConPTY path is not covered here).</summary>
public class PtySessionTests
{
    /// <summary>
    /// Starts /bin/sh and returns once its first prompt is on the screen. Input written earlier is echoed by the tty right away, so
    /// a shell without readline (dash is /bin/sh on Debian and Ubuntu) then prints its prompt in front of the first command's output
    /// ("# hello") and tests that look for a line of its own would fail depending on timing.
    /// </summary>
    private static ITerminalSession Start(string? cwd = null, int cols = 80, int rows = 24)
    {
        var session = new PtyTerminalFactory().Start(new TerminalStartOptions(cwd ?? Path.GetTempPath(), cols, rows, Program: "/bin/sh"));
        var until = Stopwatch.StartNew();
        while (until.ElapsedMilliseconds < 10000 && Snapshot(session).Trim().Length == 0) Thread.Sleep(10);
        return session;
    }

    private static void RequireUnix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "PTY tests need /bin/sh");

    private static string Snapshot(ITerminalSession s)
    {
        lock (s.Screen.SyncRoot) return s.Screen.GetAllText();
    }

    private static async Task<bool> WaitAsync(Func<bool> cond, int timeoutMs = 10000)
    {
        var until = Stopwatch.StartNew();
        while (until.ElapsedMilliseconds < timeoutMs)
        {
            if (cond()) return true;
            await Task.Delay(20);
        }
        return cond();
    }

    private static Task<bool> WaitForLineAsync(ITerminalSession s, string line, int timeoutMs = 10000) =>
        WaitAsync(() => Snapshot(s).Split('\n').Any(l => l.TrimEnd() == line), timeoutMs);

    private static bool Alive(int pid)
    {
        try { return Process.GetProcessById(pid) is { HasExited: false }; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>The ConPTY path: a real cmd.exe behind a pseudo console, input typed with CR, output parsed by the emulator.</summary>
    [Fact]
    public async Task Windows_conpty_runs_cmd_and_shows_its_output()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "ConPTY is Windows only");
        using var s = new PtyTerminalFactory().Start(new TerminalStartOptions(Path.GetTempPath(), 80, 24, Program: "cmd.exe"));
        Assert.True(await WaitAsync(() => Snapshot(s).Trim().Length > 0, 15000), "cmd printed no banner");
        s.Write("echo hello-conpty\r");
        Assert.True(await WaitForLineAsync(s, "hello-conpty", 15000), Snapshot(s));
        s.Write("exit\r");
        Assert.True(await WaitAsync(() => !s.IsRunning, 15000), Snapshot(s));
    }

    [Fact]
    public async Task Echo_appears_in_the_emulated_screen()
    {
        RequireUnix();
        using var s = Start();
        s.Write("echo hello\n");
        Assert.True(await WaitForLineAsync(s, "hello"), Snapshot(s));
    }

    [Fact]
    public async Task Output_event_delivers_raw_decoded_text_including_escape_sequences_and_multibyte_characters()
    {
        RequireUnix();
        using var s = Start();
        var seen = new System.Text.StringBuilder();
        s.Output += t => { lock (seen) seen.Append(t); };
        s.Write("printf '\\033[1mbold\\033[0m \\342\\236\\234 done\\n'\n");
        Assert.True(await WaitAsync(() => { lock (seen) return seen.ToString().Contains("\u001B[1mbold\u001B[0m \u279C done", StringComparison.Ordinal); }), seen.ToString());
    }

    [Fact]
    public async Task Working_directory_and_terminal_environment_are_set()
    {
        RequireUnix();
        var dir = Directory.CreateTempSubdirectory("da-pty-").FullName;
        try
        {
            using var s = Start(Path.GetFullPath(dir));
            s.Write("pwd; echo T=$TERM; echo C=$COLORTERM\n");
            Assert.True(await WaitForLineAsync(s, "T=xterm-256color"), Snapshot(s));
            Assert.True(await WaitForLineAsync(s, "C=truecolor"), Snapshot(s));
            // macOS reports /private/var for temp dirs; compare by suffix of the unique leaf
            Assert.Contains(Snapshot(s).Split('\n'), l => l.TrimEnd().EndsWith(Path.GetFileName(dir), StringComparison.Ordinal) && !l.Contains("pwd"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Resize_reaches_the_process()
    {
        RequireUnix();
        using var s = Start(cols: 80, rows: 24);
        s.Write("stty size\n");
        Assert.True(await WaitForLineAsync(s, "24 80"), Snapshot(s));
        s.Resize(100, 30);
        s.Write("stty size\n");
        Assert.True(await WaitForLineAsync(s, "30 100"), Snapshot(s));
        Assert.Equal((100, 30), (s.Screen.Cols, s.Screen.Rows));
    }

    [Fact]
    public async Task Ctrl_C_interrupts_a_running_command_and_the_prompt_returns()
    {
        RequireUnix();
        using var s = Start();
        s.Write("echo started; sleep 100\n");
        Assert.True(await WaitForLineAsync(s, "started"), Snapshot(s));
        s.Write(KeyEncoder.EncodeControl('c')!);
        s.Write("printf '\\nafter-interrupt\\n'\n");   // the leading newline keeps the prompt of a shell without readline off that line
        Assert.True(await WaitForLineAsync(s, "after-interrupt"), Snapshot(s));
        Assert.True(s.IsRunning);
    }

    [Fact]
    public async Task Exit_code_is_reported_and_shown_on_screen()
    {
        RequireUnix();
        using var s = new PtyTerminalFactory().Start(new TerminalStartOptions(Path.GetTempPath(), Program: "/bin/sh", ExitMessageFormat: "[exit {0}]"));
        s.Write("exit 3\n");
        Assert.True(await WaitAsync(() => !s.IsRunning), "process did not exit");
        Assert.Equal(3, s.ExitCode);
        Assert.True(await WaitForLineAsync(s, "[exit 3]"), Snapshot(s));
    }

    [Fact]
    public async Task Ctrl_D_ends_the_shell()
    {
        RequireUnix();
        using var s = Start();
        s.Write("echo ready\n");
        Assert.True(await WaitForLineAsync(s, "ready"));
        s.Write(KeyEncoder.EncodeControl('d')!);
        Assert.True(await WaitAsync(() => !s.IsRunning));
        Assert.Equal(0, s.ExitCode);
    }

    [Fact]
    public async Task Kill_ends_the_shell_and_its_children_without_orphans()
    {
        RequireUnix();
        var s = Start();
        var shellPid = s.ProcessId!.Value;
        s.Write("sleep 100 & echo CHILD=$!\n");
        Assert.True(await WaitAsync(() => Snapshot(s).Split('\n').Any(l => l.StartsWith("CHILD=", StringComparison.Ordinal))), Snapshot(s));
        var childPid = int.Parse(Snapshot(s).Split('\n').First(l => l.StartsWith("CHILD=", StringComparison.Ordinal))[6..].Trim());
        Assert.True(Alive(childPid));

        s.Kill();
        Assert.True(await WaitAsync(() => !s.IsRunning));
        Assert.True(await WaitAsync(() => !Alive(shellPid), 5000), "shell still alive");
        Assert.True(await WaitAsync(() => !Alive(childPid), 5000), "background child orphaned");
        s.Dispose();
    }

    [Fact]
    public async Task Colored_output_from_a_real_process_is_parsed()
    {
        RequireUnix();
        using var s = Start();
        s.Write("printf '\\033[1;31mred\\033[0m \\033[38;5;33mblue\\033[0m\\n'\n");
        Assert.True(await WaitAsync(() =>
        {
            lock (s.Screen.SyncRoot)
                for (var i = 0; i < s.Screen.Rows; i++)
                    if (s.Screen.GetScreenLineText(i) == "red blue") return true;
            return false;
        }), Snapshot(s));
        lock (s.Screen.SyncRoot)
        {
            var row = Enumerable.Range(0, s.Screen.Rows).First(i => s.Screen.GetScreenLineText(i) == "red blue");
            var cells = s.Screen.ScreenLine(row).Cells;
            Assert.Equal(1, cells[0].Fg.Index);
            Assert.True(cells[0].Flags.HasFlag(CellFlags.Bold));
            Assert.Equal(33, cells[4].Fg.Index);
        }
    }
}

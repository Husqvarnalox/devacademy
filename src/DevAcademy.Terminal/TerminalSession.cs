using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using DevAcademy.Terminal.Vt;
using Porta.Pty;

namespace DevAcademy.Terminal;

/// <summary>What to start in a new terminal. Everything but <see cref="Cwd"/> is optional.</summary>
public sealed record TerminalStartOptions(
    string Cwd,
    int Cols = 80,
    int Rows = 24,
    string? Program = null,
    IReadOnlyList<string>? Args = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    string ExitMessageFormat = "[process exited with code {0}]",
    int ScrollbackLines = TerminalEmulator.DefaultScrollback);

/// <summary>A running (or finished) terminal: an emulated screen fed by a process on a pseudo-terminal.</summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>Shell name or program, for the tab title.</summary>
    string Name { get; }
    TerminalEmulator Screen { get; }
    bool IsRunning { get; }
    int? ExitCode { get; }
    int? ProcessId { get; }
    /// <summary>Output was processed (raised on a background thread; coalesce before touching UI).</summary>
    event Action? Changed;
    /// <summary>The process ended (raised on a background thread, once).</summary>
    event Action? Exited;
    /// <summary>Raw decoded text the process printed, escape sequences included (raised on a background thread, in order). For passive observers such as dev-server URL detection.</summary>
    event Action<string>? Output;
    /// <summary>Sends text to the process as typed input (no newline is added).</summary>
    void Write(string text);
    void Resize(int cols, int rows);
    /// <summary>Terminates the process and its process group.</summary>
    void Kill();
}

public interface ITerminalFactory
{
    ITerminalSession Start(TerminalStartOptions options);
}

public sealed class PtyTerminalFactory : ITerminalFactory
{
    public ITerminalSession Start(TerminalStartOptions options) => PtyTerminalSession.Start(options);
}

/// <summary>A real pseudo-terminal session (forkpty on Unix, ConPTY on Windows) through Porta.Pty. It is the user's own process: no sandbox.</summary>
public sealed class PtyTerminalSession : ITerminalSession
{
    private readonly IPtyConnection _pty;
    private readonly string _exitFormat;
    private readonly object _writeGate = new();
    private readonly ConcurrentQueue<string> _replies = new();
    private int _running = 1;
    private int _disposed;
    private int _exitCode = int.MinValue;
    private int _watchedExit = int.MinValue;

    private PtyTerminalSession(IPtyConnection pty, TerminalEmulator screen, string name, string exitFormat)
    {
        _pty = pty;
        Screen = screen;
        Name = name;
        _exitFormat = exitFormat;
        screen.Reply += r => _replies.Enqueue(r);
    }

    public string Name { get; }
    public TerminalEmulator Screen { get; }
    public bool IsRunning => Volatile.Read(ref _running) == 1;
    public int? ExitCode => _exitCode == int.MinValue ? null : _exitCode;
    public int? ProcessId => _pty.Pid;

    public event Action? Changed;
    public event Action? Exited;
    public event Action<string>? Output;

    public static PtyTerminalSession Start(TerminalStartOptions o)
    {
        var shell = o.Program is null ? ShellResolver.ResolveCurrent() : new ShellSpec(o.Program, o.Args ?? [], System.IO.Path.GetFileName(o.Program));
        var env = new Dictionary<string, string> { ["TERM"] = "xterm-256color", ["COLORTERM"] = "truecolor", ["TERM_PROGRAM"] = "DevAcademy" };
        if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("LANG")) && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("LC_ALL")))
            env["LANG"] = "en_US.UTF-8";
        if (o.Environment is not null) foreach (var (k, v) in o.Environment) env[k] = v;

        var options = new PtyOptions
        {
            Name = "DevAcademy",
            Cols = Math.Max(2, o.Cols),
            Rows = Math.Max(2, o.Rows),
            Cwd = o.Cwd,
            App = shell.Path,
            CommandLine = shell.Args.ToArray(),
            Environment = env,
        };
        var pty = PtyProvider.SpawnAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        var screen = new TerminalEmulator(options.Cols, options.Rows, o.ScrollbackLines);
        var session = new PtyTerminalSession(pty, screen, shell.DisplayName, o.ExitMessageFormat);
        session.BeginRead();
        if (OperatingSystem.IsWindows()) session.BeginExitWatch();
        return session;
    }

    private void BeginRead() => Task.Factory.StartNew(ReadLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>ConPTY keeps the output pipe open after the shell exits until the pseudoconsole is closed, so the reader never sees EOF. Watch the process, give the output a moment to drain, then close the console to end the read.</summary>
    private void BeginExitWatch() => Task.Run(() =>
    {
        try
        {
            if (!_pty.WaitForExit(Timeout.Infinite)) return;
            Volatile.Write(ref _watchedExit, _pty.ExitCode);
            Thread.Sleep(500);
            if (IsRunning) _pty.Dispose();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or IOException or System.ComponentModel.Win32Exception) { }
    });

    private void ReadLoop()
    {
        var buffer = new byte[16 * 1024];
        var decoder = Encoding.UTF8.GetDecoder();
        var chars = new char[buffer.Length + 4];
        try
        {
            while (true)
            {
                var n = _pty.ReaderStream.Read(buffer, 0, buffer.Length);
                if (n <= 0) break;
                lock (Screen.SyncRoot) Screen.Feed(buffer.AsSpan(0, n));
                FlushReplies();
                Changed?.Invoke();
                if (Output is { } output)
                {
                    var count = decoder.GetChars(buffer, 0, n, chars, 0);
                    if (count > 0) output(new string(chars, 0, count));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // EIO on the master side is how Linux reports the child's exit; treat every read failure as end of output.
        }

        var code = -1;
        try
        {
            var watched = Volatile.Read(ref _watchedExit);
            if (watched != int.MinValue) code = watched;
            else if (_pty.WaitForExit(3000)) code = _pty.ExitCode;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException) { }

        _exitCode = code;
        Volatile.Write(ref _running, 0);
        if (Volatile.Read(ref _disposed) == 0)
        {
            lock (Screen.SyncRoot) Screen.Feed("\r\n\e[2m" + string.Format(System.Globalization.CultureInfo.InvariantCulture, _exitFormat, code) + "\e[0m\r\n");
            Changed?.Invoke();
        }
        Exited?.Invoke();
    }

    private void FlushReplies()
    {
        while (_replies.TryDequeue(out var reply)) Write(reply);
    }

    public void Write(string text)
    {
        if (!IsRunning || Volatile.Read(ref _disposed) == 1) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            lock (_writeGate)
            {
                _pty.WriterStream.Write(bytes, 0, bytes.Length);
                _pty.WriterStream.Flush();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { /* the process is already gone */ }
    }

    public void Resize(int cols, int rows)
    {
        cols = Math.Max(2, cols);
        rows = Math.Max(2, rows);
        lock (Screen.SyncRoot) Screen.Resize(cols, rows);
        if (!IsRunning) return;
        try { _pty.Resize(cols, rows); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    public void Kill()
    {
        if (!IsRunning) return;
        var pid = _pty.Pid;
        if (!OperatingSystem.IsWindows() && pid > 1)
        {
            // The shell is a session leader (forkpty/setsid): hang up its group first so interactive shells can clean their jobs.
            Native.KillGroup(pid, Native.SigHup);
            if (_pty.WaitForExit(400)) { Native.KillGroup(pid, Native.SigKill); Native.KillSession(pid); return; }
            Native.KillGroup(pid, Native.SigKill);
            Native.KillSession(pid);
        }
        try { _pty.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException) { }
        try { _pty.WaitForExit(2000); } catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try { Kill(); } catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException) { }
        try { _pty.Dispose(); } catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }

    private static class Native
    {
        public const int SigHup = 1;
        public const int SigKill = 9;

        [DllImport("libc", EntryPoint = "killpg", SetLastError = true)]
        private static extern int KillPg(int pgrp, int sig);

        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        private static extern int Kill(int pid, int sig);

        public static void KillGroup(int pgid, int signal)
        {
            try { _ = KillPg(pgid, signal); }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        }

        /// <summary>
        /// Kills every process left in the shell's session. Shells with job control put each background job into its own process group,
        /// so killing the shell's group misses them, and a shell that does not forward SIGHUP (dash, /bin/sh on Debian and Ubuntu) leaves them
        /// running with the terminal's slave side open: the session would never end. The shell is the session leader, so its pid is the session id.
        /// </summary>
        public static void KillSession(int sid)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("pgrep") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                psi.ArgumentList.Add("-s");
                psi.ArgumentList.Add(sid.ToString(System.Globalization.CultureInfo.InvariantCulture));
                using var pgrep = System.Diagnostics.Process.Start(psi);
                if (pgrep is null) return;
                var output = pgrep.StandardOutput.ReadToEndAsync();
                if (!pgrep.WaitForExit(2000)) { pgrep.Kill(); return; }
                foreach (var line in output.GetAwaiter().GetResult().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (int.TryParse(line, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid) && pid > 1 && pid != Environment.ProcessId)
                        _ = Kill(pid, SigKill);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { }
        }
    }
}

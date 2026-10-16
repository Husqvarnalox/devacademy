using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace DevAcademy.Debugging.Adapters;

/// <summary>A running debug adapter: the stream to read DAP messages from, the stream to write them to, and a way to end it.</summary>
public interface IAdapterProcess : IAsyncDisposable
{
    /// <summary>What the adapter sends (client reads).</summary>
    Stream FromAdapter { get; }
    /// <summary>What the client sends.</summary>
    Stream ToAdapter { get; }
    Task Exited { get; }
    void Kill();
    /// <summary>Last part of the adapter's stderr (and its stdout log lines for TCP adapters), for error messages.</summary>
    string LogTail { get; }
}

public interface IAdapterLauncher
{
    Task<IAdapterProcess> LaunchAsync(ResolvedAdapter adapter, string workingDirectory, CancellationToken ct);
}

/// <summary>Starts the adapter as an ordinary child process (never sandboxed: debuggers need ptrace / task_for_pid).</summary>
public sealed class ProcessAdapterLauncher : IAdapterLauncher
{
    private static readonly TimeSpan AnnounceTimeout = TimeSpan.FromSeconds(20);

    public async Task<IAdapterProcess> LaunchAsync(ResolvedAdapter adapter, string workingDirectory, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(adapter.Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (string a in adapter.Args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in adapter.Environment) psi.Environment[k] = v;
        var p = Process.Start(psi) ?? throw new InvalidOperationException($"{adapter.Executable} did not start");
        var tail = new LogTail();
        _ = Task.Run(() => Drain(p.StandardError, tail));

        if (adapter.Announce is null)
            return new StdioProcess(p, tail);

        // Delve (and other TCP adapters) print "... listening at: 127.0.0.1:port" and wait for a client.
        var found = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            try
            {
                var buf = new char[1024];
                var pending = new StringBuilder();
                int n;
                while ((n = await p.StandardOutput.ReadAsync(buf).ConfigureAwait(false)) > 0)
                {
                    tail.Append(buf, n);
                    if (found.Task.IsCompleted) continue;
                    pending.Append(buf, 0, n);
                    var m = adapter.Announce.Match(pending.ToString());
                    if (m.Success) found.TrySetResult(m.Groups[1].Value);
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
            found.TrySetException(new InvalidOperationException("The debug adapter exited before announcing its address.\n" + tail.Text));
        });
        try
        {
            string address = await found.Task.WaitAsync(AnnounceTimeout, ct).ConfigureAwait(false);
            int colon = address.LastIndexOf(':');
            string host = address[..colon].Trim('[', ']');
            if (!IPAddress.TryParse(host, out var ip) || !IPAddress.IsLoopback(ip))
                throw new InvalidOperationException($"The debug adapter listens on a non-loopback address ({address}); refusing to connect.");
            var tcp = new TcpClient { NoDelay = true };
            await tcp.ConnectAsync(ip, int.Parse(address[(colon + 1)..], System.Globalization.CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
            return new TcpProcess(p, tcp, tail);
        }
        catch
        {
            try { p.Kill(true); } catch (InvalidOperationException) { }
            p.Dispose();
            throw;
        }
    }

    private static async Task Drain(StreamReader r, LogTail tail)
    {
        try
        {
            var buf = new char[1024];
            int n;
            while ((n = await r.ReadAsync(buf).ConfigureAwait(false)) > 0) tail.Append(buf, n);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    private sealed class LogTail
    {
        private readonly StringBuilder _sb = new();
        public void Append(char[] buf, int n)
        {
            lock (_sb)
            {
                _sb.Append(buf, 0, n);
                if (_sb.Length > 8192) _sb.Remove(0, _sb.Length - 8192);
            }
        }
        public string Text { get { lock (_sb) return _sb.ToString(); } }
    }

    private class StdioProcess(Process p, LogTail tail) : IAdapterProcess
    {
        protected readonly Process Process = p;
        public virtual Stream FromAdapter => Process.StandardOutput.BaseStream;
        public virtual Stream ToAdapter => Process.StandardInput.BaseStream;
        public Task Exited { get; } = p.WaitForExitAsync();
        public string LogTail => tail.Text;

        public void Kill()
        {
            try { if (!Process.HasExited) Process.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }

        public virtual async ValueTask DisposeAsync()
        {
            Kill();
            try { await Task.WhenAny(Exited, Task.Delay(2000)).ConfigureAwait(false); } catch (InvalidOperationException) { }
            Process.Dispose();
        }
    }

    private sealed class TcpProcess(Process p, TcpClient tcp, LogTail tail) : StdioProcess(p, tail)
    {
        public override Stream FromAdapter => tcp.GetStream();
        public override Stream ToAdapter => tcp.GetStream();
        public override async ValueTask DisposeAsync()
        {
            tcp.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}

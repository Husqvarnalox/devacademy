using System.Diagnostics;
using System.Text;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.LanguageServices.Hosting;

/// <summary>A running server: the streams to talk to it and a way to end it. Abstracted so tests can use an in-process fake.</summary>
public interface IServerProcess : IAsyncDisposable
{
    /// <summary>Server stdout (what the client reads).</summary>
    Stream Output { get; }
    /// <summary>Server stdin (what the client writes).</summary>
    Stream Input { get; }
    Task Exited { get; }
    int? ProcessId { get; }
    void Kill();
    string StderrTail { get; }
}

public interface IServerLauncher
{
    Task<IServerProcess> LaunchAsync(ResolvedServer server, string workingDirectory, string searchPath, CancellationToken ct);
}

public sealed class ProcessServerLauncher : IServerLauncher
{
    public Task<IServerProcess> LaunchAsync(ResolvedServer server, string workingDirectory, string searchPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(server.Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in server.Args) psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = searchPath;
        var p = Process.Start(psi) ?? throw new InvalidOperationException($"{server.Executable} did not start");
        return Task.FromResult<IServerProcess>(new ProcessHandle(p));
    }

    private sealed class ProcessHandle : IServerProcess
    {
        private readonly Process _p;
        private readonly StringBuilder _stderr = new();
        private readonly Task _drain;

        public ProcessHandle(Process p)
        {
            _p = p;
            ProcessId = p.Id;
            Exited = p.WaitForExitAsync();
            _drain = Task.Run(async () =>
            {
                try
                {
                    var buf = new char[1024];
                    int n;
                    while ((n = await p.StandardError.ReadAsync(buf).ConfigureAwait(false)) > 0)
                        lock (_stderr)
                        {
                            _stderr.Append(buf, 0, n);
                            if (_stderr.Length > 8192) _stderr.Remove(0, _stderr.Length - 8192);
                        }
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
            });
        }

        public Stream Output => _p.StandardOutput.BaseStream;
        public Stream Input => _p.StandardInput.BaseStream;
        public Task Exited { get; }
        public int? ProcessId { get; }
        public string StderrTail { get { lock (_stderr) return _stderr.ToString(); } }

        public void Kill()
        {
            try { if (!_p.HasExited) _p.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }

        public async ValueTask DisposeAsync()
        {
            Kill();
            try { await Task.WhenAny(Exited, Task.Delay(2000)).ConfigureAwait(false); } catch (InvalidOperationException) { }
            _p.Dispose();
        }
    }
}

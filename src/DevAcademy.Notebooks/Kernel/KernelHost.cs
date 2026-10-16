using System.Diagnostics;
using System.Text;
using DevAcademy.Notebooks.Protocol;

namespace DevAcademy.Notebooks.Kernel;

/// <summary>A started kernel process (or a stand-in for one in tests).</summary>
public interface IKernelProcess : IDisposable
{
    /// <summary>OS process id, or null when the kernel is not a separate process.</summary>
    int? ProcessId { get; }
    bool HasExited { get; }
    int? ExitCode { get; }
    event Action? Exited;
    /// <summary>Last lines the kernel wrote to stderr (startup failures are explained there).</summary>
    string StderrTail { get; }
    /// <summary>Delivers SIGINT. False when the platform has no signals for this (the client then uses the interrupt_request message).</summary>
    bool TrySignalInterrupt();
    void Kill();
}

/// <summary>Starts kernel processes. The connection info has been chosen by the client; the host must make the kernel listen on it.</summary>
public interface IKernelHost
{
    Task<IKernelProcess> StartAsync(ConnectionInfo connection, CancellationToken ct);
}

/// <summary>
/// Launches <c>python -m ipykernel_launcher -f &lt;connection.json&gt;</c> with a given interpreter. The kernel is an ordinary child process
/// with the user's privileges, environment and network access: there is no sandbox. The connection file (it holds the HMAC key) lives in a
/// private temp directory that is removed when the process object is disposed.
/// </summary>
public sealed class PythonKernelHost(string interpreter, string workingDirectory) : IKernelHost
{
    public string Interpreter { get; } = interpreter;

    public Task<IKernelProcess> StartAsync(ConnectionInfo connection, CancellationToken ct)
    {
        if (!File.Exists(Interpreter)) throw new FileNotFoundException("Python interpreter not found: " + Interpreter);
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-kernel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var file = Path.Combine(dir, "kernel.json");
        connection.WriteTo(file);

        var psi = new ProcessStartInfo(Interpreter)
        {
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Path.GetTempPath(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("ipykernel_launcher");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(file);
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYDEVD_DISABLE_FILE_VALIDATION"] = "1";
        // ipykernel exits when this process disappears, so a crashed app does not leave kernels behind.
        psi.Environment["JPY_PARENT_PID"] = Environment.ProcessId.ToString();
        foreach (var hostile in new[] { "PYTHONHOME", "PYTHONSTARTUP", "VIRTUAL_ENV" }) psi.Environment.Remove(hostile);

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new InvalidOperationException("The kernel process did not start.");
        }
        catch
        {
            TryDeleteDirectory(dir);
            throw;
        }
        return Task.FromResult<IKernelProcess>(new LocalKernelProcess(process, dir));
    }

    internal static void TryDeleteDirectory(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private sealed class LocalKernelProcess : IKernelProcess
    {
        private readonly Process _process;
        private readonly string _dir;
        private readonly StringBuilder _stderr = new();
        private bool _disposed;

        public LocalKernelProcess(Process process, string dir)
        {
            _process = process;
            _dir = dir;
            _process.StandardInput.Close(); // the kernel never reads stdin
            _process.OutputDataReceived += (_, _) => { }; // drained and ignored (the kernel's own streams travel over ZeroMQ)
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                lock (_stderr)
                {
                    _stderr.AppendLine(e.Data);
                    if (_stderr.Length > 8192) _stderr.Remove(0, _stderr.Length - 8192);
                }
            };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _process.EnableRaisingEvents = true;
            _process.Exited += (_, _) => Exited?.Invoke();
        }

        public int? ProcessId => _process.Id;
        public bool HasExited { get { try { return _process.HasExited; } catch (InvalidOperationException) { return true; } } }
        public int? ExitCode => HasExited ? _process.ExitCode : null;
        public event Action? Exited;
        public string StderrTail { get { lock (_stderr) return _stderr.ToString(); } }

        public bool TrySignalInterrupt()
        {
            if (OperatingSystem.IsWindows() || HasExited) return false;
            try
            {
                using var kill = Process.Start(new ProcessStartInfo("kill") { ArgumentList = { "-INT", _process.Id.ToString() }, UseShellExecute = false, CreateNoWindow = true });
                kill?.WaitForExit(2000);
                return kill?.ExitCode == 0;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
        }

        public void Kill()
        {
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Kill();
            _process.Dispose();
            TryDeleteDirectory(_dir);
        }
    }
}

using System.Diagnostics;
using System.Text;

namespace DevAcademy.Execution;

/// <summary>Result of a short-lived management tool (python probe, uv, nvidia-smi...). Not learner code: never sandboxed.</summary>
public sealed record ToolOutput(int? ExitCode, string Stdout, string Stderr, bool TimedOut = false, string? StartError = null)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut && StartError is null;

    /// <summary>The most useful failure text: stderr, else stdout, else the start error.</summary>
    public string FailureText
    {
        get
        {
            string text = StartError ?? (Stderr.Trim().Length > 0 ? Stderr : Stdout);
            text = text.Trim();
            if (TimedOut) text = "timed out. " + text;
            return text.Length > 1500 ? "..." + text[^1500..] : text;
        }
    }
}

public sealed record ToolRunOptions(
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    TimeSpan? Timeout = null,
    string? Stdin = null,
    /// <summary>Names of inherited environment variables to drop (e.g. PIP_TARGET) so a tool cannot be redirected by the user's shell.</summary>
    IReadOnlyList<string>? RemoveEnvironment = null);

/// <summary>
/// Runs the helper tools of the Python Environment Manager and the ML Runtime Manager. A seam: tests substitute canned outputs,
/// so nothing about nvidia-smi, rocminfo, uv or framework probes has to exist on the test machine.
/// </summary>
public interface IToolRunner
{
    /// <summary>Resolves a tool name on PATH (plus the well-known extra directories); null when it is not installed.</summary>
    string? Find(string name);

    Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct);

    /// <summary>Runs the tool and reports every output line (stdout and stderr merged) as it arrives. Cancelling kills the process tree.</summary>
    Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct);
}

public sealed class SystemToolRunner : IToolRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private const int MaxCapturedChars = 4 * 1024 * 1024;

    public string? Find(string name) => ToolLocator.Find(name);

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        RunCoreAsync(fileName, args, options, null, ct);

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct) =>
        RunCoreAsync(fileName, args, options, onLine, ct);

    private static async Task<ToolOutput> RunCoreAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string>? onLine, CancellationToken ct)
    {
        options ??= new ToolRunOptions();
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (options.WorkingDirectory is not null) psi.WorkingDirectory = options.WorkingDirectory;
        foreach (string a in args) psi.ArgumentList.Add(a);
        foreach (string name in options.RemoveEnvironment ?? []) psi.Environment.Remove(name);
        psi.Environment["PATH"] = ToolLocator.SearchPath();
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";
        if (options.Environment is not null)
            foreach (var (k, v) in options.Environment) psi.Environment[k] = v;

        Process process;
        try { process = Process.Start(psi) ?? throw new InvalidOperationException("process did not start"); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new ToolOutput(null, "", "", StartError: $"could not start '{fileName}': {ex.Message}");
        }

        using (process)
        {
            using var timeoutCts = new CancellationTokenSource(options.Timeout ?? DefaultTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var gate = new object();

            async Task Pump(StreamReader reader, StringBuilder sink)
            {
                try
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync(linked.Token).ConfigureAwait(false)) is not null)
                    {
                        lock (gate)
                        {
                            if (sink.Length < MaxCapturedChars) sink.AppendLine(line);
                        }
                        onLine?.Invoke(line);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }

            Task outTask = Pump(process.StandardOutput, stdout);
            Task errTask = Pump(process.StandardError, stderr);
            try
            {
                if (!string.IsNullOrEmpty(options.Stdin))
                    await process.StandardInput.WriteAsync(options.Stdin.AsMemory(), linked.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
            finally
            {
                try { process.StandardInput.Close(); } catch (IOException) { }
            }

            bool timedOut = false;
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                timedOut = timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested;
                if (!timedOut) ct.ThrowIfCancellationRequested();
            }
            // Grandchildren can keep the pipes open: do not wait forever for the readers.
            try { await Task.WhenAll(outTask, errTask).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch (TimeoutException) { }
            int? exit = null;
            try { exit = process.ExitCode; } catch (InvalidOperationException) { }
            lock (gate) return new ToolOutput(timedOut ? null : exit, stdout.ToString(), stderr.ToString(), timedOut);
        }
    }
}

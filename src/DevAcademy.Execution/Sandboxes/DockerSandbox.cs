using System.Diagnostics;
using System.Globalization;

namespace DevAcademy.Execution;

/// <summary>
/// Docker container sandbox. The spec's FileName is a bare command inside <see cref="ProcessSpec.ContainerImage"/>.
/// Memory is enforced by the container (exit 137 without our own kill is reported as MemoryLimitExceeded; peak memory is not measured).
/// </summary>
public sealed class DockerSandbox : IProcessSandbox
{
    private Task<bool>? _available;

    public IsolationLevel Level => IsolationLevel.Container;

    public Task<bool> IsAvailableAsync(CancellationToken ct) => _available ??= ProbeAsync();

    private static async Task<bool> ProbeAsync()
    {
        if (ToolLocator.Find("docker") is not { } docker) return false;
        try
        {
            var psi = new ProcessStartInfo(docker) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("info");
            using var p = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                Task<string> o = p.StandardOutput.ReadToEndAsync(cts.Token), e = p.StandardError.ReadToEndAsync(cts.Token);
                await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                await Task.WhenAll(o, e).ConfigureAwait(false);
                return p.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch (InvalidOperationException) { }
                return false;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public async Task<ProcessOutcome> RunAsync(ProcessSpec spec, ResourceLimits limits, string? stdin, CancellationToken ct)
    {
        if (spec.ContainerImage is null)
            throw new InvalidOperationException("Container isolation needs ProcessSpec.ContainerImage.");
        string docker = ToolLocator.Find("docker") ?? throw new InvalidOperationException("docker executable not found.");
        string name = "devacademy-" + Guid.NewGuid().ToString("N");
        string workDir = MacSandboxExecSandbox.RealPath(spec.WorkingDirectory);
        var args = BuildArguments(name, workDir, limits, spec);

        var launch = new ProcessLaunch(
            docker, args, spec.WorkingDirectory,
            InheritedEnvironment(), MonitorMemory: false,
            OnKill: () => KillContainerAsync(docker, name));
        ProcessOutcome o = await ProcessRunner.RunAsync(launch, limits, stdin, ct).ConfigureAwait(false);

        if (o.ExitCode == 125 && o.Stderr.Contains("Unable to find image", StringComparison.Ordinal))
            throw new InvalidOperationException($"Docker image '{spec.ContainerImage}' is not available locally (docker pull {spec.ContainerImage}).");
        bool oom = o is { ExitCode: 137, TimedOut: false, Cancelled: false, OutputTruncated: false };
        return oom ? o with { MemoryLimitExceeded = true, ExitCode = null } : o;
    }

    internal static IReadOnlyList<string> BuildArguments(string name, string workDir, ResourceLimits limits, ProcessSpec spec)
    {
        long memMb = Math.Max(16, limits.MaxMemoryBytes / (1024 * 1024));
        string mem = memMb.ToString(CultureInfo.InvariantCulture) + "m";
        var a = new List<string> { "run", "--rm", "-i", "--pull", "never", "--name", name };
        if (!limits.AllowNetwork) a.AddRange(["--network", "none"]);
        a.AddRange(["--memory", mem, "--memory-swap", mem, "--pids-limit", "64", "--cpus", "1",
            "--read-only", "--tmpfs", "/tmp:rw,size=64m",
            "--security-opt", "no-new-privileges", "--cap-drop", "ALL", "--user", "65534:65534",
            "-v", $"{workDir}:{workDir}", "-w", workDir]);
        var env = LocalProcessSandbox.BuildEnvironment(spec with { WorkingDirectory = workDir });
        env.Remove("PATH"); // the image's own PATH applies
        foreach (var (k, v) in env) a.AddRange(["-e", $"{k}={v}"]);
        a.Add(spec.ContainerImage!);
        a.Add(spec.FileName);
        a.AddRange(spec.Args);
        return a;
    }

    // The docker CLI (not learner code) needs HOME/DOCKER_HOST to find the daemon, so it keeps the full environment.
    private static Dictionary<string, string> InheritedEnvironment()
    {
        var env = new Dictionary<string, string>();
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            env[(string)e.Key] = (string?)e.Value ?? "";
        env["PATH"] = ToolLocator.SearchPath();
        return env;
    }

    private static async Task KillContainerAsync(string docker, string name)
    {
        try
        {
            var psi = new ProcessStartInfo(docker) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("kill");
            psi.ArgumentList.Add(name);
            using var p = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}

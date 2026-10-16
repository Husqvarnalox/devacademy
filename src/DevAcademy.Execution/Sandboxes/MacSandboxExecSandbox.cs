using System.Diagnostics;
using System.Text;

namespace DevAcademy.Execution;

/// <summary>
/// macOS sandbox-exec: no network, writes only to the workdir (which holds TMPDIR/HOME), the runner cache and a few device nodes.
/// Reads are unrestricted except for common credential directories. The profile is deprecated-but-working Apple SBPL.
/// </summary>
public sealed class MacSandboxExecSandbox(string cacheDirectory) : LocalProcessSandbox
{
    private const string SandboxExec = "/usr/bin/sandbox-exec";
    private Task<bool>? _available;

    public override IsolationLevel Level => IsolationLevel.OsSandbox;

    public override Task<bool> IsAvailableAsync(CancellationToken ct) => _available ??= ProbeAsync();

    // Nested sandboxes are refused by the kernel, so prove that sandbox-exec really works here.
    private static async Task<bool> ProbeAsync()
    {
        if (!OperatingSystem.IsMacOS() || !File.Exists(SandboxExec)) return false;
        try
        {
            var psi = new ProcessStartInfo(SandboxExec) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (string a in new[] { "-p", "(version 1)(allow default)", "/usr/bin/true" }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return p.ExitCode == 0;
        }
        catch (Exception ex) when (ex is OperationCanceledException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    protected override (string File, IReadOnlyList<string> Args) Wrap(
        string file, IReadOnlyList<string> args, ProcessSpec spec, ResourceLimits limits)
    {
        var wrapped = new List<string> { "-p", BuildProfile(spec.WorkingDirectory, cacheDirectory, limits.AllowNetwork, spec.ReadOnlyPaths), file };
        wrapped.AddRange(args);
        return (SandboxExec, wrapped);
    }

    internal static string BuildProfile(string workDir, string cacheDir, bool allowNetwork, IReadOnlyList<string>? readOnlyPaths = null)
    {
        var sb = new StringBuilder();
        sb.Append("(version 1)(allow default)");
        if (!allowNetwork) sb.Append("(deny network*)");
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (string secret in new[] { ".ssh", ".aws", ".gnupg", "Library/Keychains" })
            foreach (string p in PathVariants(Path.Combine(home, secret)))
                sb.Append($"(deny file-read* (subpath {Quote(p)}))");
        sb.Append("(deny file-write*)");
        sb.Append("(allow file-write*");
        foreach (string dir in new[] { workDir, cacheDir })
            foreach (string p in PathVariants(dir))
                sb.Append($" (subpath {Quote(p)})");
        // .NET host/NuGet keep named-mutex shared memory below /tmp/.dotnet (not relocatable); dotnet build fails without it.
        sb.Append(" (subpath \"/private/tmp/.dotnet\")");
        // `dotnet build file.cs` keeps its file-based-app cache in the real user's Application Support (HOME is ignored there).
        foreach (string p in PathVariants(Path.Combine(home, "Library", "Application Support", "dotnet")))
            sb.Append($" (subpath {Quote(p)})");
        foreach (string dev in new[] { "/dev/null", "/dev/tty", "/dev/dtracehelper" })
            sb.Append($" (literal {Quote(dev)})");
        sb.Append(')');
        // Last matching rule wins in SBPL: an explicit read-only deny after the allows keeps an environment read-only even if it were nested in a writable area.
        if (readOnlyPaths is { Count: > 0 })
            foreach (string ro in readOnlyPaths)
                foreach (string p in PathVariants(ro))
                    sb.Append($"(allow file-read* (subpath {Quote(p)}))(deny file-write* (subpath {Quote(p)}))");
        return sb.ToString();
    }

    // The profile matches resolved paths, so allow both the given spelling and its realpath (/var -> /private/var).
    private static IEnumerable<string> PathVariants(string path)
    {
        string full = Path.GetFullPath(path);
        yield return full;
        string real = RealPath(full);
        if (real != full) yield return real;
    }

    internal static string RealPath(string path)
    {
        string current = Path.GetPathRoot(path) ?? "/";
        foreach (string part in path[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            for (int hops = 0; hops < 32; hops++)
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (info.LinkTarget is null) break;
                current = Path.GetFullPath(info.LinkTarget, Path.GetDirectoryName(current) ?? "/");
            }
        }
        return current;
    }

    internal static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

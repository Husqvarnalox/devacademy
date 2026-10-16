using System.Diagnostics;
using System.Text;

namespace DevAcademy.Execution;

/// <summary>PATH lookup and short version probes used by language adapters and sandboxes.</summary>
internal static class ToolLocator
{
    private static readonly string[] ExtraUnixDirs = ["/opt/homebrew/bin", "/usr/local/bin"];

    private static readonly AsyncLocal<IReadOnlyList<string>?> DirsOverride = new();

    /// <summary>Test seam: restricts lookups (in this async flow) to the given directories, e.g. none to simulate missing tools.</summary>
    internal static IDisposable OverrideSearchDirs(IReadOnlyList<string> dirs)
    {
        IReadOnlyList<string>? previous = DirsOverride.Value;
        DirsOverride.Value = dirs;
        return new Restore(previous);
    }

    private sealed class Restore(IReadOnlyList<string>? previous) : IDisposable
    {
        public void Dispose() => DirsOverride.Value = previous;
    }

    private static IEnumerable<string> Dirs()
    {
        if (DirsOverride.Value is { } forced) return forced;
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var dirs = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        // GUI apps on macOS start with a minimal PATH that lacks Homebrew and rustup.
        if (!OperatingSystem.IsWindows())
        {
            dirs.AddRange(ExtraUnixDirs);
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin"));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sdkman", "candidates", "kotlin", "current", "bin"));
        }
        return dirs.Distinct();
    }

    /// <summary>PATH handed to child processes: the inherited one plus the well-known extra directories.</summary>
    public static string SearchPath() => string.Join(Path.PathSeparator, Dirs());

    public static string? Find(string name)
    {
        string[] exts = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        foreach (string dir in Dirs())
            foreach (string ext in exts)
            {
                string candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    /// <summary>Runs the tool briefly and returns the first non-empty output line (stdout then stderr), or null when it fails.</summary>
    public static async Task<string?> ProbeAsync(string exe, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = SearchPath();
        psi.Environment["DOTNET_NOLOGO"] = "1";
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Close();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                Task<string> o = p.StandardOutput.ReadToEndAsync(cts.Token), e = p.StandardError.ReadToEndAsync(cts.Token);
                await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                string text = await o.ConfigureAwait(false) + "\n" + await e.ConfigureAwait(false);
                if (p.ExitCode != 0) return null;
                return text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch (InvalidOperationException) { }
                ct.ThrowIfCancellationRequested();
                return null; // probe timed out
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    public static RuntimeStatus Missing(string languageId, string what, string hint) =>
        new(languageId, RuntimeAvailability.Missing, null, null, $"{what} was not found on PATH.", hint);

    public static IEnumerable<string> FilesWithExtensions(string dir, params string[] extensions) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !Path.GetRelativePath(dir, f).StartsWith("tmp" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(dir, f))
            .Order(StringComparer.Ordinal);
}

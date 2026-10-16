using System.Diagnostics;
using System.Text;

namespace DevAcademy.Execution;

internal static class TypeScriptTypes
{
    public static string Directory(string cacheDirectory) => Path.Combine(cacheDirectory, "typescript-types");

    /// <summary>Value for tsc --typeRoots.</summary>
    public static string TypeRoot(string cacheDirectory) => Path.Combine(Directory(cacheDirectory), "node_modules", "@types");

    public static bool IsProvisioned(string cacheDirectory) =>
        File.Exists(Path.Combine(TypeRoot(cacheDirectory), "node", "index.d.ts"));
}

public sealed record ProvisionResult(bool Success, string Output);

/// <summary>
/// Installs @types/node into the runner cache so tsc can type-check. This downloads third-party code from the npm registry,
/// so it must only be called on explicit user action, never by the runner itself. Call
/// <see cref="ICodeRunner.DetectRuntimesAsync"/> afterwards to refresh the runtime notes.
/// </summary>
public static class TypeScriptTypesProvisioner
{
    public static async Task<ProvisionResult> ProvisionAsync(string cacheDirectory, CancellationToken ct)
    {
        RuntimeStatus node = await NodeProbe.DetectAsync("typescript", ct).ConfigureAwait(false);
        if (node.Availability != RuntimeAvailability.Available || !Version.TryParse(node.Version, out Version? nv))
            return new ProvisionResult(false, "Node.js is not available.");
        if (ToolLocator.Find("npm") is not { } npm)
            return new ProvisionResult(false, "npm was not found on PATH.");

        string dir = TypeScriptTypes.Directory(cacheDirectory);
        System.IO.Directory.CreateDirectory(dir);
        var psi = new ProcessStartInfo(npm)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in new[] { "install", "--no-audit", "--no-fund", "--prefix", dir, $"@types/node@{nv.Major}" })
            psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = ToolLocator.SearchPath();

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("npm did not start");
        p.StandardInput.Close();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            Task<string> o = p.StandardOutput.ReadToEndAsync(timeout.Token), e = p.StandardError.ReadToEndAsync(timeout.Token);
            await p.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string output = await o.ConfigureAwait(false) + await e.ConfigureAwait(false);
            bool ok = p.ExitCode == 0 && TypeScriptTypes.IsProvisioned(cacheDirectory);
            return new ProvisionResult(ok, output.Trim());
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch (InvalidOperationException) { }
            ct.ThrowIfCancellationRequested();
            return new ProvisionResult(false, "npm install timed out.");
        }
    }
}

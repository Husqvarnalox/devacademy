using System.Diagnostics;
using System.Text;

namespace DevAcademy.LanguageServices.Servers;

/// <summary>The exact command that would run, shown to the user BEFORE anything is downloaded.</summary>
public sealed record ProvisionPlan(string Executable, IReadOnlyList<string> Args, string WorkingDirectory)
{
    public string Display => $"{Path.GetFileName(Executable)} {string.Join(' ', Args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}";
}

public sealed record ServerProvisionResult(bool Success, string Output);

/// <summary>
/// Installs npm-distributed language servers (pyright, typescript-language-server + typescript, intelephense) into the runner
/// cache. This downloads third-party code from the npm registry, so it is only ever invoked from an explicit user action that has
/// displayed <see cref="ProvisionPlan.Display"/>; nothing calls it automatically.
/// </summary>
public static class NpmServerProvisioner
{
    public static ProvisionPlan? Plan(LanguageServerDefinition def, string cacheDirectory, ServerLocator locator)
    {
        if (!def.IsNpmInstallable) return null;
        string? npm = locator.FindExecutable("npm", includeCache: false);
        if (npm is null) return null;
        string dir = ServerLocator.InstallDirectory(cacheDirectory);
        var args = new List<string> { "install", "--no-audit", "--no-fund", "--prefix", dir };
        args.AddRange(def.NpmPackages);
        return new ProvisionPlan(npm, args, dir);
    }

    public static async Task<ServerProvisionResult> ProvisionAsync(ProvisionPlan plan, ServerLocator locator, Func<bool> isInstalled, CancellationToken ct)
    {
        Directory.CreateDirectory(plan.WorkingDirectory);
        var psi = new ProcessStartInfo(plan.Executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in plan.Args) psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = locator.ChildPath();
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("npm did not start");
        p.StandardInput.Close();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            Task<string> o = p.StandardOutput.ReadToEndAsync(timeout.Token), e = p.StandardError.ReadToEndAsync(timeout.Token);
            await p.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string output = await o.ConfigureAwait(false) + await e.ConfigureAwait(false);
            return new ServerProvisionResult(p.ExitCode == 0 && isInstalled(), output.Trim());
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch (InvalidOperationException) { }
            ct.ThrowIfCancellationRequested();
            return new ServerProvisionResult(false, "npm install timed out.");
        }
    }
}

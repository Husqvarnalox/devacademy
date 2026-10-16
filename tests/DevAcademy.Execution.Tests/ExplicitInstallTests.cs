using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

/// <summary>Network test: runs only when DEVACADEMY_ALLOW_NETWORK_INSTALL=1. Installs numpy from PyPI into a throw-away venv in the temp directory.</summary>
public class ExplicitInstallTests
{
    [Fact]
    public async Task Installing_numpy_into_a_temp_venv_flips_the_probe_to_satisfied()
    {
        if (Environment.GetEnvironmentVariable("DEVACADEMY_ALLOW_NETWORK_INSTALL") != "1")
            Assert.Skip("explicit-only: set DEVACADEMY_ALLOW_NETWORK_INSTALL=1 to download numpy from PyPI into a temp venv");
        await TestSupport.RequireAsync("python");
        string py = (await TestSupport.StatusAsync("python")).ExecutablePath!;
        string root = Path.Combine(Path.GetTempPath(), "devacademy-install-" + Guid.NewGuid().ToString("N"));
        try
        {
            var mgr = new PythonEnvironmentManager(root);
            PythonEnvResult env = await mgr.CreateAsync("np", py, PythonEnvTool.Venv, CancellationToken.None);
            Assert.True(env.Ok, env.Error);
            Assert.DoesNotContain("numpy", (await mgr.ProbeAsync(env.Environment!.InterpreterPath, ["numpy"], CancellationToken.None)).Installed.Keys);

            (PythonInstallPlan? plan, string? err) = await mgr.BuildInstallPlanAsync("np", PythonInstallRequest.FromPackages(["numpy"]), CancellationToken.None);
            Assert.Null(err);
            var log = new List<string>();
            PythonInstallResult res = await mgr.InstallAsync(plan!, log.Add, CancellationToken.None);
            Assert.True(res.Ok, res.Error + "\n" + string.Join('\n', log.TakeLast(10)));

            PythonProbeResult probe = await mgr.ProbeAsync(env.Environment.InterpreterPath, ["numpy"], CancellationToken.None);
            Assert.Contains("numpy", probe.Installed.Keys);
            (IReadOnlyList<PythonPackage> pkgs, _) = await mgr.ListPackagesAsync(env.Environment.Path, CancellationToken.None);
            Assert.Contains(pkgs, p => p.Name.Equals("numpy", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine("numpy " + probe.Installed["numpy"]);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}

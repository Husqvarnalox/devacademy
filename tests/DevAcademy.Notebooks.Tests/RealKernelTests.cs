using System.Diagnostics;
using DevAcademy.Notebooks.Kernel;
using DevAcademy.Notebooks.Nbformat;

namespace DevAcademy.Notebooks.Tests;

/// <summary>
/// Real ipykernel over the real Jupyter protocol. The first test uses the repository's .venv-content when it has ipykernel
/// (<c>make python-env</c>) and is skipped otherwise. The second creates a temporary venv and pip-installs ipykernel from the
/// network, so it only runs with DEVACADEMY_ALLOW_NETWORK_INSTALL=1.
/// </summary>
public class RealKernelTests
{
    private static string? RepoVenvPython()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var py = OperatingSystem.IsWindows()
                ? Path.Combine(dir.FullName, ".venv-content", "Scripts", "python.exe")
                : Path.Combine(dir.FullName, ".venv-content", "bin", "python");
            if (File.Exists(py)) return py;
        }
        return null;
    }

    [Fact]
    public async Task Repository_venv_kernel_runs_the_full_scenario()
    {
        var py = RepoVenvPython();
        Assert.SkipUnless(py is not null, ".venv-content not found (run `make python-env`)");
        var version = await KernelProvisioner.GetInstalledVersionAsync(py!, CancellationToken.None);
        Assert.SkipUnless(version is not null, "ipykernel is not installed in .venv-content");
        await Scenario.RunAsync(py!);
    }

    [Fact]
    public async Task Fresh_venv_with_pip_installed_ipykernel_runs_the_full_scenario()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("DEVACADEMY_ALLOW_NETWORK_INSTALL") == "1",
            "explicit-only: set DEVACADEMY_ALLOW_NETWORK_INSTALL=1 to create a venv and pip install ipykernel");
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-nb-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var created = Process.Start(new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3", ["-m", "venv", dir]) { UseShellExecute = false })!;
            await created.WaitForExitAsync();
            Assert.Equal(0, created.ExitCode);
            var py = OperatingSystem.IsWindows() ? Path.Combine(dir, "Scripts", "python.exe") : Path.Combine(dir, "bin", "python");
            Assert.Null(await KernelProvisioner.GetInstalledVersionAsync(py, CancellationToken.None));
            var plan = KernelProvisioner.PlanInstall(py);
            Assert.NotNull(plan);
            Assert.Contains("ipykernel==", plan!.CommandLine);
            var install = await KernelProvisioner.InstallAsync(plan, _ => { }, CancellationToken.None);
            Assert.True(install.Ok, install.Error);
            Assert.Equal("7.4.0", await KernelProvisioner.GetInstalledVersionAsync(py, CancellationToken.None));
            await Scenario.RunAsync(py);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private static class Scenario
    {
        public static async Task RunAsync(string python)
        {
            var work = Directory.CreateTempSubdirectory("devacademy-nb-work").FullName;
            var session = new KernelSession(new PythonKernelHost(python, work), new KernelSessionOptions { ShutdownGrace = TimeSpan.FromSeconds(5) });
            int pid;
            try
            {
                await session.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(90)).Token);
                Assert.Equal(KernelState.Idle, session.State);
                pid = session.ProcessId!.Value;
                Assert.Equal("ipython", session.KernelInfo!["implementation"]!.GetValue<string>());

                // x = 21*2; x  ->  execute_result "42"
                var (r1, e1) = await Run(session, "x = 21*2; x");
                Assert.True(r1.IsOk);
                var result = e1.OfType<OutputEvent>().Select(o => o.Output).Single(o => o.OutputType == NotebookOutput.ExecuteResult);
                Assert.Equal("42", result.GetText("text/plain"));

                // print('hi')  ->  stream
                var (_, e2) = await Run(session, "print('hi')");
                var stream = e2.OfType<OutputEvent>().Select(o => o.Output).Single();
                Assert.Equal((NotebookOutput.Stream, "stdout", "hi\n"), (stream.OutputType, stream.Name, stream.Text));

                // 1/0  ->  ZeroDivisionError
                var (r3, e3) = await Run(session, "1/0");
                Assert.Equal("error", r3.Status);
                Assert.Equal("ZeroDivisionError", r3.ErrorName);
                var err = e3.OfType<OutputEvent>().Select(o => o.Output).Single(o => o.OutputType == NotebookOutput.Error);
                Assert.Equal("ZeroDivisionError", err.ErrorName);
                Assert.NotEmpty(err.Traceback);

                // Variable inspector over silent user_expressions.
                var (rn, _) = await Run(session, "import numpy as np\narr = np.zeros((2, 3))");
                await Run(session, "name = 'abc'\nitems = [1, 2, 3]");
                var vars = await VariableInspector.QueryAsync(session, CancellationToken.None);
                Assert.Contains(vars, v => v.Name == "x" && v.Type == "int" && v.Summary == "42");
                if (rn.IsOk) Assert.Contains(vars, v => v.Name == "arr" && v.Shape == "2x3"); // numpy is only in .venv-content
                vars = await VariableInspector.QueryAsync(session, CancellationToken.None); // the snippet is idempotent
                Assert.Contains(vars, v => v.Name == "items" && v.Type == "list");
                Assert.DoesNotContain(vars, v => v.Name.StartsWith("_devacademy", StringComparison.Ordinal));
                Assert.DoesNotContain(vars, v => v.Name == "np"); // modules are not listed

                // Interrupt while-True.
                var loop = Run(session, "while True: pass");
                await Task.Delay(1500);
                Assert.Equal(KernelState.Busy, session.State);
                await session.InterruptAsync();
                var (r4, e4) = await loop.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal("error", r4.Status);
                Assert.Equal("KeyboardInterrupt", r4.ErrorName);
                Assert.Equal(KernelState.Idle, session.State);
                var (r5, _) = await Run(session, "x");
                Assert.True(r5.IsOk); // the namespace survived the interrupt

                // Restart: new process, empty namespace.
                await session.RestartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(90)).Token);
                Assert.Equal(KernelState.Idle, session.State);
                Assert.NotEqual(pid, session.ProcessId);
                AssertGone(pid);
                pid = session.ProcessId!.Value;
                var (r6, _) = await Run(session, "x");
                Assert.Equal("NameError", r6.ErrorName);
                var (r7, e7) = await Run(session, "1 + 1");
                Assert.Equal("2", e7.OfType<OutputEvent>().Single().Output.GetText("text/plain"));

                // Shutdown leaves no orphan.
                await session.ShutdownAsync();
                AssertGone(pid);
                Assert.Equal(0, session.DroppedMessages);
            }
            finally
            {
                await session.DisposeAsync();
                try { Directory.Delete(work, true); } catch (IOException) { }
            }
        }

        private static async Task<(ExecuteResult, List<KernelEvent>)> Run(KernelSession s, string code)
        {
            var events = new List<KernelEvent>();
            var r = await s.ExecuteAsync(code, null, e => { lock (events) events.Add(e); }, new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
            return (r, events);
        }

        private static void AssertGone(int pid)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                try { using var p = Process.GetProcessById(pid); if (p.HasExited) return; }
                catch (ArgumentException) { return; }
                Thread.Sleep(100);
            }
            Assert.Fail($"kernel process {pid} is still running (orphan)");
        }
    }
}

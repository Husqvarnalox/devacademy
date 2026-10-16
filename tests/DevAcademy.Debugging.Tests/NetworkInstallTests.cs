using System.Diagnostics;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;
using DevAcademy.Debugging.Session;

namespace DevAcademy.Debugging.Tests;

/// <summary>
/// Explicit-only: these install a debug adapter from the network into temp locations (debugpy into a throwaway venv, dlv into a
/// temp GOBIN). Run with DEVACADEMY_ALLOW_NETWORK_INSTALL=1. They never touch the system interpreter or GOBIN.
/// </summary>
public class NetworkInstallTests
{
    private static void RequireOptIn() =>
        Assert.SkipUnless(Environment.GetEnvironmentVariable("DEVACADEMY_ALLOW_NETWORK_INSTALL") == "1", "set DEVACADEMY_ALLOW_NETWORK_INSTALL=1 to run");

    private static async Task<(int Code, string Output)> RunAsync(string exe, string[] args, string cwd, IDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = cwd };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env is not null) foreach (var (k, v) in env) psi.Environment[k] = v;
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, await o + await e);
    }

    private static async Task<(DebugSession S, List<DebugStop> Stops, List<DebugTermination> Ended)> StartAsync(
        DebugAdapterDefinition def, ResolvedAdapter adapter, DebugLaunch launch, string file, int line)
    {
        var session = new DebugSession(new DebugSessionOptions(def, adapter, LaunchConfigurations.Create(def, launch), launch.WorkingDirectory), new ProcessAdapterLauncher());
        var stops = new List<DebugStop>(); var ended = new List<DebugTermination>();
        session.Stopped += s => { lock (stops) stops.Add(s); };
        session.Terminated += t => { lock (ended) ended.Add(t); };
        session.SeedBreakpoints(new Dictionary<string, IReadOnlyList<SourceBreakpoint>> { [file] = [new SourceBreakpoint(line)] });
        await session.StartAsync(CancellationToken.None);
        await Util.UntilAsync(() => { lock (stops) return stops.Count >= 1; }, "breakpoint", 60000);
        return (session, stops, ended);
    }

    private static async Task<Dictionary<string, string>> AllVariablesAsync(DebugSession s, DebugStop stop)
    {
        var all = new Dictionary<string, string>();
        foreach (var sc in await s.GetScopesAsync(stop.Frames[0].Id))
            foreach (var v in await s.GetVariablesAsync(sc.VariablesReference)) all[v.Name] = v.Value;
        return all;
    }

    [Fact]
    public async Task Debugpy_installs_into_a_temp_venv_and_debugs_a_script()
    {
        RequireOptIn();
        string py = new[] { "python3.14", "python3" }.Select(n => new DevAcademy.LanguageServices.Servers.ServerLocator(null).FindExecutable(n, includeCache: false)).First(p => p is not null)!;
        string root = Util.TempDir("debugpy");
        string venv = Path.Combine(root, "venv");
        var made = await RunAsync(py, ["-m", "venv", venv], root);
        Assert.True(made.Code == 0, made.Output);
        string venvPy = Path.Combine(venv, OperatingSystem.IsWindows() ? "Scripts" : "bin", OperatingSystem.IsWindows() ? "python.exe" : "python");

        var resolver = new AdapterResolver(Path.Combine(root, "cache"));
        var def = DebugAdapterRegistry.ForLanguage("python")!;
        var before = await resolver.GetStatusAsync(def, new DebugEnvironment(venvPy), default);
        Assert.Equal(AdapterState.NotInstalled, before.State);

        var (ok, output) = await AdapterResolver.ProvisionAsync(before.Plan!, () => true, default);
        Assert.True(ok, output);
        var after = await resolver.GetStatusAsync(def, new DebugEnvironment(venvPy), default);
        Assert.True(after.IsReady, after.Message);

        string script = Path.Combine(root, "app.py");
        File.WriteAllText(script, "total = 0\nfor n in range(3):\n    total += n * 2\n    label = f'n={n}'\nprint('done', total)\n");
        var (s, stops, ended) = await StartAsync(def, after.Adapter!, new DebugLaunch(script, root, [], venvPy), script, 3);
        await using (s)
        {
            Assert.Equal(3, stops[0].Frames[0].Line);
            var vars = await AllVariablesAsync(s, stops[0]);
            Assert.Equal("0", vars["n"]);
            Assert.Equal("0", vars["total"]);
            await s.StepOverAsync();
            await Util.UntilAsync(() => { lock (stops) return stops.Count >= 2; }, "step", 30000);
            Assert.Equal(4, stops[1].Frames[0].Line);
            Assert.Equal("0", (await s.EvaluateAsync("total + n", stops[1].Frames[0].Id, "watch")).Result);
            await s.SetBreakpointsAsync(script, []);
            await s.ContinueAsync();
            var end = await Util.UntilAsync(() => { lock (ended) return ended.FirstOrDefault(); }, "exit", 30000);
            Assert.Equal(0, end.ExitCode);
        }
    }

    [Fact]
    public async Task Delve_installs_into_a_temp_gobin_and_hits_a_breakpoint_in_a_go_program()
    {
        RequireOptIn();
        string root = Util.TempDir("dlv");
        var resolver = new AdapterResolver(Path.Combine(root, "cache"));
        var def = DebugAdapterRegistry.ForLanguage("go")!;
        var before = await resolver.GetStatusAsync(def, new DebugEnvironment(), default);
        Assert.SkipWhen(before.State == AdapterState.Unavailable, before.Message);
        Assert.Equal(AdapterState.NotInstalled, before.State);
        Assert.StartsWith(root, before.Plan!.Environment["GOBIN"]);

        var (ok, output) = await AdapterResolver.ProvisionAsync(before.Plan, () => File.Exists(Path.Combine(before.Plan.Environment["GOBIN"], "dlv")), default);
        Assert.True(ok, output);
        var after = await resolver.GetStatusAsync(def, new DebugEnvironment(), default);
        Assert.True(after.IsReady, after.Message);

        string proj = Path.Combine(root, "app");
        Directory.CreateDirectory(proj);
        File.WriteAllText(Path.Combine(proj, "go.mod"), "module app\n\ngo 1.22\n");
        string main = Path.Combine(proj, "main.go");
        File.WriteAllText(main, "package main\n\nimport \"os\"\n\nfunc main() {\n\ttotal := 0\n\tfor i := 0; i < 3; i++ {\n\t\ttotal += i\n\t}\n\tos.Exit(total + 10)\n}\n");
        string exe = Path.Combine(proj, "prog");
        var built = await RunAsync("go", ["build", "-gcflags=all=-N -l", "-o", exe, "."], proj, new Dictionary<string, string> { ["GOTOOLCHAIN"] = "local", ["GOFLAGS"] = "-mod=mod" });
        Assert.True(built.Code == 0, built.Output);

        var (s, stops, ended) = await StartAsync(def, after.Adapter!, new DebugLaunch(exe, proj, []), main, 8);
        await using (s)
        {
            Assert.Equal(8, stops[0].Frames[0].Line);
            var vars = await AllVariablesAsync(s, stops[0]);
            Assert.Equal("0", vars["i"]);
            await s.SetBreakpointsAsync(main, []);
            await s.ContinueAsync();
            var end = await Util.UntilAsync(() => { lock (ended) return ended.FirstOrDefault(); }, "exit", 30000);
            // Delve's DAP server reports `terminated` without an `exited` event, so there is no exit code to show.
            Assert.Null(end.Error);
            Assert.True(end.ExitCode is null or 13);
        }
    }
}

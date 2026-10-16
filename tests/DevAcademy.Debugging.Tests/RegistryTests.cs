using DevAcademy.Debugging.Adapters;

namespace DevAcademy.Debugging.Tests;

public class RegistryTests
{
    [Theory]
    [InlineData("python", "debugpy")]
    [InlineData("cpp", "lldb-dap")]
    [InlineData("rust", "lldb-dap")]
    [InlineData("go", "delve")]
    public void Languages_map_to_their_adapter(string language, string id) => Assert.Equal(id, DebugAdapterRegistry.ForLanguage(language)!.Id);

    [Theory]
    [InlineData("javascript")]
    [InlineData("typescript")]
    [InlineData("java")]
    public void Unsupported_languages_have_no_adapter_but_a_planned_note(string language)
    {
        Assert.Null(DebugAdapterRegistry.ForLanguage(language));
        Assert.True(DebugAdapterRegistry.Planned.ContainsKey(language));
    }

    [Fact]
    public void Js_note_names_vscode_js_debug_and_cdp()
    {
        Assert.Contains("vscode-js-debug", DebugAdapterRegistry.Planned["javascript"]);
        Assert.Contains("CDP", DebugAdapterRegistry.Planned["javascript"]);
    }

    [Fact]
    public void Launch_configs_have_the_per_adapter_shape()
    {
        var py = LaunchConfigurations.Create(DebugAdapterRegistry.ForLanguage("python")!, new DebugLaunch("/p/main.py", "/p", ["a"], "/p/.venv/bin/python"));
        Assert.Equal("launch", (string?)py["request"]);
        Assert.Equal("internalConsole", (string?)py["console"]);
        Assert.Equal("/p/.venv/bin/python", (string?)py["python"]![0]);

        var lldb = LaunchConfigurations.Create(DebugAdapterRegistry.ForLanguage("cpp")!, new DebugLaunch("/w/prog", "/w", []));
        Assert.Equal("/w/prog", (string?)lldb["program"]);
        Assert.False((bool)lldb["stopOnEntry"]!);

        var dlvExec = LaunchConfigurations.Create(DebugAdapterRegistry.ForLanguage("go")!, new DebugLaunch("/w/prog", "/w", []));
        Assert.Equal("exec", (string?)dlvExec["mode"]);
        var dlvDebug = LaunchConfigurations.Create(DebugAdapterRegistry.ForLanguage("go")!, new DebugLaunch("/w", "/w", [], GoBuildPackage: true));
        Assert.Equal("debug", (string?)dlvDebug["mode"]);
        Assert.Equal("-gcflags=all=-N -l", (string?)dlvDebug["buildFlags"]);
    }
}

public class ResolverTests
{
    private static string Cache() => Util.TempDir("resolver");

    private static ProbeRunner Probe(Func<IReadOnlyList<string>, ProbeResult> f) => (_, args, _) => Task.FromResult(f(args));

    private static string FakePython()
    {
        string p = Path.Combine(Util.TempDir("py"), "python");
        File.WriteAllText(p, "");
        return p;
    }

    [Fact]
    public async Task Python_without_a_selected_interpreter_is_unavailable_with_a_reason()
    {
        var r = new AdapterResolver(Cache(), probe: Probe(_ => new ProbeResult(0, "")));
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("python")!, new DebugEnvironment(null), default);
        Assert.Equal(AdapterState.Unavailable, s.State);
        Assert.Contains("окружение", s.Message);
    }

    [Fact]
    public async Task Python_with_debugpy_runs_the_adapter_module_with_the_selected_interpreter()
    {
        string py = FakePython();
        var r = new AdapterResolver(Cache(), probe: Probe(a => new ProbeResult(0, "")));
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("python")!, new DebugEnvironment(py), default);
        Assert.True(s.IsReady);
        Assert.Equal(py, s.Adapter!.Executable);
        Assert.Equal(["-m", "debugpy.adapter"], s.Adapter.Args);
    }

    [Fact]
    public async Task Python_without_debugpy_in_a_venv_offers_pip_install_with_the_exact_command()
    {
        string py = FakePython();
        var r = new AdapterResolver(Cache(), probe: Probe(a => a[1] == "import debugpy" ? new ProbeResult(1, "No module named debugpy") : new ProbeResult(0, "")));
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("python")!, new DebugEnvironment(py), default);
        Assert.Equal(AdapterState.NotInstalled, s.State);
        Assert.Equal(py, s.Plan!.Executable);
        Assert.Equal(["-m", "pip", "install", "debugpy"], s.Plan.Args);
        Assert.Equal("1", s.Plan.Environment["PIP_REQUIRE_VIRTUALENV"]);
        Assert.Contains("pip install debugpy", s.Plan.Display);
    }

    [Fact]
    public async Task Python_without_debugpy_in_the_system_interpreter_is_never_offered_an_install()
    {
        string py = FakePython();
        var r = new AdapterResolver(Cache(), probe: Probe(a => new ProbeResult(1, "")));   // not importable AND not a venv
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("python")!, new DebugEnvironment(py), default);
        Assert.Equal(AdapterState.Unavailable, s.State);
        Assert.Null(s.Plan);
        Assert.Contains("системный", s.Message);
    }

    [Fact]
    public async Task Delve_is_found_in_the_runner_cache_and_started_in_tcp_mode()
    {
        string cache = Cache();
        Directory.CreateDirectory(AdapterResolver.DelveBinDirectory(cache));
        string dlv = Path.Combine(AdapterResolver.DelveBinDirectory(cache), OperatingSystem.IsWindows() ? "dlv.exe" : "dlv");
        File.WriteAllText(dlv, "");
        var r = new AdapterResolver(cache, new DevAcademy.LanguageServices.Servers.ServerLocator(null, []), Probe(_ => new ProbeResult(0, "")));
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("go")!, new DebugEnvironment(), default);
        Assert.True(s.IsReady);
        Assert.Equal(dlv, s.Adapter!.Executable);
        Assert.Contains("--listen=127.0.0.1:0", s.Adapter.Args);
        Assert.NotNull(s.Adapter.Announce);
        Assert.Equal("127.0.0.1:5555", s.Adapter.Announce!.Match("DAP server listening at: 127.0.0.1:5555\n").Groups[1].Value);
    }

    [Fact]
    public async Task Delve_missing_with_go_present_plans_a_pinned_install_into_the_cache_gobin()
    {
        string cache = Cache();
        string binDir = Util.TempDir("gobin");
        File.WriteAllText(Path.Combine(binDir, OperatingSystem.IsWindows() ? "go.exe" : "go"), "");
        var r = new AdapterResolver(cache, new DevAcademy.LanguageServices.Servers.ServerLocator(null, [binDir]), Probe(_ => new ProbeResult(0, "")));
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("go")!, new DebugEnvironment(), default);
        Assert.Equal(AdapterState.NotInstalled, s.State);
        Assert.Equal(["install", "github.com/go-delve/delve/cmd/dlv@" + DebugAdapterDefinition.DelvePinnedVersion], s.Plan!.Args);
        Assert.StartsWith(AdapterResolver.DelveBinDirectory(cache), s.Plan.Environment["GOBIN"]);
        Assert.DoesNotContain("latest", s.Plan.Display);
        Assert.Matches(@"^v\d+\.\d+\.\d+$", DebugAdapterDefinition.DelvePinnedVersion);
    }

    [Fact]
    public async Task Delve_missing_without_go_is_unavailable()
    {
        var r = new AdapterResolver(Cache(), new DevAcademy.LanguageServices.Servers.ServerLocator(null, []), Probe(_ => new ProbeResult(0, "")));
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("go")!, new DebugEnvironment(), default);
        Assert.Equal(AdapterState.Unavailable, s.State);
    }

    [Fact]
    public async Task Lldb_dap_missing_reports_the_install_hint()
    {
        var r = new AdapterResolver(Cache(), new DevAcademy.LanguageServices.Servers.ServerLocator(null, []),
            Probe(_ => new ProbeResult(1, "")), extraSearchDirs: []);
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("cpp")!, new DebugEnvironment(), default);
        Assert.Equal(AdapterState.Unavailable, s.State);
        Assert.Contains("lldb", s.Message);
    }

    [Fact]
    public async Task Lldb_dap_is_found_through_xcrun_on_macos()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS only");
        string exe = Path.Combine(Util.TempDir("xc"), "lldb-dap");
        File.WriteAllText(exe, "");
        var r = new AdapterResolver(Cache(), new DevAcademy.LanguageServices.Servers.ServerLocator(null, []),
            (e, args, _) => Task.FromResult(e.EndsWith("xcrun") ? new ProbeResult(0, exe + "\n") : new ProbeResult(1, "")), extraSearchDirs: []);
        var s = await r.GetStatusAsync(DebugAdapterRegistry.ForLanguage("cpp")!, new DebugEnvironment(), default);
        Assert.True(s.IsReady);
        Assert.Equal(exe, s.Adapter!.Executable);
    }
}

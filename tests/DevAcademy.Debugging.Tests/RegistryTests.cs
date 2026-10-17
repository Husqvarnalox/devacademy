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
    [InlineData("javascript", "js-debug")]
    [InlineData("typescript", "js-debug")]
    [InlineData("java", "java-jdi")]
    [InlineData("csharp", "netcoredbg")]
    public void Runtime_languages_map_to_their_adapter(string language, string id) => Assert.Equal(id, DebugAdapterRegistry.ForLanguage(language)!.Id);

    [Fact]
    public void Languages_without_an_adapter_have_none_and_nothing_is_planned()
    {
        Assert.Null(DebugAdapterRegistry.ForLanguage("kotlin"));
        Assert.Empty(DebugAdapterRegistry.Planned);
    }

    [Fact]
    public void Js_java_and_csharp_launch_configs_have_the_per_adapter_shape()
    {
        var js = LaunchConfigurations.Create(DebugAdapterRegistry.ForLanguage("typescript")!,
            new DebugLaunch("/usr/bin/node", "/w", ["x"], RunArguments: ["--experimental-strip-types", "/w/main.ts"]));
        Assert.Equal("pwa-node", (string?)js["type"]);
        Assert.Equal("/w/main.ts", (string?)js["program"]);
        Assert.Equal("/usr/bin/node", (string?)js["runtimeExecutable"]);
        Assert.Equal("--experimental-strip-types", (string?)js["runtimeArgs"]![0]);
        Assert.False((bool)js["sourceMaps"]!);

        var java = LaunchConfigurations.Create(DebugAdapterRegistry.ForLanguage("java")!,
            new DebugLaunch("/jdk/bin/java", "/w", [], RunArguments: ["-XX:+UseSerialGC", "-cp", "/w/out", "app.Main"]));
        Assert.Equal("app.Main", (string?)java["mainClass"]);
        Assert.Equal("/w/out", (string?)java["classpath"]);
        Assert.Equal("-XX:+UseSerialGC", (string?)java["vmArgs"]![0]);
        Assert.Single(java["vmArgs"]!.AsArray());

        var cs = LaunchConfigurations.Create(DebugAdapterRegistry.ForLanguage("csharp")!,
            new DebugLaunch("/usr/bin/dotnet", "/w", ["a"], RunArguments: ["/w/out/App.dll"]));
        Assert.Equal("coreclr", (string?)cs["type"]);
        Assert.Equal("/usr/bin/dotnet", (string?)cs["program"]);
        Assert.Equal(["/w/out/App.dll", "a"], cs["args"]!.AsArray().Select(n => (string)n!).ToArray());
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

    private static AdapterResolver WithTools(string cache, params string[] tools)
    {
        string bin = Util.TempDir("tools");
        foreach (string t in tools) File.WriteAllText(Path.Combine(bin, OperatingSystem.IsWindows() ? t + ".exe" : t), "");
        return new AdapterResolver(cache, new DevAcademy.LanguageServices.Servers.ServerLocator(null, [bin]), Probe(_ => new ProbeResult(0, "")));
    }

    [Fact]
    public async Task JsDebug_missing_plans_a_pinned_checksum_verified_install_into_the_cache()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no automatic install");
        string cache = Cache();
        var s = await WithTools(cache, "node").GetStatusAsync(DebugAdapterRegistry.ForLanguage("javascript")!, new DebugEnvironment(), default);
        Assert.Equal(AdapterState.NotInstalled, s.State);
        string d = s.Plan!.Display;
        Assert.Contains(DebugAdapterDefinition.JsDebugSha256, d);
        Assert.Contains(DebugAdapterDefinition.JsDebugUrl, d);
        Assert.Contains(AdapterResolver.JsDebugDirectory(cache), d);
        Assert.DoesNotContain("latest", d);
        Assert.True(d.IndexOf("shasum", StringComparison.Ordinal) < d.IndexOf("tar xzf", StringComparison.Ordinal), "the checksum is verified before unpacking");
    }

    [Fact]
    public async Task JsDebug_installed_starts_on_loopback_and_announces_its_port()
    {
        string cache = Cache();
        string script = AdapterResolver.JsDebugServerScript(cache);
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "");
        var s = await WithTools(cache, "node").GetStatusAsync(DebugAdapterRegistry.ForLanguage("typescript")!, new DebugEnvironment(), default);
        Assert.True(s.IsReady);
        Assert.Equal([script, "0", "127.0.0.1"], s.Adapter!.Args);
        Assert.Equal("127.0.0.1:4711", s.Adapter.Announce!.Match("Debug server listening at 127.0.0.1:4711\n").Groups[1].Value);
    }

    [Fact]
    public async Task JsDebug_without_node_is_unavailable()
    {
        var s = await WithTools(Cache()).GetStatusAsync(DebugAdapterRegistry.ForLanguage("javascript")!, new DebugEnvironment(), default);
        Assert.Equal(AdapterState.Unavailable, s.State);
        Assert.Null(s.Plan);
    }

    [Fact]
    public async Task NetCoreDbg_installed_starts_in_vscode_interpreter_mode()
    {
        string cache = Cache();
        string exe = AdapterResolver.NetCoreDbgExecutable(cache);
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        File.WriteAllText(exe, "");
        var s = await WithTools(cache).GetStatusAsync(DebugAdapterRegistry.ForLanguage("csharp")!, new DebugEnvironment(), default);
        Assert.True(s.IsReady);
        Assert.Equal(exe, s.Adapter!.Executable);
        Assert.Equal(["--interpreter=vscode"], s.Adapter.Args);
        Assert.Null(s.Adapter.Announce);
    }

    [Fact]
    public async Task NetCoreDbg_plan_is_pinned_and_verified_before_unpacking_and_only_offered_on_macos_arm64()
    {
        string cache = Cache();
        var s = await WithTools(cache, "dotnet").GetStatusAsync(DebugAdapterRegistry.ForLanguage("csharp")!, new DebugEnvironment(), default);
        bool tested = OperatingSystem.IsMacOS() && System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
        if (!tested)
        {
            Assert.Equal(AdapterState.Unavailable, s.State);
            Assert.Null(s.Plan);
            Assert.Contains("не проверялось", s.Message);
            return;
        }
        Assert.Equal(AdapterState.NotInstalled, s.State);
        string d = s.Plan!.Display;
        Assert.Contains(DebugAdapterDefinition.NetCoreDbgOsxArm64Sha256, d);
        Assert.Contains("netcoredbg-osx-arm64.zip", d);
        Assert.Contains(DebugAdapterDefinition.NetCoreDbgVersion, d);
        Assert.StartsWith(AdapterResolver.AdaptersDirectory(cache), s.Plan.WorkingDirectory);
        Assert.True(d.IndexOf("shasum", StringComparison.Ordinal) < d.IndexOf("unzip", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NetCoreDbg_without_the_dotnet_sdk_is_unavailable()
    {
        var s = await WithTools(Cache()).GetStatusAsync(DebugAdapterRegistry.ForLanguage("csharp")!, new DebugEnvironment(), default);
        Assert.Equal(AdapterState.Unavailable, s.State);
    }

    [Fact]
    public async Task Java_adapter_is_compiled_locally_without_any_download()
    {
        var java = DebugAdapterRegistry.ForLanguage("java")!;
        var r = new AdapterResolver(Cache(), new DevAcademy.LanguageServices.Servers.ServerLocator(null, []), Probe(_ => new ProbeResult(0, "")));
        var s = await r.GetStatusAsync(java, new DebugEnvironment(), default);
        if (s.State == AdapterState.Unavailable) { Assert.SkipUnless(false, "no JDK on this machine: " + s.Message); return; }
        Assert.Equal(AdapterState.NotInstalled, s.State);
        Assert.Contains("javac", s.Plan!.Executable);
        Assert.DoesNotContain("http", s.Plan.Display);
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

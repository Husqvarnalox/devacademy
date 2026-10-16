using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

/// <summary>Canned tool outputs: nothing about uv, nvidia-smi or frameworks has to exist on the test machine.</summary>
internal sealed class FakeToolRunner : IToolRunner
{
    public Dictionary<string, string> Tools { get; } = new();
    public List<(string File, string[] Args)> Calls { get; } = [];
    public Func<string, IReadOnlyList<string>, ToolOutput?>? Handler { get; set; }

    public string? Find(string name) => Tools.GetValueOrDefault(name);

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct)
    {
        Calls.Add((fileName, args.ToArray()));
        return Task.FromResult(Handler?.Invoke(fileName, args) ?? new ToolOutput(1, "", "no canned output", false));
    }

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct) =>
        RunAsync(fileName, args, options, ct);
}

public sealed class PythonEnvFixture : IAsyncLifetime
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "devacademy-pyenv-" + Guid.NewGuid().ToString("N"));
    public string? SystemPython { get; private set; }
    public PythonEnvironmentManager Manager { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Manager = new PythonEnvironmentManager(Root);
        RuntimeStatus s = await TestSupport.StatusAsync("python");
        SystemPython = s.Availability == RuntimeAvailability.Available ? s.ExecutablePath : null;
    }

    public ValueTask DisposeAsync()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return ValueTask.CompletedTask;
    }
}

public class PythonEnvironmentTests(PythonEnvFixture fx) : IClassFixture<PythonEnvFixture>
{
    private string RequirePython()
    {
        if (fx.SystemPython is null) Assert.Skip("python is not available");
        return fx.SystemPython;
    }

    [Fact]
    public async Task Create_list_probe_packages_and_delete_a_real_venv()
    {
        string py = RequirePython();
        PythonEnvResult created = await fx.Manager.CreateAsync("lab", py, PythonEnvTool.Venv, CancellationToken.None);
        Assert.True(created.Ok, created.Error);
        string envPath = Path.Combine(fx.Root, "lab");
        Assert.Equal(envPath, created.Environment!.Path);
        Assert.True(File.Exists(created.Environment.InterpreterPath));

        Assert.Contains(fx.Manager.List(), e => e.Name == "lab");
        Assert.NotNull(fx.Manager.Get("lab"));

        PythonEnvironmentValidation v = await fx.Manager.ValidateAsync(envPath, CancellationToken.None);
        Assert.True(v.Ok, v.Error);
        Assert.Equal("cpython", v.Implementation);

        // A fresh venv has no numpy: the requirement probe reports it as missing and does not guess.
        PythonProbeResult probe = await fx.Manager.ProbeAsync(created.Environment.InterpreterPath, ["numpy", "pip"], CancellationToken.None);
        Assert.True(probe.Ok, probe.Error);
        Assert.DoesNotContain("numpy", probe.Installed.Keys);

        (IReadOnlyList<PythonPackage> pkgs, string? err) = await fx.Manager.ListPackagesAsync(envPath, CancellationToken.None);
        Assert.Null(err);
        Assert.DoesNotContain(pkgs, p => p.Name.Equals("numpy", StringComparison.OrdinalIgnoreCase));

        // A duplicate name is refused instead of overwriting.
        Assert.False((await fx.Manager.CreateAsync("lab", py, PythonEnvTool.Venv, CancellationToken.None)).Ok);

        Assert.True(fx.Manager.Delete("lab").Ok);
        Assert.False(Directory.Exists(envPath));
        Assert.Empty(fx.Manager.List());
    }

    [Theory]
    [InlineData("")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("-rf")]
    [InlineData("name.")]
    [InlineData("with space")]
    public async Task Create_rejects_unsafe_names(string name)
    {
        Assert.False((await fx.Manager.CreateAsync(name, "/usr/bin/python3", PythonEnvTool.Venv, CancellationToken.None)).Ok);
        Assert.False(Directory.Exists(Path.Combine(fx.Root, "..", "evil")));
    }

    [Fact]
    public void Delete_refuses_anything_that_is_not_a_managed_venv()
    {
        Directory.CreateDirectory(fx.Root);
        string plain = Path.Combine(fx.Root, "plain-dir");        // inside root but no pyvenv.cfg
        Directory.CreateDirectory(plain);
        File.WriteAllText(Path.Combine(plain, "keep.txt"), "x");
        string outside = Path.Combine(Path.GetTempPath(), "devacademy-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "pyvenv.cfg"), "home = /x");
        try
        {
            Assert.False(fx.Manager.Delete("plain-dir").Ok);
            Assert.False(fx.Manager.Delete(outside).Ok);              // absolute path outside the root
            Assert.False(fx.Manager.Delete("../" + Path.GetFileName(outside)).Ok);
            Assert.False(fx.Manager.Delete(fx.Root).Ok);              // the root itself
            Assert.False(fx.Manager.Delete("/").Ok);
            Assert.False(fx.Manager.Delete("").Ok);
            Assert.True(File.Exists(Path.Combine(plain, "keep.txt")));
            Assert.True(File.Exists(Path.Combine(outside, "pyvenv.cfg")));

            if (!OperatingSystem.IsWindows())
            {
                string link = Path.Combine(fx.Root, "linked");        // symlink to a real venv-like dir outside the root
                Directory.CreateSymbolicLink(link, outside);
                Assert.False(fx.Manager.Delete("linked").Ok);
                Assert.True(Directory.Exists(outside));
                File.Delete(link);
            }
        }
        finally
        {
            Directory.Delete(outside, true);
            Directory.Delete(plain, true);
        }
    }

    [Fact]
    public async Task Install_plan_targets_the_env_shows_the_command_and_rejects_option_like_specs()
    {
        string py = RequirePython();
        Assert.True((await fx.Manager.CreateAsync("planenv", py, PythonEnvTool.Venv, CancellationToken.None)).Ok);
        try
        {
            (PythonInstallPlan? plan, string? err) = await fx.Manager.BuildInstallPlanAsync("planenv",
                PythonInstallRequest.FromPackages(["numpy>=2", "pandas"]), CancellationToken.None);
            Assert.Null(err);
            Assert.True(plan!.Downloads);
            Assert.Equal(["numpy", "pandas"], plan.PackageNames);
            Assert.Equal(Path.Combine(fx.Root, "planenv"), plan.EnvironmentPath);
            Assert.Contains("numpy>=2", plan.CommandLine);
            Assert.Contains("--", plan.Arguments);
            Assert.DoesNotContain("--user", plan.Arguments);

            foreach (string bad in new[] { "--index-url=http://evil", "-r /etc/passwd", "https://evil/x.whl", "./local", "pkg @ http://x", "a;b" })
            {
                (PythonInstallPlan? p, string? e) = await fx.Manager.BuildInstallPlanAsync("planenv", PythonInstallRequest.FromPackages([bad]), CancellationToken.None);
                Assert.Null(p);
                Assert.NotNull(e);
            }
            (PythonInstallPlan? none, _) = await fx.Manager.BuildInstallPlanAsync("missing-env", PythonInstallRequest.FromPackages(["numpy"]), CancellationToken.None);
            Assert.Null(none);
        }
        finally { fx.Manager.Delete("planenv"); }
    }

    [Fact]
    public async Task Install_refuses_a_plan_for_an_environment_that_is_not_managed()
    {
        var plan = new PythonInstallPlan("/usr/bin/python3", ["-m", "pip", "install", "numpy"], "x", true, ["numpy"], "/usr");
        PythonInstallResult r = await fx.Manager.InstallAsync(plan, _ => { }, CancellationToken.None);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task Sandbox_cannot_write_into_the_selected_env_during_a_run_but_can_read_it()
    {
        if (!OperatingSystem.IsMacOS()) Assert.Skip("macOS sandbox-exec only");
        string py = RequirePython();
        if (!await new MacSandboxExecSandbox(TestSupport.Options.CacheDirectory).IsAvailableAsync(CancellationToken.None))
            Assert.Skip("sandbox-exec unusable here");
        PythonEnvResult created = await fx.Manager.CreateAsync("sbx", py, PythonEnvTool.Venv, CancellationToken.None);
        Assert.True(created.Ok, created.Error);
        try
        {
            string env = created.Environment!.Path;
            string code = $$"""
                import os, sys
                def attempt(f):
                    try:
                        f(); return 'ok'
                    except OSError:
                        return 'denied'
                sp = [p for p in sys.path if p.endswith('site-packages')][0]
                print(attempt(lambda: open(os.path.join(sp, 'evil.pth'), 'w').write('x')),
                      attempt(lambda: open({{System.Text.Json.JsonSerializer.Serialize(Path.Combine(env, "pyvenv.cfg"))}}).read()),
                      attempt(lambda: open('inside.txt', 'w').write('x')),
                      os.path.realpath(sys.prefix) == os.path.realpath({{System.Text.Json.JsonSerializer.Serialize(env)}}))
                """;
            var request = new ExecutionRequest("python", [new SourceFile("main.py", code)], "main.py", IsolationLevel.OsSandbox,
                new PythonRunSelection(created.Environment.InterpreterPath, env));
            ExecutionResult r = await TestSupport.RunAsync(request);
            Assert.Equal(IsolationLevel.OsSandbox, r.AppliedIsolation);
            Assert.Equal("denied ok ok True", r.Stdout.Trim());
            Assert.False(File.Exists(Path.Combine(env, "lib", "evil.pth")));
        }
        finally { fx.Manager.Delete("sbx"); }
    }

    [Fact]
    public async Task A_run_with_an_env_downgrades_container_isolation_instead_of_failing()
    {
        string py = RequirePython();
        PythonEnvResult created = await fx.Manager.CreateAsync("dg", py, PythonEnvTool.Venv, CancellationToken.None);
        Assert.True(created.Ok, created.Error);
        try
        {
            var request = new ExecutionRequest("python", [new SourceFile("main.py", "print('hi')")], "main.py", IsolationLevel.Container,
                new PythonRunSelection(created.Environment!.InterpreterPath, created.Environment.Path));
            ExecutionResult r = await TestSupport.RunAsync(request);
            Assert.NotEqual(IsolationLevel.Container, r.AppliedIsolation);
            Assert.Equal("hi", r.Stdout.Trim());
        }
        finally { fx.Manager.Delete("dg"); }
    }

    [Fact]
    public void Sandbox_profiles_mark_read_only_paths()
    {
        string mac = MacSandboxExecSandbox.BuildProfile("/w", "/c", false, ["/envs/x"]);
        Assert.Contains("/envs/x", mac);
        Assert.True(mac.LastIndexOf("(deny file-write*", StringComparison.Ordinal) > mac.IndexOf("/c", StringComparison.Ordinal));
        var bwrap = LinuxBubblewrapSandbox.BuildArguments("/w", "/c", "/bin/sh", ["-c", "true"], false, ["/envs/x"]);
        int i = bwrap.ToList().IndexOf("--ro-bind");
        Assert.True(i >= 0);
        Assert.Contains("/envs/x", bwrap);
    }

    // ------------------------------------------------------------ detector

    [Fact]
    public void Detector_parses_uv_and_py_launcher_output()
    {
        string uv = """[{"key":"cpython-3.12.1-macos-aarch64-none","path":"/u/py312/bin/python3.12"},{"key":"cpython-3.13.0","path":null}]""";
        Assert.Equal(["/u/py312/bin/python3.12"], PythonInterpreterDetector.ParseUvList(uv));
        Assert.Empty(PythonInterpreterDetector.ParseUvList("not json"));
        string[] py = PythonInterpreterDetector.ParsePyLauncher(" -V:3.12 *        C:\\Python312\\python.exe\n -V:3.11          C:\\Python311\\python.exe\n").ToArray();
        Assert.Equal(["C:\\Python312\\python.exe", "C:\\Python311\\python.exe"], py);
    }

    [Fact]
    public async Task Detector_reports_the_real_interpreters_with_version_and_arch()
    {
        var detector = new PythonInterpreterDetector(new SystemToolRunner());
        IReadOnlyList<PythonInterpreter> found = await detector.DetectAsync(CancellationToken.None);
        if (found.Count == 0) Assert.Skip("no python on this machine");
        Assert.All(found, p =>
        {
            Assert.True(File.Exists(p.Path));
            Assert.NotNull(p.ParsedVersion);
            Assert.NotEmpty(p.Architecture);
            Assert.NotEmpty(p.Implementation);
        });
        Assert.Equal(found.Count, found.Select(p => Path.GetFullPath(p.Path)).Distinct().Count());
    }

    [Fact]
    public async Task Detector_drops_candidates_that_do_not_run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "devacademy-det-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "python3"), "");
            var fake = new FakeToolRunner();
            var detector = new PythonInterpreterDetector(fake, new PythonDetectionOptions
            {
                SearchDirectories = [dir],
                HomeDirectory = dir,
                UvExecutable = "",
                PyLauncher = "",
                IsWindows = false,
            });
            Assert.Empty(await detector.DetectAsync(CancellationToken.None));
        }
        finally { Directory.Delete(dir, true); }
    }
}

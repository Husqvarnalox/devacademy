using DevAcademy.Content;
using DevAcademy.Domain;
using DevAcademy.Execution;
using CliMain = global::Cli;

namespace DevAcademy.CliTests;

public class ArgumentParsingTests
{
    [Fact]
    public void Parses_content_run_solutions_and_python_env()
    {
        var o = CliMain.Parse(["--content", "c", "--run-solutions", "--python-env", ".venv-content"], out var error);
        Assert.Null(error);
        Assert.Equal(new CliMain.Options("c", true, ".venv-content"), o);
    }

    [Fact]
    public void Defaults_have_no_environment()
    {
        var o = CliMain.Parse(["--run-solutions"], out _);
        Assert.Null(o!.PythonEnv);
        Assert.True(o.RunSolutions);
    }

    [Fact]
    public void Parses_node_env()
    {
        var o = CliMain.Parse(["--run-solutions", "--node-env", ".node-content"], out var error);
        Assert.Null(error);
        Assert.Equal(".node-content", o!.NodeEnv);
        Assert.Null(o.PythonEnv);
        Assert.Null(CliMain.Parse(["--run-solutions"], out _)!.NodeEnv);
    }

    [Fact]
    public void Parses_go_env()
    {
        var o = CliMain.Parse(["--run-solutions", "--go-env", ".go-content"], out var error);
        Assert.Null(error);
        Assert.Equal(".go-content", o!.GoEnv);
        Assert.Null(CliMain.Parse(["--run-solutions"], out _)!.GoEnv);
    }

    [Fact]
    public void Parses_nuget_env()
    {
        var o = CliMain.Parse(["--run-solutions", "--nuget-env", ".nuget-content"], out var error);
        Assert.Null(error);
        Assert.Equal(".nuget-content", o!.NuGetEnv);
        Assert.Null(CliMain.Parse(["--run-solutions"], out _)!.NuGetEnv);
    }

    [Theory]
    [InlineData("--nuget-env")]
    [InlineData("--nuget-env", "--run-solutions")]
    [InlineData("--go-env")]
    [InlineData("--go-env", "--run-solutions")]
    [InlineData("--node-env")]
    [InlineData("--node-env", "--run-solutions")]
    [InlineData("--python-env")]
    [InlineData("--python-env", "--run-solutions")]
    [InlineData("--bogus")]
    public void Incomplete_or_unknown_arguments_are_rejected(params string[] args)
    {
        Assert.Null(CliMain.Parse(args, out var error));
        Assert.StartsWith("error:", error);
    }
}

public class InterpreterResolutionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "da-venv-" + Guid.NewGuid().ToString("N"));

    private string Touch(string relative)
    {
        // Native separators: the resolver returns Path.Combine(full dir, "bin", "python"), never a mixed "\\bin/python" on Windows.
        var path = Path.GetFullPath(Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar)));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    private PythonEnvironmentManager Manager(bool windows) => new(Path.GetTempPath(), isWindows: windows);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unix_layout_is_found_on_any_host(bool hostIsWindows)
    {
        var python = Touch("bin/python");
        Assert.Equal(python, Manager(hostIsWindows).ResolveInterpreter(_dir));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Windows_layout_is_found_on_any_host(bool hostIsWindows)
    {
        var python = Touch("Scripts/python.exe");
        Assert.Equal(python, Manager(hostIsWindows).ResolveInterpreter(_dir));
    }

    [Fact]
    public void Host_layout_wins_and_python3_is_a_fallback()
    {
        var unix = Touch("bin/python");
        var win = Touch("Scripts/python.exe");
        Assert.Equal(win, Manager(true).ResolveInterpreter(_dir));
        Assert.Equal(unix, Manager(false).ResolveInterpreter(_dir));
        File.Delete(unix);
        var py3 = Touch("bin/python3");
        Assert.Equal(py3, Manager(false).ResolveInterpreter(Path.Combine(_dir, ".")));
    }

    [Fact]
    public void Directory_without_interpreter_resolves_to_null()
    {
        Directory.CreateDirectory(_dir);
        Assert.Null(Manager(false).ResolveInterpreter(_dir));
        Assert.Null(Manager(true).ResolveInterpreter(Path.Combine(_dir, "missing")));
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}

/// <summary>Drives the real CLI entry point; console redirection is process-wide, so these stay in one serial class.</summary>
public class ValidateWithRequirementsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "da-cli-" + Guid.NewGuid().ToString("N"));

    public ValidateWithRequirementsTests()
    {
        Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid"), Path.Combine(_root, "content"));
        // The fixture's io exercise now needs a package the test machine may lack, but a shipped set covers; the other lessons are untouched.
        var lesson = Path.Combine(_root, "content", "courses", "py-basics", "lessons", "02-vars", "lesson.yaml");
        var text = File.ReadAllText(lesson);
        const string anchor = "  entryFile: main.py\n  evaluation: io\n";
        Assert.Contains(anchor, text);
        File.WriteAllText(lesson, text.Replace(anchor, anchor + "  requires: { python: \">=3.12\", packages: [\"numpy>=1\"] }\n"));
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }

    private static async Task<(int Code, string Out, string Err)> Run(params string[] args)
    {
        var (so, se) = (Console.Out, Console.Error);
        using var o = new StringWriter();
        using var e = new StringWriter();
        Console.SetOut(o);
        Console.SetError(e);
        try { return (await CliMain.RunAsync(args), o.ToString(), e.ToString()); }
        finally { Console.SetOut(so); Console.SetError(se); }
    }

    private string Content => Path.Combine(_root, "content");

    [Fact]
    public async Task Without_python_env_the_exercise_warns_and_is_not_an_error()
    {
        var (_, output, _) = await Run("validate-content", "--content", Content, "--run-solutions");
        Assert.Contains("WARNING", output);
        Assert.Contains("requires packages numpy>=1 and python >=3.12; pass --python-env", output);
        // (the fixture's unrelated harness challenge may fail without a real test runner; only the gated exercise matters here)
        Assert.DoesNotContain(output.Split('\n'), l => l.StartsWith("ERROR", StringComparison.Ordinal) && l.Contains("py.vars.ex1"));
        Assert.Contains("code samples (python) require packages numpy>=1", output);
    }

    [Fact]
    public async Task Python_env_without_run_solutions_is_a_usage_error()
    {
        var (code, _, err) = await Run("validate-content", "--content", Content, "--python-env", _root);
        Assert.Equal(2, code);
        Assert.Contains("--run-solutions", err);
    }

    [Fact]
    public async Task Node_env_without_run_solutions_is_a_usage_error()
    {
        var (code, _, err) = await Run("validate-content", "--content", Content, "--node-env", _root);
        Assert.Equal(2, code);
        Assert.Contains("--run-solutions", err);
    }

    [Fact]
    public async Task Go_env_without_run_solutions_is_a_usage_error()
    {
        var (code, _, err) = await Run("validate-content", "--content", Content, "--go-env", _root);
        Assert.Equal(2, code);
        Assert.Contains("--run-solutions", err);
    }

    [Fact]
    public async Task Go_env_without_a_module_cache_points_to_make_go_env()
    {
        var (code, _, err) = await Run("validate-content", "--content", Content, "--run-solutions", "--go-env", Path.Combine(_root, "nope"));
        Assert.Equal(2, code);
        Assert.Contains("make go-env", err);
    }

    [Fact]
    public async Task Node_env_without_node_modules_points_to_make_node_env()
    {
        var (code, _, err) = await Run("validate-content", "--content", Content, "--run-solutions", "--node-env", Path.Combine(_root, "nope"));
        Assert.Equal(2, code);
        Assert.Contains("make node-env", err);
    }

    [Fact]
    public async Task Missing_or_empty_environment_directory_is_reported()
    {
        var (code, _, err) = await Run("validate-content", "--content", Content, "--run-solutions", "--python-env", Path.Combine(_root, "nope"));
        Assert.Equal(2, code);
        Assert.Contains("does not exist", err);

        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        (code, _, err) = await Run("python-env-check", "--content", Content, "--python-env", Path.Combine(_root, "empty"));
        Assert.Equal(2, code);
        Assert.Contains("no interpreter", err);
    }

    [Fact]
    public async Task Env_check_lists_each_requirement_against_the_probe()
    {
        if (OperatingSystem.IsWindows()) return; // the fake interpreter is a shell script
        var env = Path.Combine(_root, "venv");
        var python = Path.Combine(env, "bin", "python");
        Directory.CreateDirectory(Path.GetDirectoryName(python)!);
        // Answers the importlib.metadata probe like a Python 3.14 that has numpy 0.5 (too old for ">=1").
        File.WriteAllText(python, "#!/bin/sh\ncat >/dev/null\necho '{\"version\":\"3.14.0\",\"impl\":\"cpython\",\"installed\":{\"numpy\":\"0.5\"}}'\n");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var (code, output, _) = await Run("python-env-check", "--content", Content, "--python-env", env);
        Assert.Equal(1, code);
        Assert.Contains("MISSING numpy>=1", output);
        Assert.Contains("installed 0.5", output);
        Assert.Contains("ok      python>=3.12", output);
        Assert.Contains("1 not satisfied", output);
    }

    // An index that offers torch only on Linux, asked as an Intel Mac: nothing here depends on the host's architecture.
    private static ContentPackageIndex IntelMacIndex() => new(
    [
        new ContentPackageSet("pytorch", "PyTorch", PackageSetLanguage.Python, "", ["numpy", "torch"], ["linux-x64"]),
        new ContentPackageSet("numpy", "NumPy", PackageSetLanguage.Python, "", ["numpy"]),
    ], null, "osx-x64");

    private void RequireTorch()
    {
        var lesson = Path.Combine(_root, "content", "courses", "py-basics", "lessons", "02-vars", "lesson.yaml");
        File.WriteAllText(lesson, File.ReadAllText(lesson).Replace("numpy>=1", "torch>=2"));
    }

    [Fact]
    public async Task Exercise_whose_set_is_unsupported_on_the_platform_is_skipped_with_a_warning_not_an_error()
    {
        RequireTorch();
        var content = ContentLoader.Load(Content).Content;
        var diagnostics = await CliMain.RunSolutionsAsync(content, null, null, null, null, null, null, IntelMacIndex());
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("exercise 'py.vars.ex1': skipped: package set 'pytorch' is not available on osx-x64", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("py.vars.ex1", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Message.Contains("code samples (python): skipped: package set 'pytorch'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Supported_platform_still_asks_for_the_environment()
    {
        RequireTorch();
        var supported = new ContentPackageIndex(IntelMacIndex().Sets, null, "linux-x64");
        var diagnostics = await CliMain.RunSolutionsAsync(ContentLoader.Load(Content).Content, null, null, null, null, null, null, supported);
        Assert.Contains(diagnostics, d => d.Message.Contains("pass --python-env", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Message.Contains("is not available on", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Env_check_reports_unsupported_requirements_apart_from_missing_ones()
    {
        if (OperatingSystem.IsWindows()) return;
        RequireTorch();
        var env = Path.Combine(_root, "venv");
        var python = Path.Combine(env, "bin", "python");
        Directory.CreateDirectory(Path.GetDirectoryName(python)!);
        File.WriteAllText(python, "#!/bin/sh\ncat >/dev/null\necho '{\"version\":\"3.14.0\",\"impl\":\"cpython\",\"installed\":{}}'\n");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var (manager, selection, error) = await CliMain.OpenEnvironmentAsync(env);
        Assert.NotNull(selection);

        var (so, se) = (Console.Out, Console.Error);
        using var o = new StringWriter();
        Console.SetOut(o);
        int code;
        try { code = await CliMain.EnvCheckAsync(Content, manager!, selection, IntelMacIndex()); }
        finally { Console.SetOut(so); Console.SetError(se); }
        Assert.Equal(0, code);
        Assert.Contains("unsupported on osx-x64", o.ToString());
        Assert.DoesNotContain("MISSING torch", o.ToString());
        Assert.Contains("0 not satisfied", o.ToString());
        Assert.Contains("1 unsupported on osx-x64", o.ToString());
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}

public class MavenCliTests
{
    [Fact]
    public void Parses_maven_env_and_set()
    {
        var o = CliMain.Parse(["--run-solutions", "--maven-env", ".maven-content"], out var error);
        Assert.Null(error);
        Assert.Equal(".maven-content", o!.MavenEnv);
        Assert.Null(CliMain.Parse(["--run-solutions"], out _)!.MavenEnv);
        var s = CliMain.Parse(["--maven-env", "d", "--set", "spring-boot-web-test"], out _);
        Assert.Equal("spring-boot-web-test", s!.Set);
    }

    [Theory]
    [InlineData("--maven-env")]
    [InlineData("--maven-env", "--run-solutions")]
    [InlineData("--set")]
    public void Incomplete_maven_arguments_are_rejected(params string[] args)
    {
        Assert.Null(CliMain.Parse(args, out var error));
        Assert.StartsWith("error:", error);
    }

    [Fact]
    public async Task Maven_env_downloads_the_set_from_the_lock_and_verifies_hashes_with_a_fake_server()
    {
        var packages = ContentPackageIndex.TryLoadDefault()!;
        var set = packages.For(PackageSetLanguage.Maven).First();
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-cli-maven-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Every request is answered with the same wrong bytes: the very first jar must fail its size or sha256 check and nothing may be kept.
            var code = await CliMain.MavenEnvAsync(dir, set.Id, new WrongBytesHandler());
            Assert.Equal(1, code);
            var lib = Path.Combine(dir, set.Id, "lib");
            Assert.Empty(Directory.Exists(lib) ? Directory.GetFiles(lib) : []);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    private sealed class WrongBytesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
    }
}

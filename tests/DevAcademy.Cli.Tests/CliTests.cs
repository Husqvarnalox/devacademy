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

    [Theory]
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
        var path = Path.Combine(_dir, relative);
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

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}

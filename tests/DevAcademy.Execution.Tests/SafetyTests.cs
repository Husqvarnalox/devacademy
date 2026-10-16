using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

public class SafetyTests
{
    [Theory]
    [InlineData("../evil.py")]
    [InlineData("a/../../evil.py")]
    [InlineData("a\\..\\..\\evil.py")]
    [InlineData("/etc/evil.py")]
    [InlineData("C:\\evil.py")]
    [InlineData("")]
    public async Task Unsafe_paths_are_rejected(string path)
    {
        var req = new ExecutionRequest("python", [new SourceFile(path, "print(1)")], null, IsolationLevel.ProcessLimits);
        await Assert.ThrowsAsync<ArgumentException>(() => TestSupport.Runner.PrepareAsync(req, CancellationToken.None));
    }

    [Fact]
    public async Task Unsafe_entry_file_is_rejected()
    {
        var req = new ExecutionRequest("python", [new SourceFile("main.py", "print(1)")], "../main.py", IsolationLevel.ProcessLimits);
        await Assert.ThrowsAsync<ArgumentException>(() => TestSupport.Runner.PrepareAsync(req, CancellationToken.None));
    }

    [Fact]
    public async Task Workdir_is_deleted_after_dispose_and_owner_only()
    {
        await TestSupport.RequireAsync("python");
        IPreparedProgram p = await TestSupport.Runner.PrepareAsync(TestSupport.Request("python", "main.py", "print(1)"), CancellationToken.None);
        string dir = p.WorkingDirectory;
        Assert.True(File.Exists(Path.Combine(dir, "main.py")));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(dir));
        await p.DisposeAsync();
        Assert.False(Directory.Exists(dir));
        await p.DisposeAsync(); // idempotent
    }

    [Fact]
    public async Task Workdir_is_deleted_after_compile_failure()
    {
        await TestSupport.RequireAsync("go");
        IPreparedProgram p = await TestSupport.Runner.PrepareAsync(TestSupport.Request("go", "main.go", "package main\nfunc main() { x }\n"), CancellationToken.None);
        Assert.Equal(ExecutionStatus.CompileError, p.PreparationFailure?.Status);
        string dir = p.WorkingDirectory;
        await p.DisposeAsync();
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task Environment_does_not_leak_into_learner_code()
    {
        await TestSupport.RequireAsync("python");
        const string Name = "DEVACADEMY_TEST_SECRET";
        Environment.SetEnvironmentVariable(Name, "s3cret");
        try
        {
            ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("python", "main.py",
                $"import os\nprint(os.environ.get('{Name}'))\nprint(os.path.realpath(os.environ['HOME']) == os.path.realpath(os.getcwd()))\nprint(os.path.realpath(os.environ['TMPDIR']).startswith(os.path.realpath(os.getcwd())))\n"));
            Assert.Equal(["None", "True", "True"], r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        finally { Environment.SetEnvironmentVariable(Name, null); }
    }

    [Fact]
    public async Task Unknown_language_is_an_internal_error_result()
    {
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("cobol", "main.cob", "x"));
        Assert.Equal(ExecutionStatus.InternalError, r.Status);
    }
}
